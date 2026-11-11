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
    // Agent.Preview (#166): the request DecideAsync would send next, and what the agent is offered,
    // worked out by the same code and never sent.
    public sealed class AgentPreviewTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        // ---- The request ---------------------------------------------------------------------

        [Test]
        public async Task ThePreviewIsTheRequestDecideAsyncSendsFieldForField()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Id = "guard_north";
            agent.ObservationSources.Add(new FakeObservationSource("The gate is open.", "A cart waits outside."));
            agent.TargetSources.Add(new DroppingSource("bridge", "well"));

            // Something to remember, so the history is not empty.
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            var options = new DecideOptions
            {
                StimulusLabel = "Captain",
                Observations = new[] { "It is raining." },
                ExampleStatements = true,
                PairTargetsWithActions = true,
            };

            var preview = agent.Preview("Go to the tower now.", options);
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower now.", options));
            var sent = fake.LastRequest;

            Assert.That(preview.Error, Is.Null);
            Assert.That(preview.Request, Is.Not.SameAs(sent));
            Assert.That(preview.Request.SystemPrompt, Is.EqualTo(sent.SystemPrompt));
            Assert.That(preview.Request.FewShotBlock, Is.EqualTo(sent.FewShotBlock));
            Assert.That(preview.Request.Observations, Is.EqualTo(sent.Observations));
            Assert.That(preview.Request.History, Is.EqualTo(sent.History));
            Assert.That(preview.Request.History, Is.Not.Empty, "The first decision is in it.");
            Assert.That(preview.Request.Stimulus, Is.EqualTo(sent.Stimulus));
            Assert.That(Json(preview.Request.Schema), Is.EqualTo(Json(sent.Schema)), "The schema, descriptions and pairs included.");
            Assert.That(preview.Request.AgentName, Is.EqualTo(sent.AgentName));
            Assert.That(preview.Request.AgentId, Is.EqualTo(sent.AgentId));
            Assert.That(preview.Request.StimulusText, Is.EqualTo(sent.StimulusText));
            Assert.That(preview.Request.StimulusLabel, Is.EqualTo(sent.StimulusLabel));
        }

        [Test]
        public void ThePreviewShowsKnowledgeAndStatementRulesOutsideTheObservationCap()
        {
            // #190, Living Vale #14: what a code-built or profile agent always knows is part of the
            // system prompt, so the cap on observations never drops it.
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            agent.Identity.Knowledge = new List<string> { "The well is dry.", "The bridge closes at dusk.", "Wolves come at night." };
            agent.Identity.StatementRules = "One short sentence.";
            agent.MaxObservations = 1;
            agent.ObservationSources.Add(new FakeObservationSource("The gate is open.", "A cart waits outside."));

            var preview = agent.Preview("Go to the tower.");

            Assert.That(preview.Error, Is.Null);
            Assert.That(preview.Request.SystemPrompt, Does.Contain(string.Join("\n",
                "What you know:", "- The well is dry.", "- The bridge closes at dusk.", "- Wolves come at night.",
                "Your statement: One short sentence.")));
            Assert.That(preview.Request.Observations, Is.EqualTo("What you know right now:\n- The gate is open."));
        }

        [Test]
        public void ACodeBuiltAgentSendsTheKnowledgeItsIdentityIsGiven()
        {
            var identity = Fixtures.VillageGuardIdentity();
            identity.Knowledge = new List<string> { "The well is dry." };
            var agent = new Agent(identity, new[] { new ActionDefinition { Id = "move_to", RequiresTarget = true } }, new FakeProvider(GoToTheTower));
            agent.Bind("move_to", new FakeHandler());
            agent.Targets.Register("tower", new object());

            Assert.That(agent.Preview("Go to the tower.").Request.SystemPrompt, Does.Contain("What you know:\n- The well is dry."));

            identity.StatementRules = "One short sentence.";
            Assert.That(agent.Preview("Go to the tower.").Request.SystemPrompt, Does.Contain("Your statement: One short sentence."),
                "Read on every call, so a change applies from the next one.");
        }

        [Test]
        public void APreviewLeavesMemoryAndTheProviderUntouched()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            var memory = new CountingMemory { History = new[] { "Player: Halt.", "{\"action\":\"none\"}" } };
            agent.Memory = memory;

            var preview = agent.Preview("Go to the tower.");

            Assert.That(memory.Recalls, Is.EqualTo(1), "Recalled once, as a decision recalls, and a stateful strategy sees that call.");
            Assert.That(memory.Remembers, Is.Zero, "Nothing is remembered.");
            Assert.That(preview.Memory, Is.SameAs(memory));
            Assert.That(preview.History, Is.EqualTo(memory.History));
            Assert.That(fake.Requests, Is.Empty, "The provider is never asked.");
            Assert.That(RequestScheduler.For(fake).InFlight, Is.Zero, "Nor its queue.");
            Assert.That(ProviderHealth.For(fake).IsDown, Is.False);
            Assert.That(agent.IsDeciding, Is.False);
        }

        [Test]
        public void AFailingSourceGivesAnErrorNotAThrowAndLogsNothing()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            agent.ObservationSources.Add(new FakeObservationSource("x") { Failure = new InvalidOperationException("the source broke") });

            var preview = agent.Preview("Go to the tower.");

            Assert.That(preview.Request, Is.Null);
            Assert.That(preview.ErrorKind, Is.EqualTo(PipelineErrorKind.RequestPreparation));
            Assert.That(preview.Error, Does.Contain("Preparing the request failed").And.Contain("the source broke"));
            Assert.That(preview.RegisteredTargets, Is.EqualTo(new[] { "tower", "training_dummy" }), "What came before the failure is kept.");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task AFailedPreviewSaysWhatTheDecisionWouldRecord()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            agent.ObservationSources.Add(new FakeObservationSource("x") { Failure = new InvalidOperationException("the source broke") });
            LogAssert.Expect(LogType.Warning, new Regex("Preparing the request failed"));

            var preview = agent.Preview("Go to the tower.");
            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(preview.Error, Is.EqualTo(result.Telemetry.ErrorMessage));
            Assert.That(preview.ErrorKind, Is.EqualTo(result.Telemetry.ErrorKind));
        }

        [Test]
        public void MistakenOptionsThrowAsTheyDoForDecideAsync()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));

            Assert.Throws<ArgumentException>(() => agent.Preview("Hello.", new DecideOptions { StimulusLabel = " " }));
            Assert.Throws<ArgumentOutOfRangeException>(() => agent.Preview("Hello.", new DecideOptions { MaxListedTargets = -1 }));
        }

        // ---- What it is offered --------------------------------------------------------------

        [Test]
        public void ThePreviewSaysWhichActionsAreOfferedAndWhyTheOthersAreNot()
        {
            var profile = Fixtures.VillageGuardProfile();
            var agent = new Agent(profile, new FakeProvider(GoToTheTower));
            agent.Bind("follow_player", new FakeHandler(ctx => false));
            agent.Bind("stop_following", new FakeHandler());
            agent.Bind("move_to", new FakeHandler());

            var preview = agent.Preview("Hello.");

            Assert.That(preview.Available.Select(a => a.Id), Is.EqualTo(new[] { "stop_following" }));
            Assert.That(Reasons(preview), Is.EqualTo(new Dictionary<string, string>
            {
                ["attack_target"] = "Nothing binds it, so nothing could run it.",
                ["follow_player"] = "Its handler's IsAvailable says no.",
                ["move_to"] = "It needs a target, and none is offered.",
            }));
        }

        [Test]
        public void APairedPreviewLeavesOutAnActionThatTakesNoTargetOffered()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            agent.Actions.TryGet("attack_target", out var attack, out _);
            attack.TargetCategories = new[] { "creature" };
            agent.Targets.Unregister("training_dummy");
            agent.Targets.Register("training_dummy", new object(), "prop");

            var preview = agent.Preview("Hello.", new DecideOptions { PairTargetsWithActions = true });

            Assert.That(preview.Available.Select(a => a.Id), Does.Not.Contain("attack_target"));
            Assert.That(Reasons(preview)["attack_target"], Is.EqualTo("No target offered is one it takes."));
        }

        [Test]
        public void ThePreviewShowsTheTargetsOfEachSourceAndWhatWasLeftOut()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            var dropping = new DroppingSource("bridge", "well", "mill");
            agent.TargetSources.Add(dropping);
            agent.TargetSources.Add(new ExplicitTargetSource().Add("goblin", new object(), "creature").Add("cart", new object(), "prop"));
            agent.Targets.Unregister("tower");
            agent.Targets.Register("tower", new object(), "place");
            agent.TargetCategories.AddRange(new[] { "place", "creature" });

            var preview = agent.Preview("Hello.");

            Assert.That(preview.RegisteredTargets, Is.EqualTo(new[] { "training_dummy", "tower" }));
            Assert.That(preview.TargetSources.Select(s => s.Name), Is.EqualTo(new[] { "DroppingSource", "ExplicitTargetSource" }));
            Assert.That(preview.TargetSources[0].Source, Is.SameAs(dropping));
            Assert.That(preview.TargetSources[0].Kept, Is.EqualTo(new[] { "bridge" }));
            Assert.That(preview.TargetSources[0].Dropped, Is.EqualTo(new[] { "well", "mill" }));
            Assert.That(preview.TargetSources[1].Kept, Is.EqualTo(new[] { "goblin", "cart" }));
            Assert.That(preview.TargetSources[1].Dropped, Is.Empty);
            Assert.That(preview.TargetsLeftOutByCategory, Is.EquivalentTo(new[] { "training_dummy", "bridge", "cart" }));
            Assert.That(preview.Context.Targets.Ids, Is.EquivalentTo(new[] { "tower", "goblin" }), "What is offered.");
        }

        [Test]
        public void ThePreviewShowsTheObservationsOfEachSourceAndWhatWasLeftOut()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            agent.MaxObservations = 3;
            var first = new FakeObservationSource("The gate is open.") { Drops = new[] { "A bird sings." } };
            var second = new FakeObservationSource("A cart waits.", "The bell rings.");
            agent.ObservationSources.Add(first);
            agent.ObservationSources.Add(second);

            var preview = agent.Preview("Hello.", new DecideOptions { Observations = new[] { "It is raining.", " " } });

            Assert.That(preview.CallObservations, Is.EqualTo(new[] { "It is raining." }), "Blank lines are no observation.");
            Assert.That(preview.ObservationSources.Select(s => s.Name), Is.EqualTo(new[] { "FakeObservationSource", "FakeObservationSource" }));
            Assert.That(preview.ObservationSources[0].Source, Is.SameAs(first));
            Assert.That(preview.ObservationSources[0].Kept, Is.EqualTo(new[] { "The gate is open." }));
            Assert.That(preview.ObservationSources[0].Dropped, Is.EqualTo(new[] { "A bird sings." }), "Its own cap's.");
            Assert.That(preview.ObservationSources[1].Kept, Is.EqualTo(new[] { "A cart waits." }));
            Assert.That(preview.ObservationSources[1].Dropped, Is.EqualTo(new[] { "The bell rings." }), "The agent's cap's.");
            Assert.That(preview.Context.Observations, Is.EqualTo(new[] { "The gate is open.", "A cart waits.", "It is raining." }));
        }

        [Test]
        public async Task ThePreviewShowsTheHistoryAndWhatTheMemoryLeftOut()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            agent.Memory = new RollingHistory(turns: 6, maxTokens: 30);
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower, and be quick about it, the light is going."));
            await Fixtures.NotCancelled(agent.DecideAsync("Go."));

            var preview = agent.Preview("Hello.");

            Assert.That(preview.History, Has.Count.EqualTo(2), "Only the short exchange fits the budget.");
            Assert.That(preview.HistoryTurnsDropped, Is.EqualTo(1));
            Assert.That(preview.History, Is.EqualTo(preview.Request.History));
        }

        [Test]
        public void ThePreviewsHistoryStaysAsItWasWhenTheMemoryChangesAfter()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            var memory = new AppendingMemory();
            agent.Memory = memory;

            var preview = agent.Preview("Hello.");
            memory.Remember(null, null, null);

            Assert.That(preview.History, Is.EqualTo(new[] { "Player: Halt.", "{}" }), "A copy, as the request's is.");
            Assert.That(preview.History, Is.EqualTo(preview.Request.History));
        }

        [Test]
        public void AnAgentWithNoMemoryPreviewsNoHistory()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            agent.Memory = null;

            var preview = agent.Preview("Hello.");

            Assert.That(preview.Memory, Is.Null);
            Assert.That(preview.History, Is.Null);
            Assert.That(preview.Stimulus, Is.EqualTo("Hello."));
        }

        private static Dictionary<string, string> Reasons(AgentPreview preview)
        {
            return preview.LeftOut.ToDictionary(left => left.Action.Id, left => left.Reason);
        }

        private static string Json(DecisionSchema schema)
        {
            return new JsonSchemaSerializer().Serialize(schema);
        }

        // A memory that counts what it is asked, and recalls a fixed history.
        private sealed class CountingMemory : IMemoryStrategy
        {
            public string[] History = Array.Empty<string>();
            public int Recalls { get; private set; }
            public int Remembers { get; private set; }

            public IReadOnlyList<string> Recall(AgentContext ctx)
            {
                Recalls++;
                return History;
            }

            public void Remember(AgentContext ctx, DecisionRequest request, DecisionResult result)
            {
                Remembers++;
            }
        }

        // A memory that hands out the very list it keeps, and adds to it on remembering, as a
        // strategy may.
        private sealed class AppendingMemory : IMemoryStrategy
        {
            private readonly List<string> kept = new List<string> { "Player: Halt.", "{}" };

            public IReadOnlyList<string> Recall(AgentContext ctx)
            {
                return kept;
            }

            public void Remember(AgentContext ctx, DecisionRequest request, DecisionResult result)
            {
                kept.Add("Player: Later.");
                kept.Add("{}");
            }
        }

        // A source that offers its first id and reports the rest as left out by its cap.
        private sealed class DroppingSource : ITargetSource, IReportsDroppedTargets
        {
            private readonly string offered;
            private readonly string[] drops;

            public DroppingSource(string offered, params string[] drops)
            {
                this.offered = offered;
                this.drops = drops;
            }

            public IReadOnlyList<string> Dropped { get; private set; } = Array.Empty<string>();

            public void Collect(AgentContext ctx, TargetRegistry into)
            {
                into.Offer(offered, new object());
                Dropped = (string[])drops.Clone();
            }
        }
    }
}
