using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;

namespace Agenerela.Tests
{
    // The optional decision round (#216, item 2; DR-021): several agents decided on one snapshot,
    // then executed in an order the caller gives after seeing the results, each re-checked, a move
    // an earlier one made illegal refused as stale, and nothing from a dropped round remembered.
    // Every await of a round that is not about cancellation goes through Fixtures.NotCancelled.
    public sealed class DecisionRoundTests
    {
        private const string Stimulus = "Your move.";

        // ---- The order -----------------------------------------------------------------------

        [Test]
        public async Task NothingIsExecutedUntilTheCallerGivesTheOrder()
        {
            var (provider, world, agents) = Countries("valen", "orsk", "kesh");
            Script(provider, "valen", "recruit");
            Script(provider, "orsk", "recruit");
            Script(provider, "kesh", "recruit");

            var round = await Decide(agents);

            Assert.That(world.Log, Is.Empty, "Deciding executes nothing.");
            Assert.That(round.Entries.Select(e => e.Status), Is.All.EqualTo(RoundEntryStatus.Decided));
            Assert.That(round.Entries.Select(e => e.ActionId), Is.All.EqualTo("recruit"));
            Assert.That(round.Waiting, Has.Count.EqualTo(3));
            Assert.That(round.Entries.Select(e => e.Agent), Is.EqualTo(agents), "Entries in call order.");
        }

        [Test]
        public async Task TheOrderOfExecutionIsTheCallers()
        {
            var (provider, world, agents) = Countries("valen", "orsk", "kesh");
            foreach (var id in new[] { "valen", "orsk", "kesh" })
            {
                Script(provider, id, "recruit");
            }

            var round = await Decide(agents);
            var ran = round.Execute(agents[2], agents[0], agents[1]);

            Assert.That(world.Log, Is.EqualTo(new[] { "kesh:recruit", "valen:recruit", "orsk:recruit" }));
            Assert.That(ran.Select(e => e.Agent), Is.EqualTo(new[] { agents[2], agents[0], agents[1] }), "Returned in the order run.");
            Assert.That(ran.Select(e => e.Status), Is.All.EqualTo(RoundEntryStatus.Executed));
            Assert.That(ran.Select(e => e.Outcome.Value.Status), Is.All.EqualTo(ExecutionStatus.Done));
            Assert.That(ran.Select(e => e.Stale), Is.All.False);
        }

        [Test]
        public async Task TheRoundWaitsForEveryAgentBeforeItReturns()
        {
            var (provider, world, agents) = Countries("valen", "orsk");
            Script(provider, "valen", "recruit");
            Script(provider, "orsk", "recruit");
            provider.Hold = true;

            var pending = AsTask(DecisionRound.DecideAsync(Calls(agents)));
            await Until(() => provider.HeldCount == 1);
            provider.Release();
            await Until(() => provider.HeldCount == 1, "The second request reaches the provider once the first is answered.");

            Assert.That(pending.IsCompleted, Is.False, "One agent has answered; the round still waits for the other.");

            provider.Release();
            var round = await Fixtures.NotCancelled(pending);

            Assert.That(round.Entries.Select(e => e.Status), Is.All.EqualTo(RoundEntryStatus.Decided));
            Assert.That(world.Log, Is.Empty);
        }

        // ---- Stale moves ---------------------------------------------------------------------

        [Test]
        public async Task AMoveAnEarlierOneMadeIllegalIsRefusedAndReportedAsStale()
        {
            var (provider, world, agents) = Countries("valen", "orsk");
            Script(provider, "valen", "attack", "fenmark");
            Script(provider, "orsk", "attack", "fenmark");

            var round = await Decide(agents);
            var ran = round.Execute(agents[1], agents[0]);

            Assert.That(world.Log, Is.EqualTo(new[] { "orsk:attack fenmark" }), "Only the first attack ran.");
            var orsk = round[agents[1]];
            var valen = round[agents[0]];
            Assert.That(orsk.Outcome.Value.Status, Is.EqualTo(ExecutionStatus.Done));
            Assert.That(orsk.Stale, Is.False);
            Assert.That(valen.Status, Is.EqualTo(RoundEntryStatus.Executed));
            Assert.That(valen.Outcome.Value.Status, Is.EqualTo(ExecutionStatus.Refused));
            Assert.That(valen.Outcome.Value.Reason, Is.Not.Empty);
            Assert.That(valen.Stale, Is.True, "Refused by the re-check after DecideAsync accepted it.");
            Assert.That(valen.Result.Telemetry.Execution, Is.EqualTo(ExecutionStatus.Refused));

            Assert.That(Remembered(agents[1]), Is.EqualTo(1), "The move carried out is remembered.");
            Assert.That(Remembered(agents[0]), Is.EqualTo(0), "The stale move is not.");
        }

