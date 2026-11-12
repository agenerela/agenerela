using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Agenerela.Evaluation;
using Agenerela.Providers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Agenerela.Tests.Evaluation.RunnerFixtures;

namespace Agenerela.Tests.Evaluation
{
    // The reply checks: the mechanical flags on each answer's statement, the per-arm totals beside
    // the five outcomes, the report's section, and that no flag moves an outcome or the accuracy.
    // Every run is awaited through Fixtures.NotCancelled, since the Test Framework counts a
    // cancelled Task as passed.
    public sealed class ReplyChecksTests
    {
        private RunnerFixtures fixtures;

        [SetUp]
        public void SetUp()
        {
            fixtures = new RunnerFixtures();
        }

        [TearDown]
        public void TearDown()
        {
            fixtures.Dispose();
            Fixtures.DestroyCreated();
        }

        private static readonly string Bridge = Prompt("target_001", "Meet me over at the bridge.", "move_to", "bridge", false);
        private static readonly string Escort = Prompt("target_002", "Walk with me a while.", "follow_player", "no_target", false);
        private static readonly string Quiet = Prompt("situation_001", "The square is empty.", "none", "no_target", false, "Situation");

        private const string Clean = "Right away, I will head over there.";

        // ---- Echo ------------------------------------------------------------------------------

        [Test]
        public void AnActionsExampleRepeatedWithAnyTargetIsAnEcho()
        {
            // move_to's example is "Proceed to the {0}.", matched as the set checks match it.
            var findings = Check("proceed to the docks!");

            Assert.That(Flags(findings), Is.EqualTo(new[] { ReplyFlag.Echo }));
            Assert.That(findings[0].Detail, Does.Contain("move_to's example"));
        }

        [Test]
        public void TheProfilesIdleExampleRepeatedIsAnEcho()
        {
            var findings = Check("You seem well rested today.");

            Assert.That(Flags(findings), Is.EqualTo(new[] { ReplyFlag.Echo }));
            Assert.That(findings[0].Detail, Does.Contain("the profile's idle example"));
        }

        [Test]
        public void AnExampleOrCounterExampleStatementOrStimulusRepeatedIsAnEcho()
        {
            var profile = Fixtures.VillageGuardProfile();
            var follow = profile.Actions.First(a => a.Action.Id == "follow_player").Action;
            follow.ExampleStatement = "Lead the way.";
            follow.CounterExampleStimulus = "Follow the wolf.";
            follow.CounterExampleStatement = "I stay with the village.";
            profile.IdleExampleStatement = "All is calm.";

            Assert.That(Flags(Check("Lead the way", profile)), Is.EqualTo(new[] { ReplyFlag.Echo }));
            Assert.That(Flags(Check("Follow the wolf.", profile)), Is.EqualTo(new[] { ReplyFlag.Echo }));
            Assert.That(Flags(Check("I stay with the village.", profile)), Is.EqualTo(new[] { ReplyFlag.Echo }));
            Assert.That(Flags(Check("All is calm!", profile)), Is.EqualTo(new[] { ReplyFlag.Echo }));
        }

        [Test]
        public void TheArmsOwnIdleExampleRepeatedIsAnEcho()
        {
            // The 9 November run: 10 of 76 statements were this line, the arm's idle example.
            var arm = Arm(new DecideOptions { IdleExampleStimulus = AgentProfile.DefaultIdleExampleStimulus, IdleExampleStatement = "Quiet as ever." });

            var stimulus = Check("Nothing has changed since last time.", null, arm);
            var statement = Check("Quiet as ever.", null, arm);

            Assert.That(Flags(stimulus), Is.EqualTo(new[] { ReplyFlag.Echo }));
            Assert.That(stimulus[0].Detail, Does.Contain("arm 'A''s idle example"));
            Assert.That(Flags(statement), Is.EqualTo(new[] { ReplyFlag.Echo }));
        }

        [Test]
        public void AStatementThatOnlySharesWordsWithAnExampleIsNoEcho()
        {
            Assert.That(Check("I will not engage anyone today."), Is.Empty);
            Assert.That(Check(Clean), Is.Empty);
        }

        // ---- Empty -----------------------------------------------------------------------------

