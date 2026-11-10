using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Agenerela.Tests
{
    // DecideOptions.ObservationPlacement (#151): where a call's own observations go, against the
    // lines of the agent's observation sources. Off by default, and then byte-identical to the
    // request built before the option existed.
    public sealed class ObservationPlacementTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";

        // The request the village guard sent for this call before #151, written out by hand: the
        // source's lines, then the call's, under one heading, and the stimulus alone in its turn.
        private static readonly string BeforeTheOption = string.Join("\n",
            "What you know right now:",
            "- The gate is open.",
            "- A cart waits outside.",
            "- A wolf is at the tower",
            "- The keeper is shouting");

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        private static Agent Guard(FakeProvider fake)
        {
            var agent = Fixtures.VillageGuard(fake);
            agent.ObservationSources.Add(new FakeObservationSource("The gate is open.", "A cart waits outside."));
            return agent;
        }

        private static DecideOptions Call(ObservationPlacement? placement = null, string label = "Situation")
        {
            var options = new DecideOptions
            {
                StimulusLabel = label,
                Observations = new List<string> { "A wolf is at the tower", "The keeper is shouting" },
            };

            if (placement.HasValue)
            {
                options.ObservationPlacement = placement.Value;
            }

            return options;
        }

        [Test]
        public async Task TheDefaultIsByteIdenticalToTheRequestBeforeTheOption()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Guard(fake);

            await Fixtures.NotCancelled(agent.DecideAsync("Something is wrong up north.", Call()));
            var byDefault = fake.LastRequest;
            await Fixtures.NotCancelled(agent.DecideAsync("Something is wrong up north.", Call(ObservationPlacement.AfterSources)));
            var named = fake.LastRequest;

            Assert.That(new DecideOptions().ObservationPlacement, Is.EqualTo(ObservationPlacement.AfterSources));
            Assert.That(new PromptOptions().ObservationPlacement, Is.EqualTo(ObservationPlacement.AfterSources));
            Assert.That(default(ObservationPlacement), Is.EqualTo(ObservationPlacement.AfterSources), "An unset value means the default.");
            Assert.That(byDefault.Observations, Is.EqualTo(BeforeTheOption));
            Assert.That(byDefault.Stimulus, Is.EqualTo("Situation: \"Something is wrong up north.\""));
            Assert.That(named.Observations, Is.EqualTo(byDefault.Observations));
            Assert.That(named.Stimulus, Is.EqualTo(byDefault.Stimulus));
            Assert.That(named.SystemPrompt, Is.EqualTo(byDefault.SystemPrompt));
            Assert.That(named.FewShotBlock, Is.EqualTo(byDefault.FewShotBlock));
        }

        [Test]
        public void ACountWithTheDefaultPlacementChangesNothingInTheBuilder()
        {
            // What Agent now always passes, against what PromptBuilder was given before it.
            var ctx = Context(new[] { "The gate is open.", "A wolf is at the tower" });
            var schema = DecisionSchema.Build(ctx);

            var before = PromptBuilder.Build(ctx, schema, null, null, new PromptOptions());
            var now = PromptBuilder.Build(ctx, schema, null, null, new PromptOptions { CallObservationCount = 1 });

            Assert.That(now.Observations, Is.EqualTo(before.Observations));
            Assert.That(now.Stimulus, Is.EqualTo(before.Stimulus));
        }

        [Test]
        public async Task WithStimulusPutsTheCallsOwnLinesAfterTheQuote()
        {
            var fake = new FakeProvider(GoToTheTower);

            await Fixtures.NotCancelled(Guard(fake).DecideAsync("Something is wrong up north.", Call(ObservationPlacement.WithStimulus)));

            Assert.That(fake.LastRequest.Stimulus,
                Is.EqualTo("Situation: \"Something is wrong up north.\" A wolf is at the tower. The keeper is shouting."),
                "The stimulus stays whole inside its quote; each fact follows it as a sentence.");
            Assert.That(fake.LastRequest.Observations, Is.EqualTo(string.Join("\n",
                "What you know right now:",
                "- The gate is open.",
                "- A cart waits outside.")), "The sources' lines stay in the block.");
        }

        [Test]
        public void WithStimulusLeavesOutAnEmptyBlockAndTidiesOnlyWhatSharesTheLine()
        {
            // Every line is the call's own; one is blank, one breaks over two lines, one already ends a sentence.
            var ctx = Context(new[] { "A wolf is at the tower!", "  ", "The keeper\nis shouting" });
            var options = new PromptOptions { ObservationPlacement = ObservationPlacement.WithStimulus, CallObservationCount = 3 };

            var request = PromptBuilder.Build(ctx, DecisionSchema.Build(ctx), null, null, options);

            Assert.That(request.Observations, Is.Null);
            Assert.That(request.Stimulus, Is.EqualTo("Player: \"Something is wrong up north.\" A wolf is at the tower! The keeper is shouting."));
            Assert.That(request.StimulusText, Is.EqualTo("Something is wrong up north."));
        }

        [Test]
        public async Task ThePlacementNeverReadsTheLabel()
        {
            // DR-008: a "Player" call moves its lines exactly as a "Situation" call does.
            var fake = new FakeProvider(GoToTheTower);
            var agent = Guard(fake);

            await Fixtures.NotCancelled(agent.DecideAsync("Something is wrong up north.", Call(ObservationPlacement.WithStimulus, "Player")));
            var player = fake.LastRequest;
            await Fixtures.NotCancelled(agent.DecideAsync("Something is wrong up north.", Call(ObservationPlacement.WithStimulus, "Situation")));
            var situation = fake.LastRequest;

            Assert.That(player.Observations, Is.EqualTo(situation.Observations));
            Assert.That(player.Stimulus.Substring("Player".Length), Is.EqualTo(situation.Stimulus.Substring("Situation".Length)));
        }

        [Test]
        public async Task OnlyTheRenderingMovesNotWhatTheAgentKnows()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Guard(fake);

            var after = agent.Preview("Something is wrong up north.", Call());
            var with = agent.Preview("Something is wrong up north.", Call(ObservationPlacement.WithStimulus));
            await Fixtures.NotCancelled(agent.DecideAsync("Something is wrong up north.", Call(ObservationPlacement.WithStimulus)));

            var expected = new[] { "The gate is open.", "A cart waits outside.", "A wolf is at the tower", "The keeper is shouting" };
            Assert.That(after.Context.Observations, Is.EqualTo(expected));
            Assert.That(with.Context.Observations, Is.EqualTo(expected), "Guards, sources and handlers read the same list.");
            Assert.That(with.Request.Stimulus, Is.EqualTo(fake.LastRequest.Stimulus), "The preview shows what is sent.");
        }

        [Test]
        public void ANegativeCountIsAMistake()
        {
            var ctx = Context(new[] { "The gate is open." });

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PromptBuilder.Build(ctx, DecisionSchema.Build(ctx), null, null, new PromptOptions { CallObservationCount = -1 }));
        }

        [Test]
        public void TheCopyOfTheOptionsKeepsThePlacement()
        {
            var options = Call(ObservationPlacement.WithStimulus);

            Assert.That(options.Copy().ObservationPlacement, Is.EqualTo(ObservationPlacement.WithStimulus));
        }

        private static AgentContext Context(IReadOnlyList<string> observations)
        {
            var actions = new ActionRegistry();
            actions.Register(new ActionDefinition { Id = "move_to", Description = "Walk to a named place.", RequiresTarget = true },
                new DelegateActionHandler(c => true, (c, d) => { }));
            var targets = new TargetRegistry();
            targets.Register("tower", new object());
            return new AgentContext(Fixtures.VillageGuardIdentity(), "Something is wrong up north.", observations, null, targets, actions);
        }
    }
}
