using System;
using System.Collections.Generic;
using System.Linq;
using Agenerela.Providers;
using NUnit.Framework;

namespace Agenerela.Tests
{
    // RollingHistory on its own: what it keeps, what one recall sends, and how a decision is
    // written down. What reaches a provider through Agent is in AgentTests.
    public sealed class RollingHistoryTests
    {
        private static readonly AgentContext Ctx = new AgentContext(
            new AgentIdentity(), "", null, null, new TargetRegistry(), new ActionRegistry());

        [Test]
        public void ANewMemoryRecallsNothing()
        {
            var memory = new RollingHistory();

            Assert.That(memory.Recall(Ctx), Is.Not.Null.And.Empty);
            Assert.That(memory.Dropped, Is.Zero);
            Assert.That(memory.Forgotten, Is.Zero);
            Assert.That(memory.Turns, Is.EqualTo(6), "§2.1 sets the default window at six exchanges.");
            Assert.That(memory.MaxTokens, Is.EqualTo(RollingHistory.DefaultMaxTokens));
        }

        [Test]
        public void EachExchangeIsTheStimulusThenTheDecisionAsJsonOldestFirst()
        {
            var memory = new RollingHistory();
            memory.Remember(Ctx, Request("Player: \"Follow me.\""), Decided("follow_player", "no_target", "Lead the way."));
            memory.Remember(Ctx, Request("Player: \"Now wait here.\""), Decided("stop_following", "no_target", "I'll hold."));

            Assert.That(memory.Recall(Ctx), Is.EqualTo(new[]
            {
                "Player: \"Follow me.\"",
                "{\"action\":\"follow_player\",\"target\":\"no_target\",\"statement\":\"Lead the way.\"}",
                "Player: \"Now wait here.\"",
                "{\"action\":\"stop_following\",\"target\":\"no_target\",\"statement\":\"I'll hold.\"}",
            }));
        }

        [Test]
        public void TheDecisionIsWrittenInTheSchemasFieldOrder()
        {
            // Not the order DR-004 fixes, on purpose: the writer follows the schema it is given,
            // so it can never disagree with the request the model answered.
            var memory = new RollingHistory();
            var schema = Schema(DecisionSchema.StatementFieldName, DecisionSchema.ActionFieldName, DecisionSchema.TargetFieldName);
            memory.Remember(Ctx, Request("Player: \"Hi.\"", schema), Decided("none", "no_target", "Hello."));

            Assert.That(memory.Recall(Ctx)[1], Is.EqualTo("{\"statement\":\"Hello.\",\"action\":\"none\",\"target\":\"no_target\"}"));
        }

        [Test]
        public void ASchemaWithNoTargetFieldIsAnsweredWithNoTarget()
        {
            // With nothing registered the request asks for no target (#8), so the model's own
            // answer had none, and neither does the record of it.
            var memory = new RollingHistory();
            var schema = Schema(DecisionSchema.ActionFieldName, DecisionSchema.StatementFieldName);
            memory.Remember(Ctx, Request("Report: \"Grain fell.\"", schema), Decided("ration_grain", "no_target", ""));

            Assert.That(memory.Recall(Ctx)[1], Is.EqualTo("{\"action\":\"ration_grain\",\"statement\":\"\"}"));
        }

        [Test]
        public void AStatementIsEscapedAsJsonAndOtherwiseKeptAsWritten()
        {
            var memory = new RollingHistory();
            const string Statement = "He said \"halt\".\nThen: café, 2026-10-10 \\ done";
            memory.Remember(Ctx, Request("Player: \"Report.\""), Decided("none", "no_target", Statement));

            string written = memory.Recall(Ctx)[1];
            Assert.That(written, Does.Not.Contain("\n"), "One line per decision.");
            Assert.That(Newtonsoft.Json.Linq.JObject.Parse(written)["statement"].ToString(), Is.EqualTo(Statement));
        }

