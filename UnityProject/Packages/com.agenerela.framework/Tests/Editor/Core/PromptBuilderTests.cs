using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Agenerela.Providers;
using NUnit.Framework;

namespace Agenerela.Tests
{
    public sealed class PromptBuilderTests
    {
        // #16's golden file, as the issue writes it: the system side of the village guard's
        // request, composed the way DecisionRequest's remarks tell a provider to.
        private static readonly string GoldenSystemSide = string.Join("\n",
            "You are Village Guard, a guard at the town gate. Loyal, brief, a little suspicious of strangers.",
            "Your goals: keep the gate safe; help the player when asked.",
            "Choose exactly one action from the allowed list. Use no_target when none of the listed targets applies.",
            "",
            "Examples of correct decisions:",
            "Player: \"Accompany me on my rounds.\" -> action: follow_player, target: no_target",
            "Player: \"Proceed to the Tower.\" -> action: move_to, target: tower",
            "Player: \"Engage the TrainingDummy.\" -> action: attack_target, target: training_dummy",
            "Player: \"You seem well rested today.\" -> action: none, target: no_target",
            "Player: \"Proceed to the Atlantis.\" -> action: none, target: no_target (not an available target)");

        // The same file split where DecisionRequest splits it.
        private static readonly string GoldenSystemPrompt = string.Join("\n",
            "You are Village Guard, a guard at the town gate. Loyal, brief, a little suspicious of strangers.",
            "Your goals: keep the gate safe; help the player when asked.",
            "Choose exactly one action from the allowed list. Use no_target when none of the listed targets applies.");

        private static readonly string GoldenFewShotBlock = string.Join("\n",
            "Examples of correct decisions:",
            "Player: \"Accompany me on my rounds.\" -> action: follow_player, target: no_target",
            "Player: \"Proceed to the Tower.\" -> action: move_to, target: tower",
            "Player: \"Engage the TrainingDummy.\" -> action: attack_target, target: training_dummy",
            "Player: \"You seem well rested today.\" -> action: none, target: no_target",
            "Player: \"Proceed to the Atlantis.\" -> action: none, target: no_target (not an available target)");

        private static readonly string GoldenObservations = string.Join("\n",
            "What you know right now:",
            "- It is night",
            "- The gate is open");

        // #107's golden file: the village guard's system prompt with the target rule, the fallback
        // for a provider that reports no token probabilities. Nothing else in the request changes.
        private static readonly string GoldenSystemPromptWithTargetRule = string.Join("\n",
            "You are Village Guard, a guard at the town gate. Loyal, brief, a little suspicious of strangers.",
            "Your goals: keep the gate safe; help the player when asked.",
            "Choose exactly one action from the allowed list. Use no_target when none of the listed targets applies.",
            "Only these things are here: tower, training_dummy. If you are asked to act on anything else, " +
            "choose action none and target no_target, and say it is not here. Never act on a different thing instead.");

        private const string MoveToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        [Test]
        public void VillageGuardMatchesTheGoldenFile()
        {
            var ctx = Guard();
            var schema = DecisionSchema.Build(ctx);

            var request = Assemble(ctx, schema, GuardOptions());

            Assert.That(request.SystemPrompt, Is.EqualTo(GoldenSystemPrompt));
            Assert.That(request.FewShotBlock, Is.EqualTo(GoldenFewShotBlock));
            Assert.That(request.Observations, Is.Null);
            Assert.That(request.History, Is.Not.Null.And.Empty);
            Assert.That(request.Stimulus, Is.EqualTo("Player: \"Go to the tower.\""));
            Assert.That(request.Schema, Is.SameAs(schema));
            Assert.That(SystemSide(request), Is.EqualTo(GoldenSystemSide));
            Assert.That(SystemSide(request), Does.Not.Contain("Go to the tower."),
                "The stimulus is the final user turn, never appended to the system text.");
        }

        [Test]
        public void VillageGuardWithObservationsMatchesTheGoldenFile()
        {
            var request = Assemble(Guard("Go to the tower.", "It is night", "The gate is open"), GuardOptions());

            Assert.That(request.Observations, Is.EqualTo(GoldenObservations));
            Assert.That(SystemSide(request), Is.EqualTo(GoldenSystemSide + "\n\n" + GoldenObservations),
                "Observations come after the examples, the last thing read before the stimulus.");
            Assert.That(request.SystemPrompt, Is.EqualTo(GoldenSystemPrompt));
            Assert.That(request.Stimulus, Is.EqualTo("Player: \"Go to the tower.\""));
        }