        [Test]
        public void AnEmptyOrBlankStatementIsFlaggedWhenTheSchemaAsksForOne()
        {
            Assert.That(Flags(Check("")), Is.EqualTo(new[] { ReplyFlag.Empty }));
            Assert.That(Flags(Check("  \n ")), Is.EqualTo(new[] { ReplyFlag.Empty }));
            Assert.That(Flags(Check(null)), Is.EqualTo(new[] { ReplyFlag.Empty }));
        }

        [Test]
        public void AnEmptyStatementIsNotFlaggedWhenTheSchemaAskedForNone()
        {
            var set = fixtures.Set(new[] { Bridge });

            Assert.That(ReplyChecks.Check("", set, Fixtures.VillageGuardProfile(), Arm(), asksForStatement: false), Is.Empty);
        }

        // ---- Too long --------------------------------------------------------------------------

        [Test]
        public void AStatementOverTheDefaultLengthIsTooLong()
        {
            string limit = new string('a', EvalArm.DefaultMaxStatementLength);

            Assert.That(EvalArm.DefaultMaxStatementLength, Is.EqualTo(200));
            Assert.That(Check(limit), Is.Empty);
            Assert.That(Check("  " + limit + "  "), Is.Empty, "The length is measured once trimmed.");
            var findings = Check(limit + "a");
            Assert.That(Flags(findings), Is.EqualTo(new[] { ReplyFlag.TooLong }));
            Assert.That(findings[0].Detail, Is.EqualTo("201 characters, over 200"));
        }

        [Test]
        public void TheArmSetsTheLength()
        {
            var arm = Arm();
            arm.MaxStatementLength = 10;

            Assert.That(Flags(Check("Eleven char", null, arm)), Is.EqualTo(new[] { ReplyFlag.TooLong }));
            Assert.That(Check("Ten chars!", null, arm), Is.Empty);
            Assert.Throws<ArgumentOutOfRangeException>(() => arm.MaxStatementLength = 0);
        }

        // ---- Leaks -----------------------------------------------------------------------------

        [Test]
        public void AnIdAsWrittenIsALeak()
        {
            var findings = Check("I will move_to the docks.");

            Assert.That(Flags(findings), Is.EqualTo(new[] { ReplyFlag.Leak }));
            Assert.That(findings[0].Detail, Is.EqualTo("the id move_to"));
            Assert.That(Check("There is no_target here.")[0].Detail, Is.EqualTo("the id no_target"));
            Assert.That(Check("Attack_Target, sir.")[0].Detail, Is.EqualTo("the id attack_target"));
        }

        [Test]
        public void AnIdThatIsAlsoAWordIsNoLeak()
        {
            // "bridge" and "none" are how anyone would say them; "move to" is not move_to.
            Assert.That(Check("I will move to the bridge, and none shall pass."), Is.Empty);
        }

        [Test]
        public void ATargetIdOfTheSetIsALeak()
        {
            var set = fixtures.Set(new[] { Bridge });
            set.Targets.Add(new DeclaredTarget { Id = "north_tower", Description = "The north tower" });

            var findings = ReplyChecks.Check("Off to north_tower.", set, Fixtures.VillageGuardProfile(), Arm());

            Assert.That(findings.Single().Detail, Is.EqualTo("the id north_tower"));
        }

        [Test]
        public void AFieldNameUsedAsALabelIsALeak()
        {
            var findings = Check("Action: follow. Target : you.");

            Assert.That(Flags(findings), Is.EqualTo(new[] { ReplyFlag.Leak }));
            Assert.That(findings[0].Detail, Is.EqualTo("the field label action:, the field label target:"));
            Assert.That(Check("My target is the wolf, and that is my action."), Is.Empty);
        }

        [Test]
        public void AJsonBraceIsALeak()
        {
            Assert.That(Check("Sure {").Single().Detail, Is.EqualTo("a JSON brace"));
            Assert.That(Check("Done }").Single().Detail, Is.EqualTo("a JSON brace"));
        }

        [Test]
        public void AnAssistantsPhraseIsALeak()
        {
            Assert.That(Check("As an AI, I cannot walk.").Single().Detail, Is.EqualTo("\"as an AI\""));
            Assert.That(Check("I’m an AI, sorry.").Single().Detail, Is.EqualTo("\"I'm an AI\""));
            Assert.That(Check("A language  model cannot hold a sword.").Single().Detail, Is.EqualTo("\"language model\""));
            Assert.That(Check("She said the rain has gone."), Is.Empty);
            Assert.That(ReplyChecks.AssistantPhrases, Is.EqualTo(new[] { "as an AI", "I am an AI", "I'm an AI", "AI assistant", "language model" }));
        }

