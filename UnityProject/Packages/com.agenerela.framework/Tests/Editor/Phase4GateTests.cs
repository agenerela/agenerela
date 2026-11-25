using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Evaluation;
using Agenerela.Providers;
using Agenerela.Tests.Evaluation;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static Agenerela.Tests.Evaluation.RunnerFixtures;

namespace Agenerela.Tests
{
    /// <summary>
    /// The six checks that close Phase 4 in EditMode (build plan §3, #150), with no model running.
    /// If these pass, the evaluation harness is the instrument the build plan asks for: a set big
    /// and game-triggered enough to measure with, a runner that sorts every answer into five
    /// outcomes under recorded conditions, two people's answers to measure the labels themselves,
    /// and the method written down. Each failure message names the rule that broke.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rules 1, 2, 5 and 6 read our own set in <c>Assets/Evaluation</c> and our wiki, so they run
    /// only in agenerela's <c>UnityProject</c> (<see cref="EvalFixtures.OurSetFolder"/>) and are
    /// ignored anywhere else. Rule 3 runs a hand-built set through <see cref="EvalRunner"/> and
    /// <see cref="FakeProvider"/>; rule 4 runs our set through <see cref="OllamaProvider"/> against a
    /// stand-in server (<see cref="FakeOllama"/>), so the report has a provider version, a residency
    /// reading and a git commit to carry.
    /// </para>
    /// <para>
    /// Rule 5 waits for the second annotator's labels (#381, for #148), so its check on our set is
    /// ignored with that reason until they land; the pull request that adds them removes the
    /// <c>[Ignore]</c> and nothing else. Until then the same check runs against hand-built sets,
    /// one that meets the rule and two that miss it, so the rule itself is proved now. The live half
    /// of the gate, guards on against guards off over the whole set on <c>qwen3.5:2b</c>, is
    /// recorded in <c>docs/llm-wiki/findings.md</c>.
    /// </para>
    /// </remarks>
    public sealed class Phase4GateTests
    {
        private const string OurProfilePath = "Assets/Evaluation/VillageGuard.asset";

        // The build plan's numbers (§3, Phase 4), and the categories a game raises, not a player
        // (Assets/Evaluation/README.md, "Category balance").
        private const int MinimumPrompts = 200;
        private const int MinimumDoublyLabelled = 60;
        private static readonly string[] GameTriggered = { "situation", "event", "turn" };

        private static readonly DecisionOutcome[] FiveOutcomes =
        {
            DecisionOutcome.Correct, DecisionOutcome.WrongLegalAction, DecisionOutcome.ContainedByGuard,
            DecisionOutcome.RejectedWhenActionExpected, DecisionOutcome.PipelineError,
        };

        private RunnerFixtures fixtures;
        private string output;

        [SetUp]
        public void SetUp()
        {
            fixtures = new RunnerFixtures();
            output = Path.Combine(Path.GetTempPath(), "agenerela-gate4-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            fixtures.Dispose();
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }

            Fixtures.DestroyCreated();
        }

        // ---- 1. 200+ prompts, each passing #134's checks and declaring its state ------------------

        [Test]
        public void Gate1_TwoHundredPromptsEachPassingTheChecksAndDeclaringItsState()
        {
            var set = OurSet();
            var profile = OurProfile();
            var prompts = set.Prompts.ToList();

            Assert.That(prompts.Count, Is.GreaterThanOrEqualTo(MinimumPrompts),
                $"Rule 1 broke: the set has {prompts.Count} prompts, and Phase 4 asks for {MinimumPrompts} or more. At n ≈ 50 "
              + "the noise floor was about 10 points, so a smaller set hides most effects worth measuring (build plan §4, #147).");

            var problems = PromptSetChecks.Check(set, profile).Select(p => p.ToString()).ToList();
            Assert.That(problems, Is.Empty,
                $"Rule 1 broke: {problems.Count} problems from #134's checks against {OurProfilePath}, hard rule 4 included. "
              + "A prompt that breaks them is not a labelled prompt: fix it in the Prompt Set window.");

            // Read from the files, not the loaded set: a prompt that leaves the field out loads as an
            // empty state just like one that declares {}, and only the first is undeclared.
            var undeclared = new List<string>();
            foreach (var category in set.Categories)
            {
                string path = set.CategoryPath(category);
                if (!File.Exists(path))
                {
                    continue;
                }

                foreach (var prompt in (JArray)JObject.Parse(File.ReadAllText(path))["prompts"])
                {
                    if (!(prompt is JObject p) || !(p["preconditionState"] is JObject))
                    {
                        undeclared.Add((string)prompt["id"] ?? $"a prompt in {category.File}");
                    }
                }
            }

            Assert.That(undeclared, Is.Empty,
                "Rule 1 broke: these prompts do not declare preconditionState as an object. Every prompt says the state it "
              + "assumes, {} included, so the runner stages it before asking (build plan §4, item 4; #145).");
        }

