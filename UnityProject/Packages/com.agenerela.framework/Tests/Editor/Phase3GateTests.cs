using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;
using static Agenerela.Tests.Tokens;

namespace Agenerela.Tests
{
    /// <summary>
    /// The seven checks that close Phase 3 in EditMode (build plan §3, #110), with no model running.
    /// If these pass, the guards of §2.5 stand between the provider and the handler. Each failure
    /// message names the rule that broke.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every check runs the whole pipeline, <see cref="Agent.DecideAsync"/> against
    /// <see cref="FakeProvider"/>, with a hand-built reply and, where the grounding guard reads
    /// them, hand-built token probabilities (<see cref="Tokens"/>). The unit tests of #105, #106
    /// and #107 cover each guard on its own; these prove each rule survives the guards joined to
    /// the agent, in one file a reviewer can read in a few minutes, as <c>Phase1GateTests</c> does.
    /// </para>
    /// <para>
    /// Each check asserts only its own rule, so switching one rule off fails one check. Check 1
    /// asserts on the decision the agent will carry out; checks 5 and 6 on the guard's verdict and
    /// reading; check 4 on telemetry agreeing with the verdict, whatever the verdict was. The live
    /// half of the gate, the prototype's 20 prompts on a 2B, is <c>Phase3LiveSuiteTests</c> in
    /// the PlayMode assembly.
    /// </para>
    /// </remarks>
    public sealed class Phase3GateTests
    {
        private const string Grounding = "TargetGroundingGuard";
        private const string Legality = "SchemaLegalityGuard";

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        [Test]
        public async Task Gate1_SubstitutionIsRewrittenToNone()
        {
            var fake = new FakeProvider();
            fake.ReplyWith(Scarecrow());
            var agent = Fixtures.VillageGuard(fake);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Attack the scarecrow."));

            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None),
                "Substitution regressed: \"Attack the scarecrow.\", answered attack_target on training_dummy with "
              + "only 0.05 of the model's probability on the target list, was not rewritten to none. The model "
              + "wanted \"sc…\", and the schema's mask swapped in the nearest legal target: the worst failure, "
              + "because it looks like obedience (build plan §2.5, DR-016).");
            Assert.That(result.Decision.TargetId, Is.EqualTo(TargetRegistry.NoTarget),
                "A rewrite to none must also clear the target to no_target, or the refusal still points at the dummy.");
            Assert.That(result.Telemetry.GuardsFired, Does.Contain(Grounding),
                "The substitution was stopped, but not by TargetGroundingGuard, or without saying so (DR-016).");
            Assert.That(result.ModelDecision.ActionId + " " + result.ModelDecision.TargetId,
                Is.EqualTo("attack_target training_dummy"),
                "The model's own answer must stay on ModelDecision beside the rewrite, so the eval harness can tell "
              + "\"model wrong but contained\" from \"model right\" (build plan §2.5).");