        [Test]
        public void StimulusLabelChangesTheExamplesAndTheStimulusTurn()
        {
            var options = new PromptOptions { StimulusLabel = "Advisor", IdleExampleStimulus = "You seem well rested today." };

            var request = Assemble(Guard(), options);
            var examples = request.FewShotBlock.Split('\n').Skip(1).ToList();

            Assert.That(examples, Has.Count.EqualTo(5));
            Assert.That(examples, Has.All.StartWith("Advisor: \""));
            Assert.That(request.Stimulus, Is.EqualTo("Advisor: \"Go to the tower.\""));
            Assert.That(request.FewShotBlock + request.Stimulus, Does.Not.Contain("Player:"));
        }

        [Test]
        public void ObservationsAppearVerbatimOnePerLine()
        {
            // Wording a builder might be tempted to tidy: no full stop, capitals, an id, a
            // number, quotes and leading spaces. All of it goes in as written.
            var observations = new[]
            {
                "it is NIGHT",
                "training_dummy is 12 m away",
                "The merchant said \"the bridge is out.\"",
                "  The gate is open.",
            };

            var request = Assemble(Guard("Go to the tower.", observations), GuardOptions());

            Assert.That(request.Observations, Is.EqualTo(string.Join("\n",
                "What you know right now:",
                "- it is NIGHT",
                "- training_dummy is 12 m away",
                "- The merchant said \"the bridge is out.\"",
                "-   The gate is open.")));
        }

        [Test]
        public void ObservationsBlockIsAbsentWhenThereAreNone()
        {
            var none = Assemble(Guard("Go to the tower."), GuardOptions());
            var nullList = Assemble(GuardWithObservations(null), GuardOptions());
            var onlyBlank = Assemble(Guard("Go to the tower.", "", "   ", null), GuardOptions());

            foreach (var request in new[] { none, nullList, onlyBlank })
            {
                Assert.That(request.Observations, Is.Null);
                Assert.That(SystemSide(request), Is.EqualTo(GoldenSystemSide));
                Assert.That(SystemSide(request), Does.Not.Contain("What you know right now:"));
            }
        }

        [Test]
        public void BlankObservationsAreSkipped()
        {
            var request = Assemble(Guard("Go to the tower.", "It is night", "", "  ", null, "The gate is open"), GuardOptions());

            Assert.That(request.Observations, Is.EqualTo(GoldenObservations));
        }

        [Test]
        public void SameInputTwiceGivesIdenticalOutput()
        {
            // Two contexts built separately, so nothing is shared between the two calls.
            var first = Assemble(Guard("Go to the tower.", "It is night", "The gate is open"), GuardOptions(),
                                 new[] { "earlier turn" });
            var second = Assemble(Guard("Go to the tower.", "It is night", "The gate is open"), GuardOptions(),
                                  new[] { "earlier turn" });

            Assert.That(second.SystemPrompt, Is.EqualTo(first.SystemPrompt));
            Assert.That(second.FewShotBlock, Is.EqualTo(first.FewShotBlock));
            Assert.That(second.Observations, Is.EqualTo(first.Observations));
            Assert.That(second.History, Is.EqualTo(first.History));
            Assert.That(second.Stimulus, Is.EqualTo(first.Stimulus));
            Assert.That(SystemSide(second), Is.EqualTo(SystemSide(first)));

            foreach (var text in new[] { first.SystemPrompt, first.Observations, first.Stimulus })
            {
                Assert.That(text, Does.Not.Contain("\r"), "Line endings are \\n on every platform.");
            }
        }

        [Test]
        public void GoalsLineIsOmittedWhenGoalsAreEmpty()
        {
            foreach (var goals in new[] { null, "", "   " })
            {
                var identity = GuardIdentity();
                identity.Goals = goals;

                var request = Build(identity);

                Assert.That(request.SystemPrompt, Is.EqualTo(string.Join("\n",
                    "You are Village Guard, a guard at the town gate. Loyal, brief, a little suspicious of strangers.",
                    "Choose exactly one action from the allowed list. Use no_target when none of the listed targets applies.")));
            }
        }