        // ---- 2. At least half are game-triggered ------------------------------------------------

        [Test]
        public void Gate2_AtLeastHalfAreGameTriggered()
        {
            var set = OurSet();
            int all = set.Prompts.Count();
            int game = set.Categories.Where(c => GameTriggered.Contains(c.Id)).Sum(c => c.Prompts.Count);

            Assert.That(all, Is.GreaterThan(0), "Rule 2 broke: the set has no prompts.");
            Assert.That(2 * game, Is.GreaterThanOrEqualTo(all),
                $"Rule 2 broke: {game} of {all} prompts are game-triggered ({string.Join(", ", GameTriggered)}), fewer than half. "
              + "An accuracy measured mostly on player chat says little about an agent the game itself asks (#147, DR-008).");
        }

        // ---- 3. The five outcomes, and both arms ran every prompt -------------------------------

        [Test]
        public async Task Gate3_AHandBuiltRunIsClassifiedIntoTheFiveOutcomesInBothArms()
        {
            // One prompt per outcome, each answered as that outcome needs. None is an evaluation
            // prompt or a few-shot example (hard rule 4). Arm B is the live A/B's control:
            // CheckTarget off and no developer guards, so only the legality guard stands.
            var prompts = new[]
            {
                (Prompt("target_001", "Gate four: meet me down at the bridge.", "move_to", "bridge", false),
                    Reply("move_to", "bridge"), DecisionOutcome.Correct),
                (Prompt("target_002", "Gate four: wait for me at the docks.", "move_to", "docks", false),
                    Reply("attack_target", "wolf"), DecisionOutcome.WrongLegalAction),
                (Prompt("situation_001", "Gate four: the market has closed.", "none", "no_target", false, "Situation"),
                    Reply("stop_following", "no_target"), DecisionOutcome.ContainedByGuard),
                (Prompt("target_003", "Gate four: stay beside me on the road.", "follow_player", "no_target", false),
                    Reply("stop_following", "no_target"), DecisionOutcome.RejectedWhenActionExpected),
                (Prompt("target_004", "Gate four: guard the docks for me.", "move_to", "docks", false),
                    "this is not JSON", DecisionOutcome.PipelineError),
            };

            var replies = prompts.ToDictionary(p => (string)JObject.Parse(p.Item1)["stimulus"], p => p.Item2);
            var set = fixtures.Set(prompts.Where(p => !p.Item1.Contains("\"situation_")).Select(p => p.Item1),
                                   prompts.Where(p => p.Item1.Contains("\"situation_")).Select(p => p.Item1));
            var runner = new EvalRunner(set, Fixtures.VillageGuardProfile(),
                new EvalArm("A", fixtures.Config(new ByStimulus(replies), "GuardsOn")),
                new EvalArm("B", fixtures.Config(new ByStimulus(replies), "GuardsOff"), new DecideOptions { CheckTarget = false }));

            var report = await Fixtures.NotCancelled(runner.RunAsync());

            var ids = set.Prompts.Select(p => p.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
            foreach (string arm in new[] { "A", "B" })
            {
                var asked = report.Answers.Where(a => a.Arm == arm).Select(a => a.PromptId).OrderBy(id => id, StringComparer.Ordinal);
                Assert.That(asked, Is.EqualTo(ids),
                    $"Rule 3 broke: arm {arm} did not answer every prompt exactly once. An A/B compares two arms asked the same "
                  + "prompts in one session; an arm that skipped or repeated one measures something else (#145).");
            }

            foreach (var (json, _, expected) in prompts)
            {
                string id = (string)JObject.Parse(json)["id"];
                foreach (var answer in report.Answers.Where(a => a.PromptId == id))
                {
                    Assert.That(answer.Outcome, Is.EqualTo(expected),
                        $"Rule 3 broke: arm {answer.Arm} classified {id} as {answer.Outcome}, and its answer is {expected} "
                      + "(build plan §4, item 5; the table in #145).");
                    Assert.That(answer.Result.Telemetry.Outcome, Is.EqualTo(answer.Outcome),
                        $"Rule 3 broke: {id}'s outcome in arm {answer.Arm} was not written to the decision's telemetry (#145).");
                }
            }

            foreach (var arm in report.Arms)
            {
                Assert.That(FiveOutcomes.Select(o => arm.Overall[o]), Is.All.EqualTo(1),
                    $"Rule 3 broke: arm {arm.Name}'s counts are not one of each of the five outcomes.");
            }
        }

        // ---- 4. The report carries every condition #145 lists -----------------------------------

        [Test]
        public async Task Gate4_TheReportCarriesEveryConditionTheRunnerLists()
        {
            // Our set, both arms on one model behind a stand-in Ollama, as the live A/B ran. Every
            // reply is stop_following: legal while following, stopped by the legality guard otherwise,
            // so the report has guard reasons to carry.
            var set = OurSet();
            var server = new FakeOllama { Content = Reply("stop_following", "no_target") };
            var runner = new EvalRunner(set, OurProfile(),
                new EvalArm("A", fixtures.Ollama(server, Model2B, "GuardsOn")),
                new EvalArm("B", fixtures.Ollama(server, Model2B, "GuardsOff"), new DecideOptions { CheckTarget = false }));

            var run = await Fixtures.NotCancelled(runner.RunAsync());

            // What the file says, not what the object held: a condition that is never written is not carried.
            string folder = run.Save(output);
            var report = EvalReport.FromJson(File.ReadAllText(Path.Combine(folder, EvalReport.JsonFile)));
            string md = File.ReadAllText(Path.Combine(folder, EvalReport.MarkdownFile));
            var c = report.Conditions;
            int n = set.Prompts.Count();
            const string Broke = "Rule 4 broke: the report does not carry ";
            const string Why = " A number whose conditions are not recorded cannot be compared with another (build plan §4, #145).";

            Assert.That(c.Arms.Select(a => a.Model), Is.All.EqualTo(Model2B), Broke + "each arm's model." + Why);
            Assert.That(c.Arms.Select(a => a.ProviderVersion), Is.All.EqualTo(server.Version),
                Broke + "the provider's version. Ollama updated itself from 0.34.2 to 0.40.14 between two runs on 3 November." + Why);
            Assert.That(c.GraphicsDevice, Is.Not.Null, Broke + "the graphics device." + Why);
            foreach (var arm in c.Arms)
            {
                var reading = arm.Residency.FirstOrDefault(r => r.When == EvalRunner.AfterWarmUp);
                Assert.That(reading != null && reading.Checked && reading.Find(Model2B)?.SizeVram == server.SizeVram, Is.True,
                    Broke + $"arm {arm.Name}'s VRAM reading: size_vram from /api/ps after the warm-up (hard rule 3)." + Why);
            }

            Assert.That(c.FrameworkVersion, Is.EqualTo(AgenerelaInfo.Version), Broke + "the framework's version." + Why);
            Assert.That(c.Arms.Select(a => a.ProviderConfig), Is.EqualTo(new[] { "GuardsOn", "GuardsOff" }),
                Broke + "both arms' config names." + Why);
            Assert.That(c.Arms.Select(a => a.CheckTarget), Is.EqualTo(new[] { true, false }), Broke + "both arms' options." + Why);
            Assert.That(Utc(c.StartedUtc) && Utc(c.FinishedUtc), Is.True, Broke + $"the date: started {c.StartedUtc}, finished {c.FinishedUtc}." + Why);
            Assert.That(c.SetCommit, Does.Match("^[0-9a-f]{40}$"), Broke + "the set's commit, and our set is in git." + Why);
            Assert.That(c.SetUncommitted, Is.Not.Null, Broke + "whether the set had uncommitted changes." + Why);
            Assert.That(c.Order, Is.EqualTo(EvalArmOrder.Interleaved), Broke + "the order the arms were asked in." + Why);
            Assert.That(c.Targets, Is.Not.Empty, Broke + "how the set's targets reached the agent (#145, \"Record that choice\")." + Why);
            Assert.That(c.State, Is.Not.Empty, Broke + "how each prompt's state was staged (build plan §4, item 4)." + Why);
            Assert.That(c.Prompts, Is.EqualTo(n), Broke + "the set's size." + Why);

            var categories = set.Categories.Where(cat => cat.Prompts.Count > 0).Select(cat => cat.Id).ToList();
            foreach (var arm in report.Arms)
            {
                Assert.That(arm.Categories.Keys, Is.EquivalentTo(categories), Broke + $"arm {arm.Name}'s counts for every category." + Why);
                Assert.That(arm.Categories.Values.Sum(v => v.Total), Is.EqualTo(n).And.EqualTo(arm.Overall.Total),
                    Broke + $"arm {arm.Name}'s counts per category × outcome that add up to the whole set." + Why);
            }

            var wrong = report.NotCorrect.ToList();
            Assert.That(wrong, Is.Not.Empty, "Not a rule, only a sign the run still gives wrong answers to list.");
            Assert.That(wrong.Any(w => w.Guard != null), Is.True, "Not a rule, only a sign a guard still fired in the run.");
            foreach (var w in wrong)
            {
                Assert.That(w.ModelAction, Is.Not.Null, Broke + $"the model's own decision for {w.Arm} {w.PromptId}." + Why);
                Assert.That(w.Guard == null || !string.IsNullOrEmpty(w.GuardReason), Is.True,
                    Broke + $"{w.Guard}'s reason for stopping {w.Arm} {w.PromptId}." + Why);
            }

            Assert.That(md, Does.Contain("## Answers that were not correct"), Broke + "its non-correct answers in the Markdown summary." + Why);
            var all = report.Noise.SingleOrDefault(x => x.Scope == "all");
            Assert.That(all != null && all.N == n && Math.Abs(all.NoiseFloorPoints - NoiseCheck.FloorAt(n)) < 1e-9, Is.True,
                Broke + "the noise check over the whole set: a difference smaller than the floor at this n is \"within noise\" "
              + "(build plan §4, item 1)." + Why);
        }

        // ---- 5. 60+ prompts with two annotators' own answers, and their agreement ---------------

        [Test]
        [Ignore("Waits for #381's labels (#148): no prompt in our set has a second annotator's answer yet.")]
        public void Gate5_SixtyPromptsHaveTwoAnnotatorsOwnAnswersAndTheirAgreement()
        {
            var set = OurSet();
            string problem = Rule5(set, set.LoadLabels());

            Assert.That(problem, Is.Null, problem);
        }

        [Test]
        public void Gate5_TheRuleHoldsForAHandBuiltSetWithTwoAnnotators()
        {
            var set = TwoAnnotatorSet(MinimumDoublyLabelled, splits: 6);

            string problem = Rule5(set, set.LoadLabels());

            Assert.That(problem, Is.Null, problem);
        }

        [Test]
        public void Gate5_TheRuleBreaksWithOneAnnotatorOrTooFewSecondAnswers()
        {
            var alone = TwoAnnotatorSet(0, splits: 0);
            var short1 = TwoAnnotatorSet(MinimumDoublyLabelled - 1, splits: 0);

            Assert.That(Rule5(alone, alone.LoadLabels()), Does.StartWith("Rule 5 broke"),
                "Rule 5 must fail a set every prompt of which one person labelled, as ours is until #381.");
            Assert.That(Rule5(short1, short1.LoadLabels()), Does.StartWith("Rule 5 broke"),
                $"Rule 5 must fail a set with {MinimumDoublyLabelled - 1} doubly labelled prompts.");
        }

        // ---- 6. The methodology checklist is in the wiki ----------------------------------------

        [Test]
        public void Gate6_TheMeasuringChecklistIsInTheWiki()
        {
            EvalFixtures.OurSetFolder();
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "llm-wiki", "measuring.md"));

