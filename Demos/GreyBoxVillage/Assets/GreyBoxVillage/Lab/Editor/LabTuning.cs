using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Agenerela;
using Agenerela.Providers;
using UnityEngine;

namespace Demos.GreyBoxVillage.Lab
{
    /// <summary>
    /// Puts a <see cref="LabVariant"/> on the village's agents, and takes it off again: the provider,
    /// the prompt switches and observations through <see cref="AgentMind.Tune"/>, the persona, the
    /// example statements and memory on each agent. What the game shipped with is read once, before
    /// any variant, and every variant starts from it, so "baseline" is exactly the game.
    /// </summary>
    /// <remarks>
    /// A built agent reads its profile's own <see cref="AgentIdentity"/> object, and nothing public
    /// gives it another (gap G26), so a persona is written into the profile's identity and written
    /// back by <see cref="Restore"/>. Nothing here marks an asset dirty or saves it; the bench calls
    /// <see cref="Restore"/> in a finally and when Play mode ends.
    /// </remarks>
    public sealed class LabTuning
    {
        /// <summary>The DecideOptions switches a variant may set, by name, and what each takes.</summary>
        public static readonly IReadOnlyDictionary<string, string> Switches = new Dictionary<string, string>
        {
            { "ListActionsAndTargets", "bool" }, { "MaxListedTargets", "int" }, { "IdleExampleStimulus", "text" },
            { "IdleExampleStatement", "text" }, { "CounterExamples", "bool" }, { "PairTargetsWithActions", "bool" },
            { "ExampleStatements", "bool" }, { "ExampleTargetFallback", "bool" }, { "ObservationPlacement", "AfterSources|WithStimulus" },
            { "CheckTarget", "bool" }, { "AskForRefusalLine", "bool" },
        };

        public static bool TryParseSwitch(LabSwitch s, out string problem)
        {
            problem = null;
            if (s == null || !Switches.TryGetValue(s.name ?? "", out string kind))
            {
                problem = "'" + s?.name + "' is no switch the bench knows: " + string.Join(", ", Switches.Keys);
                return false;
            }
            bool ok = kind == "bool" ? bool.TryParse(s.value, out _)
                : kind == "int" ? int.TryParse(s.value, out _)
                : kind == "text" || kind.Split('|').Contains(s.value);
            if (!ok) problem = s.name + " takes " + kind + ", not '" + s.value + "'";
            return ok;
        }

        // The moment's own facts among the game's observation lines: what is true because of what is
        // happening now, rather than what holds on every call. The bench's reading, not the game's.
        private static readonly string[] MomentStarts =
        {
            "A wolf is ", "A thief is running", "The alarm bell is ringing", "There is a fire at", "The player is inside the ",
        };

        public static bool IsMoment(string line) => MomentStarts.Any(m => line.StartsWith(m, StringComparison.Ordinal));

        private readonly VillageCommands village;
        private readonly Dictionary<AgentMind, Original> originals = new Dictionary<AgentMind, Original>();
        private readonly List<(ActionDefinition definition, string statement, string counter)> statements =
            new List<(ActionDefinition, string, string)>();
        private readonly Dictionary<AgentMind, StandingSource> sources = new Dictionary<AgentMind, StandingSource>();

        private sealed class Original
        {
            public string Role, Personality, Goals, StatementRules;
            public List<string> Knowledge;
            public IMemoryStrategy Memory;
            public MemoryCommit Commit;
        }

        public LabTuning(VillageCommands village)
        {
            this.village = village;
            foreach (var mind in village.Minds)
            {
                var agent = mind.Agent;
                var id = agent.Identity;
                originals[mind] = new Original
                {
                    Role = id.Role, Personality = id.Personality, Goals = id.Goals, StatementRules = id.StatementRules,
                    Knowledge = new List<string>(id.Knowledge ?? new List<string>()),
                    Memory = agent.Memory, Commit = agent.MemoryCommit
                };
                foreach (var d in agent.Actions.Definitions)
                    statements.Add((d, d.ExampleStatement, d.CounterExampleStatement));
            }
        }