        [TestCase("Village Guard", "", "", "You are Village Guard.")]
        [TestCase("Village Guard", "a guard at the town gate", null, "You are Village Guard, a guard at the town gate.")]
        [TestCase("", "a guard at the town gate", "Loyal.", "You are a guard at the town gate. Loyal.")]
        [TestCase(null, null, "Loyal, brief.", "Loyal, brief.")]
        [TestCase("  Village Guard ", " a guard at the town gate. ", " Loyal. ", "You are Village Guard, a guard at the town gate. Loyal.")]
        public void IdentityLineLeavesOutEmptyPartsAndKeepsTheRestAsWritten(string name, string role, string personality, string expected)
        {
            var request = Build(new AgentIdentity { Name = name, Role = role, Personality = personality });

            Assert.That(request.SystemPrompt.Split('\n')[0], Is.EqualTo(expected));
        }

        [Test]
        public void AnEmptyIdentityLeavesOnlyTheInstructionLine()
        {
            var request = Build(new AgentIdentity());

            Assert.That(request.SystemPrompt,
                Is.EqualTo("Choose exactly one action from the allowed list. Use no_target when none of the listed targets applies."));
        }

        [Test]
        public void CountryReadingAReportNamesNoPlayerOrCharacter()
        {
            // DR-008: no speaking character. The framework's own words must not assume one, and
            // the label is the developer's.
            var targets = new TargetRegistry();
            targets.Register("eastmarch", new object());
            var actions = new ActionRegistry();
            actions.Register(Action("ration_grain", "Grain stores are falling.", false), Always());
            actions.Register(Action("declare_war", "{0} massed troops on the border.", true, "eastmarch"), Always());

            var ctx = new AgentContext(
                new AgentIdentity { Name = "Northmark", Role = "a small kingdom", Goals = "survive the winter" },
                "Grain stores fell 12% this winter.",
                new[] { "eastmarch has 3 armies" },
                null,
                targets,
                actions);

            var request = Assemble(ctx, new PromptOptions { StimulusLabel = "Report" });

            Assert.That(request.SystemPrompt, Is.EqualTo(string.Join("\n",
                "You are Northmark, a small kingdom.",
                "Your goals: survive the winter.",
                "Choose exactly one action from the allowed list. Use no_target when none of the listed targets applies.")));
            Assert.That(request.Stimulus, Is.EqualTo("Report: \"Grain stores fell 12% this winter.\""));

            string everything = (SystemSide(request) + "\n" + request.Stimulus).ToLowerInvariant();
            Assert.That(everything, Does.Not.Contain("player"));
            Assert.That(everything, Does.Not.Contain("character"));
        }

        [Test]
        public void HistoryIsNeverNullAndIsCopied()
        {
            var turns = new List<string> { "first", "second" };

            var withNone = Assemble(Guard(), GuardOptions(), history: null);
            var withTurns = Assemble(Guard(), GuardOptions(), turns);
            turns.Add("third");

            Assert.That(withNone.History, Is.Not.Null.And.Empty);
            Assert.That(withTurns.History, Is.EqualTo(new[] { "first", "second" }),
                "A request is a snapshot: the caller's list changing afterwards must not change it.");
        }

        [Test]
        public void SwappingTheFewShotBlockChangesNothingElse()
        {
            // Why the block is passed in: an A/B run swaps or drops it alone (DecisionRequest).
            var ctx = Guard("Go to the tower.", "It is night");
            var schema = DecisionSchema.Build(ctx);

            var withExamples = PromptBuilder.Build(ctx, schema, GoldenFewShotBlock, null, GuardOptions());
            var without = PromptBuilder.Build(ctx, schema, null, null, GuardOptions());

            Assert.That(withExamples.FewShotBlock, Is.EqualTo(GoldenFewShotBlock));
            Assert.That(without.FewShotBlock, Is.Null);
            Assert.That(without.SystemPrompt, Is.EqualTo(withExamples.SystemPrompt));
            Assert.That(without.Observations, Is.EqualTo(withExamples.Observations));
            Assert.That(without.Stimulus, Is.EqualTo(withExamples.Stimulus));
            Assert.That(SystemSide(without), Is.EqualTo(GoldenSystemPrompt + "\n\n" + "What you know right now:\n- It is night"));
        }

