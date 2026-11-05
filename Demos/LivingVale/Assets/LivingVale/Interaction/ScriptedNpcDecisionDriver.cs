using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agenerela;

namespace LivingVale.Interaction
{
    /// <summary>How the scripted driver answers the next turns: the failure cases G1 must survive, injected through the same boundary a real driver uses.</summary>
    public enum ScriptedFixture
    {
        /// <summary>A short, slightly variable delay, then a line from the NPC's script.</summary>
        Normal,

        /// <summary>A reply after <see cref="ScriptedNpcDecisionDriver.SlowDelay"/>: long enough to switch speakers or cancel first.</summary>
        Slow,

        /// <summary>Never answers on its own; only cancellation or the coordinator's timeout ends the turn.</summary>
        NeverAnswers,

        /// <summary>Answers with something that is not a decision.</summary>
        Malformed,

        /// <summary>The backend throws.</summary>
        Throws
    }

    /// <summary>
    /// The G1–G4 development fixture: keyword-matched lines per NPC, after a delay, with every
    /// decision <c>none / no_target</c>. It is not a model and must never be presented as one,
    /// hence <see cref="Label"/>.
    /// </summary>
    public sealed class ScriptedNpcDecisionDriver : INpcDecisionDriver
    {
        private readonly Dictionary<string, NpcScript> scripts = new Dictionary<string, NpcScript>();
        private readonly Random random;

        /// <param name="seed">Fixes the delays' jitter and the fallback line order, for tests.</param>
        public ScriptedNpcDecisionDriver(int seed = 0)
        {
            random = new Random(seed);
            AddG1Scripts();
        }

        public string Label => Fixture == ScriptedFixture.Normal ? "Scripted replies" : $"Scripted replies ({Fixture})";

        /// <summary>Applies to every turn started after it is set.</summary>
        public ScriptedFixture Fixture { get; set; } = ScriptedFixture.Normal;

        /// <summary>Delay before a <see cref="ScriptedFixture.Normal"/> reply, before jitter of up to half as much again.</summary>
        public TimeSpan NormalDelay { get; set; } = TimeSpan.FromMilliseconds(600);

        public TimeSpan SlowDelay { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>How many turns this driver was asked to execute. Tests use it to prove a stale reply never ran.</summary>
        public int ExecutedCount { get; private set; }

        /// <summary>Adds or replaces an NPC's script. A keyword matches anywhere in the player's line, ignoring case; the first match wins.</summary>
        public void SetScript(string npcId, IEnumerable<(string keyword, string line)> rules, IEnumerable<string> fallbacks)
        {
            var script = new NpcScript();
            foreach (var rule in rules)
            {
                script.Rules.Add(rule);
            }

            script.Fallbacks.AddRange(fallbacks);
            if (script.Fallbacks.Count == 0)
            {
                script.Fallbacks.Add("Hm.");
            }

            scripts[npcId] = script;
        }

        public async Task<NpcReply> DecideAsync(NpcTurn turn, CancellationToken ct)
        {
            var fixture = Fixture;
            switch (fixture)
            {
                case ScriptedFixture.NeverAnswers:
                    await Task.Delay(Timeout.Infinite, ct);
                    throw new InvalidOperationException("Unreachable: an infinite delay only ends by cancellation.");

                case ScriptedFixture.Slow:
                    await Task.Delay(SlowDelay, ct);
                    break;

                default:
                    await Task.Delay(Jittered(NormalDelay), ct);
                    break;
            }

            switch (fixture)
            {
                case ScriptedFixture.Malformed:
                    return NpcReply.Failed(NpcReplyFailure.Malformed);
                case ScriptedFixture.Throws:
                    throw new InvalidOperationException("Scripted backend failure (fixture).");
            }

            string line = LineFor(turn.Npc.Id, turn.PlayerLine);
            return NpcReply.Decided(new AgentDecision(ActionRegistry.None, TargetRegistry.NoTarget, line));
        }

        public bool Execute(NpcTurn turn, NpcReply reply)
        {
            ExecutedCount++;

            // G1 registers no actions, so "none" is the only legal one.
            return reply.Decision != null && reply.Decision.ActionId == ActionRegistry.None;
        }

        private string LineFor(string npcId, string playerLine)
        {
            if (!scripts.TryGetValue(npcId, out var script))
            {
                return "I don't think we've met.";
            }

            foreach (var (keyword, line) in script.Rules)
            {
                if (playerLine.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return line;
                }
            }

            return script.Fallbacks[random.Next(script.Fallbacks.Count)];
        }

        private TimeSpan Jittered(TimeSpan delay)
        {
            return delay + TimeSpan.FromMilliseconds(random.NextDouble() * delay.TotalMilliseconds * 0.5);
        }

        // Placeholder lines for the two G1 NPCs; real dialogue comes from the model at G5.
        private void AddG1Scripts()
        {
            SetScript("npc_mara",
                new[]
                {
                    ("potion", "Healing potions are ten gold each. I'll have stock again once the wagon turns up."),
                    ("wagon", "The medicine wagon never reached us. Ask Iven, he was waiting on it more than anyone."),
                    ("buy", "I'm not selling yet. Come back when my stall's open properly."),
                    ("hello", "Morning, traveler. Mind the mud."),
                    ("bye", "Safe roads.")
                },
                new[]
                {
                    "Can't say I know much about that.",
                    "You're not from the vale, are you?",
                    "Trade's slow since the road went quiet."
                });

            SetScript("npc_iven",
                new[]
                {
                    ("medicine", "Without that wagon I have three patients and nothing to give them."),
                    ("wagon", "It left the castle two days ago. It should have been here by now."),
                    ("herb", "Silverleaf grows by the old mill, but it's no substitute for the real supplies."),
                    ("hello", "Hello. Forgive me, I'm distracted."),
                    ("bye", "Go carefully.")
                },
                new[]
                {
                    "I'm sorry, I don't follow.",
                    "Talk to Mara at the stall, she hears everything.",
                    "Not now, please. Unless it's about the wagon."
                });
        }

        private sealed class NpcScript
        {
            public readonly List<(string keyword, string line)> Rules = new List<(string, string)>();
            public readonly List<string> Fallbacks = new List<string>();
        }
    }
}
