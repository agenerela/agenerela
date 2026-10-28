using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Agenerela.Providers;
using Newtonsoft.Json;
using NUnit.Framework;
using static Agenerela.Tests.Tokens;

namespace Agenerela.Tests
{
    // TargetGroundingGuard: on its own over hand-built inputs, and through Agent.DecideAsync with
    // FakeProvider scripting the token probabilities. What it rewrites, what it passes, and what it
    // records. The reading itself is in TargetGroundingTests. Every await of DecideAsync goes
    // through Fixtures.NotCancelled (see AgentTests).
    public sealed class TargetGroundingGuardTests
    {
        private const string ScarecrowReason =
            "Only 0.052 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write \"sc…\".";

        private readonly TargetGroundingGuard guard = new TargetGroundingGuard();

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        [Test]
        public void ItsNameIsWhatTelemetryRecords()
        {
            Assert.That(guard.Name, Is.EqualTo("TargetGroundingGuard"));
        }

        [Test]
        public void TheThresholdIsWhatDr016Measured()
        {
            Assert.That(TargetGroundingGuard.DefaultThreshold, Is.EqualTo(0.8f));
            Assert.That(guard.Threshold, Is.EqualTo(0.8f));
        }

        // ---- Through the guard alone -------------------------------------------------------

        [Test]
        public void ASubstitutionIsRewrittenToNoneWithItsReason()
        {
            var input = Input("attack_target", "training_dummy", Scarecrow());

            var verdict = guard.Inspect(input);

            Assert.That(verdict.Kind, Is.EqualTo(GuardVerdictKind.RewriteToNone), "A contained refusal, not a broken contract.");
            Assert.That(verdict.Reason, Is.EqualTo(ScarecrowReason));
            Assert.That(input.Telemetry.TargetGroundingStatus, Is.EqualTo(GroundingStatus.Scored));
            Assert.That(input.Telemetry.TargetGrounding, Is.EqualTo(0.0523f).Within(0.0001f));
        }

        [Test]
        public void TheRightTargetPasses()
        {
            var input = Input("attack_target", "training_dummy", Reply("attack_target",
                T("training", -0.001, ("training", -0.001), ("dummy", -7.5)), T("_dummy", -0.001, ("_dummy", -0.001))));

            Assert.That(guard.Inspect(input).Kind, Is.EqualTo(GuardVerdictKind.Pass));
            Assert.That(input.Telemetry.TargetGrounding, Is.EqualTo(0.999f).Within(0.001f));
        }

        [Test]
        public void ALookAlikeIsRewrittenThoughItsFirstTokenIsOnTheList()
        {
            // "Pick up the spear." beside sword. A guard reading only the first token would pass it.
            var input = Input("attack_target", "sword", Reply("attack_target",
                T("s", -0.001, ("s", -0.001)), T("word", -6.0, ("pear", -0.004), ("word", -6.0))), "sword");

            var verdict = guard.Inspect(input);

            Assert.That(verdict.Kind, Is.EqualTo(GuardVerdictKind.RewriteToNone));
            Assert.That(verdict.Reason, Is.EqualTo(
                "Only 0.002 of the model's probability at the target stayed on the target list (threshold 0.8); it wanted to write \"spear…\"."));
        }

        [Test]
        public void AnOpenChoiceBetweenTwoListedTargetsPasses()
        {
            var input = Input("move_to", "south_gate", Reply("move_to",
                T("south", -0.598, ("south", -0.598), ("north", -0.844)), T("_gate", -0.001, ("_gate", -0.001))),
                "north_gate", "south_gate");

            Assert.That(guard.Inspect(input).Kind, Is.EqualTo(GuardVerdictKind.Pass), "0.55 and 0.43, all on the list.");
            Assert.That(input.Telemetry.TargetGrounding, Is.EqualTo(0.98f).Within(0.002f));
        }