        [Test]
        public void EveryLeakIsNamedInOneFinding()
        {
            var findings = Check("{ \"action\": \"move_to\" } as an AI");

            Assert.That(Flags(findings), Is.EqualTo(new[] { ReplyFlag.Leak }));
            Assert.That(findings[0].Detail, Is.EqualTo("the id move_to, the field label action:, a JSON brace, \"as an AI\""));
        }

        // ---- Through the runner ----------------------------------------------------------------

        [Test]
        public async Task EveryAnswerCarriesItsFlagsAndEachArmItsTotals()
        {
            var provider = new ScriptedProvider()
                .When(null, "Meet me over at the bridge.", ScriptedReply.Decide("move_to", "bridge", "I will move_to the bridge."))
                .When(null, "Walk with me a while.", ScriptedReply.Decide("follow_player", TargetRegistry.NoTarget, "You seem well rested today."))
                .When(null, "The square is empty.", ScriptedReply.Decide("none", TargetRegistry.NoTarget, ""))
                .Otherwise(ScriptedReply.Decide("none", TargetRegistry.NoTarget, Clean));

            var report = await Run(new[] { Bridge, Escort }, new[] { Quiet }, provider);

            var a = report.Answers.Where(x => x.Arm == "A").ToDictionary(x => x.PromptId);
            Assert.That(a.Values.Select(x => x.ReplyChecked), Is.All.True);
            Assert.That(Flags(a["target_001"].ReplyFlags), Is.EqualTo(new[] { ReplyFlag.Leak }));
            Assert.That(Flags(a["target_002"].ReplyFlags), Is.EqualTo(new[] { ReplyFlag.Echo }));
            Assert.That(Flags(a["situation_001"].ReplyFlags), Is.EqualTo(new[] { ReplyFlag.Empty }));
            Assert.That(a["target_002"].HasReplyFlag(ReplyFlag.Echo), Is.True);

            foreach (var arm in report.Arms)
            {
                var r = arm.Replies;
                Assert.That((r.Checked, r.Echo, r.Empty, r.TooLong, r.Leak, r.Flagged), Is.EqualTo((3, 1, 1, 0, 1, 3)), arm.Name);
            }

            Assert.That(report.FlaggedReplies.Count(), Is.EqualTo(6));
            Assert.That(report.Conditions.Arms.Select(c => c.MaxStatementLength), Is.All.EqualTo(EvalArm.DefaultMaxStatementLength));
        }

        [Test]
        public async Task AnAnswerWithSeveralFlagsCountsOnceAsFlaggedAndOnceUnderEach()
        {
            string longLeak = "I will move_to the bridge " + new string('.', EvalArm.DefaultMaxStatementLength);
            var provider = new ScriptedProvider()
                .When(null, "Meet me over at the bridge.", ScriptedReply.Decide("move_to", "bridge", longLeak))
                .Otherwise(ScriptedReply.Decide("none", TargetRegistry.NoTarget, Clean));

            var report = await Run(new[] { Bridge, Escort }, null, provider);

            var r = report.Arms[0].Replies;
            Assert.That((r.Checked, r.TooLong, r.Leak, r.Flagged), Is.EqualTo((2, 1, 1, 1)));
            Assert.That(r[ReplyFlag.TooLong] + r[ReplyFlag.Leak], Is.EqualTo(2));
        }

        [Test]
        public async Task EachArmUsesItsOwnLengthAndRecordsIt()
        {
            var provider = new ScriptedProvider().Otherwise(ScriptedReply.Decide("move_to", "bridge", Clean));
            var set = fixtures.Set(new[] { Bridge });
            var armA = new EvalArm("A", fixtures.Config(provider, "FakeA"), new DecideOptions { CheckTarget = false }) { MaxStatementLength = 10 };
            var armB = new EvalArm("B", fixtures.Config(provider, "FakeB"), new DecideOptions { CheckTarget = false });

            var report = await Fixtures.NotCancelled(new EvalRunner(set, Fixtures.VillageGuardProfile(), armA, armB).RunAsync());

            Assert.That(report.Arms.Select(x => x.Replies.TooLong), Is.EqualTo(new[] { 1, 0 }));
            Assert.That(report.Conditions.Arms.Select(c => c.MaxStatementLength), Is.EqualTo(new[] { 10, 200 }));
        }

