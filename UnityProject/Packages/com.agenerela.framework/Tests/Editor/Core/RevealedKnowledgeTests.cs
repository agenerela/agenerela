using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // Knowledge that counts only when revealed (#190, Living Vale #17), with no API of its own: the
    // fact is a target of the action that reveals it, in its own category, never a standing
    // Knowledge line, and the action's per-target check removes it once told. This is the worked
    // example in Documentation~/targets.md, "Facts told through an action", end to end. Every await
    // of DecideAsync goes through Fixtures.NotCancelled.
    public sealed class RevealedKnowledgeTests
    {
        private const string AlreadyTold = "already told";

        [Test]
        public async Task AFactIsToldOnceThenItsTargetIsUnavailableWithAReason()
        {
            var fake = new FakeProvider(Reply("share_information", "fact_mill_key"));
            var (agent, clues) = Mara(fake);

            // The fact is no standing knowledge: the system prompt has no "What you know" block.
            var before = agent.Preview("Where is the mill key?");
            Assert.That(agent.Identity.Knowledge, Is.Empty);
            Assert.That(before.Request.SystemPrompt, Does.Not.Contain("What you know"));
            Assert.That(before.TargetsUnavailable, Is.Empty);

            // Telling it is the action, and only the action records the clue.
            var told = await Fixtures.NotCancelled(agent.DecideAsync("Where is the mill key?"));
            Assert.That(agent.Execute(told, out var outcome), Is.True, outcome.Reason);
            Assert.That(clues, Is.EqualTo(new[] { "fact_mill_key" }));

            // Once told, the pair is gone from the paired schema, with the check's reason recorded.
            var after = agent.Preview("Tell me again.", new DecideOptions { PairTargetsWithActions = true });
            Assert.That(after.TargetsUnavailable.Select(p => p.ToString()),
                        Is.EqualTo(new[] { $"share_information -> fact_mill_key: {AlreadyTold}" }));
            Assert.That(TargetsOf(after, "share_information"), Is.EqualTo(new[] { "fact_ford" }));

            // A model that names it anyway, flat schema, is turned to none by the guard.
            var again = await Fixtures.NotCancelled(agent.DecideAsync("Tell me again."));
            Assert.That(again.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
            Assert.That(again.Verdict.Value.Guard, Is.EqualTo("TargetAvailabilityGuard"));
            Assert.That(again.Verdict.Value.Reason, Does.Contain(AlreadyTold));

            // And a decision made before it was told, carried out after, is refused with the reason.
            var (other, otherClues) = Mara(new FakeProvider(Reply("share_information", "fact_mill_key")));
            var early = await Fixtures.NotCancelled(other.DecideAsync("Where is the mill key?"));
            var late = await Fixtures.NotCancelled(other.DecideAsync("And the key?"));
            Assert.That(other.Execute(early), Is.True);
            LogAssert.Expect(LogType.Warning, new Regex(AlreadyTold));
            Assert.That(other.Execute(late, out var refused), Is.False);
            Assert.That(refused.Status, Is.EqualTo(ExecutionStatus.Refused));
            Assert.That(refused.Reason, Is.EqualTo($"'share_information' cannot be done to 'fact_mill_key' right now: {AlreadyTold}"));
            Assert.That(otherClues, Is.EqualTo(new[] { "fact_mill_key" }), "Told once.");
        }

        [Test]
        public async Task WithEveryFactToldTheRevealingActionIsLeftOut()
        {
            var fake = new FakeProvider(Reply("share_information", "fact_mill_key"));
            var (agent, _) = Mara(fake);
            agent.Execute(await Fixtures.NotCancelled(agent.DecideAsync("Where is the mill key?")));
            fake.Reply = Reply("share_information", "fact_ford");
            agent.Execute(await Fixtures.NotCancelled(agent.DecideAsync("Is the ford safe?")));

            var preview = agent.Preview("Anything else?");

            Assert.That(preview.Available.Select(a => a.Id), Is.EqualTo(new[] { "wave" }));
            Assert.That(preview.LeftOut.Single(l => l.Action.Id == "share_information").Reason, Does.Contain(AlreadyTold));
        }

        [Test]
        public void AListedFactShowsItsDescription()
        {
            var (agent, _) = Mara(new FakeProvider());
            var options = new DecideOptions { ListActionsAndTargets = true };

            string prompt = agent.Preview("Hello.", options).Request.SystemPrompt;

            Assert.That(prompt, Does.Contain("fact_mill_key"));
            Assert.That(prompt, Does.Contain("hangs behind the bakery door"), "The content reaches the model through the target, as an action's to tell.");
        }

        // ---- Fixtures ------------------------------------------------------------------------

        private static string Reply(string action, string target) =>
            $"{{\"action\":\"{action}\",\"target\":\"{target}\",\"statement\":\"Listen.\"}}";

        // A fact an NPC can tell: what it is called, and what telling it says.
        private sealed class Fact : IDescribedTarget, INamedTarget
        {
            public Fact(string name, string description)
            {
                Name = name;
                Description = description;
            }

            public string Name { get; }
            public string Description { get; }
        }

        // Mara the miller, who can wave or tell one of two facts, each once.
        private static (Agent Agent, List<string> Clues) Mara(FakeProvider fake)
        {
            var clues = new List<string>();
            var told = new HashSet<string>();
            var actions = new[]
            {
                new ActionDefinition { Id = "wave", Description = "Wave.", RequiresTarget = false },
                new ActionDefinition
                {
                    Id = "share_information", Description = "Tell the traveller something you know.",
                    RequiresTarget = true, TargetCategories = new[] { "fact" },
                },
            };

            var share = new DelegateActionHandler(_ => true, (_, decision) =>
            {
                told.Add(decision.TargetId);
                clues.Add(decision.TargetId);
            })
            {
                IsAvailableFor = (_, id) => told.Contains(id) ? Availability.No(AlreadyTold) : Availability.Yes,
            };

            var agent = new Agent(new AgentIdentity { Name = "Mara", Role = "the miller" }, actions, fake);
            agent.Bind("wave", new FakeHandler());
            agent.Bind("share_information", share);
            agent.Targets.Register("fact_mill_key", new Fact("the mill key", "The mill key hangs behind the bakery door."), "fact");
            agent.Targets.Register("fact_ford", new Fact("the ford", "The ford floods after rain."), "fact");
            return (agent, clues);
        }

        private static IEnumerable<string> TargetsOf(AgentPreview preview, string actionId)
        {
            return preview.Request.Schema.Pairs.Single(p => p.Actions.Contains(actionId)).Targets;
        }
    }
}