        [Test]
        public void AShareFromProbabilityNotReportedSaysNothingWasWanted()
        {
            // All five alternatives on the list, summing to 0.6.
            var input = Input("move_to", "tower", Reply("move_to",
                T("tower", -1.204, ("tower", -1.204), ("training", -2.303), ("t", -2.303), ("to", -2.996), ("tr", -2.996))));

            var verdict = guard.Inspect(input);

            Assert.That(verdict.Kind, Is.EqualTo(GuardVerdictKind.RewriteToNone));
            Assert.That(verdict.Reason, Is.EqualTo("Only 0.6 of the model's probability at the target stayed on the target list (threshold 0.8)."));
        }

        [Test]
        public void TheThresholdDecides()
        {
            // The same 0.6 reading as above.
            var tokens = Reply("move_to",
                T("tower", -1.204, ("tower", -1.204), ("training", -2.303), ("t", -2.303), ("to", -2.996), ("tr", -2.996)));
            float share = TargetGrounding.Read(Join(tokens), tokens, new[] { "no_target", "tower", "training_dummy" }).Share;

            Assert.That(Inspect(0.59f, tokens), Is.EqualTo(GuardVerdictKind.Pass));
            Assert.That(Inspect(share, tokens), Is.EqualTo(GuardVerdictKind.Pass), "Only a share below the threshold is refused, as the probe compared.");
            Assert.That(Inspect(0.61f, tokens), Is.EqualTo(GuardVerdictKind.RewriteToNone));
        }

        [TestCase("follow_player")]
        [TestCase("none")]
        public void ADecisionWithoutATargetIsScoredAndRecordedButNeverRewritten(string action)
        {
            // The probe acted only on actions that take a target. The low reading is still recorded.
            var input = Input(action, "no_target", Reply(action,
                T("no", -3.0, ("sc", -0.05), ("no", -3.0)), T("_target", -0.001, ("_target", -0.001))));

            Assert.That(guard.Inspect(input).Kind, Is.EqualTo(GuardVerdictKind.Pass));
            Assert.That(input.Telemetry.TargetGroundingStatus, Is.EqualTo(GroundingStatus.Scored));
            Assert.That(input.Telemetry.TargetGrounding, Is.EqualTo(0.0498f).Within(0.0001f));
        }

        [Test]
        public void WithoutProbabilitiesItPassesAndSaysItCouldNotRun()
        {
            var ctx = Context();
            var reply = new ProviderResult { Text = Join(Scarecrow()), TokenProbabilities = null };
            var input = new GuardInput(ctx, Offering(ctx), reply, new AgentDecision("attack_target", "training_dummy", Statement), null, null);

            Assert.That(guard.Inspect(input).Kind, Is.EqualTo(GuardVerdictKind.Pass));
            Assert.That(input.Telemetry.TargetGroundingStatus, Is.EqualTo(GroundingStatus.NoProbabilities), "Never recorded as checked.");
            Assert.That(input.Telemetry.TargetGrounding, Is.Null);
        }

        [Test]
        public void WithoutAReplyItPassesAndSaysItHadNoProbabilities()
        {
            var ctx = Context();
            var input = new GuardInput(ctx, Offering(ctx), null, new AgentDecision("attack_target", "training_dummy", Statement), null, null);

            Assert.That(guard.Inspect(input).Kind, Is.EqualTo(GuardVerdictKind.Pass));
            Assert.That(input.Telemetry.TargetGroundingStatus, Is.EqualTo(GroundingStatus.NoProbabilities));
        }

        [Test]
        public void WithoutATargetFieldItPassesAndSaysSo()
        {
            // Nothing registered, so the schema has no target field.
            var empty = Context(new string[0]);
            var tokens = new List<TokenProbability> { T("{\"action\": \"follow_player\", \"statement\": \"Coming.\"}", -0.1) };
            var reply = new ProviderResult { Text = Join(tokens), TokenProbabilities = tokens };
            var input = new GuardInput(empty, Offering(empty), reply, new AgentDecision("follow_player", "no_target", "Coming."), null, null);

            Assert.That(guard.Inspect(input).Kind, Is.EqualTo(GuardVerdictKind.Pass));
            Assert.That(input.Telemetry.TargetGroundingStatus, Is.EqualTo(GroundingStatus.NoTargetField));
            Assert.That(input.Telemetry.TargetGrounding, Is.Null);
        }