        [Test]
        public async Task APipelineErrorHasNoStatementToCheck()
        {
            var provider = new ScriptedProvider().Otherwise(ScriptedReply.Raw("this is not JSON"));
            var set = fixtures.Set(new[] { Bridge });
            var runner = new EvalRunner(set, Fixtures.VillageGuardProfile(), Arm(null, provider, "A"), Arm(null, provider, "B")) { WarmUpRequests = 0 };

            var report = await Fixtures.NotCancelled(runner.RunAsync());

            Assert.That(report.Answers.Select(x => x.Outcome), Is.All.EqualTo(DecisionOutcome.PipelineError));
            Assert.That(report.Answers.Select(x => x.ReplyChecked), Is.All.False);
            Assert.That(report.Answers.SelectMany(x => x.ReplyFlags), Is.Empty);
            Assert.That(report.Arms.Select(x => x.Replies.Checked), Is.All.EqualTo(0));
        }

        [Test]
        public async Task TheSameAnswersGiveTheSameOutcomesAndAccuracyWhateverTheirStatements()
        {
            // One answer per outcome the provider can give here: correct, a wrong legal action, and
            // a none for an action. Each statement in the first run carries a flag, in the second none.
            var prompts = new[] { Bridge, Escort };
            var situations = new[] { Quiet };
            var flagged = await Run(prompts, situations, Scripted("move_to: bridge {", "You seem well rested today.", ""));
            var clean = await Run(prompts, situations, Scripted(Clean, Clean, Clean));

            Assert.That(flagged.Answers.Select(x => x.ReplyFlags.Count), Is.All.GreaterThan(0), "Every statement of the first run is flagged.");
            Assert.That(clean.Answers.SelectMany(x => x.ReplyFlags), Is.Empty, "No statement of the second run is.");

            Assert.That(flagged.Answers.Select(x => (x.Arm, x.PromptId, x.Outcome, x.Action, x.Target)),
                Is.EqualTo(clean.Answers.Select(x => (x.Arm, x.PromptId, x.Outcome, x.Action, x.Target))));
            Assert.That(flagged.Answers.Select(x => x.Result.Telemetry.Outcome), Is.EqualTo(clean.Answers.Select(x => x.Result.Telemetry.Outcome)));
            Assert.That(flagged.Answers.Select(x => x.Outcome).Distinct().Count(), Is.EqualTo(2), "Both Correct and WrongLegalAction are covered.");

            for (int i = 0; i < 2; i++)
            {
                Assert.That(JObject.FromObject(flagged.Arms[i].Overall).ToString(), Is.EqualTo(JObject.FromObject(clean.Arms[i].Overall).ToString()));
                Assert.That(flagged.Arms[i].Overall.Accuracy, Is.EqualTo(clean.Arms[i].Overall.Accuracy));
                Assert.That(JObject.FromObject(flagged.Arms[i].Categories).ToString(), Is.EqualTo(JObject.FromObject(clean.Arms[i].Categories).ToString()));
            }

            Assert.That(flagged.Noise.Select(n => (n.Scope, n.AccuracyA, n.AccuracyB, n.WithinNoise)),
                Is.EqualTo(clean.Noise.Select(n => (n.Scope, n.AccuracyA, n.AccuracyB, n.WithinNoise))));
            Assert.That(flagged.NotCorrect.Select(x => x.PromptId), Is.EqualTo(clean.NotCorrect.Select(x => x.PromptId)));
        }

        // ---- The report ------------------------------------------------------------------------

        [Test]
        public async Task TheFlagsAndTotalsReadBackFromJson()
        {
            var report = await Run(new[] { Bridge, Escort }, null, Scripted("I will move_to the bridge.", "You seem well rested today."));

            var back = EvalReport.FromJson(report.ToJson());

            Assert.That(back.Answers.Select(x => string.Join(";", x.ReplyFlags)), Is.EqualTo(report.Answers.Select(x => string.Join(";", x.ReplyFlags))));
            Assert.That(back.Answers.Select(x => x.ReplyChecked), Is.All.True);
            Assert.That(JObject.FromObject(back.Arms[0].Replies).ToString(), Is.EqualTo(JObject.FromObject(report.Arms[0].Replies).ToString()));
            Assert.That(back.Conditions.Arms[0].MaxStatementLength, Is.EqualTo(200));
            Assert.That(JObject.Parse(report.ToJson())["answers"][0]["replyFlags"][0]["flag"].ToString(), Is.EqualTo("Leak"));
        }