        [Test]
        public void NullOptionsUseThePlayerLabel()
        {
            var ctx = Guard();

            var request = PromptBuilder.Build(ctx, DecisionSchema.Build(ctx), null);

            Assert.That(request.Stimulus, Is.EqualTo("Player: \"Go to the tower.\""));
        }

        [Test]
        public void BuildRejectsNullContextOrSchema()
        {
            var ctx = Guard();
            var schema = DecisionSchema.Build(ctx);

            Assert.Throws<ArgumentNullException>(() => PromptBuilder.Build(null, schema, null));
            Assert.Throws<ArgumentNullException>(() => PromptBuilder.Build(ctx, null, null));
        }

        [Test]
        public void BuildRejectsAnEmptyStimulusLabel()
        {
            var ctx = Guard();
            var schema = DecisionSchema.Build(ctx);

            foreach (var label in new[] { null, "", "  " })
            {
                Assert.Throws<ArgumentException>(
                    () => PromptBuilder.Build(ctx, schema, null, null, new PromptOptions { StimulusLabel = label }));
            }
        }

        [Test]
        public void VillageGuardWithTheTargetRuleMatchesItsGoldenFile()
        {
            var ctx = Guard("Go to the tower.", "It is night", "The gate is open");
            var schema = DecisionSchema.Build(ctx);
            var options = GuardOptions();
            options.TargetRule = true;

            var request = Assemble(ctx, schema, options);

            Assert.That(request.SystemPrompt, Is.EqualTo(GoldenSystemPromptWithTargetRule));
            Assert.That(PromptBuilder.SendsTargetRule(schema, options), Is.True);
            Assert.That(request.FewShotBlock, Is.EqualTo(GoldenFewShotBlock), "The rule is one line of the system prompt, nothing more.");
            Assert.That(request.Observations, Is.EqualTo(GoldenObservations));
            Assert.That(request.Stimulus, Is.EqualTo("Player: \"Go to the tower.\""));
        }

        [Test]
        public void TheTargetRuleIsOffByDefault()
        {
            var ctx = Guard();
            var schema = DecisionSchema.Build(ctx);

            Assert.That(new PromptOptions().TargetRule, Is.False);
            Assert.That(PromptBuilder.SendsTargetRule(schema, new PromptOptions()), Is.False);
            Assert.That(PromptBuilder.SendsTargetRule(schema, null), Is.False);
            Assert.That(PromptBuilder.Build(ctx, schema, null).SystemPrompt, Is.EqualTo(GoldenSystemPrompt));
        }

        [Test]
        public void TheTargetRuleNamesTheSchemasTargetsInItsOrderWithoutNoTarget()
        {
            var targets = new TargetRegistry();
            targets.Register("well", new object());
            targets.Register("bridge", new object());
            targets.Register("training_dummy", new object());
            var ctx = new AgentContext(GuardIdentity(), "Go to the well.", null, null, targets, new ActionRegistry());
            var schema = DecisionSchema.Build(ctx);

            var request = PromptBuilder.Build(ctx, schema, null, null, new PromptOptions { TargetRule = true });
            var lines = request.SystemPrompt.Split('\n');

            Assert.That(schema.Fields[1].AllowedValues, Is.EqualTo(new[] { "no_target", "well", "bridge", "training_dummy" }));
            Assert.That(lines[lines.Length - 2], Does.StartWith("Choose exactly one action"),
                "The rule comes right after the fixed instruction line.");
            Assert.That(lines[lines.Length - 1], Does.StartWith("Only these things are here: well, bridge, training_dummy. If you"));
        }

        [Test]
        public void WithNoTargetFieldTheTargetRuleAddsNoLine()
        {
            // Nothing registered, so the schema has no target field and there is nothing to ground.
            var ctx = new AgentContext(GuardIdentity(), "Go to the tower.", null, null, new TargetRegistry(), new ActionRegistry());
            var schema = DecisionSchema.Build(ctx);
            var options = new PromptOptions { TargetRule = true };

            var request = PromptBuilder.Build(ctx, schema, null, null, options);

            Assert.That(schema.Fields.Select(f => f.Name), Does.Not.Contain(DecisionSchema.TargetFieldName));
            Assert.That(request.SystemPrompt, Is.EqualTo(GoldenSystemPrompt));
            Assert.That(PromptBuilder.SendsTargetRule(schema, options), Is.False);
        }