        [Test]
        public void AJoinThatDoesNotMatchBeforeTheTargetPassesAsUnreadable()
        {
            // The reply's "à" was split across two tokens and is missing from both.
            var tokens = new List<TokenProbability>
            {
                T("{\"action\": \"attack_target\", \"_\": \"\", \"target\": \""),
                T("training", -2.95, ("sc", -0.06), ("training", -2.95)),
                T("_dummy\", \"statement\": \"\"}"),
            };
            var reply = new ProviderResult
            {
                Text = "{\"action\": \"attack_target\", \"_\": \"à\", \"target\": \"training_dummy\", \"statement\": \"\"}",
                TokenProbabilities = tokens,
            };
            var ctx = Context();
            var input = new GuardInput(ctx, Offering(ctx), reply, new AgentDecision("attack_target", "training_dummy", ""), null, null);

            Assert.That(guard.Inspect(input).Kind, Is.EqualTo(GuardVerdictKind.Pass), "Unreadable is never a guess, and never a refusal.");
            Assert.That(input.Telemetry.TargetGroundingStatus, Is.EqualTo(GroundingStatus.Unreadable));
            Assert.That(input.Telemetry.TargetGrounding, Is.Null);
        }

        [Test]
        public void WithCheckTargetOffItDoesNotRunAndSaysSo()
        {
            var input = Input("attack_target", "training_dummy", Scarecrow(), options: new DecideOptions { CheckTarget = false });

            Assert.That(guard.Inspect(input).Kind, Is.EqualTo(GuardVerdictKind.Pass), "A substitution passes.");
            Assert.That(input.Telemetry.TargetGroundingStatus, Is.EqualTo(GroundingStatus.Off));
            Assert.That(input.Telemetry.TargetGrounding, Is.Null, "Not read.");
        }

        [Test]
        public void ANullInputThrows()
        {
            Assert.Throws<ArgumentNullException>(() => guard.Inspect(null));
        }

        // ---- Through Agent.DecideAsync ----------------------------------------------------

        [Test]
        public async Task AnAgentTurnsASubstitutionIntoNone()
        {
            var fake = new FakeProvider();
            fake.ReplyWith(Scarecrow());
            var agent = Fixtures.VillageGuard(fake);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Attack the scarecrow."));

            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
            Assert.That(result.Decision.TargetId, Is.EqualTo(TargetRegistry.NoTarget));
            Assert.That(result.ModelDecision.ActionId, Is.EqualTo("attack_target"));
            Assert.That(result.ModelDecision.TargetId, Is.EqualTo("training_dummy"));
            Assert.That(result.Verdict.Value.Kind, Is.EqualTo(GuardVerdictKind.RewriteToNone));
            Assert.That(result.Verdict.Value.Guard, Is.EqualTo("TargetGroundingGuard"));

            var telemetry = result.Telemetry;
            Assert.That(telemetry.GuardsFired, Is.EqualTo(new[] { "TargetGroundingGuard" }));
            Assert.That(telemetry.Verdict, Is.EqualTo(GuardVerdictKind.RewriteToNone));
            Assert.That(telemetry.GuardReason, Is.EqualTo(ScarecrowReason));
            Assert.That(telemetry.TargetGroundingStatus, Is.EqualTo(GroundingStatus.Scored));
            Assert.That(telemetry.TargetGrounding, Is.EqualTo(0.0523f).Within(0.0001f));

            Assert.That(agent.Execute(result), Is.True, "A contained refusal: none runs.");
            Assert.That(Fixtures.Handler(agent, "attack_target").Executions, Is.Empty, "The dummy is never attacked.");
        }

