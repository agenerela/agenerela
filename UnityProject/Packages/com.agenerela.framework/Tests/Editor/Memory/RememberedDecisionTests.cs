using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Agenerela.Providers;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // What a remembered decision holds (#244 item 4, #216 item 3): RollingHistory.Remembers, the
    // outcome a game attaches (IRemembersOutcomes, Agent.SetOutcome), and saving it.
    public sealed class RememberedDecisionTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";
        private const string Asked = "Player: \"Go to the tower.\"";

        private static readonly AgentContext Ctx = new AgentContext(
            new AgentIdentity(), "", null, null, new TargetRegistry(), new ActionRegistry());

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        // ---- RollingHistory on its own ------------------------------------------------------

        [Test]
        public void ByDefaultTheWholeDecisionIsSentAndAnOutcomeChangesNothing()
        {
            var memory = new RollingHistory();
            var result = Decided("move_to", "tower", "On my way.");
            memory.Remember(Ctx, Request(Asked), result);
            var before = memory.Recall(Ctx).ToList();

            Assert.That(memory.Remembers, Is.EqualTo(RememberedDecision.Whole));
            Assert.That(memory.SetOutcome(result, "The tower door was locked."), Is.True);

            Assert.That(memory.Recall(Ctx), Is.EqualTo(new[] { Asked, GoToTheTower }));
            Assert.That(memory.Recall(Ctx), Is.EqualTo(before), "Byte for byte as before the outcome.");
        }

        [Test]
        public void MoveLeavesTheStatementOutAndKeepsTheSchemasOrder()
        {
            var memory = new RollingHistory { Remembers = RememberedDecision.Move };
            var schema = Schema(DecisionSchema.TargetFieldName, DecisionSchema.StatementFieldName, DecisionSchema.ActionFieldName);
            var result = Decided("move_to", "tower", "On my way.");
            memory.Remember(Ctx, Request(Asked, schema), result);
            memory.SetOutcome(result, "Reached the tower.");

            Assert.That(memory.Recall(Ctx), Is.EqualTo(new[] { Asked, "{\"target\":\"tower\",\"action\":\"move_to\"}" }),
                "No statement, and no outcome either: Move is the move alone.");
        }

        [Test]
        public void MoveAndOutcomeSendsTheOutcomeAfterTheMove()
        {
            var memory = new RollingHistory { Remembers = RememberedDecision.MoveAndOutcome };
            var first = Decided("move_to", "tower", "On my way.");
            var second = Decided("none", "no_target", "Waiting.");
            memory.Remember(Ctx, Request(Asked), first);
            memory.Remember(Ctx, Request("Player: \"Wait.\""), second);
            memory.SetOutcome(first, "Reached the tower; the door was \"locked\".");

            Assert.That(memory.Recall(Ctx), Is.EqualTo(new[]
            {
                Asked,
                "{\"action\":\"move_to\",\"target\":\"tower\",\"outcome\":\"Reached the tower; the door was \\\"locked\\\".\"}",
                "Player: \"Wait.\"",
                "{\"action\":\"none\",\"target\":\"no_target\"}",
            }), "A decision with no outcome is sent as Move sends it.");
        }

        [Test]
        public void AnOutcomeCanBeReplacedAndCleared()
        {
            var memory = new RollingHistory { Remembers = RememberedDecision.MoveAndOutcome };
            var result = Decided("move_to", "tower", "On my way.");
            memory.Remember(Ctx, Request(Asked), result);

            memory.SetOutcome(result, "Set out.");
            memory.SetOutcome(result, "Reached the tower.");
            Assert.That(memory.Recall(Ctx)[1], Is.EqualTo("{\"action\":\"move_to\",\"target\":\"tower\",\"outcome\":\"Reached the tower.\"}"));

            Assert.That(memory.SetOutcome(result, "   "), Is.True);
            Assert.That(memory.Recall(Ctx)[1], Is.EqualTo("{\"action\":\"move_to\",\"target\":\"tower\"}"));
        }

        [Test]
        public void SetOutcomeFindsOnlyADecisionItHolds()
        {
            var memory = new RollingHistory(turns: 1) { Remembers = RememberedDecision.MoveAndOutcome };
            var forgotten = Decided("move_to", "tower", "On my way.");
            var held = Decided("move_to", "tower", "On my way.");
            memory.Remember(Ctx, Request(Asked), forgotten);
            memory.Remember(Ctx, Request(Asked), held);

            Assert.That(memory.SetOutcome(forgotten, "Gone."), Is.False, "Forgotten by the turn cap.");
            Assert.That(memory.SetOutcome(Decided("move_to", "tower", "On my way."), "Other."), Is.False,
                "An equal decision is not the same decision.");
            Assert.That(memory.SetOutcome(new DecisionResult(null, new DecisionTelemetry { Outcome = DecisionOutcome.PipelineError }), "None."), Is.False, "A pipeline error.");
            Assert.That(memory.Recall(Ctx)[1], Is.EqualTo("{\"action\":\"move_to\",\"target\":\"tower\"}"), "Nothing changed.");
            Assert.Throws<ArgumentNullException>(() => memory.SetOutcome(null, "x"));
        }

        [Test]
        public void TheTokenCapCountsTheDecisionAsSent()
        {
            // Two exchanges whose whole decisions are each over the budget, and whose moves fit together.
            string statement = new string('x', 400);
            var memory = new RollingHistory(maxTokens: 100);
            memory.Remember(Ctx, Request("A"), Decided("move_to", "tower", statement));
            memory.Remember(Ctx, Request("B"), Decided("move_to", "tower", statement));

            Assert.That(memory.Recall(Ctx), Is.Empty);
            Assert.That(memory.Dropped, Is.EqualTo(2));

            memory.Remembers = RememberedDecision.Move;
            Assert.That(memory.Recall(Ctx), Has.Count.EqualTo(4));
            Assert.That(memory.Dropped, Is.Zero);

            memory.Remembers = RememberedDecision.Whole;
            Assert.That(memory.Recall(Ctx), Is.Empty, "Switching back loses nothing kept.");
        }

        [Test]
        public void RemembersTakesOnlyWhatTheEnumDefines()
        {
            var memory = new RollingHistory();

            Assert.Throws<ArgumentOutOfRangeException>(() => memory.Remembers = (RememberedDecision)7);
            Assert.That(memory.Remembers, Is.EqualTo(RememberedDecision.Whole));
        }

        [Test]
        public void ADecisionThatIsNotJsonIsSentAsItIs()
        {
            // Only an import can bring one: Import checks a decision is there, not what it says.
            var memory = new RollingHistory { Remembers = RememberedDecision.MoveAndOutcome };
            var snapshot = new MemorySnapshot { Format = RollingHistory.SnapshotFormat, Version = RollingHistory.SnapshotVersion };
            snapshot.Exchanges.Add(new MemoryExchange("Hi.", "not json") { Outcome = "Waved." });
            memory.Import(snapshot);

            Assert.That(memory.Recall(Ctx), Is.EqualTo(new[] { "Hi.", "not json" }));
        }

        // ---- Saving -------------------------------------------------------------------------

        [Test]
        public void ExportSavesTheOutcomeAndImportSendsItBack()
        {
            var memory = new RollingHistory { Remembers = RememberedDecision.MoveAndOutcome };
            var result = Decided("move_to", "tower", "On my way.");
            memory.Remember(Ctx, Request(Asked), result);
            memory.SetOutcome(result, "Reached the tower.");

            var snapshot = memory.Export();
            Assert.That(snapshot.Version, Is.EqualTo(2));
            Assert.That(snapshot.Exchanges.Single().Decision, Is.EqualTo(GoToTheTower), "The whole decision is saved.");
            Assert.That(snapshot.Exchanges.Single().Outcome, Is.EqualTo("Reached the tower."));

            var viaJsonUtility = new RollingHistory { Remembers = RememberedDecision.MoveAndOutcome };
            viaJsonUtility.Import(JsonUtility.FromJson<MemorySnapshot>(JsonUtility.ToJson(snapshot)));
            var viaNewtonsoft = new RollingHistory { Remembers = RememberedDecision.MoveAndOutcome };
            viaNewtonsoft.Import(JsonConvert.DeserializeObject<MemorySnapshot>(JsonConvert.SerializeObject(snapshot)));

            Assert.That(viaJsonUtility.Recall(Ctx), Is.EqualTo(memory.Recall(Ctx)));
            Assert.That(viaNewtonsoft.Recall(Ctx), Is.EqualTo(memory.Recall(Ctx)));
            Assert.That(viaNewtonsoft.SetOutcome(result, "Other."), Is.False, "An import keeps the outcome, not whose it is.");
        }

        [Test]
        public void AnExchangeWithNoOutcomeSavesNoneThroughJsonUtility()
        {
            var memory = new RollingHistory();
            memory.Remember(Ctx, Request(Asked), Decided("move_to", "tower", "On my way."));

            var back = JsonUtility.FromJson<MemorySnapshot>(JsonUtility.ToJson(memory.Export()));
            Assert.That(back.Exchanges.Single().Outcome, Is.Empty, "JsonUtility writes null as empty.");

            var restored = new RollingHistory { Remembers = RememberedDecision.MoveAndOutcome };
            restored.Import(back);
            Assert.That(restored.Recall(Ctx)[1], Is.EqualTo("{\"action\":\"move_to\",\"target\":\"tower\"}"), "Empty is none.");
        }

        [Test]
        public void AVersionOneSnapshotStillImports()
        {
            var snapshot = new MemorySnapshot { Format = RollingHistory.SnapshotFormat, Version = 1 };
            snapshot.Exchanges.Add(new MemoryExchange(Asked, GoToTheTower));
            var memory = new RollingHistory();

            Assert.That(memory.CanImport(snapshot, out string reason), Is.True, reason);
            memory.Import(snapshot);
            Assert.That(memory.Recall(Ctx), Is.EqualTo(new[] { Asked, GoToTheTower }));
        }

        // ---- Through Agent ------------------------------------------------------------------

        [Test]
        public async Task ByDefaultAnAgentsOutcomeChangesNoRequest()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            var result = await Decide(agent);

            Assert.That(agent.SetOutcome(result, "Reached the tower."), Is.True, "RollingHistory keeps it.");
            await Decide(agent, "Anything else?");

            Assert.That(fake.LastRequest.History, Is.EqualTo(new[] { Asked, GoToTheTower }));
        }

        [Test]
        public async Task AnOutcomeSetWhileHeldIsRememberedWithTheDecision()
        {
            var (agent, fake) = Remembering(RememberedDecision.MoveAndOutcome, MemoryCommit.OnExecute);
            var result = await Decide(agent);

            Assert.That(agent.SetOutcome(result, "Set out for the tower."), Is.True);
            await Decide(agent, "Still there?");
            Assert.That(fake.LastRequest.History, Is.Empty, "Still held.");

            agent.Execute(result);
            await Decide(agent, "Anything else?");
            Assert.That(fake.LastRequest.History, Is.EqualTo(new[]
            {
                Asked, "{\"action\":\"move_to\",\"target\":\"tower\",\"outcome\":\"Set out for the tower.\"}",
            }));
        }

        [Test]
        public async Task AnOutcomeKnownLaterAmendsTheRememberedDecision()
        {
            // A battle resolves after the move lands: the game says what came of it then.
            foreach (var commit in new[] { MemoryCommit.OnExecute, MemoryCommit.OnDecide })
            {
                var (agent, fake) = Remembering(RememberedDecision.MoveAndOutcome, commit);
                var result = await Decide(agent);
                agent.Execute(result);

                Assert.That(agent.SetOutcome(result, "Took the tower; lost 3."), Is.True, commit.ToString());
                await Decide(agent, "Anything else?");
                Assert.That(fake.LastRequest.History[1],
                    Is.EqualTo("{\"action\":\"move_to\",\"target\":\"tower\",\"outcome\":\"Took the tower; lost 3.\"}"), commit.ToString());
            }
        }

        [Test]
        public async Task AFailedHandlersReasonIsTheOutcomeOfADecisionCommittedAnyway()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = new Agent(Fixtures.VillageGuardProfile(), fake)
            {
                Memory = new RollingHistory { Remembers = RememberedDecision.MoveAndOutcome },
                MemoryCommit = MemoryCommit.OnExecute,
            };
            agent.Bind("move_to", DelegateActionHandler.WithResult(ctx => true, (ctx, d) => ActionResult.Failed("The bridge was out.")));
            agent.Targets.Register("tower", new object());
            var failed = await Decide(agent);
            var told = await Decide(agent);

            Assert.That(agent.Execute(failed), Is.False);
            Assert.That(agent.Commit(failed), Is.True);
            Assert.That(agent.SetOutcome(told, "Turned back at the river."), Is.True);
            Assert.That(agent.Execute(told), Is.False, "Fails too, but the game's own line stays.");
            agent.Commit(told);
            await Decide(agent, "Anything else?");

            Assert.That(fake.LastRequest.History, Is.EqualTo(new[]
            {
                Asked, "{\"action\":\"move_to\",\"target\":\"tower\",\"outcome\":\"The bridge was out.\"}",
                Asked, "{\"action\":\"move_to\",\"target\":\"tower\",\"outcome\":\"Turned back at the river.\"}",
            }));
        }

        [Test]
        public async Task ARetryReplacesTheFailuresReasonAndASuccessClearsIt()
        {
            string why = "The bridge was out.";
            var fake = new FakeProvider(GoToTheTower);
            var agent = new Agent(Fixtures.VillageGuardProfile(), fake)
            {
                Memory = new RollingHistory { Remembers = RememberedDecision.MoveAndOutcome },
                MemoryCommit = MemoryCommit.OnExecute,
            };
            agent.Bind("move_to", DelegateActionHandler.WithResult(ctx => true,
                (ctx, d) => why == null ? ActionResult.Done() : ActionResult.Failed(why)));
            agent.Targets.Register("tower", new object());
            var retried = await Decide(agent);
            var succeeded = await Decide(agent);

            agent.Execute(retried);
            why = "The ford was flooded.";
            agent.Execute(retried);
            agent.Commit(retried);

            why = "The bridge was out.";
            agent.Execute(succeeded);
            why = null;
            Assert.That(agent.Execute(succeeded), Is.True);
            await Decide(agent, "Anything else?");

            Assert.That(fake.LastRequest.History, Is.EqualTo(new[]
            {
                Asked, "{\"action\":\"move_to\",\"target\":\"tower\",\"outcome\":\"The ford was flooded.\"}",
                Asked, "{\"action\":\"move_to\",\"target\":\"tower\"}",
            }), "The latest failure's reason; none once it succeeded.");
        }

        [Test]
        public async Task ADiscardedOrResetDecisionTakesNoOutcome()
        {
            var (agent, fake) = Remembering(RememberedDecision.MoveAndOutcome, MemoryCommit.OnExecute);
            var discarded = await Decide(agent);
            agent.Discard(discarded);
            Assert.That(agent.SetOutcome(discarded, "Never happened."), Is.False);

            var dropped = await Decide(agent);
            agent.Reset();
            Assert.That(agent.SetOutcome(dropped, "Never happened."), Is.False);
            Assert.That(agent.Execute(dropped), Is.True, "It can still be carried out.");

            await Decide(agent, "Anything else?");
            Assert.That(fake.LastRequest.History, Is.Empty, "Neither was remembered.");
        }

        [Test]
        public async Task AMemoryThatKeepsNoOutcomesSaysSo()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Memory = new PlainMemory();
            var remembered = await Decide(agent);
            Assert.That(agent.SetOutcome(remembered, "Reached it."), Is.False);

            agent.MemoryCommit = MemoryCommit.OnExecute;
            var held = await Decide(agent);
            Assert.That(agent.SetOutcome(held, "Reached it."), Is.False, "Nothing would keep it.");

            agent.Memory = null;
            Assert.That(agent.SetOutcome(await Decide(agent), "Reached it."), Is.False, "No memory at all.");
        }

        [Test]
        public async Task OnlyTheAgentThatDecidedSetsTheOutcomeOfAHeldDecision()
        {
            var (agent, _) = Remembering(RememberedDecision.MoveAndOutcome, MemoryCommit.OnExecute);
            var (other, _) = Remembering(RememberedDecision.MoveAndOutcome, MemoryCommit.OnExecute);
            var result = await Decide(agent);

            Assert.Throws<ArgumentException>(() => other.SetOutcome(result, "Not mine."));
            Assert.Throws<ArgumentNullException>(() => agent.SetOutcome(null, "x"));
        }

        [Test]
        public async Task AMemoryThatThrowsOnAnOutcomeIsLogged()
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Memory = new ThrowingMemory();
            var result = await Decide(agent);

            LogAssert.Expect(LogType.Warning, new Regex("could not keep a decision's outcome"));
            Assert.That(agent.SetOutcome(result, "Reached it."), Is.False);
        }

        // ---- Helpers ------------------------------------------------------------------------

        private static (Agent Agent, FakeProvider Fake) Remembering(RememberedDecision remembers, MemoryCommit commit)
        {
            var fake = new FakeProvider(GoToTheTower);
            var agent = Fixtures.VillageGuard(fake);
            agent.Memory = new RollingHistory { Remembers = remembers };
            agent.MemoryCommit = commit;
            return (agent, fake);
        }

        private static Task<DecisionResult> Decide(Agent agent, string stimulus = "Go to the tower.")
        {
            return Fixtures.NotCancelled(agent.DecideAsync(stimulus));
        }

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

        private sealed class PlainMemory : IMemoryStrategy
        {
            public IReadOnlyList<string> Recall(AgentContext ctx) => Array.Empty<string>();
            public void Remember(AgentContext ctx, DecisionRequest request, DecisionResult result) { }
        }

        private sealed class ThrowingMemory : IMemoryStrategy, IRemembersOutcomes
        {
            public IReadOnlyList<string> Recall(AgentContext ctx) => Array.Empty<string>();
            public void Remember(AgentContext ctx, DecisionRequest request, DecisionResult result) { }
            public bool SetOutcome(DecisionResult result, string outcome) => throw new InvalidOperationException("No room.");
        }
    }
}