        [Test]
        public void TheTargetRuleNamesNoPlayerOrCharacter()
        {
            // DR-008, as for the country above: the probe's rule said "the player", and this one must not.
            var targets = new TargetRegistry();
            targets.Register("eastmarch", new object());
            var ctx = new AgentContext(new AgentIdentity { Name = "Northmark" }, "Grain stores fell.", null, null,
                                       targets, new ActionRegistry());

            var request = PromptBuilder.Build(ctx, DecisionSchema.Build(ctx), null, null,
                                              new PromptOptions { StimulusLabel = "Report", TargetRule = true });

            string rule = request.SystemPrompt.Split('\n').Last().ToLowerInvariant();
            Assert.That(rule, Does.StartWith("only these things are here: eastmarch."));
            Assert.That(rule, Does.Not.Contain("player"));
            Assert.That(rule, Does.Not.Contain("character"));
        }

        [Test]
        public async Task AnAgentSendsTheTargetRuleToAProviderWithoutProbabilities()
        {
            var fake = new FakeProvider(MoveToTheTower) { Capabilities = WithoutProbabilities() };
            var agent = Fixtures.VillageGuard(fake);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(fake.LastRequest.SystemPrompt, Is.EqualTo(GoldenSystemPromptWithTargetRule));
            Assert.That(result.Telemetry.TargetRuleSent, Is.True);
            Assert.That(result.Telemetry.TargetGroundingStatus, Is.EqualTo(GroundingStatus.NoProbabilities),
                "The guard still runs, and records that it could not read the reply.");
            Assert.That(result.Decision.ActionId, Is.EqualTo("move_to"));
        }

        [Test]
        public async Task AnAgentSendsTheTargetRuleWhenTheProviderStatesNoCapabilities()
        {
            // A provider that states nothing claims no probabilities either.
            var fake = new FakeProvider(MoveToTheTower) { Capabilities = null };
            var agent = Fixtures.VillageGuard(fake);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(fake.LastRequest.SystemPrompt, Is.EqualTo(GoldenSystemPromptWithTargetRule));
            Assert.That(result.Telemetry.TargetRuleSent, Is.True);
        }

        [Test]
        public async Task CheckTargetOffSendsNoTargetRule()
        {
            var fake = new FakeProvider(MoveToTheTower) { Capabilities = WithoutProbabilities() };
            var agent = Fixtures.VillageGuard(fake);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.", new DecideOptions { CheckTarget = false }));

            Assert.That(fake.LastRequest.SystemPrompt, Is.EqualTo(GoldenSystemPrompt));
            Assert.That(result.Telemetry.TargetRuleSent, Is.False);
            Assert.That(result.Telemetry.TargetGroundingStatus, Is.EqualTo(GroundingStatus.Off));
        }

        [Test]
        public async Task AnAgentWithNoTargetsSendsNoTargetRule()
        {
            var fake = new FakeProvider("{\"action\":\"none\",\"statement\":\"All quiet.\"}") { Capabilities = WithoutProbabilities() };
            var agent = Fixtures.VillageGuard(fake);
            agent.Targets.Unregister("tower");
            agent.Targets.Unregister("training_dummy");

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Anything to report?"));

            Assert.That(fake.LastRequest.Schema.Fields.Select(f => f.Name), Does.Not.Contain(DecisionSchema.TargetFieldName));
            Assert.That(fake.LastRequest.SystemPrompt, Does.Not.Contain("Only these things are here"));
            Assert.That(result.Telemetry.TargetRuleSent, Is.False);
            Assert.That(result.Telemetry.Outcome, Is.Null, "The decision itself went through.");
        }