        [Test]
        public async Task AnAgentCarriesOutTheRightTargetAndRecordsTheReading()
        {
            var fake = new FakeProvider();
            fake.ReplyWith(Reply("attack_target",
                T("training", -0.002, ("training", -0.002), ("dummy", -7.5)), T("_dummy", -0.001, ("_dummy", -0.001))));
            var agent = Fixtures.VillageGuard(fake);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Attack the training dummy."));

            Assert.That(result.Decision.ActionId, Is.EqualTo("attack_target"));
            Assert.That(result.Decision.TargetId, Is.EqualTo("training_dummy"));
            Assert.That(result.Verdict.HasValue, Is.False);
            Assert.That(result.Telemetry.GuardsFired, Is.Empty);
            Assert.That(result.Telemetry.TargetGroundingStatus, Is.EqualTo(GroundingStatus.Scored));
            Assert.That(result.Telemetry.TargetGrounding, Is.EqualTo(0.998f).Within(0.001f));

            Assert.That(agent.Execute(result), Is.True);
            Assert.That(Fixtures.Handler(agent, "attack_target").Executions, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task CheckTargetOffSkipsTheGuardForThatCallOnly()
        {
            var fake = new FakeProvider();
            fake.ReplyWith(Scarecrow());
            var agent = Fixtures.VillageGuard(fake);

            var offCall = await Fixtures.NotCancelled(agent.DecideAsync("Attack the scarecrow.", new DecideOptions { CheckTarget = false }));
            var onCall = await Fixtures.NotCancelled(agent.DecideAsync("Attack the scarecrow."));

            Assert.That(offCall.Decision.ActionId, Is.EqualTo("attack_target"), "Its best guess, as the caller asked.");
            Assert.That(offCall.Telemetry.TargetGroundingStatus, Is.EqualTo(GroundingStatus.Off));
            Assert.That(offCall.Telemetry.TargetGrounding, Is.Null);
            Assert.That(offCall.Telemetry.GuardsFired, Is.Empty);

            Assert.That(agent.Execute(offCall), Is.True, "Execute never runs the grounding guard: it has no reply.");
            Assert.That(offCall.Telemetry.GuardsFired, Is.Empty);
            Assert.That(Fixtures.Handler(agent, "attack_target").Executions, Has.Count.EqualTo(1));

            Assert.That(onCall.Decision.ActionId, Is.EqualTo(ActionRegistry.None), "The next call is checked again.");
            Assert.That(onCall.Telemetry.TargetGroundingStatus, Is.EqualTo(GroundingStatus.Scored));
        }

        [Test]
        public async Task CheckTargetIsReadAsItWasWhenTheCallWasMade()
        {
            var fake = new FakeProvider { Hold = true };
            fake.ReplyWith(Scarecrow());
            var agent = Fixtures.VillageGuard(fake);
            var options = new DecideOptions { CheckTarget = false };

            var decision = agent.DecideAsync("Attack the scarecrow.", options);
            options.CheckTarget = true;
            fake.Release();
            var result = await Fixtures.NotCancelled(decision);

            Assert.That(result.Telemetry.TargetGroundingStatus, Is.EqualTo(GroundingStatus.Off));
            Assert.That(result.Decision.ActionId, Is.EqualTo("attack_target"));
        }

        [Test]
        public async Task AReplyWithoutProbabilitiesPassesAndIsRecordedAsSuch()
        {
            var fake = new FakeProvider();
            fake.Reply = Join(Scarecrow());
            var agent = Fixtures.VillageGuard(fake);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Attack the scarecrow."));

            Assert.That(result.Decision.ActionId, Is.EqualTo("attack_target"), "Nothing to read, so nothing to refuse on.");
            Assert.That(result.Telemetry.TargetGroundingStatus, Is.EqualTo(GroundingStatus.NoProbabilities));
            Assert.That(result.Telemetry.TargetGrounding, Is.Null);
        }

        [Test]
        public async Task ADecisionTheLegalityGuardStoppedNeverReachesThisOne()
        {
            var fake = new FakeProvider();
            fake.ReplyWith(Reply("move_to", T("no", -3.0, ("sc", -0.05), ("no", -3.0)), T("_target", 0, ("_target", 0))));
            var agent = Fixtures.VillageGuard(fake);
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning,
                "Agent 'Village Guard' will do nothing: SchemaLegalityGuard rejected 'move_to' on 'no_target': the action needs a target, and the decision has none.");

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go somewhere."));

            Assert.That(result.Telemetry.GuardsFired, Is.EqualTo(new[] { "SchemaLegalityGuard" }));
            Assert.That(result.Telemetry.TargetGroundingStatus, Is.Null, "Null when the guard never ran.");
            Assert.That(result.Telemetry.TargetGrounding, Is.Null);
        }

        [Test]
        public void ThePipelineErrorTelemetryHasNoReading()
        {
            var telemetry = new DecisionTelemetry();

            Assert.That(telemetry.TargetGroundingStatus, Is.Null);
            Assert.That(telemetry.TargetGrounding, Is.Null);
        }

        [Test]
        public void TheReadingSurvivesANewtonsoftRoundTrip()
        {
            var t = new DecisionTelemetry { TargetGrounding = 0.0523f, TargetGroundingStatus = GroundingStatus.Scored };
            var off = new DecisionTelemetry { TargetGroundingStatus = GroundingStatus.Off };

            var back = JsonConvert.DeserializeObject<DecisionTelemetry>(JsonConvert.SerializeObject(t));
            var backOff = JsonConvert.DeserializeObject<DecisionTelemetry>(JsonConvert.SerializeObject(off));

            Assert.That(back.TargetGrounding, Is.EqualTo(0.0523f));
            Assert.That(back.TargetGroundingStatus, Is.EqualTo(GroundingStatus.Scored));
            Assert.That(backOff.TargetGroundingStatus, Is.EqualTo(GroundingStatus.Off));
            Assert.That(backOff.TargetGrounding, Is.Null);
            Assert.That(JsonConvert.SerializeObject(off), Does.Contain("\"TargetGroundingStatus\":4"),
                "Serialized by position, so a new status goes at the end.");
        }

        // ---- Helpers ------------------------------------------------------------------------

        // "Attack the scarecrow.": the mask wrote training_dummy, but the model wanted "sc". 0.0523.
        private static IReadOnlyList<TokenProbability> Scarecrow()
        {
            return Reply("attack_target",
                T("training", -2.95, ("sc", -0.06), ("str", -3.4), ("training", -2.95), ("the", -4.1), ("dummy", -4.8)),
                T("_dummy", -0.01, ("_dummy", -0.01)));
        }

        private static GuardVerdictKind Inspect(float threshold, IReadOnlyList<TokenProbability> tokens)
        {
            return new TargetGroundingGuard { Threshold = threshold }.Inspect(Input("move_to", "tower", tokens)).Kind;
        }

        // The decision the tokens spell, over a context with these targets, or tower and
        // training_dummy when none are given, offered as DecideAsync offers them.
        private static GuardInput Input(string action, string target, IReadOnlyList<TokenProbability> tokens,
                                        params string[] targets)
        {
            return Input(action, target, tokens, null, targets);
        }

        private static GuardInput Input(string action, string target, IReadOnlyList<TokenProbability> tokens,
                                        DecideOptions options, params string[] targets)
        {
            var ctx = Context(targets.Length > 0 ? targets : null);
            var reply = new ProviderResult { Text = Join(tokens), TokenProbabilities = tokens };
            return new GuardInput(ctx, Offering(ctx), reply, new AgentDecision(action, target, Statement), options, new DecisionTelemetry());
        }

        // attack_target and move_to need a target, follow_player does not. Null registers tower and
        // training_dummy; an empty list registers nothing.
        private static AgentContext Context(string[] targets = null)
        {
            var actions = new ActionRegistry();
            actions.Register(Definition("follow_player", false), new FakeHandler());
            actions.Register(Definition("move_to", true), new FakeHandler());
            actions.Register(Definition("attack_target", true), new FakeHandler());

            var registry = new TargetRegistry();
            foreach (string id in targets ?? new[] { "tower", "training_dummy" })
            {
                registry.Register(id, new object());
            }

            return new AgentContext(new AgentIdentity { Name = "Guard" }, "", null, new Dictionary<string, object>(), registry, actions);
        }

        private static DecisionRequest Offering(AgentContext ctx)
        {
            return new DecisionRequest { Schema = DecisionSchema.Build(ctx) };
        }

        private static ActionDefinition Definition(string id, bool requiresTarget)
        {
            return new ActionDefinition
            {
                Id = id,
                Description = id,
                RequiresTarget = requiresTarget,
                ExampleStimulus = "Example for " + id + ".",
            };
        }
    }
}
