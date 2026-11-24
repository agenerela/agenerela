using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // The prompt budget (#151): with none, every request is what it was; with one, each decision
    // records its size and whether it went over, and only the trims the developer listed leave
    // anything out, in the order listed, each recorded. Budgets here are set at 1 character a token,
    // so a budget is a count of characters and each test can place it exactly. Every await of
    // DecideAsync goes through Fixtures.NotCancelled.
    public sealed class PromptBudgetTests
    {
        private const string Chop = "{\"action\":\"chop\",\"target\":\"tree_a\",\"statement\":\"On it.\"}";

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        // ---- No budget -------------------------------------------------------------------------

        [Test]
        public void NoBudgetByDefaultAndTheRequestIsUnchanged()
        {
            Assert.That(new DecideOptions().PromptBudget, Is.Null);
            Assert.That(Woodcutter().PromptBudget, Is.Null);

            var plain = Woodcutter().Preview("What now?", Options());
            var none = Woodcutter().Preview("What now?", Options(PromptBudget.None));
            var roomy = Woodcutter().Preview("What now?", Options(new PromptBudget(100000, AllTrims)));

            foreach (var other in new[] { none, roomy })
            {
                AssertSameRequest(other.Request, plain.Request);
            }

            Assert.That(plain.Request.SystemPrompt, Does.Contain("- well"), "The untakeable target is listed with no budget.");
            Assert.That(plain.Request.Observations, Does.Contain("Smoke to the east."), "And the optional lines are sent.");
        }

        [Test]
        public void ACallsNoneTurnsTheAgentsBudgetOff()
        {
            var agent = Woodcutter();
            agent.PromptBudget = new PromptBudget(1, 1f, AllTrims);

            var request = agent.Preview("What now?", Options(PromptBudget.None)).Request;

            AssertSameRequest(request, Woodcutter().Preview("What now?", Options()).Request);
        }

        // ---- Measuring -------------------------------------------------------------------------

        [Test]
        public void TheEstimateIsTheTextsCharactersOverCharsPerToken()
        {
            var request = new DecisionRequest
            {
                SystemPrompt = "12345",
                FewShotBlock = "123",
                Observations = "12",
                History = new List<string> { "1", "1234" },
                Stimulus = "123456",
            };

            Assert.That(PromptBudget.Characters(request), Is.EqualTo(21));
            Assert.That(PromptBudget.Estimate(request, 1f), Is.EqualTo(21));
            Assert.That(PromptBudget.Estimate(request), Is.EqualTo(7), "21 characters at the default 3 a token.");
            Assert.That(PromptBudget.Estimate(request, 4f), Is.EqualTo(6), "Rounded up.");
        }

        [Test]
        public async Task EveryDecisionRecordsItsSizeAndABudgetOnlyWhenSet()
        {
            var free = await Fixtures.NotCancelled(Woodcutter(new FakeProvider(Chop)).DecideAsync("What now?", Options()));
            int characters = PromptBudget.Characters(free.Request);

            Assert.That(free.Telemetry.PromptCharacters, Is.EqualTo(characters));
            Assert.That(free.Telemetry.PromptTokensEstimated, Is.EqualTo((int)Math.Ceiling(characters / PromptBudget.DefaultCharsPerToken)));
            Assert.That(free.Telemetry.PromptBudget, Is.EqualTo(0));
            Assert.That(free.Telemetry.OverBudget, Is.False);

            var agent = Woodcutter(new FakeProvider(Chop));
            agent.PromptBudget = new PromptBudget(4096);
            var budgeted = await Fixtures.NotCancelled(agent.DecideAsync("What now?", Options()));

            Assert.That(budgeted.Telemetry.PromptBudget, Is.EqualTo(4096));
            Assert.That(budgeted.Telemetry.OverBudget, Is.False);
            Assert.That(budgeted.Telemetry.PromptTrims, Is.Empty);
        }

        [Test]
        public async Task TheProvidersCountDecidesOnceItReportsOne()
        {
            // An estimate far over, a count under: the count wins.
            var provider = new FakeProvider(Chop) { PromptTokens = 90 };
            var agent = Woodcutter(provider);
            agent.PromptBudget = new PromptBudget(100, 1f);

            var under = await Fixtures.NotCancelled(agent.DecideAsync("What now?", Options()));
            Assert.That(under.Telemetry.PromptTokensEstimated, Is.GreaterThan(100), "The fixture's estimate must be over for this to mean anything.");
            Assert.That(under.Telemetry.OverBudget, Is.False);

            // And a count over a roomy estimate is over.
            provider.PromptTokens = 5000;
            agent.PromptBudget = new PromptBudget(4096);
            LogAssert.Expect(LogType.Warning, new Regex("Agent 'Fenn': its prompt was 5000 tokens, as the provider counted it, over its budget of 4096"));
            var over = await Fixtures.NotCancelled(agent.DecideAsync("What now?", Options()));
            Assert.That(over.Telemetry.PromptTokensEstimated, Is.LessThan(4096));
            Assert.That(over.Telemetry.OverBudget, Is.True);
        }

        [Test]
        public async Task OnlyTheFirstDecisionOverTheBudgetWarns()
        {
            var agent = Woodcutter(new FakeProvider(Chop) { PromptTokens = 500 });
            agent.PromptBudget = new PromptBudget(400);

            LogAssert.Expect(LogType.Warning, new Regex("over its budget of 400"));
            var first = await Fixtures.NotCancelled(agent.DecideAsync("What now?", Options()));
            var second = await Fixtures.NotCancelled(agent.DecideAsync("What now?", Options()));

            LogAssert.NoUnexpectedReceived();
            Assert.That(first.Telemetry.OverBudget, Is.True);
            Assert.That(second.Telemetry.OverBudget, Is.True, "Still recorded, only not warned again.");
        }

        [Test]
        public async Task ABudgetWithNoTrimsLeavesNothingOut()
        {
            var agent = Woodcutter(new FakeProvider(Chop));
            agent.PromptBudget = new PromptBudget(1, 1f);

            LogAssert.Expect(LogType.Warning, new Regex("over its budget of 1"));
            var result = await Fixtures.NotCancelled(agent.DecideAsync("What now?", Options()));

            AssertSameRequest(result.Request, Woodcutter().Preview("What now?", Options()).Request);
            Assert.That(result.Telemetry.OverBudget, Is.True);
            Assert.That(result.Telemetry.PromptTrims, Is.Empty);
        }

        // ---- Trims -------------------------------------------------------------------------------

        [Test]
        public void OptionalLinesGoFromTheLastWhileOver()
        {
            // Room for the first optional line and not the second.
            int withFirst = Size(Woodcutter().Preview("What now?", Options(null, "Smoke to the east.")).Request);
            var preview = Woodcutter().Preview("What now?",
                Options(new PromptBudget(withFirst, 1f, PromptTrim.OptionalObservations), "Smoke to the east.", "A crow on the fence.", "Rain later."));

            Assert.That(preview.Request.Observations, Does.Contain("Smoke to the east.").And.Not.Contain("crow").And.Not.Contain("Rain"));
            Assert.That(preview.Request.Observations, Does.Contain("The axe is sharp."), "A line the call must send is never trimmed.");
            Assert.That(preview.Context.Observations, Does.Not.Contain("Rain later."), "The context the guards read is the one sent.");
            Assert.That(Size(preview.Request), Is.EqualTo(withFirst));
        }

        [Test]
        public async Task ADecisionRecordsTheLinesItLeftOut()
        {
            int withFirst = Size(Woodcutter().Preview("What now?", Options(null, "Smoke to the east.")).Request);
            var result = await Fixtures.NotCancelled(Woodcutter(new FakeProvider(Chop)).DecideAsync("What now?",
                Options(new PromptBudget(withFirst, 1f, PromptTrim.OptionalObservations), "Smoke to the east.", "A crow on the fence.", "Rain later.")));

            Assert.That(result.Telemetry.ObservationsDropped, Is.EqualTo(new[] { "Rain later.", "A crow on the fence." }));
            Assert.That(result.Telemetry.PromptTrims, Is.EqualTo(new[] { "OptionalObservations: 2 lines" }));
            Assert.That(result.Telemetry.OverBudget, Is.False);
            Assert.That(result.Telemetry.PromptCharacters, Is.EqualTo(withFirst));
        }

        [Test]
        public void UntakeableTargetsLeaveTheListButNotTheSchema()
        {
            var full = Woodcutter().Preview("What now?", Options());
            var preview = Woodcutter().Preview("What now?", Options(new PromptBudget(1, 1f, PromptTrim.UntakeableTargets)));

            Assert.That(preview.Request.SystemPrompt, Does.Contain("- tree_a").And.Contain("- tree_b").And.Not.Contain("- well"));
            Assert.That(preview.TargetsUnlisted, Is.EqualTo(new[] { "well" }));
            Assert.That(TargetEnum(preview.Request.Schema), Is.EqualTo(TargetEnum(full.Request.Schema)), "The schema still offers it.");
            Assert.That(TargetEnum(preview.Request.Schema), Does.Contain("well"));
        }

        [Test]
        public async Task HistoryGoesOldestFirstWhileOver()
        {
            var agent = Woodcutter(new FakeProvider(Chop));
            agent.Memory = new RollingHistory(turns: 6, maxTokens: 100000);
            foreach (string said in new[] { "First.", "Second.", "Third." })
            {
                await Fixtures.NotCancelled(agent.DecideAsync(said, Options()));
            }

            var whole = agent.Preview("What now?", Options()).Request;
            Assert.That(whole.History.Count, Is.EqualTo(6), "Three exchanges held.");
            int lastTwo = Size(whole) - whole.History[0].Length - whole.History[1].Length;

            var result = await Fixtures.NotCancelled(agent.DecideAsync("What now?",
                Options(new PromptBudget(lastTwo, 1f, PromptTrim.History))));

            Assert.That(result.Request.History, Is.EqualTo(whole.History.Skip(2).ToList()));
            Assert.That(result.Telemetry.HistoryTurnsSent, Is.EqualTo(2));
            Assert.That(result.Telemetry.HistoryTurnsDropped, Is.EqualTo(1));
            Assert.That(result.Telemetry.PromptTrims, Is.EqualTo(new[] { "History: 1 exchange" }));
        }

        [Test]
        public void TrimsRunInTheDevelopersOrderAndStopOnceUnder()
        {
            // Either trim alone is enough; the first listed is the one taken.
            int all = Size(Woodcutter().Preview("What now?", Options(null, "Smoke to the east.")).Request);
            string[] optional = { "Smoke to the east." };

            var linesFirst = Woodcutter().Preview("What now?",
                Options(new PromptBudget(all - 5, 1f, PromptTrim.OptionalObservations, PromptTrim.UntakeableTargets), optional));
            var targetsFirst = Woodcutter().Preview("What now?",
                Options(new PromptBudget(all - 5, 1f, PromptTrim.UntakeableTargets, PromptTrim.OptionalObservations), optional));

            Assert.That(linesFirst.Request.Observations, Does.Not.Contain("Smoke"));
            Assert.That(linesFirst.Request.SystemPrompt, Does.Contain("- well"), "Under once the line went, so the targets stay.");
            Assert.That(targetsFirst.Request.SystemPrompt, Does.Not.Contain("- well"));
            Assert.That(targetsFirst.Request.Observations, Does.Contain("Smoke"), "Under once the target went, so the line stays.");
        }

        [Test]
        public void NothingIsTrimmedWhenUnder()
        {
            int all = Size(Woodcutter().Preview("What now?", Options()).Request);
            var preview = Woodcutter().Preview("What now?", Options(new PromptBudget(all, 1f, AllTrims)));

            AssertSameRequest(preview.Request, Woodcutter().Preview("What now?", Options()).Request);
        }

        // ---- The budget itself, and the log ----------------------------------------------------------

        [Test]
        public void ABudgetThatCannotBeIsAMistake()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PromptBudget(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PromptBudget(-5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PromptBudget(100, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PromptBudget(100, float.NaN));
            Assert.Throws<ArgumentException>(() => new PromptBudget(100, PromptTrim.History, PromptTrim.History));
            Assert.Throws<ArgumentException>(() => new PromptBudget(100, (PromptTrim)99));
            Assert.That(PromptBudget.None.IsSet, Is.False);
            Assert.That(new PromptBudget(100, PromptTrim.History, PromptTrim.OptionalObservations).Trims,
                Is.EqualTo(new[] { PromptTrim.History, PromptTrim.OptionalObservations }));
        }

        [Test]
        public async Task TheDecisionLogCarriesTheBudget()
        {
            var agent = Woodcutter(new FakeProvider(Chop));
            int withFirst = Size(agent.Preview("What now?", Options(null, "Smoke to the east.")).Request);
            var result = await Fixtures.NotCancelled(agent.DecideAsync("What now?",
                Options(new PromptBudget(withFirst, 1f, PromptTrim.OptionalObservations), "Smoke to the east.", "Rain later.")));

            var line = DecisionLogLine.From(new DecisionFeedEntry(agent, result, DecisionStage.Decided, null, DateTime.UtcNow, 1));

            Assert.That(line.PromptBudget, Is.EqualTo(withFirst));
            Assert.That(line.OverBudget, Is.False);
            Assert.That(line.PromptCharacters, Is.EqualTo(withFirst));
            Assert.That(line.PromptTokensEstimated, Is.EqualTo(withFirst));
            Assert.That(line.PromptTrims, Is.EqualTo(new[] { "OptionalObservations: 1 line" }));
            Assert.That(DecisionLogLine.CurrentVersion, Is.AtLeast(3), "Version 3 added the budget fields.");

            var free = await Fixtures.NotCancelled(Woodcutter(new FakeProvider(Chop)).DecideAsync("What now?", Options()));
            var plain = DecisionLogLine.From(new DecisionFeedEntry(agent, free, DecisionStage.Decided, null, DateTime.UtcNow, 2));
            Assert.That(plain.PromptBudget, Is.Null, "Left out with no budget.");
            Assert.That(plain.OverBudget, Is.Null);
            Assert.That(plain.PromptTrims, Is.Null);
        }

        // ---- Fixtures --------------------------------------------------------------------------------

        private static readonly PromptTrim[] AllTrims = { PromptTrim.OptionalObservations, PromptTrim.UntakeableTargets, PromptTrim.History };

        // A woodcutter who can only chop: the well is a target no action takes.
        private static Agent Woodcutter(FakeProvider provider = null)
        {
            var actions = new[]
            {
                new ActionDefinition { Id = "chop", Description = "Chop a tree.", RequiresTarget = true, TargetCategories = new[] { "tree" } },
            };

            var agent = new Agent(new AgentIdentity { Name = "Fenn", Role = "a woodcutter" }, actions, provider ?? new FakeProvider(Chop));
            agent.Bind("chop", new FakeHandler());
            agent.Targets.Register("tree_a", new object(), "tree");
            agent.Targets.Register("tree_b", new object(), "tree");
            agent.Targets.Register("well", new object(), "place");
            return agent;
        }

        private static DecideOptions Options(PromptBudget budget = null, params string[] optional)
        {
            return new DecideOptions
            {
                ListActionsAndTargets = true,
                Observations = new List<string> { "The axe is sharp." },
                OptionalObservations = optional == null || optional.Length == 0 ? new List<string> { "Smoke to the east." } : optional.ToList(),
                PromptBudget = budget,
            };
        }

        private static int Size(DecisionRequest request) => PromptBudget.Characters(request);

        private static void AssertSameRequest(DecisionRequest actual, DecisionRequest expected)
        {
            Assert.That(actual.SystemPrompt, Is.EqualTo(expected.SystemPrompt));
            Assert.That(actual.FewShotBlock, Is.EqualTo(expected.FewShotBlock));
            Assert.That(actual.Observations, Is.EqualTo(expected.Observations));
            Assert.That(actual.History, Is.EqualTo(expected.History));
            Assert.That(actual.Stimulus, Is.EqualTo(expected.Stimulus));
            Assert.That(new JsonSchemaSerializer().Serialize(actual.Schema), Is.EqualTo(new JsonSchemaSerializer().Serialize(expected.Schema)));
        }

        private static IReadOnlyList<string> TargetEnum(DecisionSchema schema)
        {
            return schema.Fields.First(f => f.Name == DecisionSchema.TargetFieldName).AllowedValues;
        }
    }
}
