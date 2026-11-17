using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // #190 workstream B (DR-020): targets carry a category, an action names the categories it takes
    // and TargetCategoryGuard holds it to them, an agent names the only categories it is offered,
    // the same object from two sources is one target, and RegisterMethods can take only some of an
    // owner's actions. Every await of DecideAsync goes through Fixtures.NotCancelled (see AgentTests).
    public sealed class TargetCategoryTests
    {
        private const string CompleteAtTheGate = "{\"action\":\"complete_quest\",\"target\":\"village_gate\",\"statement\":\"Done.\"}";
        private const string CompleteTheQuest = "{\"action\":\"complete_quest\",\"target\":\"quest_wolf\",\"statement\":\"Done.\"}";
        private const string GoToTheGate = "{\"action\":\"move_to\",\"target\":\"village_gate\",\"statement\":\"On my way.\"}";

        // ---- TargetRegistry ------------------------------------------------------------------

        [Test]
        public void ATargetRegisteredInACategoryReportsIt()
        {
            var registry = new TargetRegistry();
            registry.Register("quest_wolf", new object(), "quest");
            registry.Register("bridge", new object());

            Assert.That(registry.CategoryOf("quest_wolf"), Is.EqualTo("quest"));
            Assert.That(registry.CategoryOf("bridge"), Is.Null, "Registered without one.");
            Assert.That(registry.CategoryOf("tower"), Is.Null, "Not registered.");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(TargetRegistry.NoTarget)]
        public void CategoryOfNeverThrows(string id)
        {
            var registry = new TargetRegistry();

            Assert.That(registry.CategoryOf(id), Is.Null);
        }

        [TestCase("")]
        [TestCase("   ")]
        public void ABlankCategoryIsNone(string category)
        {
            var registry = new TargetRegistry();
            registry.Register("bridge", new object(), category);

            Assert.That(registry.CategoryOf("bridge"), Is.Null);
        }

        [Test]
        public void ACategoryIsKeptAsGiven()
        {
            var registry = new TargetRegistry();
            registry.Register("bridge", new object(), "Place ");

            Assert.That(registry.CategoryOf("bridge"), Is.EqualTo("Place "), "Compared ordinally, like Targetable.Category.");
        }

        [Test]
        public void UnregisteringATargetForgetsItsCategory()
        {
            var registry = new TargetRegistry();
            registry.Register("quest_wolf", new object(), "quest");
            registry.Unregister("quest_wolf");
            registry.Register("quest_wolf", new object());

            Assert.That(registry.CategoryOf("quest_wolf"), Is.Null);
        }

        [Test]
        public void RegisteringInACategoryRefusesWhatRegisterRefuses()
        {
            var registry = new TargetRegistry();
            registry.Register("bridge", new object(), "place");

            Assert.Throws<ArgumentException>(() => registry.Register("bridge", new object(), "place"));
            Assert.Throws<ArgumentException>(() => registry.Register(TargetRegistry.NoTarget, new object(), "place"));
            Assert.Throws<ArgumentNullException>(() => registry.Register("tower", null, "place"));
            Assert.That(registry.Count, Is.EqualTo(1));
        }

        [Test]
        public void OfferingTheSameObjectAgainKeepsTheFirstOffer()
        {
            var registry = new TargetRegistry();
            var guard = new object();

            Assert.That(registry.Offer("guard_wren", guard, "person"), Is.True);
            Assert.That(registry.Offer("guard_wren", guard, "guard"), Is.False);

            Assert.That(registry.Ids, Is.EqualTo(new[] { "guard_wren" }));
            Assert.That(registry.CategoryOf("guard_wren"), Is.EqualTo("person"), "The first offer's category is kept.");
        }

        [Test]
        public void OfferingAnotherObjectUnderARegisteredIdIsAnError()
        {
            var registry = new TargetRegistry();
            var first = new object();
            registry.Register("guard_wren", first);

            var error = Assert.Throws<ArgumentException>(() => registry.Offer("guard_wren", new object()));

            Assert.That(error.Message, Does.Contain("'guard_wren'"));
            Assert.That(error.Message, Does.Contain("another object"));
            Assert.That(registry.TryGet("guard_wren", out var kept) && kept == first, Is.True);
        }

        [Test]
        public void OfferRefusesWhatRegisterRefuses()
        {
            var registry = new TargetRegistry();

            Assert.Throws<ArgumentException>(() => registry.Offer("", new object()));
            Assert.Throws<ArgumentException>(() => registry.Offer(TargetRegistry.NoTarget, new object()));
            Assert.Throws<ArgumentNullException>(() => registry.Offer("tower", null));
            Assert.That(registry.Count, Is.EqualTo(0));
        }

        // ---- ActionDefinition ----------------------------------------------------------------

        [Test]
        public void AnActionThatNamesNoCategoryTakesAnyTarget()
        {
            foreach (var categories in new[] { null, new string[0], new[] { "", "  " } })
            {
                var action = new ActionDefinition { Id = "move_to", TargetCategories = categories };

                Assert.That(action.DeclaresTargetCategories, Is.False);
                Assert.That(action.AcceptsCategory("place"), Is.True);
                Assert.That(action.AcceptsCategory(null), Is.True);
            }
        }

        [Test]
        public void AnActionThatNamesCategoriesTakesOnlyThose()
        {
            var action = new ActionDefinition { Id = "complete_quest", TargetCategories = new[] { "quest", " ", "errand" } };

            Assert.That(action.DeclaresTargetCategories, Is.True);
            Assert.That(action.AcceptsCategory("quest"), Is.True);
            Assert.That(action.AcceptsCategory("errand"), Is.True);
            Assert.That(action.AcceptsCategory("place"), Is.False);
            Assert.That(action.AcceptsCategory("Quest"), Is.False, "Ordinal.");
            Assert.That(action.AcceptsCategory(null), Is.False, "A target with no category.");
            Assert.That(action.AcceptsCategory(" "), Is.False, "A blank category is none.");
        }

        public sealed class QuestGiver
        {
            [AgentAction("complete_quest", "Take what the player brought.", RequiresTarget = true, TargetCategories = new[] { "quest" })]
            private void Complete(object quest) { }

            [AgentAction("wave", "Wave.")]
            private void Wave() { }
        }

        [Test]
        public void TheAttributeCarriesItsCategoriesToTheDefinition()
        {
            var registry = new ActionRegistry();
            registry.RegisterMethods(new QuestGiver());

            registry.TryGet("complete_quest", out var complete, out _);
            registry.TryGet("wave", out var wave, out _);

            Assert.That(complete.TargetCategories, Is.EqualTo(new[] { "quest" }));
            Assert.That(wave.TargetCategories, Is.Empty);
            Assert.That(wave.DeclaresTargetCategories, Is.False);
        }

        // ---- TargetCategoryGuard -------------------------------------------------------------

        [Test]
        public void TheGuardPassesAnActionThatNamesNoCategory()
        {
            var agent = QuestAgent(new FakeProvider());

            Assert.That(Inspect(agent, "move_to", "quest_wolf").Kind, Is.EqualTo(GuardVerdictKind.Pass));
            Assert.That(Inspect(agent, "move_to", "village_gate").Kind, Is.EqualTo(GuardVerdictKind.Pass));
        }

        [Test]
        public void TheGuardPassesATargetOfACategoryTheActionTakes()
        {
            var agent = QuestAgent(new FakeProvider());

            Assert.That(Inspect(agent, "complete_quest", "quest_wolf").Kind, Is.EqualTo(GuardVerdictKind.Pass));
        }

        [Test]
        public void TheGuardPassesNoneAndNoTarget()
        {
            var agent = QuestAgent(new FakeProvider());

            Assert.That(Inspect(agent, ActionRegistry.None, "village_gate").Kind, Is.EqualTo(GuardVerdictKind.Pass));
            Assert.That(Inspect(agent, "complete_quest", TargetRegistry.NoTarget).Kind, Is.EqualTo(GuardVerdictKind.Pass),
                "No target is SchemaLegalityGuard's to refuse.");
        }

        [Test]
        public void TheGuardTurnsATargetOfAnotherCategoryIntoNone()
        {
            var agent = QuestAgent(new FakeProvider());

            var verdict = Inspect(agent, "complete_quest", "village_gate");

            Assert.That(verdict.Kind, Is.EqualTo(GuardVerdictKind.RewriteToNone));
            Assert.That(verdict.Reason, Is.EqualTo(
                "'complete_quest' takes only targets in the category 'quest', and 'village_gate' is in the category 'place'."));
        }

        [Test]
        public void TheGuardTurnsATargetWithNoCategoryIntoNone()
        {
            var agent = QuestAgent(new FakeProvider());
            agent.Targets.Register("odd_stone", new object());

            var verdict = Inspect(agent, "complete_quest", "odd_stone");

            Assert.That(verdict.Kind, Is.EqualTo(GuardVerdictKind.RewriteToNone));
            Assert.That(verdict.Reason, Does.EndWith("'odd_stone' has no category."));
        }

        [Test]
        public void TheGuardNamesEveryCategoryTheActionTakes()
        {
            var agent = QuestAgent(new FakeProvider());
            agent.Actions.TryGet("complete_quest", out var complete, out _);
            complete.TargetCategories = new[] { "quest", "errand" };

            var verdict = Inspect(agent, "complete_quest", "village_gate");

            Assert.That(verdict.Reason, Does.StartWith("'complete_quest' takes only targets in the categories 'quest', 'errand',"));
        }

        [Test]
        public async Task DecidingAQuestActionOnAPlaceEndsInNone()
        {
            var agent = QuestAgent(new FakeProvider(CompleteAtTheGate));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Here is the wolf's pelt."));

            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
            Assert.That(result.Decision.TargetId, Is.EqualTo(TargetRegistry.NoTarget));
            Assert.That(result.ModelDecision.TargetId, Is.EqualTo("village_gate"));
            Assert.That(result.Verdict.Value.Guard, Is.EqualTo("TargetCategoryGuard"));
            Assert.That(result.Telemetry.GuardsFired, Is.EqualTo(new[] { "TargetCategoryGuard" }));
            Assert.That(result.Telemetry.Verdict, Is.EqualTo(GuardVerdictKind.RewriteToNone));

            // A contained refusal: nothing ran, and Execute reports none done.
            Assert.That(agent.Execute(result), Is.True);
            Assert.That(Fixtures.Handler(agent, "complete_quest").Executions, Is.Empty);
        }

        [Test]
        public async Task TheGuardRunsAfterGrounding()
        {
            var agent = QuestAgent(new FakeProvider(CompleteAtTheGate));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Here is the wolf's pelt."));

            // Grounding ran first and recorded its reading; the fake reports no probabilities.
            Assert.That(result.Telemetry.TargetGroundingStatus, Is.EqualTo(GroundingStatus.NoProbabilities));
        }

        [Test]
        public async Task ADeveloperGuardNeverSeesWhatTheCategoryGuardStopped()
        {
            var agent = QuestAgent(new FakeProvider(CompleteAtTheGate));
            bool ran = false;
            agent.Guards.Add(new DelegateGuard("mine", _ => { ran = true; return GuardVerdict.Pass; }));

            await Fixtures.NotCancelled(agent.DecideAsync("Here is the wolf's pelt."));

            Assert.That(ran, Is.False);
        }

        [Test]
        public async Task AMatchingPairIsCarriedOut()
        {
            var agent = QuestAgent(new FakeProvider(CompleteTheQuest));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Here is the wolf's pelt."));

            Assert.That(result.Verdict, Is.Null);
            Assert.That(agent.Execute(result), Is.True);
            Assert.That(Fixtures.Handler(agent, "complete_quest").Executions, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task ExecuteRefusesATargetWhoseCategoryChangedSinceDeciding()
        {
            var agent = QuestAgent(new FakeProvider(CompleteTheQuest));
            var result = await Fixtures.NotCancelled(agent.DecideAsync("Here is the wolf's pelt."));

            // The same object, now registered as something else.
            agent.Targets.TryGet("quest_wolf", out var quest);
            agent.Targets.Unregister("quest_wolf");
            agent.Targets.Register("quest_wolf", quest, "trophy");

            LogAssert.Expect(LogType.Warning, new Regex("takes only targets in the category 'quest'"));
            Assert.That(agent.Execute(result, out var outcome), Is.False);

            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatus.Refused));
            Assert.That(result.Telemetry.GuardsFired, Does.Contain("TargetCategoryGuard@Execute"));
            Assert.That(Fixtures.Handler(agent, "complete_quest").Executions, Is.Empty);
        }

        [Test]
        public async Task DeclaringCategoriesChangesNothingTheModelIsSent()
        {
            var plainProvider = new FakeProvider(GoToTheGate);
            var declaredProvider = new FakeProvider(GoToTheGate);
            var plain = QuestAgent(plainProvider, declare: false);
            var declared = QuestAgent(declaredProvider, declare: true);

            await Fixtures.NotCancelled(plain.DecideAsync("Walk to the gate."));
            await Fixtures.NotCancelled(declared.DecideAsync("Walk to the gate."));

            var a = plainProvider.LastRequest;
            var b = declaredProvider.LastRequest;
            var json = new JsonSchemaSerializer();
            Assert.That(json.Serialize(b.Schema), Is.EqualTo(json.Serialize(a.Schema)));
            Assert.That(b.SystemPrompt, Is.EqualTo(a.SystemPrompt));
            Assert.That(b.FewShotBlock, Is.EqualTo(a.FewShotBlock));
        }

        // ---- Agent.TargetCategories ----------------------------------------------------------

        [Test]
        public async Task AnAgentWithNoCategoriesListedIsOfferedEveryTarget()
        {
            var provider = new FakeProvider(GoToTheGate);
            var agent = QuestAgent(provider);
            agent.Targets.Register("odd_stone", new object());

            await Fixtures.NotCancelled(agent.DecideAsync("Walk to the gate."));

            Assert.That(TargetEnum(provider), Is.EqualTo(new[] { TargetRegistry.NoTarget, "village_gate", "quest_wolf", "odd_stone" }));
        }

        [Test]
        public async Task AnAgentIsOfferedOnlyTheCategoriesItLists()
        {
            var provider = new FakeProvider(GoToTheGate);
            var agent = QuestAgent(provider);
            agent.Targets.Register("odd_stone", new object());
            agent.TargetSources.Add(new ExplicitTargetSource().Add("bread", new object(), "ware").Add("well", new object(), "place"));
            agent.TargetCategories.Add("place");
            agent.TargetCategories.Add(" ");

            await Fixtures.NotCancelled(agent.DecideAsync("Walk to the gate."));

            // The quest, the ware and the stone with no category are never offered, whichever source had them.
            Assert.That(TargetEnum(provider), Is.EqualTo(new[] { TargetRegistry.NoTarget, "village_gate", "well" }));
        }

        [Test]
        public async Task ExecuteRefusesATargetTheAgentMayNoLongerBeOffered()
        {
            var agent = QuestAgent(new FakeProvider(CompleteTheQuest));
            var result = await Fixtures.NotCancelled(agent.DecideAsync("Here is the wolf's pelt."));
            agent.TargetCategories.Add("place");

            LogAssert.Expect(LogType.Warning, new Regex("the target is not registered"));
            Assert.That(agent.Execute(result), Is.False);
            Assert.That(Fixtures.Handler(agent, "complete_quest").Executions, Is.Empty);
        }

        // ---- The same target from two sources (G13) ------------------------------------------

        [Test]
        public async Task TheSameObjectFromTwoSourcesIsOneTarget()
        {
            var provider = new FakeProvider(GoToTheGate);
            var agent = QuestAgent(provider);
            agent.Targets.TryGet("village_gate", out var gate);
            var wren = new object();
            agent.TargetSources.Add(new ExplicitTargetSource().Add("guard_wren", wren, "person").Add("village_gate", gate, "landmark"));
            agent.TargetSources.Add(new ExplicitTargetSource().Add("guard_wren", wren, "guard"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Walk to the gate."));

            Assert.That(result.Telemetry.Outcome, Is.Not.EqualTo(DecisionOutcome.PipelineError), result.Telemetry.ErrorMessage);
            Assert.That(TargetEnum(provider), Is.EqualTo(new[] { TargetRegistry.NoTarget, "village_gate", "quest_wolf", "guard_wren" }));
        }

        [Test]
        public void TheFirstSourceToOfferAnObjectKeepsItsCategory()
        {
            var into = new TargetRegistry();
            var wren = new object();
            new ExplicitTargetSource().Add("guard_wren", wren, "person").Collect(null, into);
            new ExplicitTargetSource().Add("guard_wren", wren, "guard").Collect(null, into);

            Assert.That(into.CategoryOf("guard_wren"), Is.EqualTo("person"));
        }

        [Test]
        public async Task AnotherObjectUnderAnOfferedIdIsStillAPipelineError()
        {
            var agent = QuestAgent(new FakeProvider(GoToTheGate));
            agent.TargetSources.Add(new ExplicitTargetSource().Add("village_gate", new object(), "place"));

            LogAssert.Expect(LogType.Warning, new Regex("village_gate"));
            var result = await Fixtures.NotCancelled(agent.DecideAsync("Walk to the gate."));

            Assert.That(result.Telemetry.Outcome, Is.EqualTo(DecisionOutcome.PipelineError));
            Assert.That(result.Telemetry.ErrorMessage, Does.Contain("another object"));
        }

        // ---- RegisterMethods with a filter (G10) ---------------------------------------------

        [Test]
        public void RegisterMethodsCanTakeOnlySomeActions()
        {
            var registry = new ActionRegistry();

            registry.RegisterMethods(new QuestGiver(), null, new[] { "wave", "fetch_water" });

            Assert.That(registry.TryGet("wave", out _, out _), Is.True);
            Assert.That(registry.TryGet("complete_quest", out _, out _), Is.False);
            Assert.That(registry.Definitions, Has.Count.EqualTo(1));
        }

        [Test]
        public void RegisterMethodsWithNothingListedForTheOwnerRegistersNothing()
        {
            var registry = new ActionRegistry();

            Assert.DoesNotThrow(() => registry.RegisterMethods(new QuestGiver(), null, new string[0]));
            Assert.DoesNotThrow(() => registry.RegisterMethods(new QuestGiver(), null, new[] { "fetch_water" }));
            Assert.That(registry.Definitions, Is.Empty);
        }

        [Test]
        public void RegisterMethodsWithANullFilterRegistersEveryAction()
        {
            var registry = new ActionRegistry();

            registry.RegisterMethods(new QuestGiver(), null, null);

            Assert.That(registry.Definitions, Has.Count.EqualTo(2));
        }

        public sealed class NoActions { }

        public sealed class Malformed
        {
            [AgentAction("wave", "Two targets.")]
            private void Wave(object a, object b) { }
        }

        [Test]
        public void RegisterMethodsWithAFilterStillChecksEveryMethod()
        {
            var registry = new ActionRegistry();

            Assert.Throws<ArgumentException>(() => registry.RegisterMethods(new NoActions(), null, new[] { "wave" }));
            Assert.Throws<ArgumentException>(() => registry.RegisterMethods(new Malformed(), null, new string[0]),
                "A malformed method is refused whether or not it is listed.");
        }

        // ---- Helpers -------------------------------------------------------------------------

        // A quest giver in code: complete_quest takes a quest, move_to anything. village_gate is a
        // place and quest_wolf a quest, both registered by hand.
        private static Agent QuestAgent(FakeProvider provider, bool declare = true)
        {
            var actions = new[]
            {
                new ActionDefinition { Id = "move_to", Description = "Walk to a place.", RequiresTarget = true },
                new ActionDefinition
                {
                    Id = "complete_quest", Description = "Take what the player brought.", RequiresTarget = true,
                    TargetCategories = declare ? new[] { "quest" } : null,
                },
            };

            var agent = new Agent(new AgentIdentity { Name = "Odile", Role = "a merchant" }, actions, provider);
            agent.Bind("move_to", new FakeHandler());
            agent.Bind("complete_quest", new FakeHandler());
            agent.Targets.Register("village_gate", new object(), "place");
            agent.Targets.Register("quest_wolf", new object(), "quest");
            return agent;
        }

        private static GuardVerdict Inspect(Agent agent, string action, string target)
        {
            var ctx = new AgentContext(agent.Identity, "", null, null, agent.Targets, agent.Actions);
            return new TargetCategoryGuard().Inspect(new GuardInput(ctx, null, null, new AgentDecision(action, target, ""), null, null));
        }

        private static IReadOnlyList<string> TargetEnum(FakeProvider provider)
        {
            foreach (var field in provider.LastRequest.Schema.Fields)
            {
                if (field.Name == DecisionSchema.TargetFieldName)
                {
                    return field.AllowedValues;
                }
            }

            return null;
        }
    }
}