            Assert.That(agent.Execute(result), Is.True, "A contained refusal: none is carried out.");
            Assert.That(Fixtures.Handler(agent, "attack_target").Executions, Is.Empty,
                "The training dummy was attacked after the guard refused (hard rule 5).");
        }

        [Test]
        public async Task Gate2_UnavailableActionIsRejected()
        {
            // Not following, so stop_following was never offered.
            var fake = new FakeProvider(Answer("stop_following", TargetRegistry.NoTarget));
            var agent = Fixtures.VillageGuard(fake, isFollowing: false);

            var (result, warnings) = await Warnings(() => agent.DecideAsync("Wait for me by the gate."));

            Assert.That(result.Verdict.HasValue && result.Verdict.Value.Kind == GuardVerdictKind.Reject
                      && result.Verdict.Value.Guard == Legality, Is.True,
                "Legality regressed: stop_following, answered by an agent that is not following and so was never "
              + "offered it, was not rejected by SchemaLegalityGuard. Providers are untrusted: what the grammar "
              + "should have enforced is checked again in code (build plan §2.5, item 1).");
            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None), "A rejected decision becomes none.");
            Assert.That(warnings, Has.Exactly(1).Contains(Legality + " rejected 'stop_following'"),
                "A rejection is the provider breaking the contract, and is logged as a warning, once (#106).");
            Assert.That(agent.Execute(result), Is.False, "A rejected decision is never carried out.");
            Assert.That(Fixtures.Handler(agent, "stop_following").Executions, Is.Empty);
        }

        [Test]
        public async Task Gate3_TargetNeedingActionWithNoTargetIsRejected()
        {
            var fake = new FakeProvider(Answer("move_to", TargetRegistry.NoTarget));
            var agent = Fixtures.VillageGuard(fake);

            var (result, warnings) = await Warnings(() => agent.DecideAsync("Go somewhere useful."));

            Assert.That(result.Verdict.HasValue && result.Verdict.Value.Kind == GuardVerdictKind.Reject
                      && result.Verdict.Value.Guard == Legality, Is.True,
                "Legality regressed: move_to answered with no_target was not rejected by SchemaLegalityGuard. An "
              + "action that needs a target must get a real one; no_target is how the model refuses, not a place "
              + "to walk to (build plan §2.5, item 1).");
            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None), "A rejected decision becomes none.");
            Assert.That(warnings, Has.Exactly(1).Contains(Legality + " rejected 'move_to' on 'no_target'"),
                "A rejection is the provider breaking the contract, and is logged as a warning, once (#106).");
            Assert.That(agent.Execute(result), Is.False, "A rejected decision is never carried out.");
            Assert.That(Fixtures.Handler(agent, "move_to").Executions, Is.Empty);
        }

        [Test]
        public async Task Gate4_VerdictsAreInTelemetry()
        {
            // The decisions of checks 1 to 3, then one every guard passes. Whether a guard stops each
            // is those checks' rule; this one is that telemetry says exactly what the verdict was, so a
            // rule switched off fails its own check rather than this one too. The rejections' warnings
            // are taken for the same reason, without asserting on them.
            var cases = new (string Name, Func<FakeProvider, Agent> Setup, string Stimulus)[]
            {
                ("the substitution of check 1", f => { f.ReplyWith(Scarecrow()); return Fixtures.VillageGuard(f); },
                    "Attack the scarecrow."),
                ("the unavailable action of check 2", f => { f.Reply = Answer("stop_following", TargetRegistry.NoTarget); return Fixtures.VillageGuard(f); },
                    "Wait for me by the gate."),
                ("the missing target of check 3", f => { f.Reply = Answer("move_to", TargetRegistry.NoTarget); return Fixtures.VillageGuard(f); },
                    "Go somewhere useful."),
                ("a decision every guard passes", f => { f.ReplyWith(TrainingDummy()); return Fixtures.VillageGuard(f); },
                    "Attack the training dummy."),
            };

            int stopped = 0;
            foreach (var (name, setup, stimulus) in cases)
            {
                var fake = new FakeProvider();
                var (result, _) = await Warnings(() => setup(fake).DecideAsync(stimulus));
                var t = result.Telemetry;

                if (!result.Verdict.HasValue)
                {
                    Assert.That(t.GuardsFired, Is.Empty, $"Telemetry regressed for {name}: no guard stopped it, yet GuardsFired lists {string.Join(", ", t.GuardsFired)}.");
                    Assert.That(t.Verdict, Is.Null, $"Telemetry regressed for {name}: no guard stopped it, yet Verdict is {t.Verdict}.");
                    Assert.That(t.GuardReason, Is.Null, $"Telemetry regressed for {name}: no guard stopped it, yet GuardReason is \"{t.GuardReason}\".");
                    continue;
                }

                stopped++;
                var verdict = result.Verdict.Value;
                const string why = " Telemetry is how the eval harness tells \"model right\", \"model wrong but "
                                 + "contained\" and \"model wrong and executed\" apart (build plan §2.5).";
                Assert.That(t.GuardsFired, Is.EqualTo(new[] { verdict.Guard }),
                    $"Telemetry regressed for {name}: {verdict.Guard} stopped it, and GuardsFired must name that guard alone." + why);
                Assert.That(t.Verdict, Is.EqualTo(verdict.Kind),
                    $"Telemetry regressed for {name}: {verdict.Guard} returned {verdict.Kind}, and Verdict must record it." + why);
                Assert.That(t.GuardReason, Is.EqualTo(verdict.Reason).And.Not.Empty,
                    $"Telemetry regressed for {name}: GuardReason must be {verdict.Guard}'s reason, word for word." + why);
            }

            // Not a guard rule, only a sign the cases still exercise both branches above.
            Assert.That(stopped, Is.GreaterThan(0), "No case was stopped by a guard, so nothing about recorded verdicts was checked.");
        }

        [Test]
        public async Task Gate5_BelowTheThresholdIsRewrittenASplitPasses()
        {
            // 0.70 on tower, 0.25 on "sc", off the list: below 0.8, so refused.
            var fake = new FakeProvider();
            fake.ReplyWith(Reply("move_to", T("tower", Math.Log(0.70), ("tower", Math.Log(0.70)), ("sc", Math.Log(0.25)))));
            var agent = Fixtures.VillageGuard(fake);

            var below = await Fixtures.NotCancelled(agent.DecideAsync("Head to the lookout."));

            Assert.That(below.Telemetry.TargetGrounding, Is.EqualTo(0.70f).Within(0.001f),
                "The grounding reading is not the share of probability on the list (DR-016).");
            Assert.That(below.Verdict.HasValue && below.Verdict.Value.Kind == GuardVerdictKind.RewriteToNone, Is.True,
                "Threshold regressed: a target with 0.70 of the model's probability on the list, below the 0.8 of "
              + "DR-016, was not rewritten to none. Any change to the threshold needs an A/B run with a control arm "
              + "(build plan §2.5, Limits).");

            // 0.55 on tower and 0.43 on training…, both listed: the model is torn between two targets
            // that are both here, which is a choice, not a substitution. 0.98 stays on the list.
            fake.ReplyWith(Reply("move_to",
                T("tower", Math.Log(0.55), ("tower", Math.Log(0.55)), ("training", Math.Log(0.43)))));

            var split = await Fixtures.NotCancelled(agent.DecideAsync("Go wherever you think best."));

            Assert.That(split.Telemetry.TargetGrounding, Is.EqualTo(0.98f).Within(0.001f),
                "A split between two listed targets must be read as the sum of both, all of it on the list (DR-016).");
            Assert.That(split.Verdict.HasValue, Is.False,
                "Split regressed: a decision whose probability split 0.55 and 0.43 between two listed targets was "
              + "refused. Only probability that went to something absent counts against an answer; a model torn "
              + "between two things that are here keeps all of it on the list (build plan §2.5, DR-016).");
        }

        [Test]
        public async Task Gate6_CheckTargetOffSkipsTheGuardForThatCallOnly()
        {
            var fake = new FakeProvider();
            fake.ReplyWith(Scarecrow());
            var agent = Fixtures.VillageGuard(fake);

            var off = await Fixtures.NotCancelled(agent.DecideAsync("Attack the scarecrow.", new DecideOptions { CheckTarget = false }));

            Assert.That(off.Telemetry.TargetGroundingStatus, Is.EqualTo(GroundingStatus.Off),
                "CheckTarget regressed: with CheckTarget = false the grounding guard still ran on that call. The "
              + "developer turned it off to have the agent act on its best guess (DecideOptions.CheckTarget, DR-016).");
            Assert.That(off.Verdict.HasValue, Is.False,
                "CheckTarget regressed: with CheckTarget = false the substitution was still refused.");

            var next = await Fixtures.NotCancelled(agent.DecideAsync("Attack the scarecrow."));

            Assert.That(next.Telemetry.TargetGroundingStatus, Is.EqualTo(GroundingStatus.Scored),
                "CheckTarget regressed: turning it off for one call left it off for the next. It is a per-call "
              + "switch, on by default, whoever calls (DR-016).");
            Assert.That(next.Verdict.HasValue && next.Verdict.Value.Guard == Grounding, Is.True,
                "CheckTarget regressed: the call after an unchecked one was not checked again.");
        }

        [Test]
        public async Task Gate7_NoProbabilitiesIsRecordedNotPassedSilently()
        {
            var fake = new FakeProvider(Join(Scarecrow()))
            {
                Capabilities = new ProviderCapabilities
                {
                    SupportsConstrainedDecoding = true,
                    Dialect = SchemaDialect.JsonSchema,
                    ReportsTokenProbabilities = false,
                },
            };
            var agent = Fixtures.VillageGuard(fake);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Attack the scarecrow."));

            Assert.That(result.Telemetry.TargetGroundingStatus, Is.EqualTo(GroundingStatus.NoProbabilities),
                "No-probabilities regressed: a provider that reports no token probabilities was not recorded as "
              + "such. The guard could not run, and telemetry must say so rather than pass the decision as checked "
              + "(build plan §2.5, DR-016).");
            Assert.That(result.Telemetry.TargetGrounding, Is.Null, "With nothing to read, there is no reading.");
            Assert.That(result.Telemetry.TargetRuleSent, Is.True,
                "Target-rule regressed: with CheckTarget on and a provider that reports no probabilities, the "
              + "prompt must carry the one-line target rule, the fallback that costs nothing, and telemetry must "
              + "say it was sent (#107, DR-016: 2B 73 → 88 in the probe's form).");
            Assert.That(fake.LastRequest.SystemPrompt, Does.Contain("Only these things are here: "),
                "TargetRuleSent says the rule was sent, but the system prompt does not carry it (#107).");
        }

        // Starts a decision and awaits it, collecting the warnings logged meanwhile. Not
        // LogAssert.Expect: Unity reports an expected log that never came ahead of the test's own
        // failure, which would hide the message naming the rule that broke. The decision is started
        // here, after the recorder, since a fake reply completes it within the call.
        private static async Task<(DecisionResult Result, List<string> Warnings)> Warnings(Func<Awaitable<DecisionResult>> decide)
        {
            var recorder = new WarningRecorder(Debug.unityLogger.logHandler);
            Debug.unityLogger.logHandler = recorder;
            try
            {
                return (await Fixtures.NotCancelled(decide()), recorder.Warnings);
            }
            finally
            {
                Debug.unityLogger.logHandler = recorder.Inner;
            }
        }

        // Passes every log on unchanged, keeping the warnings' text.
        private sealed class WarningRecorder : ILogHandler
        {
            public readonly ILogHandler Inner;
            public readonly List<string> Warnings = new List<string>();

            public WarningRecorder(ILogHandler inner)
            {
                Inner = inner;
            }

            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
            {
                if (logType == LogType.Warning)
                {
                    Warnings.Add(string.Format(format, args));
                }

                Inner.LogFormat(logType, context, format, args);
            }

            public void LogException(Exception exception, UnityEngine.Object context)
            {
                Inner.LogException(exception, context);
            }
        }

        // ---- Replies --------------------------------------------------------------------------

        // "Attack the scarecrow." as DR-016's probe saw it: the mask wrote training_dummy, but the
        // model wanted "sc". 0.052 of its probability stayed on the list.
        private static IReadOnlyList<TokenProbability> Scarecrow()
        {
            return Reply("attack_target",
                T("training", -2.95, ("sc", -0.06), ("str", -3.4), ("training", -2.95), ("the", -4.1), ("dummy", -4.8)),
                T("_dummy", -0.01, ("_dummy", -0.01)));
        }

        // "Attack the training dummy.": 0.998 on the list.
        private static IReadOnlyList<TokenProbability> TrainingDummy()
        {
            return Reply("attack_target",
                T("training", -0.002, ("training", -0.002), ("dummy", -7.5)), T("_dummy", -0.001, ("_dummy", -0.001)));
        }

        private static string Answer(string action, string target)
        {
            return "{\"action\":\"" + action + "\",\"target\":\"" + target + "\",\"statement\":\"" + Statement + "\"}";
        }
    }
}
