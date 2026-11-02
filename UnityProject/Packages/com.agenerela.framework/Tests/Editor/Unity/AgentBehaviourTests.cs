using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Agenerela.Tests
{
    public class AgentBehaviourTests
    {
        // Far from anything a scene open in the editor might hold, as in TargetSourceTests.
        private static readonly Vector3 Here = new Vector3(20000f, 0f, 20000f);

        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";
        private const string GoToTheBridge = "{\"action\":\"move_to\",\"target\":\"bridge\",\"statement\":\"On my way.\"}";

        private readonly List<Object> created = new List<Object>();

        /// <summary>A guard's own code: the [AgentAction] methods of the component #93 registers.</summary>
        public sealed class GuardMoves : MonoBehaviour
        {
            public Transform WentTo;

            [AgentAction("move_to", "The attribute's own wording.", RequiresTarget = true)]
            private void MoveTo(Transform place)
            {
                WentTo = place;
            }
        }

        /// <summary>A second component with actions, on the same GameObject.</summary>
        public sealed class GuardFollows : MonoBehaviour
        {
            [AgentAction("follow_player", "Walk alongside the player.")]
            private void Follow()
            {
            }
        }

        /// <summary>A component with no actions, which registering must skip rather than refuse.</summary>
        public sealed class NoActions : MonoBehaviour
        {
        }

        /// <summary>A component whose [AgentAction] method can never be called.</summary>
        public sealed class MalformedActions : MonoBehaviour
        {
            [AgentAction("move_to", "Two parameters no decision can fill.", RequiresTarget = true)]
            private void MoveTo(Transform place, Transform other)
            {
            }
        }

        /// <summary>A scene handler for an asset-authored action, for a row of Handlers.</summary>
        public sealed class AttackHandler : ActionHandlerBehaviour
        {
            public override bool IsAvailable(AgentContext ctx)
            {
                return true;
            }

            public override void Execute(AgentContext ctx, AgentDecision decision)
            {
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created)
            {
                Object.DestroyImmediate(obj);
            }

            created.Clear();
            Fixtures.DestroyCreated();
            AgenerelaSettings.Override = null;
        }

        // ---- Building the agent ---------------------------------------------------------------

        [Test]
        public async Task TheComponentsOwnConfigWinsOverTheProjectDefault()
        {
            var own = Config("OwnConfig", GoToTheTower);
            var fallback = Config("DefaultConfig", GoToTheTower);
            UseDefault(fallback);
            var guard = Guard(Profile("move_to"), own);

            var result = await Fixtures.NotCancelled(guard.DecideAsync("Go to the tower."));

            Assert.That(Fake(own).Requests, Has.Count.EqualTo(1));
            Assert.That(Fake(fallback).Requests, Is.Empty);
            Assert.That(result.Telemetry.ProviderConfig, Is.EqualTo("OwnConfig"));
            Assert.That(guard.Agent.Identity, Is.SameAs(guard.Profile.Identity));
        }

        [Test]
        public async Task AnEmptyProviderFieldUsesTheProjectDefault()
        {
            var fallback = Config("DefaultConfig", GoToTheTower);
            UseDefault(fallback);
            var guard = Guard(Profile("move_to"), null);

            var result = await Fixtures.NotCancelled(guard.DecideAsync("Go to the tower."));

            Assert.That(Fake(fallback).Requests, Has.Count.EqualTo(1));
            Assert.That(result.Telemetry.ProviderConfig, Is.EqualTo("DefaultConfig"));
        }

        [Test]
        public void NoProviderAnywhereSaysHowToMakeOne()
        {
            UseDefault(null);
            var guard = Guard(Profile("move_to"), null);

            var thrown = Assert.Throws<InvalidOperationException>(() => _ = guard.Agent);

            Assert.That(thrown.Message, Does.Contain(guard.name));
            Assert.That(thrown.Message, Does.Contain(ProviderConfig.NoConfigMessage));
        }

        [Test]
        public void AwakeBuildsTheAgentAndLaterReadsReturnThatOne()
        {
            var config = Config();
            var guard = Guard(Profile("move_to"), config);
            Assert.That(config.Builds, Is.EqualTo(0));

            InvokePrivate(guard, "Awake");

            Assert.That(config.Builds, Is.EqualTo(1), "Awake built the agent, not the first read.");
            var agent = guard.Agent;
            Assert.That(guard.Agent, Is.SameAs(agent));
            InvokePrivate(guard, "Awake");
            Assert.That(guard.Agent, Is.SameAs(agent), "A later Awake does not build another.");
        }

        [Test]
        public void TheFirstReadBuildsTheAgentWhenAwakeHasNotRun()
        {
            var guard = Guard(Profile("move_to"), Config());

            Assert.That(guard.Agent, Is.Not.Null);
            Assert.That(guard.Agent.Actions.TryGet("move_to", out _, out _), Is.False, "Nothing binds it.");
        }

        [Test]
        public void AwakeLogsWhatIsWrongAsAnErrorAndDoesNotThrow()
        {
            UseDefault(null);
            var guard = Guard(Profile("move_to"), null);
            LogAssert.Expect(LogType.Error, new Regex("could not build its agent.*No provider config"));

            InvokePrivate(guard, "Awake");

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AwakeStaysSilentWithNoProfileSoCodeCanSetOneAfterAddingTheComponent()
        {
            var guard = Guard(null, Config());

            InvokePrivate(guard, "Awake");
            LogAssert.NoUnexpectedReceived();

            var thrown = Assert.Throws<InvalidOperationException>(() => _ = guard.Agent);
            Assert.That(thrown.Message, Does.Contain("no Profile"));

            guard.Profile = Profile("move_to");
            Assert.That(guard.Agent.Identity, Is.SameAs(guard.Profile.Identity));
        }

        [Test]
        public void AFailedBuildKeepsNothingAndTheNextReadTriesAgain()
        {
            var guard = Guard(Profile("attack_target"), Config());
            guard.Handlers.Add(new AgentBehaviour.ActionHandlerBinding { ActionId = "attack_target" });

            Assert.Throws<InvalidOperationException>(() => _ = guard.Agent);

            guard.Handlers[0] = new AgentBehaviour.ActionHandlerBinding
            {
                ActionId = "attack_target",
                Handler = guard.gameObject.AddComponent<AttackHandler>(),
            };

            Assert.That(guard.Agent.Actions.TryGet("attack_target", out _, out _), Is.True);
        }

        // ---- Actions --------------------------------------------------------------------------

        [Test]
        public void AgentActionMethodsOfEveryComponentOnTheGameObjectAreRegistered()
        {
            var guard = Guard(Profile("move_to", "follow_player"), Config());
            var moves = guard.gameObject.AddComponent<GuardMoves>();
            guard.gameObject.AddComponent<NoActions>();
            guard.gameObject.AddComponent<GuardFollows>();

            var actions = guard.Agent.Actions;

            Assert.That(actions.TryGet("move_to", out _, out var moveTo), Is.True);
            Assert.That(actions.TryGet("follow_player", out _, out _), Is.True);

            var target = NewObject("Place", Here).transform;
            moveTo.Execute(Context(guard.Agent, target), new AgentDecision("move_to", "place", ""));
            Assert.That(moves.WentTo, Is.SameAs(target), "The handler is the method on the component that declares it.");
        }

        [Test]
        public void AMethodOnAnotherGameObjectIsNotRegistered()
        {
            var guard = Guard(Profile("move_to"), Config());
            NewObject("Other", Here).AddComponent<GuardMoves>();

            Assert.That(guard.Agent.Actions.TryGet("move_to", out _, out _), Is.False);
        }

        [Test]
        public void TheProfilesAssetWinsTheDefinitionOfAMethodsAction()
        {
            var profile = Profile("move_to");
            var guard = Guard(profile, Config());
            guard.gameObject.AddComponent<GuardMoves>();

            guard.Agent.Actions.TryGet("move_to", out var definition, out _);

            Assert.That(definition, Is.SameAs(profile.Actions[0].Action));
        }

        [Test]
        public void ListedHandlersAreBoundToTheProfilesActions()
        {
            var profile = Profile("attack_target");
            var guard = Guard(profile, Config());
            var handler = guard.gameObject.AddComponent<AttackHandler>();
            guard.Handlers.Add(new AgentBehaviour.ActionHandlerBinding { ActionId = "attack_target", Handler = handler });

            Assert.That(guard.Agent.Actions.TryGet("attack_target", out var definition, out var bound), Is.True);
            Assert.That(bound, Is.SameAs(handler));
            Assert.That(definition, Is.SameAs(profile.Actions[0].Action));
        }

        [Test]
        public void ARowForAnActionThatAlreadyHasAMethodFailsTheBuild()
        {
            var guard = Guard(Profile("move_to"), Config());
            guard.gameObject.AddComponent<GuardMoves>();
            guard.Handlers.Add(new AgentBehaviour.ActionHandlerBinding
            {
                ActionId = "move_to",
                Handler = guard.gameObject.AddComponent<AttackHandler>(),
            });

            var thrown = Assert.Throws<InvalidOperationException>(() => _ = guard.Agent);
            Assert.That(thrown.Message, Does.Contain("move_to"));
        }

        // ---- Targets and observations ---------------------------------------------------------

        [Test]
        public async Task TargetsComeFromAroundThisTransformAndReachTheMethod()
        {
            var config = Config(reply: GoToTheTower);
            var guard = Guard(Profile("move_to"), config);
            var moves = guard.gameObject.AddComponent<GuardMoves>();
            var tower = Target("tower", Here + new Vector3(5f, 0f, 0f));

            var result = await Fixtures.NotCancelled(guard.DecideAsync("Go to the tower."));

            Assert.That(result.Decision.TargetId, Is.EqualTo("tower"));
            Assert.That(guard.Agent.Execute(result), Is.True);
            Assert.That(moves.WentTo, Is.SameAs(tower.transform));
        }

        [Test]
        public async Task ATargetableOutOfRangeOfThisTransformIsNotOffered()
        {
            var config = Config(reply: GoToTheBridge);
            var guard = Guard(Profile("move_to"), config);
            guard.gameObject.AddComponent<GuardMoves>();
            Target("bridge", Here + new Vector3(500f, 0f, 0f));
            LogAssert.Expect(LogType.Warning, new Regex("rejected 'move_to' on 'bridge'"));

            var result = await Fixtures.NotCancelled(guard.DecideAsync("Go to the bridge."));

            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
        }

        [Test]
        public void AnOriginAlreadySetIsKept()
        {
            var elsewhere = NewObject("Elsewhere", Here).transform;
            var guard = Guard(Profile("move_to"), Config());
            guard.Targets.Origin = elsewhere;
            guard.Observations.Origin = elsewhere;

            _ = guard.Agent;

            Assert.That(guard.Targets.Origin, Is.SameAs(elsewhere));
            Assert.That(guard.Observations.Origin, Is.SameAs(elsewhere));
        }

        [Test]
        public void TheProximitySourcesAreThisComponentsAndAreOnByDefault()
        {
            var guard = Guard(Profile("move_to"), Config());

            Assert.That(guard.Agent.TargetSources, Is.EqualTo(new ITargetSource[] { guard.Targets }));
            Assert.That(guard.Agent.ObservationSources, Is.EqualTo(new IObservationSource[] { guard.Observations }));
            Assert.That(guard.Targets.Origin, Is.SameAs(guard.transform));
            Assert.That(guard.Observations.Origin, Is.SameAs(guard.transform));
        }

        [Test]
        public void UntickingTheBoolsLeavesTheProximitySourcesOut()
        {
            var guard = Guard(Profile("move_to"), Config());
            guard.FindTargetsNearby = false;
            guard.NoticeNearby = false;

            Assert.That(guard.Agent.TargetSources, Is.Empty);
            Assert.That(guard.Agent.ObservationSources, Is.Empty);
        }

        [Test]
        public async Task DescriptionsAroundThisTransformReachTheRequest()
        {
            var config = Config(reply: GoToTheTower);
            var guard = Guard(Profile("move_to"), config);
            Target("tower", Here + new Vector3(5f, 0f, 0f), "The watchtower door stands open.");
            Target("far_well", Here + new Vector3(500f, 0f, 0f), "The far well has run dry.");

            await Fixtures.NotCancelled(guard.DecideAsync("Anything to report?"));

            Assert.That(Fake(config).LastRequest.Observations, Does.Contain("The watchtower door stands open."));
            Assert.That(Fake(config).LastRequest.Observations, Does.Not.Contain("far well"));
        }

        [Test]
        public async Task TheOptionsReachTheAgent()
        {
            var config = Config(reply: GoToTheTower);
            var guard = Guard(Profile("move_to"), config);

            await Fixtures.NotCancelled(guard.DecideAsync("Smoke over the north field.", new DecideOptions { StimulusLabel = "Event" }));

            Assert.That(Fake(config).LastRequest.Stimulus, Does.Contain("Event"));
        }

        [Test]
        public async Task ADecisionCreatesNoManagerObjectInTheScene()
        {
            var guard = Guard(Profile("move_to"), Config(reply: GoToTheTower));
            int before = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

            await Fixtures.NotCancelled(guard.DecideAsync("Go to the tower."));

            Assert.That(Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(before));
        }

        // ---- Destruction ----------------------------------------------------------------------

        // A real destruction ending the decision is in AgentBehaviourPlayModeTests: DestroyImmediate
        // in EditMode does not raise destroyCancellationToken. This is the half EditMode can see.
        [Test]
        public void TheProviderIsSentTheTokenThatDestroyingTheGameObjectCancels()
        {
            var config = Config(reply: GoToTheTower);
            Fake(config).Hold = true;
            var guard = Guard(Profile("move_to"), config);

            _ = guard.DecideAsync("Go to the tower.");

            Assert.That(Fake(config).LastToken, Is.EqualTo(guard.destroyCancellationToken));
        }

        [Test]
        public async Task ACallAfterTheDestructionEndsCancelledAndSendsNothing()
        {
            var config = Config(reply: GoToTheTower);
            var guard = Guard(Profile("move_to"), config);
            _ = guard.Agent;
            Object.DestroyImmediate(guard.gameObject);

            bool cancelled = false;
            try
            {
                await guard.DecideAsync("Go to the tower.");
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            Assert.That(cancelled, Is.True);
            Assert.That(Fake(config).Requests, Is.Empty);
        }

        [Test]
        public void ReadingAnAgentNeverBuiltAfterTheDestructionSaysSo()
        {
            var guard = Guard(Profile("move_to"), Config());
            Object.DestroyImmediate(guard.gameObject);

            var thrown = Assert.Throws<InvalidOperationException>(() => _ = guard.Agent);
            Assert.That(thrown.Message, Does.Contain("destroyed"));
        }

        // ---- Inspector warnings ---------------------------------------------------------------

        [Test]
        public void ACompleteSetupHasNoProblem()
        {
            var guard = Guard(Profile("move_to", "attack_target"), Config());
            guard.gameObject.AddComponent<GuardMoves>();
            guard.Handlers.Add(new AgentBehaviour.ActionHandlerBinding
            {
                ActionId = "attack_target",
                Handler = guard.gameObject.AddComponent<AttackHandler>(),
            });

            Assert.That(guard.Validate(out string problem), Is.True, problem);
            guard.WarnIfInvalid();
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AMissingProfileWarnsInTheInspector()
        {
            var guard = Guard(null, Config());

            Assert.That(guard.Validate(out string problem), Is.False);
            Assert.That(problem, Does.Contain("Profile is empty"));

            LogAssert.Expect(LogType.Warning, new Regex("Guard: Profile is empty"));
            guard.WarnIfInvalid();
        }

        [Test]
        public void AnActionWithNoHandlerWarnsInTheInspector()
        {
            var guard = Guard(Profile("move_to", "attack_target", "follow_player"), Config());
            guard.gameObject.AddComponent<GuardMoves>();

            Assert.That(guard.Validate(out string problem), Is.False);
            Assert.That(problem, Does.Contain("follow_player, attack_target"));
            Assert.That(problem, Does.Not.Contain("move_to"), "A method is a handler.");

            LogAssert.Expect(LogType.Warning, new Regex("No handler for follow_player, attack_target"));
            guard.WarnIfInvalid();
        }

        [Test]
        public void TwoComponentsDeclaringOneActionWarnInTheInspector()
        {
            var guard = Guard(Profile("move_to"), Config());
            guard.gameObject.AddComponent<GuardMoves>();
            guard.gameObject.AddComponent<GuardMoves>();

            Assert.That(guard.Validate(out string problem), Is.False);
            Assert.That(problem, Does.Contain("also declares"));
            Assert.Throws<InvalidOperationException>(() => _ = guard.Agent, "Build refuses what Validate warned about.");
        }

        // Unity may call OnValidate on its loading thread, so nothing scene-related happens in it.
        [UnityTest]
        public IEnumerator OnValidateChecksOnALaterEditorTick()
        {
            var guard = Guard(null, Config());

            InvokePrivate(guard, "OnValidate");
            LogAssert.NoUnexpectedReceived();

            LogAssert.Expect(LogType.Warning, new Regex("Guard: Profile is empty"));
            yield return NextDelayCall();
        }

        [UnityTest]
        public IEnumerator AGuardDestroyedBeforeItsCheckComesIsSkipped()
        {
            var guard = Guard(null, Config());
            InvokePrivate(guard, "OnValidate");
            Object.DestroyImmediate(guard.gameObject);

            yield return NextDelayCall();

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ARowThatCannotBeBoundWarnsInTheInspector()
        {
            var guard = Guard(Profile("move_to", "attack_target"), Config());
            guard.gameObject.AddComponent<GuardMoves>();
            var handler = guard.gameObject.AddComponent<AttackHandler>();

            guard.Handlers.Add(new AgentBehaviour.ActionHandlerBinding { ActionId = "attack_target" });
            Assert.That(guard.Validate(out string problem), Is.False);
            Assert.That(problem, Does.Contain("has no handler"));

            guard.Handlers[0] = new AgentBehaviour.ActionHandlerBinding { ActionId = "not_in_profile", Handler = handler };
            Assert.That(guard.Validate(out problem), Is.False);
            Assert.That(problem, Does.Contain("does not list"));

            guard.Handlers[0] = new AgentBehaviour.ActionHandlerBinding { ActionId = "move_to", Handler = handler };
            Assert.That(guard.Validate(out problem), Is.False);
            Assert.That(problem, Does.Contain("already has a handler"));
        }

        [Test]
        public void AMalformedMethodWarnsInTheInspectorRatherThanThrowing()
        {
            var guard = Guard(Profile("move_to"), Config());
            guard.gameObject.AddComponent<MalformedActions>();

            Assert.That(guard.Validate(out string problem), Is.False);
            Assert.That(problem, Does.Contain(nameof(MalformedActions)));
        }

        // ---- Helpers --------------------------------------------------------------------------

        // Until the calls queued so far have run: delayCall runs them in order, and any number of
        // yields can pass before it does.
        private static IEnumerator NextDelayCall()
        {
            bool ran = false;
            UnityEditor.EditorApplication.delayCall += () => ran = true;

            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!ran)
            {
                Assert.That(clock.Elapsed.TotalSeconds, Is.LessThan(10), "delayCall never ran.");
                yield return null;
            }
        }

        private GameObject NewObject(string objectName, Vector3 position)
        {
            var obj = new GameObject(objectName);
            created.Add(obj);
            obj.transform.position = position;
            return obj;
        }

        private AgentBehaviour Guard(AgentProfile profile, ProviderConfig config)
        {
            var obj = NewObject("Guard", Here);

            var guard = obj.AddComponent<AgentBehaviour>();
            guard.Profile = profile;
            guard.Provider = config;
            return guard;
        }

        private Targetable Target(string id, Vector3 position, string description = null)
        {
            var obj = NewObject(id, position);

            // As above: AddComponent warns about the empty id before it is set.
            Debug.unityLogger.logEnabled = false;
            try
            {
                var targetable = obj.AddComponent<Targetable>();
                targetable.Id = id;
                targetable.Description = description;
                return targetable;
            }
            finally
            {
                Debug.unityLogger.logEnabled = true;
            }
        }

        // The village guard's profile, with only these of its actions.
        private static AgentProfile Profile(params string[] ids)
        {
            var profile = Fixtures.VillageGuardProfile();
            profile.Actions.RemoveAll(asset => Array.IndexOf(ids, asset.Action.Id) < 0);
            return profile;
        }

        private FakeProviderConfig Config(string configName = "FakeConfig", string reply = "")
        {
            var config = ScriptableObject.CreateInstance<FakeProviderConfig>();
            config.name = configName;
            config.Reply = reply;
            created.Add(config);
            return config;
        }

        private static FakeProvider Fake(ProviderConfig config)
        {
            return (FakeProvider)config.Provider;
        }

        private void UseDefault(ProviderConfig config)
        {
            var settings = ScriptableObject.CreateInstance<AgenerelaSettings>();
            settings.DefaultProvider = config;
            created.Add(settings);
            AgenerelaSettings.Override = settings;
        }

        private static AgentContext Context(Agent agent, Transform target)
        {
            var targets = new TargetRegistry();
            targets.Register("place", target);
            return new AgentContext(agent.Identity, "", null, null, targets, agent.Actions);
        }

        // Unity calls Awake and OnValidate only in Play mode and on an Inspector edit, so an
        // EditMode test calls them as TargetSourceTests calls OnValidate.
        private static void InvokePrivate(AgentBehaviour guard, string method)
        {
            typeof(AgentBehaviour)
                .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(guard, null);
        }
    }
}