        /// <summary>The variant applied now; null before the first.</summary>
        public LabVariant Current { get; private set; }

        /// <summary>
        /// Puts <paramref name="variant"/> on every agent. Call it after F1, which keeps all of this:
        /// the reset clears memory, never the strategy, the identity or the hook.
        /// </summary>
        public void Apply(LabVariant variant)
        {
            Current = variant;
            foreach (var mind in village.Minds)
            {
                var agent = mind.Agent;
                var o = originals[mind];
                var id = agent.Identity;
                var p = (variant.persona ?? new List<LabPersona>()).FirstOrDefault(x => x.agent == mind.Name);
                id.Role = Pick(p?.role, o.Role);
                id.Personality = Pick(p?.personality, o.Personality);
                id.Goals = Pick(p?.goals, o.Goals);
                id.StatementRules = Pick(p?.statementRules, o.StatementRules);
                var knowledge = p?.knowledge ?? new List<string>();
                id.Knowledge = knowledge.Count == 0 ? new List<string>(o.Knowledge)
                    : knowledge.Count == 1 && knowledge[0] == "-" ? new List<string>()
                    : new List<string>(knowledge);

                var m = variant.memory ?? new LabMemory();
                agent.Memory = string.IsNullOrEmpty(m.historyTurns) ? o.Memory
                    : m.historyTurns == "0" ? null
                    : new RollingHistory(int.Parse(m.historyTurns, CultureInfo.InvariantCulture));
                agent.MemoryCommit = string.IsNullOrEmpty(m.commit) ? o.Commit : (MemoryCommit)Enum.Parse(typeof(MemoryCommit), m.commit);

                var layout = variant.layout ?? "";
                if (layout == "sources" && !sources.ContainsKey(mind))
                {
                    var source = new StandingSource();
                    sources[mind] = source;
                    agent.ObservationSources.Add(source);
                }
                if (sources.TryGetValue(mind, out var standing)) standing.Lines = null;

                mind.Tune = Tuner(variant);
            }

            foreach (var (definition, statement, counter) in statements)
            {
                definition.ExampleStatement = statement;
                definition.CounterExampleStatement = counter;
            }
            foreach (var s in variant.statements ?? new List<LabStatement>())
            {
                var mind = village.Minds.FirstOrDefault(x => x.Name == s.agent);
                if (mind == null || !mind.Agent.Actions.TryGet(s.action, out var d, out _))
                    throw new InvalidOperationException("Variant " + variant.name + ": " + s.agent + " has no action " + s.action + " to give a statement.");
                if (!string.IsNullOrEmpty(s.statement)) d.ExampleStatement = s.statement;
                if (!string.IsNullOrEmpty(s.counter)) d.CounterExampleStatement = s.counter;
            }
        }

        private static string Pick(string over, string own) =>
            string.IsNullOrEmpty(over) ? own : over == "-" ? "" : over;

        private Action<AgentMind, DecideOptions> Tuner(LabVariant variant)
        {
            var switches = (variant.prompt ?? new List<LabSwitch>()).ToList();
            string style = variant.observations ?? "";
            bool split = (variant.layout ?? "") == "sources";
            if (switches.Count == 0 && style == "" && !split) return null;
            return (mind, options) =>
            {
                foreach (var s in switches) Set(options, s);
                var lines = (options.Observations ?? new List<string>()).ToList();
                if (style == "terse")
                    lines = lines.Where(l => !l.StartsWith("In the village:", StringComparison.Ordinal))
                        .Select(Terse).ToList();
                else if (style == "none")
                    lines = new List<string>();
                if (split && sources.TryGetValue(mind, out var source))
                {
                    source.Lines = lines.Where(l => !IsMoment(l)).ToList();
                    lines = lines.Where(IsMoment).ToList();
                }
                options.Observations = lines;
            };
        }

        // "bridge, 12 m away: The wooden bridge over the stream" to "bridge, 12 m away".
        private static string Terse(string line)
        {
            int at = line.IndexOf(" away: ", StringComparison.Ordinal);
            return at > 0 && line.IndexOf(", ", StringComparison.Ordinal) > 0 ? line.Substring(0, at + 5) : line;
        }

