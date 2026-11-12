using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Providers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Agenerela.Tests
{
    // The scripted provider (#190, G6): replies keyed by agent and stimulus or chosen by a callback,
    // after a latency, and never passing for a model. Every agent here is Fixtures' village guard,
    // the second renamed where a test needs two.
    public sealed class ScriptedProviderTests
    {
        private const string Guard = "Village Guard";
        private const string Captain = "Gate Captain";
        private const string GoToTheTower = "Go to the tower.";

        [TearDown]
        public void TearDown()
        {
            Fixtures.DestroyCreated();
        }

        private static Agent Named(string name, ILLMProvider provider)
        {
            var agent = Fixtures.VillageGuard(provider);
            agent.Identity.Name = name;
            return agent;
        }

        // ---- Never a model --------------------------------------------------------------------

        [Test]
        public async Task TelemetryNamesItScriptedWhereverItWouldNameTheModel()
        {
            // Even a reply copied word for word from a model's: replaying it is still a script.
            var scripted = new ScriptedProvider()
                .Otherwise(ScriptedReply.Raw("{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}"));
            var agent = Fixtures.VillageGuard(scripted);

            var result = await Fixtures.NotCancelled(agent.DecideAsync(GoToTheTower));

            Assert.That(result.Decision.ActionId, Is.EqualTo("move_to"));
            Assert.That(result.Telemetry.ProviderName, Is.EqualTo("scripted"));
            Assert.That(result.Telemetry.Model, Is.EqualTo("scripted"));
            Assert.That(scripted.Name, Is.EqualTo(ScriptedProvider.ProviderName));
        }

        [Test]
        public void NothingOnAReplyCanNameAModel()
        {
            // The guarantee above holds only while nothing can set them: no model, no token counts.
            var settable = typeof(ScriptedReply).GetProperties().Where(p => p.CanWrite).Select(p => p.Name);
            var fields = typeof(ScriptedReply).GetFields(BindingFlags.Instance | BindingFlags.Public).Select(f => f.Name);
            var nameProperty = typeof(ScriptedProvider).GetProperty(nameof(ScriptedProvider.Name));

            Assert.That(settable, Is.Empty);
            Assert.That(fields, Is.Empty);
            Assert.That(typeof(ScriptedReply).GetMember("Model"), Is.Empty);
            Assert.That(nameProperty.CanWrite, Is.False);
        }

        [Test]
        public void ItClaimsOnlyWhatAScriptCanDo()
        {
            var capabilities = new ScriptedProvider().Capabilities;

            Assert.That(capabilities.ReportsTokenProbabilities, Is.False, "A scripted reply carries none unless one is given them.");
            Assert.That(capabilities.SupportsConstrainedDecoding, Is.False, "Nothing constrains what a script says.");
            Assert.That(capabilities.Dialect, Is.EqualTo(SchemaDialect.JsonSchema));
        }

        [Test]
        public async Task GivenOllamasCapabilitiesItReceivesOllamasPrompt()
        {
            var ollamaLike = new ProviderCapabilities { SupportsConstrainedDecoding = true, ReportsTokenProbabilities = true };
            var scripted = new ScriptedProvider(ollamaLike).Otherwise(ScriptedReply.Decide("none"));
            var plain = new ScriptedProvider().Otherwise(ScriptedReply.Decide("none"));

            var ollamas = await Fixtures.NotCancelled(Fixtures.VillageGuard(scripted).DecideAsync(GoToTheTower));
            var its = await Fixtures.NotCancelled(Fixtures.VillageGuard(plain).DecideAsync(GoToTheTower));

            Assert.That(scripted.Capabilities, Is.SameAs(ollamaLike));
            Assert.That(ollamas.Telemetry.TargetRuleSent, Is.False, "The grounding guard stands in for the rule.");
            Assert.That(its.Telemetry.TargetRuleSent, Is.True, "Without token probabilities the prompt carries the rule.");
        }

        // ---- Which reply answers --------------------------------------------------------------

        [Test]
        public async Task EachAgentGetsTheReplyScriptedForItAndTheStimulus()
        {
            var scripted = new ScriptedProvider()
                .When(Guard, GoToTheTower, ScriptedReply.Decide("move_to", "tower", "On my way."))
                .When(Captain, GoToTheTower, ScriptedReply.Decide("none", TargetRegistry.NoTarget, "Not on my watch."));
            var guard = Named(Guard, scripted);
            var captain = Named(Captain, scripted);

            var guards = await Fixtures.NotCancelled(guard.DecideAsync(GoToTheTower));
            var captains = await Fixtures.NotCancelled(captain.DecideAsync(GoToTheTower));

            Assert.That(guards.Decision.ActionId, Is.EqualTo("move_to"));
            Assert.That(guards.Decision.TargetId, Is.EqualTo("tower"));
            Assert.That(captains.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
            Assert.That(captains.Decision.Statement, Is.EqualTo("Not on my watch."));
        }

        [Test]
        public async Task TheMostSpecificScriptAnswers()
        {
            var scripted = new ScriptedProvider()
                .When(Guard, GoToTheTower, ScriptedReply.Decide("none", statement: "agent and stimulus"))
                .When(null, GoToTheTower, ScriptedReply.Decide("none", statement: "any agent, this stimulus"))
                .When(Guard, null, ScriptedReply.Decide("none", statement: "this agent, any stimulus"))
                .Otherwise(ScriptedReply.Decide("none", statement: "otherwise"));
            scripted.Callback = r => r.Stimulus == "Ask the callback." ? ScriptedReply.Decide("none", statement: "callback") : null;
            var guard = Named(Guard, scripted);
            var captain = Named(Captain, scripted);

            async Task<string> Says(Agent agent, string stimulus) =>
                (await Fixtures.NotCancelled(agent.DecideAsync(stimulus))).Decision.Statement;

            Assert.That(await Says(guard, GoToTheTower), Is.EqualTo("agent and stimulus"));
            Assert.That(await Says(captain, GoToTheTower), Is.EqualTo("any agent, this stimulus"));
            Assert.That(await Says(guard, "Ask the callback."), Is.EqualTo("this agent, any stimulus"), "A key comes before the callback.");
            Assert.That(await Says(captain, "Ask the callback."), Is.EqualTo("callback"));
            Assert.That(await Says(captain, "Anything else."), Is.EqualTo("otherwise"), "The callback returned null.");
        }

        // ---- Agents told apart by id (#190, Living Vale #21) ------------------------------------

        [Test]
        public async Task TwoAgentsWithOneNameAreToldApartByTheirIds()
        {
            var scripted = new ScriptedProvider()
                .When("guard_north", GoToTheTower, ScriptedReply.Decide("move_to", "tower", "North goes."))
                .When("guard_south", GoToTheTower, ScriptedReply.Decide("none", statement: "South stays."));
            var north = Named(Guard, scripted);
            north.Id = "guard_north";
            var south = Named(Guard, scripted);
            south.Id = "guard_south";

            var norths = await Fixtures.NotCancelled(north.DecideAsync(GoToTheTower));
            var asked = scripted.LastRequest;
            var souths = await Fixtures.NotCancelled(south.DecideAsync(GoToTheTower));

            Assert.That(norths.Decision.Statement, Is.EqualTo("North goes."));
            Assert.That(souths.Decision.Statement, Is.EqualTo("South stays."));
            Assert.That(asked.AgentId, Is.EqualTo("guard_north"));
            Assert.That(asked.Agent, Is.EqualTo(Guard), "The name is still reported beside the id.");
            Assert.That(scripted.LastRequest.AgentId, Is.EqualTo("guard_south"));
        }

        [Test]
        public async Task AnIdScriptComesBeforeANameScriptWhichStillAnswersTheRest()
        {
            var scripted = new ScriptedProvider()
                .When(Guard, GoToTheTower, ScriptedReply.Decide("none", statement: "by name"))
                .When("guard_north", GoToTheTower, ScriptedReply.Decide("none", statement: "by id"));
            var north = Named(Guard, scripted);
            north.Id = "guard_north";
            var south = Named(Guard, scripted);
            south.Id = "guard_south";
            var plain = Named(Guard, scripted);

            async Task<string> Says(Agent agent) => (await Fixtures.NotCancelled(agent.DecideAsync(GoToTheTower))).Decision.Statement;

            Assert.That(await Says(north), Is.EqualTo("by id"));
            Assert.That(await Says(south), Is.EqualTo("by name"), "An id with no script of its own falls back to the name.");
            Assert.That(await Says(plain), Is.EqualTo("by name"), "An agent with no id is matched by name, as before.");
        }

        [Test]
        public async Task AnIdOnlyAgentIsMatchedByIdWithinEachStage()
        {
            // Agent and any stimulus is a later stage than any agent and this stimulus, for an id as
            // for a name; and an agent with no name is still matched by its id.
            var scripted = new ScriptedProvider()
                .When(null, GoToTheTower, ScriptedReply.Decide("none", statement: "anyone, this stimulus"))
                .When("guard_north", null, ScriptedReply.Decide("none", statement: "north, anything"))
                .Otherwise(ScriptedReply.Decide("none", statement: "otherwise"));
            var north = Named("", scripted);
            north.Id = "guard_north";

            var tower = await Fixtures.NotCancelled(north.DecideAsync(GoToTheTower));
            var other = await Fixtures.NotCancelled(north.DecideAsync("Anything else."));

            Assert.That(tower.Decision.Statement, Is.EqualTo("anyone, this stimulus"));
            Assert.That(other.Decision.Statement, Is.EqualTo("north, anything"));
        }

        [Test]
        public async Task AnIdEqualToTheNameIsOneKeyTakenOnce()
        {
            var scripted = new ScriptedProvider().When(Guard, GoToTheTower,
                ScriptedReply.Decide("none", statement: "one"), ScriptedReply.Decide("none", statement: "two"));
            var guard = Named(Guard, scripted);
            guard.Id = Guard;

            var first = await Fixtures.NotCancelled(guard.DecideAsync(GoToTheTower));

            Assert.That(first.Decision.Statement, Is.EqualTo("one"), "Looked up by id and then by name, it would skip a reply.");
        }

        [Test]
        public async Task AnUnscriptedRequestNamesTheAgentsIdToo()
        {
            var scripted = new ScriptedProvider();
            var guard = Named(Guard, scripted);
            guard.Id = "guard_north";

            var result = await Fixtures.NotCancelled(guard.DecideAsync(GoToTheTower));

            Assert.That(result.Telemetry.ErrorMessage,
                Does.Contain("no reply for agent 'Village Guard' (id 'guard_north') and stimulus 'Go to the tower.'"));
        }

        [Test]
        public async Task WithNoAgentOrNoStimulusTheCallbackStillAnswersBeforeOtherwise()
        {
            // A blank agent or stimulus means "any" in a key, so a request that has none must skip
            // the stages that need one, not land on Otherwise ahead of the callback (Codex, #194).
            var scripted = new ScriptedProvider
            {
                Callback = _ => ScriptedReply.Decide("none", statement: "callback"),
            }.Otherwise(ScriptedReply.Decide("none", statement: "otherwise"));
            var unnamed = Named("", scripted);
            var guard = Named(Guard, scripted);

            var emptyStimulus = await Fixtures.NotCancelled(guard.DecideAsync(""));
            var noName = await Fixtures.NotCancelled(unnamed.DecideAsync(GoToTheTower));
            var byHand = scripted.RequestAsync(new DecisionRequest { Stimulus = "Player: \"Go to the tower.\"" }, CancellationToken.None);

            Assert.That(emptyStimulus.Decision.Statement, Is.EqualTo("callback"), "An empty stimulus.");
            Assert.That(noName.Decision.Statement, Is.EqualTo("callback"), "An agent with no name.");
            Assert.That(byHand.GetAwaiter().GetResult().Text, Is.EqualTo(ScriptedReply.Decide("none", statement: "callback").Text),
                "A request built by hand.");
        }

        [Test]
        public async Task TheStimulusIsMatchedExactlyWithoutItsLabel()
        {
            var scripted = new ScriptedProvider()
                .When(Guard, GoToTheTower, ScriptedReply.Decide("move_to", "tower"))
                .Otherwise(ScriptedReply.Decide("none"));
            var guard = Named(Guard, scripted);

            var labelled = await Fixtures.NotCancelled(guard.DecideAsync(GoToTheTower, new DecideOptions { StimulusLabel = "Captain" }));
            var otherCase = await Fixtures.NotCancelled(guard.DecideAsync("go to the tower."));

            Assert.That(labelled.Decision.ActionId, Is.EqualTo("move_to"), "Any label: the key is the stimulus.");
            Assert.That(otherCase.Decision.ActionId, Is.EqualTo(ActionRegistry.None), "Case-sensitive.");
        }

        [Test]
        public async Task RepliesForOneKeyAnswerInOrderAndTheLastKeepsAnswering()
        {
            var scripted = new ScriptedProvider().When(Guard, GoToTheTower,
                ScriptedReply.Decide("none", statement: "one"),
                ScriptedReply.Decide("none", statement: "two"));
            var guard = Named(Guard, scripted);

            var said = new string[3];
            for (int i = 0; i < said.Length; i++)
            {
                said[i] = (await Fixtures.NotCancelled(guard.DecideAsync(GoToTheTower))).Decision.Statement;
            }

            Assert.That(said, Is.EqualTo(new[] { "one", "two", "two" }));
        }

        [Test]
        public async Task ScriptingAKeyAgainReplacesItAndStartsItOver()
        {
            var scripted = new ScriptedProvider().When(Guard, GoToTheTower,
                ScriptedReply.Decide("none", statement: "old"), ScriptedReply.Decide("none", statement: "older"));
            var guard = Named(Guard, scripted);
            await Fixtures.NotCancelled(guard.DecideAsync(GoToTheTower));

            scripted.When(Guard, GoToTheTower, ScriptedReply.Decide("none", statement: "new"));
            var result = await Fixtures.NotCancelled(guard.DecideAsync(GoToTheTower));

            Assert.That(result.Decision.Statement, Is.EqualTo("new"));
        }

        [Test]
        public async Task TheCallbackSeesWhoAskedWhatAndWhatTheyMayAnswer()
        {
            ScriptedRequest seen = null;
            var scripted = new ScriptedProvider
            {
                Callback = r =>
                {
                    seen = r;
                    // As a game's rule would: answer only with what the agent is offered now.
                    return r.OfferedActions.Contains("follow_player")
                        ? ScriptedReply.Decide("follow_player")
                        : ScriptedReply.Decide("none");
                },
            };
            var guard = Named(Guard, scripted);

            var result = await Fixtures.NotCancelled(guard.DecideAsync("Walk with me.", new DecideOptions { StimulusLabel = "Traveller" }));

            Assert.That(result.Decision.ActionId, Is.EqualTo("follow_player"));
            Assert.That(seen.Agent, Is.EqualTo(Guard));
            Assert.That(seen.Stimulus, Is.EqualTo("Walk with me."));
            Assert.That(seen.Label, Is.EqualTo("Traveller"));
            Assert.That(seen.AgentId, Is.Null, "This agent has no id.");
            Assert.That(seen.IsRefusalLine, Is.False);
            Assert.That(seen.Index, Is.EqualTo(0));
            Assert.That(seen.Request, Is.SameAs(result.Request));
            Assert.That(seen.OfferedActions, Is.EquivalentTo(new[] { "none", "follow_player", "move_to", "attack_target" }),
                "stop_following is masked while the guard is not following.");
            Assert.That(seen.OfferedTargets, Is.EquivalentTo(new[] { "tower", "training_dummy", TargetRegistry.NoTarget }));
            Assert.That(scripted.LastRequest, Is.SameAs(seen));
            Assert.That(scripted.RequestCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ACallbackThatThrowsFailsTheRequest()
        {
            var scripted = new ScriptedProvider { Callback = _ => throw new InvalidOperationException("The script broke.") };
            var guard = Named(Guard, scripted);

            var result = await Fixtures.NotCancelled(guard.DecideAsync(GoToTheTower));

            Assert.That(result.Decision, Is.Null);
            Assert.That(result.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.ProviderFailed));
            Assert.That(result.Telemetry.ErrorMessage, Does.Contain("The script broke."));
        }

        [Test]
        public async Task AnUnscriptedRequestFailsNamingTheAgentAndTheStimulus()
        {
            var scripted = new ScriptedProvider().When(Captain, GoToTheTower, ScriptedReply.Decide("none"));
            var guard = Named(Guard, scripted);

            var result = await Fixtures.NotCancelled(guard.DecideAsync(GoToTheTower));

            Assert.That(result.Decision, Is.Null);
            Assert.That(result.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.ProviderRejected));
            // The provider's own words: the agent's warning around them names the agent anyway.
            Assert.That(result.Telemetry.ErrorMessage,
                Does.Contain("no reply for agent 'Village Guard' and stimulus 'Go to the tower.'"));
            Assert.That(result.Telemetry.ProviderName, Is.EqualTo("scripted"));
        }

        [Test]
        public async Task ClearForgetsEveryScriptButTheCallback()
        {
            var scripted = new ScriptedProvider()
                .When(Guard, GoToTheTower, ScriptedReply.Decide("move_to", "tower"))
                .Otherwise(ScriptedReply.Decide("none"));
            scripted.Clear();
            var guard = Named(Guard, scripted);

            var forgotten = await Fixtures.NotCancelled(guard.DecideAsync(GoToTheTower));
            scripted.Callback = _ => ScriptedReply.Decide("none", statement: "callback");
            scripted.Clear();
            var kept = await Fixtures.NotCancelled(guard.DecideAsync(GoToTheTower));

            Assert.That(forgotten.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.ProviderRejected));
            Assert.That(kept.Decision.Statement, Is.EqualTo("callback"));
        }

        // ---- Refusal lines --------------------------------------------------------------------

        [Test]
        public async Task ARefusalLineIsAnsweredByRefusalLineAndLeavesTheKeysAlone()
        {
            var scripted = new ScriptedProvider
            {
                RefusalLine = ScriptedReply.Statement("I will not go there."),
            }.When(Guard, GoToTheTower,
                ScriptedReply.Decide("move_to", "tower", "On my way."),
                ScriptedReply.Decide("none", statement: "second"));
            var guard = Named(Guard, scripted);
            guard.Guards.Add(FakeGuard.Rewriting("Stopper"));

            var stopped = await Fixtures.NotCancelled(guard.DecideAsync(GoToTheTower, new DecideOptions { AskForRefusalLine = true }));
            guard.Guards.Clear();
            var next = await Fixtures.NotCancelled(guard.DecideAsync(GoToTheTower));

            Assert.That(scripted.RequestCount, Is.EqualTo(3), "The decision, its refusal line, then the next decision.");
            Assert.That(stopped.Decision.Statement, Is.EqualTo("I will not go there."));
            Assert.That(stopped.Telemetry.RefusalLineError, Is.Null);
            Assert.That(next.Decision.Statement, Is.EqualTo("second"), "The refusal line did not take the key's second reply.");
        }

        [Test]
        public async Task TheCallbackIsToldARequestIsARefusalLine()
        {
            ScriptedRequest line = null;
            var scripted = new ScriptedProvider
            {
                Callback = r =>
                {
                    if (!r.IsRefusalLine)
                    {
                        return null;
                    }

                    line = r;
                    return ScriptedReply.Statement("Not that.");
                },
            }.Otherwise(ScriptedReply.Decide("move_to", "tower"));
            var guard = Named(Guard, scripted);
            guard.Guards.Add(FakeGuard.Rewriting("Stopper"));

            var stopped = await Fixtures.NotCancelled(guard.DecideAsync(GoToTheTower, new DecideOptions { AskForRefusalLine = true }));

            Assert.That(stopped.Decision.Statement, Is.EqualTo("Not that."));
            Assert.That(line.Agent, Is.EqualTo(Guard));
            Assert.That(line.Stimulus, Is.EqualTo(GoToTheTower), "The decision's stimulus, not the refusal request's wording.");
            Assert.That(line.OfferedActions, Is.Empty);
        }

        [Test]
        public async Task WithNoRefusalLineTheDecisionStandsWithoutOne()
        {
            var scripted = new ScriptedProvider().Otherwise(ScriptedReply.Decide("move_to", "tower", "On my way."));
            var guard = Named(Guard, scripted);
            guard.Guards.Add(FakeGuard.Rewriting("Stopper"));

            var stopped = await Fixtures.NotCancelled(guard.DecideAsync(GoToTheTower, new DecideOptions { AskForRefusalLine = true }));

            Assert.That(stopped.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
            Assert.That(stopped.Decision.Statement, Is.Empty);
            Assert.That(stopped.Telemetry.RefusalLineError, Is.EqualTo(PipelineErrorKind.ProviderRejected),
                "Otherwise answers decisions, never refusal lines.");
        }

        // ---- Latency, failure and cancellation ------------------------------------------------

        [Test]
        public async Task AReplyArrivesAfterItsLatencyOnTheMainThreadAndTelemetryRecordsIt()
        {
            Assert.That(SynchronizationContext.Current, Is.Not.Null, "The test needs Unity's main-thread context to prove anything.");
            int main = Thread.CurrentThread.ManagedThreadId;
            int? answeredOn = null;
            var scripted = new ScriptedProvider { Callback = _ => ScriptedReply.Decide("move_to", "tower").After(0.3f) };
            var guard = Named(Guard, scripted);

            var pending = guard.DecideAsync(GoToTheTower);
            Assert.That(pending.GetAwaiter().IsCompleted, Is.False, "It has not had its latency yet.");
            var result = await Fixtures.NotCancelled(pending);
            answeredOn = Thread.CurrentThread.ManagedThreadId;

            Assert.That(result.Decision.ActionId, Is.EqualTo("move_to"));
            Assert.That(result.Telemetry.LatencySeconds, Is.GreaterThanOrEqualTo(0.29f));
            Assert.That(result.Telemetry.LatencySeconds, Is.LessThan(2f));
            Assert.That(result.Reply.LatencySeconds, Is.GreaterThanOrEqualTo(0.29f), "The provider measures it too.");
            Assert.That(answeredOn, Is.EqualTo(main));
        }

        [Test]
        public async Task AReplyWithNoLatencyAnswersWithinTheCall()
        {
            var scripted = new ScriptedProvider().Otherwise(ScriptedReply.Decide("none"));
            var guard = Named(Guard, scripted);

            var pending = guard.DecideAsync(GoToTheTower);

            Assert.That(pending.GetAwaiter().IsCompleted, Is.True);
            var result = await Fixtures.NotCancelled(pending);
            Assert.That(result.Telemetry.LatencySeconds, Is.LessThan(0.1f));
        }

        [Test]
        public async Task CancellingDuringTheLatencyEndsTheDecisionCancelledAtOnce()
        {
            var scripted = new ScriptedProvider().Otherwise(ScriptedReply.Decide("none").After(30f));
            var guard = Named(Guard, scripted);
            using (var cts = new CancellationTokenSource())
            {
                var pending = guard.DecideAsync(GoToTheTower, null, cts.Token);
                var clock = System.Diagnostics.Stopwatch.StartNew();
                cts.CancelAfter(50);

                Assert.That(await EndsCancelled(pending), Is.True);
                Assert.That(clock.Elapsed.TotalSeconds, Is.LessThan(5), "Cancelling ends the wait.");
            }
        }

        [Test]
        public async Task ANeverReplyWaitsUntilItIsCancelled()
        {
            var scripted = new ScriptedProvider().Otherwise(ScriptedReply.Never());
            var guard = Named(Guard, scripted);
            using (var cts = new CancellationTokenSource())
            {
                var pending = guard.DecideAsync(GoToTheTower, null, cts.Token);
                await Task.Delay(100);
                Assert.That(pending.GetAwaiter().IsCompleted, Is.False, "It never answers on its own.");

                cts.Cancel();

                Assert.That(await EndsCancelled(pending), Is.True);
            }
        }

        [Test]
        public async Task AFailingReplyFailsAsAProviderDoesAfterItsLatency()
        {
            var scripted = new ScriptedProvider().Otherwise(ScriptedReply.Fail(ProviderErrorKind.Unreachable).After(0.2f));
            var guard = Named(Guard, scripted);

            var result = await Fixtures.NotCancelled(guard.DecideAsync(GoToTheTower));

            Assert.That(result.Decision, Is.Null);
            Assert.That(result.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.ProviderUnreachable));
            Assert.That(result.Telemetry.ProviderName, Is.EqualTo("scripted"));
            Assert.That(result.Telemetry.LatencySeconds, Is.GreaterThanOrEqualTo(0.19f));
        }

        [Test]
        public async Task AnUnreadableReplyReachesTheAgentAsSent()
        {
            var scripted = new ScriptedProvider().Otherwise(ScriptedReply.Raw("Sure, I'll head to the tower!"));
            var guard = Named(Guard, scripted);

            var result = await Fixtures.NotCancelled(guard.DecideAsync(GoToTheTower));

            Assert.That(result.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.UnreadableReply));
            Assert.That(result.Reply.Text, Is.EqualTo("Sure, I'll head to the tower!"));
        }

        [Test]
        public async Task HoldKeepsRepliesBackUntilReleasedOldestFirst()
        {
            var scripted = new ScriptedProvider
            {
                Hold = true,
                Callback = r => ScriptedReply.Decide("none", statement: r.Stimulus),
            };
            // Two agents on one provider share its queue, so let two requests through it at once.
            RequestScheduler.For(scripted).MaxInFlight = 2;
            var guard = Named(Guard, scripted);
            var captain = Named(Captain, scripted);

            var first = guard.DecideAsync("first");
            var second = captain.DecideAsync("second");
            Assert.That(scripted.HeldCount, Is.EqualTo(2));

            scripted.Release();
            var firstResult = await Fixtures.NotCancelled(first);
            Assert.That(second.GetAwaiter().IsCompleted, Is.False, "Released one at a time.");
            Assert.That(scripted.HeldCount, Is.EqualTo(1));

            scripted.Release();
            var secondResult = await Fixtures.NotCancelled(second);

            Assert.That(firstResult.Decision.Statement, Is.EqualTo("first"), "Oldest first.");
            Assert.That(secondResult.Decision.Statement, Is.EqualTo("second"));
            Assert.That(scripted.HeldCount, Is.Zero);
            Assert.Throws<InvalidOperationException>(scripted.Release, "Nothing is held.");
        }

        [Test]
        public async Task CancellingAHeldRequestEndsItAndReleasesNothing()
        {
            var scripted = new ScriptedProvider { Hold = true }.Otherwise(ScriptedReply.Decide("none"));
            var guard = Named(Guard, scripted);
            using (var cts = new CancellationTokenSource())
            {
                var pending = guard.DecideAsync(GoToTheTower, null, cts.Token);
                Assert.That(scripted.HeldCount, Is.EqualTo(1));

                cts.Cancel();

                Assert.That(await EndsCancelled(pending), Is.True);
                Assert.That(scripted.HeldCount, Is.Zero);
            }
        }

        // ---- The replies themselves -----------------------------------------------------------

        [Test]
        public void ADecisionIsTheSchemasObjectInFieldOrderWithItsStatementEscaped()
        {
            const string statement = "He said \"halt\",\nthen left \\ ran.";

            var reply = ScriptedReply.Decide("move_to", "tower", statement);
            var parsed = JObject.Parse(reply.Text);

            Assert.That(parsed.Properties().Select(p => p.Name), Is.EqualTo(new[] { "action", "target", "statement" }),
                "action, then target, then the free text (AGENTS.md, settled rules).");
            Assert.That((string)parsed["action"], Is.EqualTo("move_to"));
            Assert.That((string)parsed["target"], Is.EqualTo("tower"));
            Assert.That((string)parsed["statement"], Is.EqualTo(statement));
            Assert.That(reply.Text, Does.Not.Contain("\n"), "One line, as a model's reply would be.");
            Assert.That(ScriptedReply.Decide("none").Text, Is.EqualTo("{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"\"}"));
            Assert.That(ScriptedReply.Statement("Not that.").Text, Is.EqualTo("{\"statement\":\"Not that.\"}"));
        }

        [Test]
        public void AfterCopiesTheReplyAndRefusesALatencyOutOfRange()
        {
            var once = ScriptedReply.Decide("none");
            var later = once.After(1.5f);

            Assert.That(once.LatencySeconds, Is.Zero, "A reply is never changed in place.");
            Assert.That(later.LatencySeconds, Is.EqualTo(1.5f));
            Assert.That(later.Text, Is.EqualTo(once.Text));
            Assert.Throws<ArgumentOutOfRangeException>(() => once.After(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => once.After(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => once.After(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => once.After(ScriptedReply.MaxLatencySeconds + 1f));
        }

        [Test]
        public void WhenRefusesNoReplies()
        {
            var scripted = new ScriptedProvider();

            Assert.Throws<ArgumentException>(() => scripted.When(Guard, GoToTheTower));
            Assert.Throws<ArgumentNullException>(() => scripted.When(Guard, GoToTheTower, (ScriptedReply[])null));
            Assert.Throws<ArgumentNullException>(() => scripted.When(Guard, GoToTheTower, ScriptedReply.Decide("none"), null));
            Assert.Throws<ArgumentNullException>(() => ScriptedReply.Decide(null));
        }

        [Test]
        public async Task TokenProbabilitiesReachTheGroundingGuard()
        {
            var tokens = Tokens.Reply("move_to", Tokens.T("tower", -0.01, ("tower", -0.01)));
            var scripted = new ScriptedProvider().Otherwise(ScriptedReply.Raw(Tokens.Join(tokens)).WithTokens(tokens));
            var guard = Named(Guard, scripted);

            var result = await Fixtures.NotCancelled(guard.DecideAsync(GoToTheTower));

            Assert.That(result.Reply.TokenProbabilities, Is.SameAs(tokens));
            Assert.That(result.Telemetry.TargetGroundingStatus, Is.EqualTo(GroundingStatus.Scored));
        }

        // ---- What the request carries ---------------------------------------------------------

        [Test]
        public void WhoAskedAndWhatNeverBecomePublicFieldsOfTheRequest()
        {
            // A provider must not branch on who is asking (DR-008), so only the framework sees them.
            var request = typeof(DecisionRequest);

            foreach (var name in new[] { "AgentName", "StimulusText", "StimulusLabel" })
            {
                var field = request.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, name);
                Assert.That(field.IsAssembly, Is.True, name + " must stay internal.");
            }
        }

        [Test]
        public void ARequestBuiltByHandHasNoAgentAndTakesOnlyTheFallback()
        {
            // Its wording names the guard and the stimulus, but the script never reads the wording.
            var scripted = new ScriptedProvider()
                .When(Guard, null, ScriptedReply.Decide("move_to", "tower"))
                .When(null, GoToTheTower, ScriptedReply.Decide("move_to", "tower"))
                .Otherwise(ScriptedReply.Decide("none"));
            var byHand = new DecisionRequest { SystemPrompt = "You are Village Guard.", Stimulus = "Player: \"Go to the tower.\"" };

            var reply = scripted.RequestAsync(byHand, CancellationToken.None);

            Assert.That(reply.GetAwaiter().IsCompleted, Is.True);
            Assert.That(reply.GetAwaiter().GetResult().Text, Is.EqualTo(ScriptedReply.Decide("none").Text));
            Assert.That(scripted.LastRequest.Agent, Is.Null);
            Assert.That(scripted.LastRequest.Stimulus, Is.Null);
            Assert.That(scripted.LastRequest.IsRefusalLine, Is.False, "With no schema, nothing says it is one.");
        }

        private static async Task<bool> EndsCancelled(Awaitable<DecisionResult> pending)
        {
            try
            {
                await pending;
                return false;
            }
            catch (OperationCanceledException)
            {
                return true;
            }
        }
    }
}
