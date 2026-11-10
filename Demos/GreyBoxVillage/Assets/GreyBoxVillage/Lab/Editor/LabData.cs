using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Demos.GreyBoxVillage.Lab
{
    // The bench's data, as Lab/scenarios.json and Lab/variants.json write it. JsonUtility reads both,
    // so every field is a string, a number, a bool or a list, and "" or -1 means "as the game is".

    /// <summary>Lab/scenarios.json: every scenario the bench can play.</summary>
    [Serializable]
    public sealed class LabScenarioFile
    {
        public List<LabScenario> scenarios = new List<LabScenario>();
    }

    /// <summary>
    /// One situation in the village and what an agent is asked in it, turn by turn, with the answers
    /// that count as right. Lab/README.md says how to write one.
    /// </summary>
    [Serializable]
    public sealed class LabScenario
    {
        public string id = "";
        /// <summary>One line: what this scenario checks.</summary>
        public string purpose = "";
        /// <summary>Who is asked: Bram, Wren, Odile or Hale.</summary>
        public string agent = "";
        /// <summary>command, question, smalltalk, event, bait or memory.</summary>
        public string kind = "";
        /// <summary>For bait: unknown_target, out_of_world, unaffordable, too_dear_gift, quest_not_ready, villager_attack, not_offered.</summary>
        public string bait = "";
        /// <summary>easy, medium or hard.</summary>
        public string difficulty = "";
        public LabSetup setup = new LabSetup();
        public List<LabTurn> turns = new List<LabTurn>();

        public IEnumerable<string> Tags()
        {
            yield return "agent:" + agent;
            yield return "kind:" + kind;
            if (!string.IsNullOrEmpty(bait)) yield return "bait:" + bait;
            yield return "difficulty:" + difficulty;
        }
    }

    /// <summary>The village before the first turn, set through the presenter's own calls after F1.</summary>
    [Serializable]
    public sealed class LabSetup
    {
        /// <summary>"" for F1's clear morning, or "dusk": F5, the gate shut and the watch changed.</summary>
        public string hour = "";
        /// <summary>"" for clear, or overcast, rain or fog.</summary>
        public string weather = "";
        /// <summary>"", wolf_gate (the wolf standing outside the gate), wolf_road (further down the road), thief (one running with a ware), fire, or bell (ringing).</summary>
        public string situation = "";
        /// <summary>"" for 3 m before the asked agent, "zone:&lt;id&gt;" inside a zone that is off limits, or "far" (20 m off).</summary>
        public string player = "";
        /// <summary>The player's coins; -1 for F1's 8.</summary>
        public int coins = -1;
        public List<LabItem> items = new List<LabItem>();
        public List<LabQuest> quests = new List<LabQuest>();
    }

    [Serializable]
    public sealed class LabItem
    {
        public string id = "";
        public int count = 1;
    }

    [Serializable]
    public sealed class LabQuest
    {
        public string id = "";
        /// <summary>active or done.</summary>
        public string state = "";
    }

    /// <summary>
    /// One question: a player line (<see cref="say"/>) or a game event (<see cref="@event"/>), and the
    /// answers, each "action target", where a target may be * (any) or several ids joined by |.
    /// </summary>
    [Serializable]
    public sealed class LabTurn
    {
        public string say = "";
        /// <summary>wolf, thief, snatched, fire, bell, zone, quiet, night, merchant or captain, in the game's own words.</summary>
        public string @event = "";
        /// <summary>The event's detail: the bell's ringer, the zone, the captain's place, the words Odile or Hale say.</summary>
        public string arg = "";
        /// <summary>Asked and recorded, never scored: a memory probe's first turn.</summary>
        public bool primer;
        public List<string> accept = new List<string>();
        /// <summary>Acceptable but weaker: reported apart, never counted as exact.</summary>
        public List<string> weaker = new List<string>();
        public List<string> mustNot = new List<string>();

        public bool IsEvent => !string.IsNullOrEmpty(@event);
        public string Line => IsEvent ? "[" + @event + (string.IsNullOrEmpty(arg) ? "" : ": " + arg) + "]" : say;
    }

    /// <summary>Lab/variants.json: every named bundle the bench can run as an arm.</summary>
    [Serializable]
    public sealed class LabVariantFile
    {
        public List<LabVariant> variants = new List<LabVariant>();
    }

    /// <summary>
    /// A named way to build the village's agents: the provider, the prompt switches, the persona,
    /// the observations, memory. An empty field keeps what the game ships with, so "baseline", with
    /// nothing set, is exactly the game.
    /// </summary>
    [Serializable]
    public sealed class LabVariant
    {
        public string name = "";
        public string description = "";
        public LabProvider provider = new LabProvider();
        /// <summary>DecideOptions switches by name (see <see cref="LabTuning.Switches"/>).</summary>
        public List<LabSwitch> prompt = new List<LabSwitch>();
        public List<LabPersona> persona = new List<LabPersona>();
        /// <summary>Statements shown with an action's example (#218), for ExampleStatements.</summary>
        public List<LabStatement> statements = new List<LabStatement>();
        /// <summary>"" for the game's observations, "terse" (places by id and distance, no crowd line), or "none" (only the moment's own facts).</summary>
        public string observations = "";
        /// <summary>"" for everything with the call, as the game does, or "sources": the standing facts in an observation source, the moment's own with the call (#239).</summary>
        public string layout = "";
        public LabMemory memory = new LabMemory();
    }

    [Serializable]
    public sealed class LabProvider
    {
        /// <summary>The Ollama model; "" for the game's VillageOllama.asset.</summary>
        public string model = "";
        public string temperature = "";
        public string numCtx = "";
        /// <summary>Sent with the bench's warm-up only: OllamaSettings has no keep_alive (gap G25).</summary>
        public string keepAlive = "";
        public string timeoutSeconds = "";
    }

    [Serializable]
    public sealed class LabSwitch
    {
        public string name = "";
        public string value = "";
    }

    /// <summary>
    /// One agent's identity in a variant. An empty field keeps the profile's; "-" empties it. A
    /// non-empty knowledge list replaces the profile's; ["-"] empties it.
    /// </summary>
    [Serializable]
    public sealed class LabPersona
    {
        public string agent = "";
        public string role = "";
        public string personality = "";
        public string goals = "";
        public string statementRules = "";
        public List<string> knowledge = new List<string>();
    }

    [Serializable]
    public sealed class LabStatement
    {
        public string agent = "";
        public string action = "";
        public string statement = "";
        public string counter = "";
    }

    [Serializable]
    public sealed class LabMemory
    {
        /// <summary>"" for the game's (the framework's six exchanges), "0" for none, or a number of exchanges.</summary>
        public string historyTurns = "";
        /// <summary>"", OnDecide or OnExecute.</summary>
        public string commit = "";
    }

    /// <summary>Where the bench's data lives, and how it is read and checked.</summary>
    public static class LabFiles
    {
        public const string Folder = "Assets/GreyBoxVillage/Lab";
        public static string ScenariosPath => Path.Combine(Folder, "scenarios.json");
        public static string VariantsPath => Path.Combine(Folder, "variants.json");

        public static readonly string[] Agents = { "Bram", "Wren", "Odile", "Hale" };
        public static readonly string[] Kinds = { "command", "question", "smalltalk", "event", "bait", "memory" };
        public static readonly string[] Baits = { "unknown_target", "out_of_world", "unaffordable", "too_dear_gift", "quest_not_ready", "villager_attack", "not_offered" };
        public static readonly string[] Difficulties = { "easy", "medium", "hard" };
        public static readonly string[] Events = { "wolf", "thief", "snatched", "fire", "bell", "zone", "quiet", "night", "merchant", "captain" };
        public static readonly string[] Situations = { "", "wolf_gate", "wolf_road", "thief", "fire", "bell" };
        public static readonly string[] Weathers = { "", "clear", "overcast", "rain", "fog" };

        public static LabScenarioFile LoadScenarios(string path = null) =>
            JsonUtility.FromJson<LabScenarioFile>(File.ReadAllText(path ?? ScenariosPath)) ?? new LabScenarioFile();

        public static LabVariantFile LoadVariants(string path = null) =>
            JsonUtility.FromJson<LabVariantFile>(File.ReadAllText(path ?? VariantsPath)) ?? new LabVariantFile();

        /// <summary>What is wrong with the scenarios as written, one line each; empty when nothing is.</summary>
        public static List<string> Check(LabScenarioFile file)
        {
            var problems = new List<string>();
            var ids = new HashSet<string>();
            foreach (var s in file.scenarios)
            {
                string at = string.IsNullOrEmpty(s.id) ? "(a scenario with no id)" : s.id;
                if (string.IsNullOrEmpty(s.id)) problems.Add(at + ": no id");
                else if (!ids.Add(s.id)) problems.Add(at + ": the id is used twice");
                if (string.IsNullOrWhiteSpace(s.purpose)) problems.Add(at + ": no purpose");
                if (!Agents.Contains(s.agent)) problems.Add(at + ": agent '" + s.agent + "' is none of " + string.Join(", ", Agents));
                if (!Kinds.Contains(s.kind)) problems.Add(at + ": kind '" + s.kind + "' is none of " + string.Join(", ", Kinds));
                if (s.kind == "bait" != !string.IsNullOrEmpty(s.bait)) problems.Add(at + ": a bait scenario names its bait, and only a bait one");
                if (!string.IsNullOrEmpty(s.bait) && !Baits.Contains(s.bait)) problems.Add(at + ": bait '" + s.bait + "' is none of " + string.Join(", ", Baits));
                if (!Difficulties.Contains(s.difficulty)) problems.Add(at + ": difficulty '" + s.difficulty + "' is none of " + string.Join(", ", Difficulties));
                var setup = s.setup ?? new LabSetup();
                if (setup.hour != "" && setup.hour != "dusk") problems.Add(at + ": hour is \"\" or dusk");
                if (!Weathers.Contains(setup.weather ?? "")) problems.Add(at + ": weather '" + setup.weather + "'");
                if (!Situations.Contains(setup.situation ?? "")) problems.Add(at + ": situation '" + setup.situation + "'");
                if (!string.IsNullOrEmpty(setup.player) && setup.player != "far" && !setup.player.StartsWith("zone:")) problems.Add(at + ": player is \"\", far or zone:<id>");
                foreach (var q in setup.quests ?? new List<LabQuest>())
                    if (q.state != "active" && q.state != "done") problems.Add(at + ": quest " + q.id + " is active or done");
                var turns = s.turns ?? new List<LabTurn>();
                if (turns.Count == 0) problems.Add(at + ": no turns");
                if (turns.Count > 0 && turns.Last().primer) problems.Add(at + ": the last turn is a primer, so nothing is scored");
                if (s.kind == "memory" != turns.Count > 1) problems.Add(at + ": a memory probe has two turns or more, and only a memory probe");
                for (int i = 0; i < turns.Count; i++)
                {
                    var t = turns[i];
                    string turn = at + " turn " + (i + 1);
                    if (string.IsNullOrWhiteSpace(t.say) == string.IsNullOrWhiteSpace(t.@event)) problems.Add(turn + ": says a line or names an event, one of the two");
                    if (t.IsEvent && !Events.Contains(t.@event)) problems.Add(turn + ": event '" + t.@event + "' is none of " + string.Join(", ", Events));
                    if ((t.accept ?? new List<string>()).Count == 0) problems.Add(turn + ": nothing is acceptable");
                    foreach (var pair in (t.accept ?? new List<string>()).Concat(t.weaker ?? new List<string>()).Concat(t.mustNot ?? new List<string>()))
                        if (!LabPair.TryParse(pair, out _)) problems.Add(turn + ": '" + pair + "' is not \"action target\"");
                    foreach (var pair in t.accept ?? new List<string>())
                        if (LabPair.TryParse(pair, out var p) && (t.mustNot ?? new List<string>()).Any(m => LabPair.TryParse(m, out var n) && n.Overlaps(p)))
                            problems.Add(turn + ": '" + pair + "' is both acceptable and a must-not");
                }
            }
            return problems;
        }

        /// <summary>What is wrong with the variants as written; empty when nothing is.</summary>
        public static List<string> Check(LabVariantFile file)
        {
            var problems = new List<string>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!file.variants.Any(v => v.name == "baseline")) problems.Add("there is no variant named baseline");
            foreach (var v in file.variants)
            {
                string at = string.IsNullOrEmpty(v.name) ? "(a variant with no name)" : v.name;
                if (string.IsNullOrEmpty(v.name) || v.name.Contains("~") || v.name.Contains(",")) problems.Add(at + ": a name is needed, without ~ or ,");
                else if (!names.Add(v.name)) problems.Add(at + ": the name is used twice");
                foreach (var s in v.prompt ?? new List<LabSwitch>())
                    if (!LabTuning.TryParseSwitch(s, out string why)) problems.Add(at + ": " + why);
                foreach (var p in v.persona ?? new List<LabPersona>())
                    if (!Agents.Contains(p.agent)) problems.Add(at + ": a persona for '" + p.agent + "'");
                foreach (var st in v.statements ?? new List<LabStatement>())
                    if (!Agents.Contains(st.agent) || string.IsNullOrEmpty(st.action)) problems.Add(at + ": a statement needs an agent and an action");
                if (v.observations != "" && v.observations != "terse" && v.observations != "none") problems.Add(at + ": observations is \"\", terse or none");
                if (v.layout != "" && v.layout != "sources") problems.Add(at + ": layout is \"\" or sources");
                var m = v.memory ?? new LabMemory();
                if (m.historyTurns != "" && !(int.TryParse(m.historyTurns, out int n) && n >= 0)) problems.Add(at + ": historyTurns is \"\" or a number");
                if (m.commit != "" && m.commit != "OnDecide" && m.commit != "OnExecute") problems.Add(at + ": commit is \"\", OnDecide or OnExecute");
                var pr = v.provider ?? new LabProvider();
                if (pr.temperature != "" && !float.TryParse(pr.temperature, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _)) problems.Add(at + ": temperature is not a number");
                if (pr.numCtx != "" && !int.TryParse(pr.numCtx, out _)) problems.Add(at + ": numCtx is not a number");
                if (pr.timeoutSeconds != "" && !float.TryParse(pr.timeoutSeconds, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _)) problems.Add(at + ": timeoutSeconds is not a number");
            }
            return problems;
        }
    }

    /// <summary>One "action target" answer, where the target may be * or several ids joined by |.</summary>
    public readonly struct LabPair
    {
        public readonly string Action;
        public readonly string[] Targets;

        private LabPair(string action, string[] targets)
        {
            Action = action;
            Targets = targets;
        }

        public bool AnyTarget => Targets.Length == 1 && Targets[0] == "*";

        public static bool TryParse(string text, out LabPair pair)
        {
            pair = default;
            var parts = (text ?? "").Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return false;
            var targets = parts[1].Split('|').Where(t => t.Length > 0).ToArray();
            if (targets.Length == 0) return false;
            pair = new LabPair(parts[0], targets);
            return true;
        }

        public bool Matches(string action, string target) =>
            action == Action && (AnyTarget || Targets.Contains(target ?? ""));

        public bool Overlaps(LabPair other) =>
            other.Action == Action && (AnyTarget || other.AnyTarget || other.Targets.Any(Targets.Contains));
    }
}