            Assert.That(File.Exists(path) && new FileInfo(path).Length > 0, Is.True,
                $"Rule 6 broke: {path} is missing or empty. It is the checklist every A/B run follows, control arm, one model "
              + "resident, warm-up, staged preconditions (hard rule 2, build plan §4).");
        }

        // ---- Helpers ----------------------------------------------------------------------------

        /// <summary>
        /// Null when <paramref name="set"/> meets rule 5, otherwise why not. Two annotators' own
        /// answers are counted as <see cref="AgreementReport"/> counts them, and the agreement it
        /// reports is computed again here from those answers, so a report that only claimed one fails.
        /// </summary>
        private static string Rule5(PromptSet set, IEnumerable<AnnotatorLabels> labels)
        {
            var report = AgreementReport.Compute(set, labels);
            var doubly = report.Prompts.Where(p => p.Answered >= 2).ToList();
            if (doubly.Count < MinimumDoublyLabelled)
            {
                return $"Rule 5 broke: {doubly.Count} prompts have two annotators' own answers, and Phase 4 asks for "
                     + $"{MinimumDoublyLabelled}. If one person writes every label, accuracy measures agreement with that person (#148).";
            }

            int unanimous = doubly.Count(p => p.Answers.Values.Distinct().Count() == 1);
            double raw = 100.0 * unanimous / doubly.Count;
            if (report.PercentAgreement == null || Math.Abs(report.PercentAgreement.Value - raw) > 1e-9)
            {
                return $"Rule 5 broke: the reported agreement is {report.PercentAgreement}%, and the answers give {raw}% "
                     + $"({unanimous} of {doubly.Count} unanimous). The agreement must be computed from them (#148).";
            }

            var pair = report.Pairwise.OrderByDescending(p => p.AnsweredByBoth).FirstOrDefault();
            if (pair == null || pair.AnsweredByBoth < MinimumDoublyLabelled || pair.CohensKappa == null)
            {
                return $"Rule 5 broke: no pair of annotators answered {MinimumDoublyLabelled} prompts in common with a Cohen's "
                     + "kappa, which #148 reports beside the raw agreement.";
            }

            int agreed = doubly.Count(p => p.Answers.TryGetValue(pair.First, out var a)
                                        && p.Answers.TryGetValue(pair.Second, out var b) && a.Equals(b));
            if (pair.Agreed != agreed)
            {
                return $"Rule 5 broke: {pair.First} and {pair.Second} are reported agreeing on {pair.Agreed} prompts, and their "
                     + $"answers agree on {agreed}. The agreement must be computed from them (#148).";
            }

            return null;
        }