        private static void Set(DecideOptions o, LabSwitch s)
        {
            string v = s.value;
            switch (s.name)
            {
                case "ListActionsAndTargets": o.ListActionsAndTargets = bool.Parse(v); break;
                case "MaxListedTargets": o.MaxListedTargets = int.Parse(v, CultureInfo.InvariantCulture); break;
                case "IdleExampleStimulus": o.IdleExampleStimulus = v; break;
                case "IdleExampleStatement": o.IdleExampleStatement = v; break;
                case "CounterExamples": o.CounterExamples = bool.Parse(v); break;
                case "PairTargetsWithActions": o.PairTargetsWithActions = bool.Parse(v); break;
                case "ExampleStatements": o.ExampleStatements = bool.Parse(v); break;
                case "ExampleTargetFallback": o.ExampleTargetFallback = bool.Parse(v); break;
                case "ObservationPlacement": o.ObservationPlacement = (ObservationPlacement)Enum.Parse(typeof(ObservationPlacement), v); break;
                case "CheckTarget": o.CheckTarget = bool.Parse(v); break;
                case "AskForRefusalLine": o.AskForRefusalLine = bool.Parse(v); break;
                default: throw new ArgumentException("No switch " + s.name);
            }
        }

        /// <summary>Puts every agent back as the game shipped it, and takes the bench's sources and hooks off.</summary>
        public void Restore()
        {
            foreach (var pair in originals)
            {
                var mind = pair.Key;
                if (mind == null) continue;
                var o = pair.Value;
                mind.Tune = null;
                if (!mind.Behaviour.HasAgent) continue;
                var agent = mind.Agent;
                var id = agent.Identity;
                id.Role = o.Role;
                id.Personality = o.Personality;
                id.Goals = o.Goals;
                id.StatementRules = o.StatementRules;
                id.Knowledge = new List<string>(o.Knowledge);
                agent.Memory = o.Memory;
                agent.MemoryCommit = o.Commit;
                if (sources.TryGetValue(mind, out var source)) agent.ObservationSources.Remove(source);
            }
            sources.Clear();
            foreach (var (definition, statement, counter) in statements)
            {
                definition.ExampleStatement = statement;
                definition.CounterExampleStatement = counter;
            }
            Current = null;
        }

        /// <summary>
        /// A provider config for <paramref name="variant"/> on <paramref name="server"/>: the game's
        /// VillageOllama.asset copied, with the variant's fields and the server's address and key
        /// variable on top. Never the asset itself, which stays as the game ships it.
        /// </summary>
        public static OllamaProviderConfig ConfigFor(LabVariant variant, OllamaProviderConfig game, LabServer server)
        {
            var config = ScriptableObject.CreateInstance<OllamaProviderConfig>();
            config.name = "Lab " + variant.name;
            // Made in Edit mode, before Play starts: entering Play mode would unload it as unused.
            config.hideFlags = HideFlags.HideAndDontSave;
            config.Endpoint = server.Endpoint;
            config.ApiKeyVariable = server.ApiKeyVariable;
            var p = variant.provider ?? new LabProvider();
            config.Model = string.IsNullOrEmpty(p.model) ? game.Model : p.model;
            config.ContextSize = string.IsNullOrEmpty(p.numCtx) ? game.ContextSize : int.Parse(p.numCtx, CultureInfo.InvariantCulture);
            config.Temperature = string.IsNullOrEmpty(p.temperature) ? game.Temperature : float.Parse(p.temperature, CultureInfo.InvariantCulture);
            config.TimeoutSeconds = string.IsNullOrEmpty(p.timeoutSeconds) ? game.TimeoutSeconds : float.Parse(p.timeoutSeconds, CultureInfo.InvariantCulture);
            return config;
        }

        // The standing facts, for the "sources" layout: what the call's tuner set aside, read back by
        // the agent's observation pass of the same call.
        private sealed class StandingSource : IObservationSource
        {
            public List<string> Lines;

            public IEnumerable<string> Observe(AgentContext ctx) => Lines ?? Enumerable.Empty<string>();
        }
    }
}