        [Test]
        public void PastTheTurnCapTheOldestExchangeIsForgottenAndCounted()
        {
            var memory = new RollingHistory(turns: 2);
            memory.Remember(Ctx, Request("Player: \"One.\""), Decided("none", "no_target", ""));
            memory.Remember(Ctx, Request("Player: \"Two.\""), Decided("none", "no_target", ""));
            memory.Remember(Ctx, Request("Player: \"Three.\""), Decided("none", "no_target", ""));

            var history = memory.Recall(Ctx);

            Assert.That(Stimuli(history), Is.EqualTo(new[] { "Player: \"Two.\"", "Player: \"Three.\"" }));
            Assert.That(memory.Count, Is.EqualTo(2));
            Assert.That(memory.Forgotten, Is.EqualTo(1));
            Assert.That(memory.Dropped, Is.Zero, "Everything still held was sent; the token cap cut nothing.");
        }

        [Test]
        public void DroppedIsThisRecallsCutAndForgottenKeepsRising()
        {
            // Both caps at once: three held, two fit. Dropped stays the token cap's cut however
            // long the conversation runs, so it says whether the budget is binding; Forgotten is
            // the total the turn cap let go of. Neither counts an exchange the other does.
            var exchange = Decided("none", "no_target", "");
            var memory = new RollingHistory(turns: 3, maxTokens: Estimate("Player: \"Order 1.\"", exchange) * 2);

            for (int i = 1; i <= 5; i++)
            {
                memory.Remember(Ctx, Request($"Player: \"Order {i}.\""), exchange);
            }

            Assert.That(Stimuli(memory.Recall(Ctx)), Is.EqualTo(new[] { "Player: \"Order 4.\"", "Player: \"Order 5.\"" }));
            Assert.That(memory.Dropped, Is.EqualTo(1));
            Assert.That(memory.Forgotten, Is.EqualTo(2));

            for (int i = 6; i <= 8; i++)
            {
                memory.Remember(Ctx, Request($"Player: \"Order {i}.\""), exchange);
            }

            memory.Recall(Ctx);
            Assert.That(memory.Dropped, Is.EqualTo(1));
            Assert.That(memory.Forgotten, Is.EqualTo(5));
        }

        [Test]
        public void PastTheTokenCapTheOldestWholeExchangesAreLeftOut()
        {
            // Each exchange is the same length, so the budget fits exactly two of them.
            var exchange = Decided("none", "no_target", "");
            int tokens = Estimate("Player: \"One.\"", exchange);
            var memory = new RollingHistory(turns: 6, maxTokens: tokens * 2 + 1);
            memory.Remember(Ctx, Request("Player: \"One.\""), exchange);
            memory.Remember(Ctx, Request("Player: \"Two.\""), exchange);
            memory.Remember(Ctx, Request("Player: \"Six.\""), exchange);

            var history = memory.Recall(Ctx);

            Assert.That(Stimuli(history), Is.EqualTo(new[] { "Player: \"Two.\"", "Player: \"Six.\"" }),
                "The newest exchanges that fit, oldest first, each one whole.");
            Assert.That(history.Count, Is.EqualTo(4), "Never a stimulus without its decision.");
            Assert.That(memory.Count, Is.EqualTo(3), "The token cap leaves an exchange out of a request; it does not forget it.");
            Assert.That(memory.Dropped, Is.EqualTo(1));
            Assert.That(memory.Forgotten, Is.Zero);
        }

        [Test]
        public void ALongExchangeStopsTheRecallEvenWhenAnOlderOneWouldFit()
        {
            // Skipping it to send the older one would leave a gap the model cannot see.
            var shortOne = Decided("none", "no_target", "");
            var longOne = Decided("none", "no_target", new string('x', 400));
            var memory = new RollingHistory(maxTokens: Estimate("Player: \"Hi.\"", shortOne) * 2);
            memory.Remember(Ctx, Request("Player: \"Hi.\""), shortOne);
            memory.Remember(Ctx, Request("Player: \"Hi.\""), longOne);
            memory.Remember(Ctx, Request("Player: \"Hi.\""), shortOne);

            Assert.That(memory.Recall(Ctx), Has.Count.EqualTo(2), "Only the newest exchange fits before the long one.");
            Assert.That(memory.Dropped, Is.EqualTo(2));
        }