        [Test]
        public async Task APipelineErrorIsRefusedButNotStale()
        {
            var (provider, _, agents) = Countries("valen");
            provider.When("valen", Stimulus, ScriptedReply.Fail(ProviderErrorKind.Unreachable));

            var round = await Decide(agents);
            var valen = round.Execute(agents[0]).Single();

            Assert.That(valen.Result.Telemetry.Outcome, Is.EqualTo(DecisionOutcome.PipelineError));
            Assert.That(valen.Outcome.Value.Status, Is.EqualTo(ExecutionStatus.Refused));
            Assert.That(valen.Stale, Is.False, "There was never a legal move to go stale.");
        }

        // ---- One agent's trouble ---------------------------------------------------------------

        [Test]
        public async Task OneAgentsPipelineErrorDoesNotSinkTheOthers()
        {
            var (provider, world, agents) = Countries("valen", "orsk", "kesh");
            Script(provider, "valen", "recruit");
            provider.When("orsk", Stimulus, ScriptedReply.Fail(ProviderErrorKind.Unreachable));
            Script(provider, "kesh", "recruit");

            var round = await Decide(agents);

            Assert.That(round.Entries.Select(e => e.Status), Is.All.EqualTo(RoundEntryStatus.Decided));
            Assert.That(round[agents[1]].Decision, Is.Null);
            Assert.That(round[agents[1]].Result.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.ProviderUnreachable));

            round.Execute(agents);
            Assert.That(world.Log, Is.EqualTo(new[] { "valen:recruit", "kesh:recruit" }));
        }

        [Test]
        public async Task AnAgentResetMidRoundFailsAloneAndTheRoundStillReturns()
        {
            // One provider each, so valen's request never waits behind orsk's held one.
            var world = new World();
            var fast = new ScriptedProvider().Otherwise(ScriptedReply.Decide("recruit"));
            var slow = new ScriptedProvider().Otherwise(ScriptedReply.Never());
            var valen = Country("valen", fast, world);
            var orsk = Country("orsk", slow, world);

            var pending = AsTask(DecisionRound.DecideAsync(Calls(new[] { valen, orsk })));
            await Until(() => slow.RequestCount == 1);
            orsk.Reset();
            var round = await Fixtures.NotCancelled(pending);

            Assert.That(round[valen].Status, Is.EqualTo(RoundEntryStatus.Decided));
            Assert.That(round[orsk].Status, Is.EqualTo(RoundEntryStatus.Failed));
            Assert.That(round[orsk].Result, Is.Null);
            Assert.That(round[orsk].Error, Is.InstanceOf<OperationCanceledException>());
            Assert.Throws<InvalidOperationException>(() => round.Execute(orsk), "A failed entry has nothing to execute.");
            Assert.That(round.Execute(valen).Single().Outcome.Value.Status, Is.EqualTo(ExecutionStatus.Done));
        }

        // ---- Cancellation and memory ---------------------------------------------------------

        [Test]
        public async Task CancellingTheRoundDropsItAndCommitsNothing()
        {
            var world = new World();
            var fast = new ScriptedProvider().Otherwise(ScriptedReply.Decide("recruit"));
            var slow = new ScriptedProvider().Otherwise(ScriptedReply.Never());
            var valen = Country("valen", fast, world);
            var orsk = Country("orsk", slow, world);
            var answered = Recorded();

            using (var cts = new CancellationTokenSource())
            {
                var pending = AsTask(DecisionRound.DecideAsync(Calls(new[] { valen, orsk }), ct: cts.Token));
                await Until(() => slow.RequestCount == 1 && answered.Count == 1);
                cts.Cancel();

                Assert.That(await Cancelled(pending), Is.True, "The round ends cancelled.");
            }

            var valenResult = answered.Single().Result;
            Assert.That(valen.Commit(valenResult), Is.False, "The answer already in was discarded with the round.");
            Assert.That(Remembered(valen), Is.EqualTo(0));
            Assert.That(Remembered(orsk), Is.EqualTo(0));
            Assert.That(world.Log, Is.Empty);
        }