        [Test]
        public async Task AProviderThatClaimsProbabilitiesAndReturnsNoneGetsNeitherTheRuleNorTheGuard()
        {
            // The gap ProviderCapabilities.ReportsTokenProbabilities documents, pinned: the flag is read
            // before the request and the probabilities after it, so this decision is left unprotected.
            var fake = new FakeProvider(MoveToTheTower);
            Assert.That(fake.Capabilities.ReportsTokenProbabilities, Is.True);
            Assert.That(fake.TokenProbabilities, Is.Null);
            var agent = Fixtures.VillageGuard(fake);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(fake.LastRequest.SystemPrompt, Is.EqualTo(GoldenSystemPrompt), "No rule: byte for byte the prompt Ollama is sent.");
            Assert.That(result.Telemetry.TargetRuleSent, Is.False);
            Assert.That(result.Telemetry.TargetGroundingStatus, Is.EqualTo(GroundingStatus.NoProbabilities), "No guard either.");
            Assert.That(result.Telemetry.TargetGrounding, Is.Null);
        }

        private static ProviderCapabilities WithoutProbabilities()
        {
            return new ProviderCapabilities
            {
                SupportsConstrainedDecoding = true,
                Dialect = SchemaDialect.JsonSchema,
                ReportsTokenProbabilities = false,
            };
        }

        // #17's pipeline steps 2 and 3, the way Agent will call them: the schema and the few-shot
        // block from the same context, the block built with the same label and idle stimulus.
        private static DecisionRequest Assemble(AgentContext ctx, PromptOptions options, IReadOnlyList<string> history = null)
        {
            return Assemble(ctx, DecisionSchema.Build(ctx), options, history);
        }

        private static DecisionRequest Assemble(AgentContext ctx, DecisionSchema schema, PromptOptions options,
                                                IReadOnlyList<string> history = null)
        {
            string fewShot = FewShotBuilder.Build(ActionAvailability.For(ctx), ctx.Targets,
                                                  options.StimulusLabel, options.IdleExampleStimulus);

            return PromptBuilder.Build(ctx, schema, fewShot, history, options);
        }

        // DecisionRequest's remarks: the non-empty system-side blocks, in order, joined by a
        // blank line.
        private static string SystemSide(DecisionRequest request)
        {
            var blocks = new[] { request.SystemPrompt, request.FewShotBlock, request.Observations };
            return string.Join("\n\n", blocks.Where(block => !string.IsNullOrEmpty(block)));
        }

        private static DecisionRequest Build(AgentIdentity identity)
        {
            var ctx = new AgentContext(identity, "Go to the tower.", null, null, new TargetRegistry(), new ActionRegistry());
            return PromptBuilder.Build(ctx, DecisionSchema.Build(ctx), null);
        }

        private static PromptOptions GuardOptions()
        {
            return new PromptOptions { IdleExampleStimulus = "You seem well rested today." };
        }

        // The village guard of #16's golden file: #18's profile, with the goals written as the
        // golden shows them. Shared with the agent's tests (#17), so the two cannot drift apart.
        private static AgentIdentity GuardIdentity()
        {
            return Fixtures.VillageGuardIdentity();
        }

        private static AgentContext Guard(string stimulus = "Go to the tower.", params string[] observations)
        {
            return GuardWith(stimulus, observations);
        }

        private static AgentContext GuardWithObservations(IReadOnlyList<string> observations)
        {
            return GuardWith("Go to the tower.", observations);
        }

        private static AgentContext GuardWith(string stimulus, IReadOnlyList<string> observations)
        {
            var actions = new ActionRegistry();
            actions.Register(Action("follow_player", "Accompany me on my rounds.", false), Always());
            actions.Register(Action("move_to", "Proceed to the {0}.", true, "tower", "bridge"), Always());
            actions.Register(Action("attack_target", "Engage the {0}.", true, "training_dummy"), Always());

            var targets = new TargetRegistry();
            targets.Register("tower", new object());
            targets.Register("training_dummy", new object());

            return new AgentContext(GuardIdentity(), stimulus, observations, null, targets, actions);
        }

        private static ActionDefinition Action(string id, string stimulus, bool requiresTarget,
                                               params string[] preferredTargets)
        {
            return new ActionDefinition
            {
                Id = id,
                Description = $"Action {id}",
                ExampleStimulus = stimulus,
                RequiresTarget = requiresTarget,
                PreferredExampleTargets = preferredTargets,
            };
        }

        private static IActionHandler Always()
        {
            return new DelegateActionHandler(ctx => true, (ctx, decision) => { });
        }
    }
}