        [Test]
        public void AnExchangeBiggerThanTheWholeBudgetSendsNothing()
        {
            var memory = new RollingHistory(maxTokens: 5);
            memory.Remember(Ctx, Request("Player: \"Follow me.\""), Decided("follow_player", "no_target", "Lead the way."));

            Assert.That(memory.Recall(Ctx), Is.Empty);
            Assert.That(memory.Dropped, Is.EqualTo(1));
        }

        [Test]
        public void TokensAreCharactersOverFourRoundedUp()
        {
            Assert.That(RollingHistory.EstimateTokens(0), Is.Zero);
            Assert.That(RollingHistory.EstimateTokens(1), Is.EqualTo(1));
            Assert.That(RollingHistory.EstimateTokens(4), Is.EqualTo(1));
            Assert.That(RollingHistory.EstimateTokens(5), Is.EqualTo(2));
        }

        [Test]
        public void SixOrdinaryExchangesFitTheDefaultBudget()
        {
            // The default's justification, kept honest: if it stops holding, the doc comment on
            // DefaultMaxTokens is wrong.
            var memory = new RollingHistory();
            for (int i = 0; i < 6; i++)
            {
                memory.Remember(Ctx, Request("Player: \"Follow me to the tower.\""),
                                Decided("follow_player", "no_target", "Right behind you, lead the way."));
            }

            Assert.That(memory.Recall(Ctx), Has.Count.EqualTo(12));
            Assert.That(memory.Dropped, Is.Zero);
        }

        [Test]
        public void APipelineErrorIsNotRemembered()
        {
            var memory = new RollingHistory();
            var failed = new DecisionResult(null, new DecisionTelemetry { Outcome = DecisionOutcome.PipelineError });

            memory.Remember(Ctx, Request("Player: \"Hello?\""), failed);

            Assert.That(memory.Count, Is.Zero);
            Assert.That(memory.Recall(Ctx), Is.Empty);
        }

        [Test]
        public void RecallingTwiceGivesTheSameHistoryAndRemembersNothing()
        {
            var memory = new RollingHistory();
            memory.Remember(Ctx, Request("Player: \"Follow me.\""), Decided("follow_player", "no_target", ""));

            var first = memory.Recall(Ctx);
            var second = memory.Recall(Ctx);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(memory.Count, Is.EqualTo(1));
        }

        [Test]
        public void ConstructionAndRememberRejectWhatCannotWork()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RollingHistory(turns: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RollingHistory(maxTokens: 0));

            var memory = new RollingHistory();
            Assert.Throws<ArgumentNullException>(() => memory.Remember(Ctx, null, Decided("none", "no_target", "")));
            Assert.Throws<ArgumentNullException>(() => memory.Remember(Ctx, Request("Player: \"Hi.\""), null));
        }

        // ---- Helpers ------------------------------------------------------------------------

        private static DecisionResult Decided(string action, string target, string statement)
        {
            return new DecisionResult(new AgentDecision(action, target, statement), new DecisionTelemetry());
        }

        private static DecisionRequest Request(string stimulus, DecisionSchema schema = null)
        {
            return new DecisionRequest
            {
                Stimulus = stimulus,
                Schema = schema ?? Schema(DecisionSchema.ActionFieldName, DecisionSchema.TargetFieldName, DecisionSchema.StatementFieldName),
            };
        }

        private static DecisionSchema Schema(params string[] names)
        {
            return new DecisionSchema(names.Select(name => new SchemaField { Name = name }));
        }

        // The estimate RollingHistory makes for one exchange of the default schema.
        private static int Estimate(string stimulus, DecisionResult result)
        {
            string written = RollingHistory.Write(result.Decision, Request(stimulus).Schema);
            return RollingHistory.EstimateTokens(stimulus.Length + written.Length);
        }

        private static IEnumerable<string> Stimuli(IReadOnlyList<string> history)
        {
            return history.Where((entry, i) => i % 2 == 0);
        }
    }
}