        [Test]
        public async Task ACancelledRoundEndsAtOnceThoughItsProviderIgnoresCancellation()
        {
            var world = new World();
            var stubborn = new FakeProvider(Reply("recruit")) { Hold = true, IgnoresCancellation = true };
            var valen = Country("valen", stubborn, world);

            using (var cts = new CancellationTokenSource())
            {
                var pending = AsTask(DecisionRound.DecideAsync(Calls(new[] { valen }), ct: cts.Token));
                await Until(() => stubborn.HeldCount == 1);
                cts.Cancel();

                Assert.That(await Cancelled(pending), Is.True, "The round ends cancelled while the provider still holds the request.");
                Assert.That(stubborn.HeldCount, Is.EqualTo(1));
            }

            stubborn.Release();
            for (int i = 0; i < 10; i++)
            {
                await Task.Yield();
            }

            Assert.That(Remembered(valen), Is.EqualTo(0), "Whatever came of the late answer, nothing remembers it.");
            Assert.That(world.Log, Is.Empty);
        }

        [Test]
        public async Task AnAgentTheCallerDoesNotExecuteLeavesNoMemory()
        {
            var (provider, world, agents) = Countries("valen", "orsk");
            Script(provider, "valen", "recruit");
            Script(provider, "orsk", "recruit");

            var round = await Decide(agents);
            round.Execute(agents[0]);
            Assert.That(round.End(), Is.EqualTo(1));

            var orsk = round[agents[1]];
            Assert.That(orsk.Status, Is.EqualTo(RoundEntryStatus.Skipped));
            Assert.That(orsk.Outcome, Is.Null);
            Assert.That(agents[1].Commit(orsk.Result), Is.False, "End discarded it, so nothing remembers it later.");
            Assert.That(Remembered(agents[1]), Is.EqualTo(0));
            Assert.That(Remembered(agents[0]), Is.EqualTo(1));
            Assert.That(world.Log, Is.EqualTo(new[] { "valen:recruit" }));
            Assert.That(round.IsEnded, Is.True);
            Assert.Throws<InvalidOperationException>(() => round.Execute(agents[1]));
        }

        [Test]
        public async Task AnAgentWithMemoryOnDecideIsRefusedBeforeAnyoneIsAsked()
        {
            var (provider, _, agents) = Countries("valen", "orsk");
            agents[1].MemoryCommit = MemoryCommit.OnDecide;

            Assert.Throws<InvalidOperationException>(() => DecisionRound.DecideAsync(Calls(agents)));
            Assert.That(provider.RequestCount, Is.EqualTo(0));

            agents[1].Memory = null;
            Script(provider, "valen", "recruit");
            Script(provider, "orsk", "recruit");
            Assert.That((await Decide(agents)).Entries, Has.Count.EqualTo(2), "An agent with no memory has nothing to hold back.");
        }

        // ---- Phases --------------------------------------------------------------------------

        [Test]
        public async Task PhasesRunWithoutASecondDecide()
        {
            var (provider, world, agents) = Countries("valen", "orsk", "kesh");
            Script(provider, "valen", "attack", "fenmark");
            Script(provider, "orsk", "recruit");
            Script(provider, "kesh", "recruit");

            var round = await Decide(agents);
            int asked = provider.RequestCount;

            // Home moves first, then attacks: the order read from what was chosen.
            var home = round.Execute(round.Waiting.Where(e => e.ActionId == "recruit").Select(e => e.Agent));
            Assert.That(home, Has.Count.EqualTo(2));
            Assert.That(round.Waiting.Select(e => e.Agent), Is.EqualTo(new[] { agents[0] }), "The attack still waits.");

            var attacks = round.Execute(round.Waiting.Where(e => e.ActionId == "attack").Select(e => e.Agent));

            Assert.That(attacks.Single().Outcome.Value.Status, Is.EqualTo(ExecutionStatus.Done));
            Assert.That(world.Log, Is.EqualTo(new[] { "orsk:recruit", "kesh:recruit", "valen:attack fenmark" }));
            Assert.That(provider.RequestCount, Is.EqualTo(asked), "No phase asked anyone again.");
            Assert.That(round.Waiting, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => round.Execute(agents[1]), "Each decision runs once.");
            Assert.That(world.Log, Has.Count.EqualTo(3));
        }

