using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // The optional proposals helper (#190, Living Vale #16): a decision held for the player instead
    // of carried out, confirmed through Agent.Execute and its re-check, declined, superseded, gone
    // stale or reset, and remembered exactly once. Every await of DecideAsync goes through
    // Fixtures.NotCancelled.
    public sealed class ProposalsTests
    {
        private const string Flooded = "the ford is flooded";

        private static string Reply(string action, string target) =>
            $"{{\"action\":\"{action}\",\"target\":\"{target}\",\"statement\":\"Shall we?\"}}";

        // ---- Hold or submit ------------------------------------------------------------------

        [Test]
        public async Task AHeldProposalIsNotExecuted()
        {
            var (agent, fake, proposals, offered) = Guide();
            var result = await Decide(agent, fake, "offer_route", "route_mill");

            var proposal = proposals.Submit(result, out var outcome);

            Assert.That(proposal, Is.Not.Null);
            Assert.That(outcome, Is.Null, "Nothing was carried out.");
            Assert.That(offered, Is.Empty, "No handler ran.");
            Assert.That(proposal.Status, Is.EqualTo(ProposalStatus.Pending));
            Assert.That(proposals.Pending, Is.EqualTo(new[] { proposal }));
            Assert.That(proposals.Latest, Is.SameAs(proposal));
            Assert.That(result.Telemetry.Execution, Is.Null, "Offered, not accepted: the feed has no Executed record yet.");
            Assert.That(proposal.ActionId, Is.EqualTo("offer_route"));
            Assert.That(proposal.TargetId, Is.EqualTo("route_mill"));
        }

        [Test]
        public async Task SubmitCarriesOutAnActionThatIsNotHeldAtOnce()
        {
            var (agent, fake, proposals, _) = Guide();
            var result = await Decide(agent, fake, "move_to", "well");

            var proposal = proposals.Submit(result, out var outcome);

            Assert.That(proposal, Is.Null);
            Assert.That(outcome.HasValue, Is.True);
            Assert.That(outcome.Value.Status, Is.EqualTo(ExecutionStatus.Done));
            Assert.That(Fixtures.Handler(agent, "move_to").Executions, Has.Count.EqualTo(1));
            Assert.That(proposals.Pending, Is.Empty);
        }

        [Test]
        public async Task NothingIsHeldForNoneOrAPipelineError()
        {
            var (agent, fake, proposals, _) = Guide();
            var none = await Decide(agent, fake, "none", "no_target");

            Assert.Throws<ArgumentException>(() => proposals.Hold(none));
            Assert.That(proposals.Submit(none, out var outcome), Is.Null, "Submit carries it out instead, which does nothing.");
            Assert.That(outcome.Value.Status, Is.EqualTo(ExecutionStatus.NoAction));
            Assert.That(proposals.Pending, Is.Empty);
        }

        // ---- Confirm -------------------------------------------------------------------------

        [Test]
        public async Task ConfirmExecutesOnceAndRefusesASecondClick()
        {
            var (agent, fake, proposals, offered) = Guide();
            var proposal = proposals.Hold(await Decide(agent, fake, "offer_route", "route_mill"));

            Assert.That(proposals.Confirm(proposal, out var first), Is.True);
            Assert.That(first.Status, Is.EqualTo(ExecutionStatus.Done));
            Assert.That(offered, Is.EqualTo(new[] { "route_mill" }));
            Assert.That(proposal.Status, Is.EqualTo(ProposalStatus.Accepted));
            Assert.That(proposal.Outcome.Value.Status, Is.EqualTo(ExecutionStatus.Done));
            Assert.That(proposals.Pending, Is.Empty);

            Assert.That(proposals.Confirm(proposal, out var second), Is.False);
            Assert.That(second.Status, Is.EqualTo(ExecutionStatus.Refused));
            Assert.That(offered, Has.Count.EqualTo(1), "Carried out once.");
            Assert.That(proposal.Result.Telemetry.Execution, Is.EqualTo(ExecutionStatus.Done), "The refused second click does not overwrite what was done.");
        }

        [Test]
        public async Task AClickOnASupersededCardIsRefused()
        {
            var (agent, fake, proposals, offered) = Guide();
            proposals.OnePending = true;
            var old = proposals.Hold(await Decide(agent, fake, "offer_route", "route_mill"));
            var newer = proposals.Hold(await Decide(agent, fake, "offer_route", "route_ford"));

            Assert.That(old.Status, Is.EqualTo(ProposalStatus.Superseded));
            Assert.That(proposals.Pending, Is.EqualTo(new[] { newer }));

            Assert.That(proposals.Confirm(old, out var outcome), Is.False);
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatus.Refused));
            Assert.That(outcome.Reason, Does.Contain("Superseded"));
            Assert.That(offered, Is.Empty, "The old card ran nothing, and certainly not the newer proposal.");
            Assert.That(old.Result.Telemetry.Execution, Is.EqualTo(ExecutionStatus.Refused));

            Assert.That(proposals.Confirm(newer), Is.True);
            Assert.That(offered, Is.EqualTo(new[] { "route_ford" }));
        }

        [Test]
        public async Task WithoutOnePendingEveryProposalWaits()
        {
            var (agent, fake, proposals, _) = Guide();
            var a = proposals.Hold(await Decide(agent, fake, "offer_route", "route_mill"));
            var b = proposals.Hold(await Decide(agent, fake, "offer_route", "route_ford"));

            Assert.That(proposals.Pending, Is.EqualTo(new[] { a, b }));
            Assert.That(a.Number, Is.EqualTo(1));
            Assert.That(b.Number, Is.EqualTo(2));
            Assert.That(proposals.Hold(a.Result), Is.SameAs(a), "Holding the same result again changes nothing.");
            Assert.That(proposals.Pending, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task AProposalTheGameSaysIsStaleIsRefused()
        {
            var (agent, fake, proposals, offered) = Guide();
            int conversation = 1;
            proposals.StillValid = p => (int)p.Correlation == conversation;
            var result = await Fixtures.NotCancelled(Ask(agent, fake, "offer_route", "route_mill", new DecideOptions { Correlation = 1 }));
            var proposal = proposals.Hold(result);

            conversation = 2;
            Assert.That(proposals.Confirm(proposal, out var outcome), Is.False);

            Assert.That(offered, Is.Empty);
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatus.Refused));
            Assert.That(proposal.Status, Is.EqualTo(ProposalStatus.Refused));
            Assert.That(proposal.Reason, Does.Contain("StillValid"));
            Assert.That(proposals.Pending, Is.Empty);
            Assert.That(result.Telemetry.Execution, Is.EqualTo(ExecutionStatus.Refused));
        }

        [Test]
        public async Task AProposalWhoseTargetBecameUnavailableIsRefusedAtConfirmWithTheReason()
        {
            var (agent, fake, proposals, offered, closed) = GuideWithRoutes();
            var proposal = proposals.Hold(await Decide(agent, fake, "offer_route", "route_ford"));

            // The ford floods while the card is on screen.
            closed.Add("route_ford");
            LogAssert.Expect(LogType.Warning, new Regex(Flooded));
            Assert.That(proposals.Confirm(proposal, out var outcome), Is.False);

            Assert.That(offered, Is.Empty, "No handler ran.");
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatus.Refused));
            Assert.That(outcome.Reason, Is.EqualTo($"'offer_route' cannot be done to 'route_ford' right now: {Flooded}"));
            Assert.That(proposal.Status, Is.EqualTo(ProposalStatus.Refused));
            Assert.That(proposal.Reason, Is.EqualTo(outcome.Reason));
            Assert.That(proposal.Outcome.Value.Status, Is.EqualTo(ExecutionStatus.Refused));
            Assert.That(proposals.Pending, Is.Empty);
        }

        [Test]
        public async Task AProposalCannotBeConfirmedTwiceFromInsideItsOwnHandler()
        {
            var fake = new FakeProvider();
            var agent = Agent(fake, out _);
            var proposals = new Proposals(agent, new[] { "offer_route" });
            Proposal proposal = null;
            bool? inner = null;
            var runs = 0;
            agent.Actions.Register(
                new ActionDefinition { Id = "offer_trade", Description = "Offer a trade.", RequiresTarget = false },
                new DelegateActionHandler(_ => true, (_, __) =>
                {
                    runs++;
                    inner = proposals.Confirm(proposal);
                    Assert.That(proposals.Pending, Is.Empty, "The handler sees it gone.");
                }));
            proposal = proposals.Hold(await Decide(agent, fake, "offer_trade", "no_target"));

            Assert.That(proposals.Confirm(proposal), Is.True);
            Assert.That(inner, Is.False);
            Assert.That(runs, Is.EqualTo(1));
        }

        // Codex, round 1 of #236: a guard on the innermost confirmation alone let A's handler confirm
        // B, whose handler confirmed A again.
        [Test]
        public async Task NestedConfirmationsNeverRunAnOuterProposalAgain()
        {
            var fake = new FakeProvider();
            var agent = Agent(fake, out _);
            var proposals = new Proposals(agent);
            Proposal a = null, b = null;
            int aRuns = 0, bRuns = 0;
            bool? again = null;
            agent.Actions.Register(
                new ActionDefinition { Id = "offer_a", Description = "Offer A.", RequiresTarget = false },
                new DelegateActionHandler(_ => true, (_, __) => { if (++aRuns == 1) proposals.Confirm(b); }));
            agent.Actions.Register(
                new ActionDefinition { Id = "offer_b", Description = "Offer B.", RequiresTarget = false },
                new DelegateActionHandler(_ => true, (_, __) => { if (++bRuns == 1) again = proposals.Confirm(a); }));
            a = proposals.Hold(await Decide(agent, fake, "offer_a", "no_target"));
            b = proposals.Hold(await Decide(agent, fake, "offer_b", "no_target"));

            Assert.That(proposals.Confirm(a), Is.True);

            Assert.That(aRuns, Is.EqualTo(1), "A ran once.");
            Assert.That(bRuns, Is.EqualTo(1));
            Assert.That(again, Is.False, "B's handler could not confirm A, which was being confirmed.");
            Assert.That(a.Status, Is.EqualTo(ProposalStatus.Accepted));
            Assert.That(b.Status, Is.EqualTo(ProposalStatus.Accepted));
        }

        // Codex, round 1 of #236: the telemetry guard looked at the proposal, not the shared result.
        [Test]
        public async Task ARefusalNeverOverwritesTheResultsRealExecution()
        {
            var (agent, fake, proposals, offered) = Guide();
            var result = await Decide(agent, fake, "offer_route", "route_mill");
            var old = proposals.Hold(result);
            proposals.Decline(old);
            var current = proposals.Hold(result);
            Assert.That(current, Is.Not.SameAs(old));

            Assert.That(proposals.Confirm(current), Is.True);
            Assert.That(proposals.Confirm(old, out var refused), Is.False);

            Assert.That(refused.Status, Is.EqualTo(ExecutionStatus.Refused));
            Assert.That(result.Telemetry.Execution, Is.EqualTo(ExecutionStatus.Done), "What was done stays recorded.");
            Assert.That(offered, Has.Count.EqualTo(1));
        }

        // Codex, round 1 of #236: StillValid is the game's code, and ran before any re-check.
        [TestCase(true)]
        [TestCase(false)]
        public async Task AProposalSettledFromInsideStillValidDoesNotRun(bool reset)
        {
            var (agent, fake, proposals, offered) = Guide();
            var proposal = proposals.Hold(await Decide(agent, fake, "offer_route", "route_mill"));
            proposals.StillValid = p =>
            {
                if (reset) agent.Reset();
                else proposals.Decline(p);
                return true;
            };

            Assert.That(proposals.Confirm(proposal, out var outcome), Is.False);

            Assert.That(offered, Is.Empty);
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatus.Refused));
            Assert.That(proposal.Status, Is.EqualTo(reset ? ProposalStatus.Reset : ProposalStatus.Declined));
            Assert.That(proposal.Outcome, Is.Null);
        }

        [Test]
        public async Task NothingCanBeHeldFromInsideAReset()
        {
            var (agent, fake, proposals, _) = Guide();
            proposals.Hold(await Decide(agent, fake, "offer_route", "route_mill"));
            var late = await Decide(agent, fake, "offer_route", "route_ford");
            bool tried = false;
            proposals.Changed += _ =>
            {
                if (!tried)
                {
                    tried = true;
                    proposals.Hold(late);
                }
            };

            LogAssert.Expect(LogType.Exception, new Regex("being reset"));
            agent.Reset();

            Assert.That(tried, Is.True);
            Assert.That(proposals.Pending, Is.Empty, "Empty when Reset returns.");
        }

        // Codex, round 2 of #236: a hold from another helper's reset callback moved this helper's
        // watch to the new token before its own callback ran, so its proposal survived the reset.
        [Test]
        public async Task AHoldFromAnotherHelpersResetCallbackLeavesNothingHeld()
        {
            var (agent, fake, first, _) = Guide();
            var second = new Proposals(agent, new[] { "offer_route" });
            var held = first.Hold(await Decide(agent, fake, "offer_route", "route_mill"));
            second.Hold(await Decide(agent, fake, "offer_route", "route_ford"));
            var fresh = await Decide(agent, fake, "offer_route", "route_mill");
            second.Changed += _ => first.Hold(fresh);

            LogAssert.Expect(LogType.Exception, new Regex("being reset"));
            agent.Reset();

            Assert.That(held.Status, Is.EqualTo(ProposalStatus.Reset));
            Assert.That(first.Pending, Is.Empty);
            Assert.That(second.Pending, Is.Empty);
        }

        [Test]
        public async Task AResultFromBeforeAResetIsNotHeldAfterIt()
        {
            var (agent, fake, proposals, _) = Guide();
            agent.MemoryCommit = MemoryCommit.OnExecute;
            var before = await Decide(agent, fake, "offer_route", "route_mill");

            agent.Reset();

            Assert.Throws<ArgumentException>(() => proposals.Hold(before));
            Assert.That(proposals.Pending, Is.Empty);
        }

        [Test]
        public async Task ProposalsRefuseAProposalHeldByAnotherHelper()
        {
            var (agent, fake, proposals, _) = Guide();
            var other = new Proposals(agent);
            var proposal = other.Hold(await Decide(agent, fake, "offer_route", "route_mill"));

            Assert.Throws<ArgumentException>(() => proposals.Confirm(proposal));
            Assert.Throws<ArgumentException>(() => proposals.Decline(proposal));
        }

        // ---- Decline and reset ---------------------------------------------------------------

        [Test]
        public async Task DeclineEndsOneAndDeclineAllEndsEveryOneWithOneChange()
        {
            var (agent, fake, proposals, offered) = Guide();
            var a = proposals.Hold(await Decide(agent, fake, "offer_route", "route_mill"));
            var b = proposals.Hold(await Decide(agent, fake, "offer_route", "route_ford"));
            var c = proposals.Hold(await Decide(agent, fake, "offer_route", "route_mill"));
            int changes = 0;
            proposals.Changed += _ => changes++;

            Assert.That(proposals.Decline(a, "Not today."), Is.True);
            Assert.That(a.Status, Is.EqualTo(ProposalStatus.Declined));
            Assert.That(a.Reason, Is.EqualTo("Not today."));
            Assert.That(proposals.Decline(a), Is.False, "Already declined.");
            Assert.That(changes, Is.EqualTo(1));

            Assert.That(proposals.DeclineAll(), Is.EqualTo(2));
            Assert.That(changes, Is.EqualTo(2), "One change for the lot.");
            Assert.That(b.Status, Is.EqualTo(ProposalStatus.Declined));
            Assert.That(c.Status, Is.EqualTo(ProposalStatus.Declined));
            Assert.That(proposals.Pending, Is.Empty);
            Assert.That(proposals.DeclineAll(), Is.Zero);
            Assert.That(changes, Is.EqualTo(2), "Nothing to decline, nothing changed.");

            Assert.That(proposals.Confirm(b), Is.False);
            Assert.That(offered, Is.Empty);
            Assert.That(b.Result.Telemetry.Execution, Is.EqualTo(ExecutionStatus.Refused), "Only the click after the decline wrote anything.");
        }

        [Test]
        public async Task AResetLeavesNothingHeld()
        {
            var (agent, fake, proposals, offered) = Guide();
            var first = proposals.Hold(await Decide(agent, fake, "offer_route", "route_mill"));
            int changes = 0;
            proposals.Changed += _ => changes++;

            agent.Reset();

            Assert.That(first.Status, Is.EqualTo(ProposalStatus.Reset));
            Assert.That(proposals.Pending, Is.Empty);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(proposals.Confirm(first), Is.False);
            Assert.That(offered, Is.Empty);

            // The helper watches the agent after the reset too.
            var second = proposals.Hold(await Decide(agent, fake, "offer_route", "route_ford"));
            agent.Reset();
            Assert.That(second.Status, Is.EqualTo(ProposalStatus.Reset));
            Assert.That(changes, Is.EqualTo(3), "The hold, then the second reset.");
        }

        [Test]
        public async Task AChangedHandlerThatThrowsDuringAResetIsLoggedAndTheResetCompletes()
        {
            var (agent, fake, proposals, _) = Guide();
            var proposal = proposals.Hold(await Decide(agent, fake, "offer_route", "route_mill"));
            proposals.Changed += _ => throw new InvalidOperationException("the card is gone");

            LogAssert.Expect(LogType.Exception, new Regex("the card is gone"));
            Assert.DoesNotThrow(() => agent.Reset());
            Assert.That(proposal.Status, Is.EqualTo(ProposalStatus.Reset));
        }

        // ---- Memory --------------------------------------------------------------------------

        [TestCase(MemoryCommit.OnDecide, true)]
        [TestCase(MemoryCommit.OnDecide, false)]
        [TestCase(MemoryCommit.OnExecute, true)]
        [TestCase(MemoryCommit.OnExecute, false)]
        public async Task MemoryHoldsTheProposalExactlyOnce(MemoryCommit commit, bool confirmed)
        {
            var (agent, fake, proposals, offered) = Guide();
            agent.MemoryCommit = commit;
            var result = await Decide(agent, fake, "offer_route", "route_mill");
            Assert.That(Remembered(agent), Is.EqualTo(commit == MemoryCommit.OnDecide ? 1 : 0));

            var proposal = proposals.Hold(result);
            Assert.That(Remembered(agent), Is.EqualTo(1), "Remembered once it is held: the agent did propose it.");

            if (confirmed)
            {
                Assert.That(proposals.Confirm(proposal), Is.True);
                Assert.That(offered, Has.Count.EqualTo(1));
            }
            else
            {
                Assert.That(proposals.Decline(proposal), Is.True);
            }

            Assert.That(Remembered(agent), Is.EqualTo(1), "Execute at confirm does not remember it twice, and declining does not forget it.");
            Assert.That(agent.Commit(result), Is.False, "Already settled.");
        }

        // ---- Helpers -------------------------------------------------------------------------

        // A guide who offers routes (held) and walks (not held), with no per-target check.
        private static (Agent Agent, FakeProvider Fake, Proposals Proposals, List<string> Offered) Guide()
        {
            var fake = new FakeProvider();
            var agent = Agent(fake, out var offered);
            return (agent, fake, new Proposals(agent, new[] { "offer_route" }), offered);
        }

        // The same guide whose routes can close: offer_route checks each route per target.
        private static (Agent Agent, FakeProvider Fake, Proposals Proposals, List<string> Offered, HashSet<string> Closed) GuideWithRoutes()
        {
            var fake = new FakeProvider();
            var closed = new HashSet<string>();
            var agent = Agent(fake, out var offered, closed);
            return (agent, fake, new Proposals(agent, new[] { "offer_route" }), offered, closed);
        }

        private static Agent Agent(FakeProvider fake, out List<string> offered, HashSet<string> closed = null)
        {
            var actions = new[]
            {
                new ActionDefinition { Id = "move_to", Description = "Walk somewhere.", RequiresTarget = true },
                new ActionDefinition { Id = "offer_route", Description = "Offer to guide the traveller.", RequiresTarget = true, TargetCategories = new[] { "route" } },
            };

            var log = new List<string>();
            offered = log;
            var offer = new DelegateActionHandler(_ => true, (_, decision) => log.Add(decision.TargetId));
            if (closed != null)
            {
                offer.IsAvailableFor = (_, id) => closed.Contains(id) ? Availability.No(Flooded) : Availability.Yes;
            }

            var agent = new Agent(new AgentIdentity { Name = "Kest", Role = "a guide" }, actions, fake);
            agent.Bind("move_to", new FakeHandler());
            agent.Bind("offer_route", offer);
            agent.Targets.Register("route_mill", new object(), "route");
            agent.Targets.Register("route_ford", new object(), "route");
            agent.Targets.Register("well", new object(), "place");
            Assert.That(agent.Memory, Is.InstanceOf<RollingHistory>());
            return agent;
        }

        private static Task<DecisionResult> Decide(Agent agent, FakeProvider fake, string action, string target)
        {
            return Fixtures.NotCancelled(Ask(agent, fake, action, target, null));
        }

        private static Awaitable<DecisionResult> Ask(Agent agent, FakeProvider fake, string action, string target, DecideOptions options)
        {
            fake.Reply = Reply(action, target);
            return agent.DecideAsync("What now?", options);
        }

        private static int Remembered(Agent agent)
        {
            return ((RollingHistory)agent.Memory).Count;
        }
    }
}