        [Test]
        public async Task TheMarkdownListsTheTotalsAndEveryFlaggedAnswer()
        {
            var report = await Run(new[] { Bridge, Escort }, null, Scripted("I will move_to the bridge.", Clean));

            string md = report.ToMarkdown();

            Assert.That(md, Does.Contain("## Reply checks"));
            Assert.That(md.IndexOf("## Reply checks", StringComparison.Ordinal),
                Is.GreaterThan(md.IndexOf("## Answers that were not correct", StringComparison.Ordinal)));
            Assert.That(md, Does.Contain("| Checked | 2 | 2 |"));
            Assert.That(md, Does.Contain("| Leak | 1 | 1 |"));
            Assert.That(md, Does.Contain("| TooLong (over 200 characters) | 0 | 0 |"));
            Assert.That(md, Does.Contain("| **Any flag** | **1** | **1** |"));
            Assert.That(md, Does.Contain("| A | target_001 | Correct | I will move_to the bridge. | Leak: the id move_to |"));
            Assert.That(md.Substring(md.IndexOf("## Reply checks", StringComparison.Ordinal)), Does.Not.Contain("target_002"),
                "An answer no check flagged is not listed.");
        }

        [Test]
        public void AReportFromBeforeReplyChecksSaysNothingWasChecked()
        {
            var old = EvalReport.FromJson("{ \"name\": \"old\", \"arms\": [ { \"name\": \"A\", \"overall\": { \"total\": 1, \"correct\": 1 } } ], "
                                        + "\"answers\": [ { \"arm\": \"A\", \"promptId\": \"target_001\", \"outcome\": \"Correct\", \"modelStatement\": \"Hi\" } ] }");

            Assert.That(old.Answers[0].ReplyChecked, Is.False);
            Assert.That(old.Answers[0].ReplyFlags, Is.Empty);
            Assert.That(old.Arms[0].Replies.Checked, Is.EqualTo(0));
            Assert.That(old.ToMarkdown(), Does.Contain("No statement was checked"));
        }

        // ---- Helpers -----------------------------------------------------------------------------

        private IReadOnlyList<ReplyFinding> Check(string statement, AgentProfile profile = null, EvalArm arm = null)
        {
            return ReplyChecks.Check(statement, fixtures.Set(new[] { Bridge }), profile ?? Fixtures.VillageGuardProfile(), arm ?? Arm());
        }

        private static ReplyFlag[] Flags(IEnumerable<ReplyFinding> findings) => findings.Select(f => f.Flag).ToArray();

        private EvalArm Arm(DecideOptions options = null, ILLMProvider provider = null, string name = "A")
        {
            options = options ?? new DecideOptions();
            options.CheckTarget = false;
            return new EvalArm(name, fixtures.Config(provider ?? new ScriptedProvider(), "Fake" + name), options);
        }

        // Bridge answered move_to/bridge (correct), Escort answered none (a wrong legal action),
        // Quiet answered none (correct), each with its statement.
        private static ScriptedProvider Scripted(string bridge, string escort, string quiet = "")
        {
            return new ScriptedProvider()
                .When(null, "Meet me over at the bridge.", ScriptedReply.Decide("move_to", "bridge", bridge))
                .When(null, "Walk with me a while.", ScriptedReply.Decide("none", TargetRegistry.NoTarget, escort))
                .When(null, "The square is empty.", ScriptedReply.Decide("none", TargetRegistry.NoTarget, quiet))
                .Otherwise(ScriptedReply.Decide("none", TargetRegistry.NoTarget, Clean));
        }

        private Task<EvalReport> Run(string[] commands, string[] situations, ScriptedProvider provider)
        {
            var set = fixtures.Set(commands, situations);
            var runner = new EvalRunner(set, Fixtures.VillageGuardProfile(), Arm(null, provider, "A"), Arm(null, provider, "B"));
            return Fixtures.NotCancelled(runner.RunAsync());
        }
    }
}