        [Test]
        public async Task AMistakeInTheOrderExecutesNothing()
        {
            var (provider, world, agents) = Countries("valen", "orsk");
            Script(provider, "valen", "recruit");
            Script(provider, "orsk", "recruit");
            var stranger = Country("ilvar", provider, world);

            var round = await Decide(agents);

            Assert.Throws<ArgumentException>(() => round.Execute(agents[0], agents[0]));
            Assert.Throws<ArgumentException>(() => round.Execute(agents[0], stranger));
            Assert.Throws<ArgumentException>(() => round.Execute(agents[0], null));
            Assert.That(world.Log, Is.Empty, "The order is checked whole before anything runs.");
            Assert.That(round.Waiting, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task AHandlerCannotExecuteTheRoundFromInside()
        {
            var (provider, world, agents) = Countries("valen", "orsk");
            Script(provider, "valen", "recruit");
            Script(provider, "orsk", "recruit");
            DecisionRound round = null;
            Exception inner = null;
            agents[0].Bind("recruit", new DelegateActionHandler(_ => true, (_, __) =>
            {
                inner = Record(() => round.Execute(agents[1]));
                world.Log.Add("valen:recruit");
            }));

            round = await Decide(agents);
            round.Execute(agents[0]);

            Assert.That(inner, Is.InstanceOf<InvalidOperationException>());
            Assert.That(round[agents[1]].Status, Is.EqualTo(RoundEntryStatus.Decided), "orsk still waits for the next phase.");
            Assert.That(world.Log, Is.EqualTo(new[] { "valen:recruit" }));
        }

        [Test]
        public async Task AHandlerThatThrowsStopsTheCallAndLeavesTheRestWaiting()
        {
            var (provider, world, agents) = Countries("valen", "orsk", "kesh");
            foreach (var id in new[] { "valen", "orsk", "kesh" })
            {
                Script(provider, id, "recruit");
            }

            agents[1].Bind("recruit", new DelegateActionHandler(_ => true, (_, __) => throw new InvalidCastException("orsk's bug")));

            var round = await Decide(agents);
            Assert.Throws<InvalidCastException>(() => round.Execute(agents));

            Assert.That(round[agents[0]].Status, Is.EqualTo(RoundEntryStatus.Executed));
            Assert.That(round[agents[1]].Status, Is.EqualTo(RoundEntryStatus.Threw));
            Assert.That(round[agents[1]].Error, Is.InstanceOf<InvalidCastException>());
            Assert.That(round[agents[2]].Status, Is.EqualTo(RoundEntryStatus.Decided));
            round.Execute(agents[2]);
            Assert.That(world.Log, Is.EqualTo(new[] { "valen:recruit", "kesh:recruit" }));
        }

        // ---- The log -------------------------------------------------------------------------

        [Test]
        public async Task EveryDecisionCarriesTheRoundIdAsItsChainAndKeepsItsCorrelation()
        {
            var (provider, _, agents) = Countries("valen", "orsk", "kesh");
            foreach (var id in new[] { "valen", "orsk", "kesh" })
            {
                Script(provider, id, "recruit");
            }

            var own = DecisionChain.Start("relayed-earlier");
            var calls = new[]
            {
                new RoundCall(agents[0], Stimulus, new DecideOptions { Correlation = "offer-7" }),
                new RoundCall(agents[1], Stimulus),
                new RoundCall(agents[2], Stimulus, new DecideOptions { Chain = own }),
            };
            var recorded = Recorded();

            var round = await Fixtures.NotCancelled(DecisionRound.DecideAsync(calls, "turn-7-council"));

            Assert.That(round.Id, Is.EqualTo("turn-7-council"));
            Assert.That(round[agents[0]].Result.Telemetry.ChainId, Is.EqualTo("turn-7-council"));
            Assert.That(round[agents[0]].Result.Telemetry.ChainHop, Is.EqualTo(0));
            Assert.That(round[agents[0]].Result.Correlation, Is.EqualTo("offer-7"), "Correlation stays the caller's.");
            Assert.That(round[agents[1]].Result.Telemetry.ChainId, Is.EqualTo("turn-7-council"));
            Assert.That(round[agents[2]].Result.Telemetry.ChainId, Is.EqualTo("relayed-earlier"), "A chain of its own is kept.");
            Assert.That(recorded.Where(e => e.Stage == DecisionStage.Decided).Select(e => e.Agent), Is.EquivalentTo(agents),
                "Each decision reaches the feed as any decision does.");
            Assert.That(calls[0].Options.Chain, Is.Null, "The caller's options are not changed.");

            var unnamed = await Fixtures.NotCancelled(DecisionRound.DecideAsync(Calls(agents)));
            Assert.That(unnamed.Id, Is.Not.Empty.And.Not.EqualTo(round.Id));
        }

        [Test]
        public void MistakesInTheCallsThrowBeforeAnyoneIsAsked()
        {
            var (provider, _, agents) = Countries("valen", "orsk");

            Assert.Throws<ArgumentNullException>(() => DecisionRound.DecideAsync(null));
            Assert.Throws<ArgumentException>(() => DecisionRound.DecideAsync(new RoundCall[0]));
            Assert.Throws<ArgumentException>(() => DecisionRound.DecideAsync(new[] { new RoundCall(agents[0], Stimulus), new RoundCall(agents[0], Stimulus) }));
            Assert.Throws<ArgumentException>(() => DecisionRound.DecideAsync(new[] { new RoundCall(agents[0], Stimulus), default(RoundCall) }));
            Assert.Throws<ArgumentException>(() => DecisionRound.DecideAsync(new[]
            {
                new RoundCall(agents[0], Stimulus), new RoundCall(agents[1], Stimulus, new DecideOptions { StimulusLabel = " " }),
            }));
            Assert.That(provider.RequestCount, Is.EqualTo(0));
        }

        // ---- Helpers -------------------------------------------------------------------------

        private readonly List<Action<DecisionFeedEntry>> subscribed = new List<Action<DecisionFeedEntry>>();

        [TearDown]
        public void Unsubscribe()
        {
            foreach (var handler in subscribed)
            {
                DecisionFeed.Recorded -= handler;
            }

            subscribed.Clear();
        }

        // Whether the task ends cancelled, awaited rather than waited on, which would block the main
        // thread its continuations need.
        private static async Task<bool> Cancelled(Task task)
        {
            try
            {
                await task;
                return false;
            }
            catch (OperationCanceledException)
            {
                return true;
            }
        }

        private sealed class World
        {
            public readonly List<string> Log = new List<string>();
            public bool FenmarkTaken;
        }

        // Countries sharing one scripted provider, and so one queue, each remembering on execution.
        private static (ScriptedProvider Provider, World World, Agent[] Agents) Countries(params string[] ids)
        {
            var provider = new ScriptedProvider();
            var world = new World();
            return (provider, world, ids.Select(id => Country(id, provider, world)).ToArray());
        }

        // A country that may recruit, or attack fenmark while nobody has taken it.
        private static Agent Country(string id, ILLMProvider provider, World world)
        {
            var actions = new[]
            {
                new ActionDefinition { Id = "recruit", Description = "Raise soldiers.", RequiresTarget = false },
                new ActionDefinition { Id = "attack", Description = "Attack a province.", RequiresTarget = true },
            };

            var agent = new Agent(new AgentIdentity { Name = id, Role = "a country" }, actions, provider)
            {
                Id = id,
                MemoryCommit = MemoryCommit.OnExecute,
            };
            agent.Bind("recruit", new DelegateActionHandler(_ => true, (_, __) => world.Log.Add(id + ":recruit")));
            agent.Bind("attack", new DelegateActionHandler(_ => !world.FenmarkTaken, (_, d) =>
            {
                world.FenmarkTaken = true;
                world.Log.Add($"{id}:attack {d.TargetId}");
            }));
            agent.Targets.Register("fenmark", new object());
            Assert.That(agent.Memory, Is.InstanceOf<RollingHistory>());
            return agent;
        }

        private static void Script(ScriptedProvider provider, string id, string action, string target = TargetRegistry.NoTarget)
        {
            provider.When(id, Stimulus, ScriptedReply.Decide(action, target));
        }

        private static string Reply(string action, string target = TargetRegistry.NoTarget) =>
            $"{{\"action\":\"{action}\",\"target\":\"{target}\",\"statement\":\"\"}}";

        private static IEnumerable<RoundCall> Calls(IEnumerable<Agent> agents)
        {
            return agents.Select(a => new RoundCall(a, Stimulus)).ToList();
        }

        private static Task<DecisionRound> Decide(IEnumerable<Agent> agents)
        {
            return Fixtures.NotCancelled(DecisionRound.DecideAsync(Calls(agents)));
        }

        private static async Task<T> AsTask<T>(Awaitable<T> awaitable)
        {
            return await awaitable;
        }

        // Every result the feed raises from now until the test ends.
        private List<DecisionFeedEntry> Recorded()
        {
            var seen = new List<DecisionFeedEntry>();
            Action<DecisionFeedEntry> handler = entry =>
            {
                if (entry.Stage == DecisionStage.Decided)
                {
                    seen.Add(entry);
                }
            };
            DecisionFeed.Recorded += handler;
            subscribed.Add(handler);
            return seen;
        }

        private static async Task Until(Func<bool> condition, string what = null)
        {
            for (int i = 0; i < 200 && !condition(); i++)
            {
                await Task.Yield();
            }

            Assert.That(condition(), Is.True, what ?? "The condition never came true.");
        }

        private static int Remembered(Agent agent)
        {
            return ((RollingHistory)agent.Memory).Count;
        }

        private static Exception Record(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (Exception e)
            {
                return e;
            }
        }
    }
}
