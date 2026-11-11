using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Agenerela;
using Agenerela.Providers;
using Demos.GreyBoxVillage.Lab;
using Demos.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Demos.GreyBoxVillage.Tests
{
    /// <summary>
    /// The lab bench (L2) pinned to scripted models: its data is well formed and keeps hard rule 4,
    /// it scores a right answer right and a wrong one wrong, a preview changes nothing in the village,
    /// and the whole bench, played in Play mode, scores the oracle 100%, the contrary model 0%, and
    /// finds the village after every F1 as it found it after the first.
    /// </summary>
    public sealed class LabBenchTests
    {
        // ---- The data -----------------------------------------------------------------------------

        [Test]
        public void TheScenariosAndVariantsAreWellFormedAndBalanced()
        {
            var scenarios = LabFiles.LoadScenarios();
            var variants = LabFiles.LoadVariants();
            Assert.That(LabFiles.Check(scenarios), Is.Empty);
            Assert.That(LabFiles.Check(variants), Is.Empty);
            var all = scenarios.scenarios;
            Assert.That(all.Count, Is.InRange(70, 100), "The brief asks for about 80.");
            double Share(params string[] kinds) => all.Count(s => kinds.Contains(s.kind)) / (double)all.Count;
            Assert.That(Share("command"), Is.InRange(0.35, 0.45), "about 40% clear commands");
            Assert.That(Share("question", "smalltalk"), Is.InRange(0.15, 0.25), "about 20% questions or small talk");
            Assert.That(Share("event"), Is.InRange(0.10, 0.20), "about 15% events");
            Assert.That(Share("bait"), Is.InRange(0.10, 0.20), "about 15% bait");
            Assert.That(Share("memory"), Is.InRange(0.05, 0.15), "about 10% two-turn memory probes");
            foreach (var agent in LabFiles.Agents)
                Assert.That(all.Count(s => s.agent == agent), Is.GreaterThanOrEqualTo(12), agent);
            // Small talk and questions have none as their answer; that is what they check.
            foreach (var s in all.Where(s => s.kind == "question" || s.kind == "smalltalk"))
                Assert.That(s.turns.Last().accept, Does.Contain("none no_target"), s.id);
            Assert.That(variants.variants.Single(v => v.name == "baseline").prompt, Is.Empty.Or.Null, "baseline must be the game, nothing set");
        }

        // Bram is the evaluation agent: his scenarios ask only for his pinned actions (LAB.md).
        [Test]
        public void BramsScenariosUseOnlyHisPinnedActions()
        {
            string[] his = { "follow_player", "stop_following", "move_to", "attack_target", "warn_player", "block_path", "sound_alarm", "patrol", "none" };
            foreach (var s in LabFiles.LoadScenarios().scenarios.Where(s => s.agent == "Bram"))
                foreach (var t in s.turns)
                    foreach (var pair in t.accept.Concat(t.weaker))
                    {
                        Assert.That(LabPair.TryParse(pair, out var p), Is.True);
                        Assert.That(his, Does.Contain(p.Action), s.id + ": " + pair);
                    }
        }

        // ---- Hard rule 4 -------------------------------------------------------------------------

        [Test]
        public void NoScenarioLineIsAnEvaluationPromptOrAFewShotExample()
        {
            var prompts = LabRuleFour.EvaluationPrompts();
            Assert.That(prompts.Count, Is.GreaterThan(400), "Both evaluation sets, the curated one and the interim one, must be read.");
            var examples = Examples();
            Assert.That(examples.Count, Is.GreaterThan(15), "The agents' few-shot lines were not found.");
            var scenarios = LabFiles.LoadScenarios();
            Assert.That(LabRuleFour.Check(LabRuleFour.Lines(scenarios), prompts, examples), Is.Empty);
            // The bench's own warm-up and probe lines are never scored, but must not be a scenario's,
            // a prompt or an example either.
            var own = LabBench.WarmUpLines.Concat(LabBench.ProbeLines).Select(l => ("the bench's own line", l)).ToList();
            Assert.That(LabRuleFour.Check(own, LabRuleFour.Lines(scenarios).Select(x => x.Line).Concat(prompts), examples), Is.Empty);
        }

        [Test]
        public void TheRuleFourCheckCatchesAPlantedCopy()
        {
            var prompts = new[] { "Take me to the docks.", "Wait here." };
            var examples = new[] { "Go and stand by the {0}.", "Fine day for it." };
            List<string> Check(string line) => LabRuleFour.Check(new[] { ("planted", line) }, prompts, examples);
            Assert.That(Check("take me to the DOCKS!"), Is.Not.Empty, "a prompt with its case and punctuation changed");
            Assert.That(Check("Go and stand by the well."), Is.Not.Empty, "an example's template with a target");
            Assert.That(Check("Please, take me to the docks right now."), Is.Not.Empty, "a line holding a prompt whole");
            Assert.That(Check("Fine day for it, Bram."), Is.Not.Empty, "a line holding the idle example whole");
            Assert.That(Check("wait here"), Is.Not.Empty, "a short prompt, equal");
            Assert.That(Check("Wait here by the cart while I look."), Is.Empty, "a two-word prompt inside a longer line is not a copy");
            Assert.That(Check("Head for the shelter, Bram."), Is.Empty, "a fresh line");
        }

        // Every few-shot line the four agents are shown, and every variant's idle example.
        private static List<string> Examples()
        {
            var scene = SceneManager.GetSceneByPath(DemoLauncher.VillagePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(DemoLauncher.VillagePath, OpenSceneMode.Additive);
            try
            {
                var commands = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<VillageCommands>()).Single();
                var lines = new List<string>();
                foreach (var mind in commands.Minds)
                {
                    foreach (var d in mind.Agent.Actions.Definitions)
                    {
                        lines.Add(d.ExampleStimulus);
                        lines.Add(d.CounterExampleStimulus);
                    }
                    lines.Add(mind.IdleExampleStimulus);
                    lines.Add(mind.Behaviour.Profile.IdleExampleStimulus);
                }
                foreach (var v in LabFiles.LoadVariants().variants)
                    lines.AddRange(v.prompt.Where(s => s.name == "IdleExampleStimulus").Select(s => s.value));
                return lines.Where(l => !string.IsNullOrWhiteSpace(l)).Distinct().ToList();
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        // One run per folder: a second would append to the first's log, its sequence numbers colliding.
        [Test]
        public void AFolderThatHoldsARunIsRefused()
        {
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lab-bench-test-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(folder);
            try
            {
                Assert.That(LabBench.DecisionsPath(folder), Does.EndWith("decisions.jsonl"));
                System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "decisions.jsonl"), "{}\n");
                Assert.Throws<InvalidOperationException>(() => LabBench.DecisionsPath(folder));
            }
            finally { System.IO.Directory.Delete(folder, true); }
        }

        // A refused folder keeps everything in it: the refusal comes before anything is opened or written.
        [Test]
        public void ARefusedRunWritesNothingOverTheRunThere()
        {
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lab-bench-test-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(folder);
            try
            {
                var names = new[] { "decisions.jsonl", "scores.jsonl", "run.json", "report.md", "statements.md" };
                foreach (var name in names) System.IO.File.WriteAllText(System.IO.Path.Combine(folder, name), "the first run's " + name);
                var settings = new LabBench.Settings { Out = folder };
                Assert.Throws<InvalidOperationException>(() => LabBench.Start(settings, exit: false));
                Assert.That(EditorApplication.isPlaying, Is.False);
                foreach (var name in names)
                    Assert.That(System.IO.File.ReadAllText(System.IO.Path.Combine(folder, name)), Is.EqualTo("the first run's " + name), name);
            }
            finally { System.IO.Directory.Delete(folder, true); }
        }

        // ---- Scoring -------------------------------------------------------------------------------

        [Test]
        public void AWrongAnswerScoresAsWrong()
        {
            var turn = new LabTurn { say = "x", accept = { "sell bread" }, weaker = { "give bread" }, mustNot = { "give rope" } };
            LabTurnResult Scored(string action, string target, string outcome, string kind = "command")
            {
                var r = new LabTurnResult { modelAction = action, modelTarget = target, outcome = outcome, kind = kind };
                LabScore.Score(r, turn);
                return r;
            }
            Assert.That(Scored("sell", "bread", "executed").verdict, Is.EqualTo("exact"));
            Assert.That(Scored("give", "bread", "executed").verdict, Is.EqualTo("weaker"));
            var wrong = Scored("give", "rope", "executed");
            Assert.That(wrong.verdict, Is.EqualTo("wrong"));
            Assert.That(wrong.actionRight, Is.False);
            Assert.That(wrong.mustNotAsked && wrong.mustNotRan, Is.True, "a must-not asked and run");
            var stopped = Scored("give", "rope", "contained", "bait");
            Assert.That(stopped.verdict, Is.EqualTo("wrong"), "accuracy is the model's answer, whatever a guard made of it");
            Assert.That(stopped.mustNotAsked && !stopped.mustNotRan, Is.True);
            Assert.That(stopped.caughtBy, Is.EqualTo("guard"));
            Assert.That(Scored("sell", "apple", "executed").actionRight, Is.True, "the action alone is right");
            Assert.That(Scored("sell", "apple", "executed").verdict, Is.EqualTo("wrong"));
            Assert.That(Scored("", "", "error").verdict, Is.EqualTo("error"));
            Assert.That(Scored("none", "no_target", "none", "bait").caughtBy, Is.EqualTo(""), "a bait answered with something unacceptable is not caught");
        }

        [Test]
        public void ThePairedTestIsTheExactSignTest()
        {
            Assert.That(LabScore.McNemar(0, 0), Is.EqualTo(1));
            Assert.That(LabScore.McNemar(0, 6), Is.EqualTo(2.0 / 64).Within(1e-12));
            Assert.That(LabScore.McNemar(1, 9), Is.EqualTo(2.0 * 11 / 1024).Within(1e-12));
            Assert.That(LabScore.McNemar(5, 5), Is.EqualTo(1));
            var results = new List<LabTurnResult>();
            for (int i = 0; i < 10; i++)
            {
                results.Add(new LabTurnResult { arm = "a", scenario = "s" + i, turn = 1, verdict = i < 2 ? "exact" : "wrong" });
                results.Add(new LabTurnResult { arm = "b", scenario = "s" + i, turn = 1, verdict = "exact" });
            }
            var c = LabPairComparison.Of("a", "b", results);
            Assert.That((c.Paired, c.BothRight, c.OnlyA, c.OnlyB), Is.EqualTo((10, 2, 0, 8)));
            Assert.That(c.DifferencePoints, Is.EqualTo(80));
        }

        // ---- A preview sends nothing, and changes nothing --------------------------------------------

        // Agent.Preview runs every target and observation source and every availability check. In
        // every situation the bench sets up, previewing every agent leaves the village as it was.
        [Test]
        public void APreviewLeavesTheVillageUnchanged()
        {
            var scene = SceneManager.GetSceneByPath(DemoLauncher.VillagePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(DemoLauncher.VillagePath, OpenSceneMode.Additive);
            var commands = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<VillageCommands>()).Single();
            var original = commands.Mind("Bram").Behaviour.Provider;
            var config = ScriptableObject.CreateInstance<ScriptedProviderConfig>();
            var provider = config.Scripted.Otherwise(ScriptedReply.Decide("none", "no_target", "Hm."));
            commands.UseProvider(config);
            var merchant = (VillageMerchant)commands.Mind("Odile").Body;
            // The thief's own report to Odile would ask the provider; only the previews are tested.
            commands.EventsMuted = true;
            try
            {
                VillageSituationTests.ResetVillage(commands);
                commands.Inventory.Reset(8);
                commands.Quests.Define();
                foreach (var setup in new Action[]
                {
                    () => { },
                    () => { merchant.Thief.Begin(); merchant.Thief.Grab(); },
                    () => merchant.Fire.Ignite(),
                    () => { commands.Inventory.Add("moonpetal", 3); commands.Quests.Get("quest_moonpetals").State = QuestState.Active; },
                })
                {
                    setup();
                    string before = LabBench.Snapshot(commands);
                    int records = commands.Recorder.Records.Count;
                    int requests = provider.RequestCount;
                    foreach (var mind in commands.Minds)
                        foreach (var (source, label) in new[] { ("player", "Player"), ("wolf", "Event"), ("quiet", "Time"), ("merchant", "Merchant") })
                        {
                            var preview = mind.Agent.Preview("Anything?", mind.OptionsFor(source, label, DecisionPriority.PlayerFacing));
                            Assert.That(preview.Request, Is.Not.Null, mind.Name + " " + source + ": " + preview.Error);
                        }
                    Assert.That(LabBench.Snapshot(commands), Is.EqualTo(before), "A preview changed the village.");
                    Assert.That(commands.Recorder.Records.Count, Is.EqualTo(records), "A preview was recorded as a decision.");
                    Assert.That(provider.RequestCount, Is.EqualTo(requests), "A preview asked the provider.");
                }
            }
            finally
            {
                commands.EventsMuted = false;
                VillageSituationTests.ResetVillage(commands);
                commands.Inventory.Reset(8);
                commands.Quests.Define();
                if (opened) EditorSceneManager.CloseScene(scene, true);
                else commands.UseProvider(original);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

    }

    /// <summary>
    /// The whole bench, played in Play mode on two scripted models over every scenario: the oracle
    /// scores 100%, the contrary model 0%, and the village after every F1 is the village after the
    /// first. Its own fixture, since it leaves Play mode and empties the scene after.
    /// </summary>
    public sealed class LabBenchPlayTests
    {
        private ScriptedProviderConfig oracle, contrary;

        [UnityTest]
        public IEnumerator TheBenchScoresTheOracleRightAndTheContraryWrongAndLeavesNothingBehind()
        {
            EditorSceneManager.OpenScene(DemoLauncher.VillagePath);
            yield return new EnterPlayMode();
            yield return null;
            // As VillagePresenterScriptTests: the body is its own enumerator, made once Play has started.
            var body = Body();
            while (true)
            {
                bool more;
                try { more = body.MoveNext(); }
                catch (Exception e)
                {
                    Debug.Log("[bench test] " + e);
                    throw;
                }
                if (!more) break;
                yield return body.Current;
            }
            yield return new ExitPlayMode();
        }

        private IEnumerator Body()
        {
            oracle = ScriptableObject.CreateInstance<ScriptedProviderConfig>();
            oracle.Scripted.Callback = LabScripted.Oracle;
            contrary = ScriptableObject.CreateInstance<ScriptedProviderConfig>();
            contrary.Scripted.Callback = LabScripted.Contrary;
            var baseline = LabFiles.LoadVariants().variants.Single(v => v.name == "baseline");
            var settings = new LabBench.Settings
            {
                Scenarios = LabFiles.LoadScenarios().scenarios,
                Arms =
                {
                    new LabBench.Arm { Name = "oracle", Variant = baseline, Config = oracle },
                    new LabBench.Arm { Name = "contrary", Variant = baseline, Config = contrary },
                },
                WarmUpDecisions = 0,
                TimeScale = 4f,
                TurnTimeoutSeconds = 10f,
            };
            var run = new LabBench.Run();
            var play = LabBench.Play(settings, run);
            while (play.MoveNext()) yield return play.Current;

            int scored = settings.Scenarios.Sum(s => s.turns.Count(t => !t.primer));
            var oracleMissed = run.Results.Where(r => r.arm == "oracle" && !r.primer && !r.Exact)
                .Select(r => r.scenario + " turn " + r.turn + ": chose " + r.modelAction + " " + r.modelTarget + " (" + r.outcome + (r.error == "" ? "" : ", " + r.error) +
                             "), offered " + string.Join(" ", r.offeredActions) + " / " + string.Join(" ", r.offeredTargets)).ToList();
            var contraryRight = run.Results.Where(r => r.arm == "contrary" && !r.primer && r.Exact)
                .Select(r => r.scenario + " turn " + r.turn + ": " + r.modelAction + " " + r.modelTarget).ToList();
            var problems = new List<string>();
            int asked = run.Results.Count(r => r.arm == "oracle" && !r.primer);
            if (asked != scored) problems.Add("the oracle was asked " + asked + " of " + scored + " scored turns");
            problems.AddRange(oracleMissed.Select(m => "the oracle scored wrong (an acceptable answer the setup does not offer?): " + m));
            problems.AddRange(contraryRight.Select(m => "the contrary model scored right: " + m));
            problems.AddRange(run.Leaks.Select(l => "state left behind: " + l));
            problems.AddRange(run.Results.Where(r => r.outcome == "error").Select(r => "error: " + r.arm + " " + r.scenario + ": " + r.error));
            problems.AddRange(run.Notes.Where(n => n.StartsWith("The run stopped")));
            if (run.Failures > 0) problems.Add(run.Failures + " failure(s)");
            // Every problem in the message: NUnit shows only the first ten items of a list.
            Assert.That(problems.Count, Is.Zero, problems.Count + " problem(s):\n" + string.Join("\n", problems));
            // Nothing of the bench is left on the agents.
            var commands = UnityEngine.Object.FindFirstObjectByType<VillageCommands>();
            Assert.That(commands.EventsMuted, Is.False);
            Assert.That(commands.Minds.All(m => m.Tune == null), Is.True);
        }

        // A turn the model never answers is cancelled and settled before anything else is asked, and
        // the rest of its scenario is not asked (Codex, L2).
        [UnityTest]
        public IEnumerator ATurnThatNeverAnswersIsCancelledAndTheRestOfItsScenarioSkipped()
        {
            EditorSceneManager.OpenScene(DemoLauncher.VillagePath);
            yield return new EnterPlayMode();
            yield return null;
            var body = Held();
            while (true)
            {
                bool more;
                try { more = body.MoveNext(); }
                catch (Exception e)
                {
                    Debug.Log("[bench test] " + e);
                    throw;
                }
                if (!more) break;
                yield return body.Current;
            }
            yield return new ExitPlayMode();
        }

        private IEnumerator Held()
        {
            oracle = ScriptableObject.CreateInstance<ScriptedProviderConfig>();
            oracle.Scripted.Callback = LabScripted.Oracle;
            oracle.Scripted.Hold = true;
            var baseline = LabFiles.LoadVariants().variants.Single(v => v.name == "baseline");
            var settings = new LabBench.Settings
            {
                Scenarios = LabFiles.LoadScenarios().scenarios.Where(s => s.id == "bram_mem_01").ToList(),
                Arms = { new LabBench.Arm { Name = "held", Variant = baseline, Config = oracle } },
                WarmUpDecisions = 0,
                TurnTimeoutSeconds = 1f,
            };
            var run = new LabBench.Run();
            var play = LabBench.Play(settings, run);
            while (play.MoveNext()) yield return play.Current;
            Assert.That(run.Results.Select(r => r.turn + ": " + r.outcome + ", " + r.error), Is.EqualTo(new[]
            {
                "1: error, no answer within 1 s",
                "2: error, not asked: an earlier turn of this scenario did not finish",
            }));
            Assert.That(run.Notes.Any(n => n.Contains("the agent was reset, and the call ended.")), Is.True, string.Join("\n", run.Notes));
            Assert.That(oracle.Scripted.HeldCount, Is.Zero, "The held request is still with the provider.");
            var bram = UnityEngine.Object.FindFirstObjectByType<VillageCommands>().Mind("Bram");
            Assert.That(bram.IsDeciding, Is.False);
        }

        /// <summary>A provider that never answers and ignores being cancelled, as one stuck on a server might.</summary>
        private sealed class StubbornProvider : ILLMProvider
        {
            public string Name => "stubborn";
            public ProviderCapabilities Capabilities { get; } = new ProviderCapabilities();
            public Awaitable<ProviderResult> RequestAsync(DecisionRequest req, System.Threading.CancellationToken ct) =>
                new AwaitableCompletionSource<ProviderResult>().Awaitable;
        }

        private sealed class StubbornConfig : ProviderConfig
        {
            protected override ILLMProvider CreateProvider() => new StubbornProvider();
        }

        private StubbornConfig stubborn;

        // A call still with the model after it was cancelled holds the queue's one slot, so the run
        // stops rather than time anything behind it (Codex, L2, round 2).
        [UnityTest]
        public IEnumerator ACallThatIgnoresItsCancellationStopsTheRun()
        {
            EditorSceneManager.OpenScene(DemoLauncher.VillagePath);
            yield return new EnterPlayMode();
            yield return null;
            stubborn = ScriptableObject.CreateInstance<StubbornConfig>();
            var baseline = LabFiles.LoadVariants().variants.Single(v => v.name == "baseline");
            var settings = new LabBench.Settings
            {
                Scenarios = LabFiles.LoadScenarios().scenarios.Where(s => s.id == "bram_cmd_01" || s.id == "wren_cmd_01").ToList(),
                Arms = { new LabBench.Arm { Name = "stubborn", Variant = baseline, Config = stubborn } },
                WarmUpDecisions = 0,
                TurnTimeoutSeconds = 1f,
                CancelWaitSeconds = 1f,
            };
            var run = new LabBench.Run();
            var play = LabBench.Play(settings, run);
            Exception stopped = null;
            while (true)
            {
                bool more;
                try { more = play.MoveNext(); }
                catch (Exception e) { stopped = e; break; }
                if (!more) break;
                yield return play.Current;
            }
            Assert.That(stopped, Is.Not.Null, "The run went on with a call still with the model.");
            Assert.That(stopped.Message, Does.Contain("still with the model"));
            Assert.That(run.Results.Count, Is.EqualTo(1), "A scenario was asked after the stuck one.");
            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            Time.timeScale = 1f;
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (oracle != null) UnityEngine.Object.DestroyImmediate(oracle);
            if (contrary != null) UnityEngine.Object.DestroyImmediate(contrary);
            if (stubborn != null) UnityEngine.Object.DestroyImmediate(stubborn);
        }
    }
}
