using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Agenerela.Tests
{
    /// <summary>
    /// The live half of the Phase 3 gate (build plan §3, #110): the prototype's 20-prompt suite,
    /// run through the framework's own pipeline against a 2B served by a local Ollama, with the
    /// guards on and, as the control arm, with <see cref="DecideOptions.CheckTarget"/> off. The
    /// gate passes at 19 of 20 or better with the guards on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The suite is <c>tools/benchmarks/compare_2b_4b.py</c>'s, prompts word for word: 5 simple
    /// commands, 5 commands with a target, 6 impossible requests and 4 chat lines, with their
    /// expected actions and targets and whether the agent is already following. It is the
    /// "20-prompt focused suite" behind Appendix A's 95%, scored the way that script scores it: the
    /// action must match, and the target too where one is expected. All 20 are scored, and the 6
    /// impossible requests are reported on their own as well, since 5 of 6 alone is 83%.
    /// </para>
    /// <para>
    /// <b>How the targets reach the prompt.</b> <see cref="PromptBuilder"/> lists none, and Ollama
    /// never shows the model the schema (findings, 10 October 2026). So each decision carries the
    /// five targets as observations, one line each, id then a short description, as GreyBoxVillage
    /// does, and one line for whether the guard is following the player, which the prototype put in
    /// its system prompt. DR-016's numbers came from a prompt that listed every target, so the
    /// findings entry records this choice beside the result.
    /// </para>
    /// <para>
    /// <b>The arms.</b> Each prompt is decided twice in a row, guards on and then guards off, on two
    /// agents sharing one provider, so both arms run in one session on one warm model. The model's
    /// request is the same in both: with a provider that reports token probabilities, turning the
    /// check off changes no prompt line (#107), only whether the grounding guard reads the reply.
    /// Decoding is greedy, as <see cref="Providers.OllamaProvider"/> sends it. No memory is kept,
    /// so no prompt sees another, as in the prototype.
    /// </para>
    /// <para>
    /// At n = 20 the noise floor is wide. This gate says whether Phase 3 passes, not by how many
    /// points the guard improves accuracy: that claim is Phase 4's, at 200+ prompts (hard rule 2).
    /// Each decision prints a <c>PHASE3</c> line and each arm a <c>PHASE3-ARM</c> summary, which
    /// the findings entry is built from.
    /// </para>
    /// </remarks>
    public sealed class Phase3LiveSuiteTests
    {
        private const string Group1 = "1. simple commands";
        private const string Group2 = "2. commands with a target";
        private const string Group3 = "3. impossible requests";
        private const string Group4 = "4. chat";
        private const int PassMark = 19;

        // compare_2b_4b.py's GROUPS, its target names as the ids FewShotBuilder displays them.
        private static readonly SuitePrompt[] Suite =
        {
            new SuitePrompt(Group1, "Follow me.", "follow_player", null, false),
            new SuitePrompt(Group1, "Come with me please.", "follow_player", null, false),
            new SuitePrompt(Group1, "Stay close.", "follow_player", null, false),
            new SuitePrompt(Group1, "Wait here.", "stop_following", null, true),
            new SuitePrompt(Group1, "Hold your position.", "stop_following", null, true),

            new SuitePrompt(Group2, "Move to the Bridge.", "move_to", "bridge", false),
            new SuitePrompt(Group2, "Walk to the Bridge please.", "move_to", "bridge", false),
            new SuitePrompt(Group2, "Go to the Tower.", "move_to", "tower", false),
            new SuitePrompt(Group2, "Attack the TrainingDummy.", "attack_target", "training_dummy", false),
            new SuitePrompt(Group2, "Pick up the Sword.", "pick_up_item", "sword", false),

            new SuitePrompt(Group3, "Attack Godzilla.", ActionRegistry.None, null, false),
            new SuitePrompt(Group3, "Pick up Excalibur.", ActionRegistry.None, null, false),
            new SuitePrompt(Group3, "Go to the Castle.", ActionRegistry.None, null, false),
            new SuitePrompt(Group3, "Move to Moon.", ActionRegistry.None, null, false),
            new SuitePrompt(Group3, "Fly to Mars.", ActionRegistry.None, null, false),
            new SuitePrompt(Group3, "Attack the player.", ActionRegistry.None, null, false),

            new SuitePrompt(Group4, "Hello Marcus.", ActionRegistry.None, null, false),
            new SuitePrompt(Group4, "How is the weather?", ActionRegistry.None, null, false),
            new SuitePrompt(Group4, "Thank you.", ActionRegistry.None, null, false),
            new SuitePrompt(Group4, "That's an interesting sword you have.", ActionRegistry.None, null, false),
        };

        // The prototype's five targets as registered ids, each with the line it is observed by.
        private static readonly (string Id, string Description)[] Targets =
        {
            ("bridge", "the stone bridge over the river"),
            ("tower", "the watchtower at the edge of the village"),
            ("blacksmith", "the blacksmith's forge"),
            ("training_dummy", "a straw training dummy in the yard"),
            ("sword", "a sword lying on the weapon rack"),
        };

        // Hard rule 4: no example may share a phrase or a sentence frame with a suite prompt. The
        // prototype's own examples ("Proceed to the Tower.", "Engage the TrainingDummy.", "Retrieve
        // the Sword.") share the frame "<verb> the <target>." with the suite, so these are new.
        // TheExamplesShareNoPhraseWithTheSuite checks them, as FewShotBuilder renders them, against
        // all 20 prompts. As rendered, the block is:
        //   Player: "I could use an escort on my rounds." -> follow_player   (when not following)
        //   Player: "You can return to your post now." -> stop_following     (when following)
        //   Player: "I need you up on that Tower." -> move_to, tower
        //   Player: "Give that TrainingDummy a good beating." -> attack_target, training_dummy
        //   Player: "Could you grab me that Sword?" -> pick_up_item, sword
        //   Player: "You seem well rested today." -> none
        //   Player: "I need you up on that Atlantis." -> none (the generated refusal)
        private const string IdleExample = "You seem well rested today.";

        private static ActionDefinition[] Actions()
        {
            return new[]
            {
                Action("follow_player", "Start following the player.", "I could use an escort on my rounds.", false),
                Action("stop_following", "Stop following and hold position.", "You can return to your post now.", false),
                Action("move_to", "Walk to the named target.", "I need you up on that {0}.", true, "tower"),
                Action("attack_target", "Attack the named target.", "Give that {0} a good beating.", true, "training_dummy"),
                Action("pick_up_item", "Pick up the named target.", "Could you grab me that {0}?", true, "sword"),
            };
        }

        [OneTimeSetUp]
        public void ForgetTheLastRun()
        {
            LiveOllama.Reset();
        }

        // Runs without Ollama: it checks the test's own data, so it is not in the category.
        [Test]
        public void TheExamplesShareNoPhraseWithTheSuite()
        {
            var registry = new TargetRegistry();
            foreach (var (id, _) in Targets)
            {
                registry.Register(id, id);
            }

            // Every action at once, so both following states' examples are checked.
            string block = FewShotBuilder.Build(Actions(), registry, "Player", IdleExample);
            var examples = block.Split('\n').Skip(1)
                .Select(line => Regex.Match(line, "^Player: \"(?<s>.*)\" -> ").Groups["s"].Value)
                .ToList();

            Assert.That(examples, Has.Count.EqualTo(7).And.None.Empty, "Could not read the few-shot block:\n" + block);

            foreach (string example in examples)
            {
                var exampleWords = Words(example);
                foreach (var prompt in Suite)
                {
                    var promptWords = Words(prompt.Text);
                    Assert.That(string.Join(" ", exampleWords), Is.Not.EqualTo(string.Join(" ", promptWords)),
                        $"Hard rule 4: the example \"{example}\" is the suite prompt \"{prompt.Text}\".");

                    var shared = Phrases(exampleWords).Intersect(Phrases(promptWords)).ToList();
                    Assert.That(shared, Is.Empty,
                        $"Hard rule 4: the example \"{example}\" shares \"{string.Join("\", \"", shared)}\" with the suite "
                      + $"prompt \"{prompt.Text}\". Demonstrating a phrase and then scoring on it invalidates the result.");
                }
            }
        }

        [Test]
        [Category(LiveOllama.Category)]
        public async Task TwentyPromptSuitePassesWithTheGuardsOn()
        {
            LiveOllama.RequireReachable();
            await LiveOllama.WarmUp();
            AssertOnlyTheModelIsResident();
            TestContext.Out.WriteLine($"PHASE3-RUN model={LiveOllama.Model} ollama={Get("/api/version")} endpoint={LiveOllama.Endpoint}");

            var provider = LiveOllama.Provider();
            var guarded = Build(provider);
            var control = Build(provider);
            var on = new Arm("guards-on");
            var off = new Arm("control-check-target-off");

            foreach (var prompt in Suite)
            {
                on.Score(prompt, guarded, await Decide(guarded, prompt, checkTarget: true));
                off.Score(prompt, control, await Decide(control, prompt, checkTarget: false));
            }

            on.Report();
            off.Report();

            Assert.That(on.Correct, Is.GreaterThanOrEqualTo(PassMark),
                $"The Phase 3 gate failed: {on.Correct}/20 with the guards on, below {PassMark}/20 (build plan §3). "
              + $"The control arm scored {off.Correct}/20. Wrong: {string.Join("; ", on.Wrong)}");
        }

        private static async Task<DecisionResult> Decide(Agent agent, SuitePrompt prompt, bool checkTarget)
        {
            agent.State[Following] = prompt.AlreadyFollowing;

            var options = new DecideOptions { IdleExampleStimulus = IdleExample, CheckTarget = checkTarget };
            options.Observations.Add(prompt.AlreadyFollowing ? "You are following the player." : "You are not following the player.");
            foreach (var (id, description) in Targets)
            {
                options.Observations.Add(id + ": " + description + ".");
            }

            var result = await LiveOllama.NotCancelled(agent.DecideAsync(prompt.Text, options));
            Assert.That(result.Telemetry.Outcome, Is.Not.EqualTo(DecisionOutcome.PipelineError),
                $"\"{prompt.Text}\" failed: {result.Telemetry.ErrorKind}: {result.Telemetry.ErrorMessage}");
            return result;
        }

        private const string Following = "isFollowing";

        // The prototype's guard, Marcus, with its five actions; handlers change only the state.
        private static Agent Build(Providers.ILLMProvider provider)
        {
            var identity = new AgentIdentity
            {
                Name = "Marcus",
                Role = "a village guard",
                Personality = "Friendly but cautious village guard. Speaks naturally and briefly.",
            };

            var agent = new Agent(identity, Actions(), provider) { Memory = null };
            agent.Bind("follow_player", new DelegateActionHandler(ctx => !(bool)ctx.State[Following], (ctx, d) => agent.State[Following] = true));
            agent.Bind("stop_following", new DelegateActionHandler(ctx => (bool)ctx.State[Following], (ctx, d) => agent.State[Following] = false));
            agent.Bind("move_to", new DelegateActionHandler(ctx => true, (ctx, d) => { }));
            agent.Bind("attack_target", new DelegateActionHandler(ctx => true, (ctx, d) => { }));
            agent.Bind("pick_up_item", new DelegateActionHandler(ctx => true, (ctx, d) => { }));

            foreach (var (id, _) in Targets)
            {
                agent.Targets.Register(id, id);
            }

            agent.State[Following] = false;
            return agent;
        }

        // Hard rule 3: two models resident on an 8 GB card share compute. Asked after the warm-up,
        // so the model under test is loaded and anything else shows.
        private static void AssertOnlyTheModelIsResident()
        {
            string ps = Get("/api/ps");
            var resident = Regex.Matches(ps, "\"name\":\\s*\"(?<n>[^\"]+)\"").Cast<Match>().Select(m => m.Groups["n"].Value).ToList();
            Assert.That(resident, Is.EqualTo(new[] { LiveOllama.Model }),
                $"Hard rule 3: {LiveOllama.Model} must be the only model loaded, and Ollama has [{string.Join(", ", resident)}]. "
              + "Unload the others first (POST /api/generate with keep_alive 0).");
        }

        // On a worker thread, so the blocking wait cannot deadlock against Unity's synchronization context.
        private static string Get(string path)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
            {
                return Task.Run(() => client.GetStringAsync(LiveOllama.Endpoint.TrimEnd('/') + path)).Result.Trim();
            }
        }

        // ---- Hard rule 4's check -------------------------------------------------------------

        // Words too common to make a phrase shared on their own: a phrase of two of these is ignored.
        private static readonly HashSet<string> Common = new HashSet<string>
        {
            "a", "an", "the", "to", "me", "my", "you", "your", "i", "it", "is", "on", "at", "in", "of", "and", "that", "this", "now", "please",
        };

        // Lower case, punctuation gone, every spelling of a target ("Tower", "TrainingDummy",
        // "training dummy") as one placeholder, so a frame that differs only in its target matches.
        private static List<string> Words(string text)
        {
            string s = " " + Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9_' ]", " ").Replace("'", "") + " ";
            foreach (var (id, _) in Targets)
            {
                foreach (string form in new[] { id, id.Replace("_", ""), id.Replace("_", " ") })
                {
                    s = s.Replace(" " + form + " ", " {t} ");
                }
            }

            return s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        // Every two-word phrase with at least one uncommon word, the target placeholder included.
        private static IEnumerable<string> Phrases(List<string> words)
        {
            for (int i = 0; i + 1 < words.Count; i++)
            {
                if (!Common.Contains(words[i]) || !Common.Contains(words[i + 1]))
                {
                    yield return words[i] + " " + words[i + 1];
                }
            }
        }

        private static ActionDefinition Action(string id, string description, string exampleStimulus,
                                               bool requiresTarget, params string[] preferredTargets)
        {
            return new ActionDefinition
            {
                Id = id,
                Description = description,
                RequiresTarget = requiresTarget,
                ExampleStimulus = exampleStimulus,
                PreferredExampleTargets = preferredTargets,
            };
        }

        private sealed class SuitePrompt
        {
            public readonly string Group;
            public readonly string Text;
            public readonly string Action;
            public readonly string Target;
            public readonly bool AlreadyFollowing;

            public SuitePrompt(string group, string text, string action, string target, bool alreadyFollowing)
            {
                Group = group;
                Text = text;
                Action = action;
                Target = target;
                AlreadyFollowing = alreadyFollowing;
            }
        }

        // One arm's score, as compare_2b_4b.py grades it, plus what findings.md records: the wrong
        // actions carried out and the good requests refused.
        private sealed class Arm
        {
            private readonly string name;
            private readonly Dictionary<string, (int Correct, int Total)> groups = new Dictionary<string, (int, int)>();

            public Arm(string name)
            {
                this.name = name;
            }

            public int Correct { get; private set; }
            public List<string> Wrong { get; } = new List<string>();
            public List<string> CarriedOutWrong { get; } = new List<string>();
            public List<string> Refused { get; } = new List<string>();
            public int Rewrites { get; private set; }

            public void Score(SuitePrompt prompt, Agent agent, DecisionResult result)
            {
                var d = result.Decision;
                bool correct = d.ActionId == prompt.Action && (prompt.Target == null || d.TargetId == prompt.Target);
                bool executed = agent.Execute(result);

                groups.TryGetValue(prompt.Group, out var g);
                groups[prompt.Group] = (g.Correct + (correct ? 1 : 0), g.Total + 1);

                if (result.Verdict.HasValue)
                {
                    Rewrites++;
                }

                if (correct)
                {
                    Correct++;
                }
                else
                {
                    string what = $"\"{prompt.Text}\" -> {d.ActionId}/{d.TargetId}, expected {prompt.Action}"
                                + (prompt.Target != null ? "/" + prompt.Target : "");
                    Wrong.Add(what);
                    if (d.ActionId != ActionRegistry.None && executed)
                    {
                        CarriedOutWrong.Add(what);
                    }

                    if (prompt.Action != ActionRegistry.None && d.ActionId == ActionRegistry.None)
                    {
                        Refused.Add(what);
                    }
                }

                var t = result.Telemetry;
                TestContext.Out.WriteLine(
                    $"PHASE3 arm={name} group=\"{prompt.Group}\" prompt=\"{prompt.Text}\" ok={correct} "
                  + $"model={result.ModelDecision.ActionId}/{result.ModelDecision.TargetId} decision={d.ActionId}/{d.TargetId} "
                  + $"grounding={(t.TargetGrounding.HasValue ? t.TargetGrounding.Value.ToString("0.###") : "-")} status={t.TargetGroundingStatus} "
                  + $"guard={(result.Verdict.HasValue ? result.Verdict.Value.Guard + ":" + result.Verdict.Value.Kind : "-")} "
                  + $"latency={t.LatencySeconds:F2}");
            }

            public void Report()
            {
                TestContext.Out.WriteLine(
                    $"PHASE3-ARM arm={name} total={Correct}/20 "
                  + string.Join(" ", groups.Select(kv => $"\"{kv.Key}\"={kv.Value.Correct}/{kv.Value.Total}"))
                  + $" guard_rewrites={Rewrites} wrong_carried_out={CarriedOutWrong.Count} good_refused={Refused.Count}");
                foreach (string w in Wrong)
                {
                    TestContext.Out.WriteLine($"PHASE3-WRONG arm={name} {w}");
                }
            }
        }
    }
}