        /// <summary>
        /// A set of 70 situations, each written and so answered by Maxim, and a labels file in which
        /// Ana answers the first <paramref name="secondAnswers"/>, <paramref name="splits"/> of them
        /// differently.
        /// </summary>
        private PromptSet TwoAnnotatorSet(int secondAnswers, int splits)
        {
            const int count = MinimumDoublyLabelled + 10;
            var prompts = Enumerable.Range(1, count)
                .Select(i => Prompt($"situation_{i:000}", $"Gate four fixture situation {i}.", "none", "no_target", false, "Situation"));
            fixtures.Set(Enumerable.Empty<string>(), prompts);

            var ana = new JObject();
            for (int i = 1; i <= secondAnswers; i++)
            {
                ana[$"situation_{i:000}"] = i <= splits ? "move_to/bridge" : "none/no_target";
            }

            string folder = Path.Combine(fixtures.Folder, PromptSet.LabelsFolder, "Ana");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "situation.json"),
                new JObject { ["annotator"] = "Ana", ["category"] = "situation", ["labels"] = ana }.ToString());
            return PromptSet.Load(Path.Combine(fixtures.Folder, "agent.json"));
        }

        private static PromptSet OurSet() => PromptSet.Load(Path.Combine(EvalFixtures.OurSetFolder(), "agent.json"));

        private static AgentProfile OurProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AgentProfile>(OurProfilePath);
            Assert.That(profile, Is.Not.Null, $"{OurProfilePath} is the agent our set is measured against, and it is missing.");
            return profile;
        }

        private static bool Utc(string iso) =>
            !string.IsNullOrEmpty(iso) && DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out _);

        /// <summary>
        /// A <see cref="FakeProvider"/> that answers each stimulus with its own reply, and anything
        /// else, the runner's warm-ups included, with <c>none</c>.
        /// </summary>
        private sealed class ByStimulus : ILLMProvider
        {
            private readonly Dictionary<string, string> replies;
            private readonly FakeProvider inner = new FakeProvider();

            public ByStimulus(Dictionary<string, string> replies)
            {
                this.replies = replies;
            }

            public string Name => inner.Name;

            public ProviderCapabilities Capabilities => inner.Capabilities;

            public Awaitable<ProviderResult> RequestAsync(DecisionRequest req, CancellationToken ct)
            {
                var match = replies.FirstOrDefault(r => req.Stimulus != null && req.Stimulus.Contains(r.Key));
                inner.Reply = match.Key != null ? match.Value : Reply("none", "no_target");
                return inner.RequestAsync(req, ct);
            }
        }
    }
}
