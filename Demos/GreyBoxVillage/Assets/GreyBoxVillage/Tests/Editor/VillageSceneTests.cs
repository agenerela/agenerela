using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agenerela;
using Agenerela.Providers;
using Demos.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Demos.GreyBoxVillage.Tests
{
    /// <summary>
    /// Answers every request with <see cref="Reply"/>, so the scene is tested with no model. With
    /// <see cref="Hold"/> set, the next request waits until <see cref="Release"/>.
    /// </summary>
    internal sealed class FakeVillageProvider : ILLMProvider
    {
        public string Reply;
        public DecisionRequest LastRequest;
        public int Calls;
        public bool Hold;
        private AwaitableCompletionSource<ProviderResult> held;

        public FakeVillageProvider(string reply) => Reply = reply;

        public string Name => "fake";
        public ProviderCapabilities Capabilities { get; } = new ProviderCapabilities { SupportsConstrainedDecoding = true };

        public Awaitable<ProviderResult> RequestAsync(DecisionRequest req, CancellationToken ct)
        {
            LastRequest = req;
            Calls++;
            var source = new AwaitableCompletionSource<ProviderResult>();
            if (Hold)
            {
                Hold = false;
                held = source;
            }
            else source.SetResult(Result());
            return source.Awaitable;
        }

        public void Release() => held.SetResult(Result());

        private ProviderResult Result() => new ProviderResult { Text = Reply, Model = "fake-model" };

        public static string Decision(string action, string target, string statement = "Understood.") =>
            "{\"action\":\"" + action + "\",\"target\":\"" + target + "\",\"statement\":\"" + statement + "\"}";

        public string[] Offered(string field) =>
            LastRequest.Schema.Fields.Single(f => f.Name == field).AllowedValues.ToArray();
    }

    public sealed class VillageSceneTests
    {
        // Every target of the evaluation agent (UnityProject/Assets/Evaluation/agent.json), which
        // this village is built to match.
        private static readonly string[] EvaluationTargets =
        {
            "bridge", "village_gate", "campfire", "well", "north_tower", "market", "docks", "shelter",
            "eastern_road", "wolf", "locked_cave"
        };

        private Scene scene;
        private bool openedScene;
        private VillageCommands commands;
        private FakeVillageProvider provider;

        [SetUp]
        public void SetUp()
        {
            scene = SceneManager.GetSceneByPath(DemoLauncher.VillagePath);
            openedScene = !scene.isLoaded;
            if (openedScene) scene = EditorSceneManager.OpenScene(DemoLauncher.VillagePath, OpenSceneMode.Additive);
            commands = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<VillageCommands>()).Single();
            provider = new FakeVillageProvider(FakeVillageProvider.Decision("none", "no_target"));
            commands.UseProvider(provider);
            // The scene can stay loaded between tests, and the guard's state with it.
            commands.Guard.ResetState();
            commands.Guard.Alarm.Silence();
        }

        [TearDown]
        public void TearDown()
        {
            if (openedScene) EditorSceneManager.CloseScene(scene, true);
        }

        [TestCase("move_to", "north_tower", "walking to the north_tower")]
        [TestCase("move_to", "docks", "walking to the docks")]
        [TestCase("attack_target", "training_dummy", "charging the training_dummy")]
        [TestCase("follow_player", "no_target", "following the player")]
        [TestCase("block_path", "armory", "blocking the way into the armory")]
        [TestCase("warn_player", "no_target", "warning the player")]
        public async Task DecisionIsExecutedOnTheGuard(string action, string target, string activity)
        {
            provider.Reply = FakeVillageProvider.Decision(action, target, "On it.");
            await commands.SubmitAsync("anything at all");
            Assert.That(commands.Overlay.ActionId, Is.EqualTo(action));
            Assert.That(commands.Overlay.TargetId, Is.EqualTo(target));
            Assert.That(commands.Overlay.Statement, Is.EqualTo("On it."));
            Assert.That(commands.Overlay.Details, Does.Not.Contain("REFUSED"));
            Assert.That(commands.Guard.Activity, Is.EqualTo(activity));
        }

        [Test]
        public async Task TheGuardIsOfferedEveryPlaceAndReadsThemInItsObservations()
        {
            await commands.SubmitAsync("  Would you mind heading over to the bridge?  ");
            var request = provider.LastRequest;
            Assert.That(request.Stimulus, Is.EqualTo("Player: \"Would you mind heading over to the bridge?\""));
            Assert.That(request.Observations, Does.Contain("You are not following the player."));
            Assert.That(request.Observations, Does.Contain("Off limits to the player: armory, locked_cave, lords_keep."));
            Assert.That(provider.Offered("action"), Is.EqualTo(new[]
                { "follow_player", "move_to", "attack_target", "warn_player", "block_path", "sound_alarm", "none" }));
            foreach (var id in EvaluationTargets)
            {
                Assert.That(provider.Offered("target"), Does.Contain(id), id);
                // A local model never reads the schema: the id has to be in the prompt itself.
                Assert.That(request.Observations, Does.Contain(id + ", "), id);
            }
        }

        // The README's "Offered when" column, for a guard standing at its post with the bell silent
        // and no wolf in sight. A model cannot pick an action that is not offered.
        [TestCase("player", "follow_player move_to attack_target warn_player block_path sound_alarm none")]
        [TestCase("zone", "move_to attack_target warn_player block_path none")]
        [TestCase("wolf", "move_to attack_target warn_player block_path none")]
        [TestCase("quiet", "move_to attack_target block_path patrol none")]
        public async Task WhoIsAskingDecidesWhatIsOffered(string source, string offered)
        {
            await Ask(source);
            Assert.That(provider.Offered("action"), Is.EqualTo(offered.Split(' ')));
            Assert.That(commands.Recorder.Records.Last().OfferedActions, Is.EqualTo(offered.Split(' ')));
        }

        [Test]
        public async Task AQuietMomentCanStartAPatrol()
        {
            provider.Reply = FakeVillageProvider.Decision("patrol", "no_target", "On my rounds.");
            await Ask("quiet");
            Assert.That(commands.Recorder.Records.Last().Outcome, Is.EqualTo("executed"));
            Assert.That(commands.Guard.IsPatrolling, Is.True);
            Assert.That(commands.Guard.Activity, Is.EqualTo("patrolling the village"));
        }

        [TestCase("player")]
        [TestCase("zone")]
        [TestCase("wolf")]
        public async Task AnythingButAQuietMomentBreaksOffAPatrol(string source)
        {
            provider.Reply = FakeVillageProvider.Decision("patrol", "no_target");
            await Ask("quiet");
            Assert.That(commands.Guard.IsPatrolling, Is.True);

            provider.Reply = FakeVillageProvider.Decision("none", "no_target");
            await Ask(source);
            Assert.That(commands.Guard.IsPatrolling, Is.False);
            Assert.That(commands.Guard.IsMoving, Is.False);
            Assert.That(provider.LastRequest.Observations, Does.Contain("You are standing still, your patrol broken off."));
            Assert.That(provider.Offered("action"), Does.Not.Contain("patrol"));
        }

        [TestCase("zone")]
        [TestCase("wolf")]
        public async Task OnlyThePlayerEndsAnEscort(string source)
        {
            provider.Reply = FakeVillageProvider.Decision("follow_player", "no_target");
            await Ask("player");
            Assert.That(commands.Guard.IsFollowing, Is.True);

            provider.Reply = FakeVillageProvider.Decision("stop_following", "no_target");
            await Ask(source);
            Assert.That(provider.Offered("action"), Does.Not.Contain("stop_following"));
            Assert.That(commands.Recorder.Records.Last().Outcome, Is.EqualTo("refused"));
            Assert.That(commands.Guard.IsFollowing, Is.True);
        }

        [Test]
        public async Task TheBellIsOfferedForAWolfCloseByAndNotForOneFarOff()
        {
            var wolf = commands.Senses.Wolf;
            var start = wolf.transform.position;
            var guard = commands.Guard.transform.position;
            try
            {
                wolf.transform.position = guard + new Vector3(20, 0, 0);
                Physics.SyncTransforms();
                await Ask("wolf");
                Assert.That(provider.LastRequest.Observations, Does.Contain("A wolf is 20 m from you."), "The guard must see the far wolf.");
                Assert.That(provider.Offered("action"), Does.Not.Contain("sound_alarm"));

                wolf.transform.position = guard + new Vector3(0, 0, 5);
                Physics.SyncTransforms();
                await Ask("wolf");
                Assert.That(provider.LastRequest.Observations, Does.Contain("A wolf is 5 m from you."), "The guard must see the near wolf.");
                Assert.That(provider.Offered("action"), Does.Contain("sound_alarm"));
            }
            finally
            {
                wolf.transform.position = start;
                Physics.SyncTransforms();
            }
        }

        [Test]
        public async Task FollowingSwapsWhichActionIsOffered()
        {
            provider.Reply = FakeVillageProvider.Decision("follow_player", "no_target");
            await commands.SubmitAsync("first");
            await commands.SubmitAsync("second");
            Assert.That(provider.Offered("action"), Does.Contain("stop_following"));
            Assert.That(provider.Offered("action"), Does.Not.Contain("follow_player"));
            Assert.That(provider.Offered("action"), Does.Not.Contain("patrol"));
        }

        [Test]
        public async Task TheAlarmCannotBeRungTwice()
        {
            provider.Reply = FakeVillageProvider.Decision("sound_alarm", "no_target");
            await commands.SubmitAsync("first");
            Assert.That(commands.Guard.Alarm.IsSounding, Is.True);
            await commands.SubmitAsync("second");
            Assert.That(provider.Offered("action"), Does.Not.Contain("sound_alarm"));
            Assert.That(commands.Overlay.Details, Does.Contain("REFUSED"));
        }

        [Test]
        public async Task AnActionTheGuardCannotTakeIsRefused()
        {
            provider.Reply = FakeVillageProvider.Decision("follow_player", "no_target");
            await commands.SubmitAsync("first");
            // Already following, so follow_player is unavailable: Execute must refuse it (hard rule 5).
            await commands.SubmitAsync("second");
            Assert.That(commands.Overlay.Details, Does.Contain("REFUSED"));
            Assert.That(commands.Recorder.Records.Last().Outcome, Is.EqualTo("refused"));
        }

        [Test]
        public async Task AnUnregisteredTargetIsRefused()
        {
            provider.Reply = FakeVillageProvider.Decision("attack_target", "godzilla");
            await commands.SubmitAsync("Attack the thing behind the hill");
            Assert.That(commands.Overlay.Details, Does.Contain("the target was not offered"));
            Assert.That(commands.Guard.Activity, Is.EqualTo("standing at the village_gate"));
        }

        [Test]
        public async Task SecondDecisionCarriesTheFirstAsHistory()
        {
            await commands.SubmitAsync("first");
            await commands.SubmitAsync("second");
            Assert.That(commands.Overlay.Details, Does.Contain("history sent 1"));
        }

        [Test]
        public async Task DistantPlayerCannotSpeakToTheGuard()
        {
            var start = commands.Player.position;
            try
            {
                commands.Player.position = new Vector3(20, 0, -25);
                await commands.SubmitAsync("Go to the tower");
                Assert.That(provider.Calls, Is.Zero);
                Assert.That(commands.Overlay.Message, Does.Contain("Move within"));
            }
            finally { commands.Player.position = start; }
        }

        [Test]
        public async Task AnEventIsAskedWithItsOwnLabelAndPriority()
        {
            await commands.ReportEventAsync("zone", "Event", "The player has walked into the armory, which is off limits.",
                DecisionPriority.PlayerFacing);
            Assert.That(provider.LastRequest.Stimulus, Is.EqualTo("Event: \"The player has walked into the armory, which is off limits.\""));
            var record = commands.Recorder.Records.Single();
            Assert.That(record.Source, Is.EqualTo("zone"));
            Assert.That(record.Label, Is.EqualTo("Event"));
        }

        [Test]
        public async Task AnEventDuringADecisionWaitsItsTurn()
        {
            provider.Hold = true;
            var first = commands.SubmitAsync("first");
            await commands.ReportEventAsync("wolf", "Event", "A wolf has come out of the woods.", DecisionPriority.Background);
            Assert.That(provider.Calls, Is.EqualTo(1), "The event must not be sent while the first decision runs.");
            provider.Release();
            await first;
            Assert.That(provider.Calls, Is.EqualTo(2));
            Assert.That(provider.LastRequest.Stimulus, Does.StartWith("Event: "));
        }

        [Test]
        public async Task AQuietMomentAsksInTheBackgroundWithTheTimeLabel()
        {
            var senses = commands.Senses;
            float quiet = senses.QuietSeconds;
            try
            {
                senses.QuietSeconds = -1f;
                senses.Sense();
                for (int i = 0; i < 200 && (commands.IsDeciding || provider.Calls == 0); i++) await Task.Delay(10);
                Assert.That(provider.LastRequest.Stimulus, Is.EqualTo("Time: \"Time passes quietly at your post.\""));
                Assert.That(commands.Recorder.Records.Last().Priority, Is.EqualTo("Background"));
            }
            finally { senses.QuietSeconds = quiet; }
        }

        [Test]
        public async Task TheRecorderKeepsWhatWasSentAndWhatCameBack()
        {
            provider.Reply = FakeVillageProvider.Decision("patrol", "no_target", "I will walk the rounds.");
            await Ask("quiet");
            var record = commands.Recorder.Records.Single();
            Assert.That(record.Sent, Is.True);
            Assert.That(record.RawReply, Is.EqualTo(provider.Reply));
            Assert.That(record.SystemPrompt, Does.Contain("Bram"));
            Assert.That(record.Observations, Does.Contain("village_gate"));
            Assert.That(record.OfferedActions, Does.Contain("patrol"));
            Assert.That(record.SchemaJson, Does.Contain("\"patrol\""));
            Assert.That(record.Outcome, Is.EqualTo("executed"));
            Assert.That(DecisionHistoryPanel.Describe(record), Does.Contain("== RAW REPLY"));
        }

        [Test]
        public async Task TheRecorderWritesOneJsonLinePerDecision()
        {
            string path = Path.Combine(Path.GetTempPath(), "agenerela-test-" + Guid.NewGuid().ToString("N") + ".jsonl");
            try
            {
                var recorder = new DecisionRecorder(provider, path);
                var record = recorder.Begin("player", "Player", DecisionPriority.PlayerFacing, "hello");
                await recorder.RequestAsync(new DecisionRequest { SystemPrompt = "system", Stimulus = "Player: \"hello\"" },
                    CancellationToken.None);
                recorder.Complete(record, null, "none", "");
                var lines = File.ReadAllLines(path);
                Assert.That(lines, Has.Length.EqualTo(1));
                var read = JsonUtility.FromJson<DecisionRecord>(lines[0]);
                Assert.That(read.RawReply, Is.EqualTo(provider.Reply));
                Assert.That(read.SystemPrompt, Is.EqualTo("system"));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void BakedNavMeshReachesEveryPlace()
        {
            var navigation = scene.GetRootGameObjects().Select(g => g.GetComponent<VillageNavigation>()).Single(n => n != null);
            Assert.That(navigation.BakedData, Is.Not.Null);
            var instance = NavMesh.AddNavMeshData(navigation.BakedData);
            try
            {
                var places = Targetables().Where(t => t.Category == "place").ToList();
                Assert.That(places.Count, Is.GreaterThanOrEqualTo(10));
                var start = commands.Guard.transform.position;
                foreach (var place in places)
                {
                    var path = new NavMeshPath();
                    Assert.That(NavMesh.CalculatePath(start, place.transform.position, NavMesh.AllAreas, path), Is.True, place.Id);
                    Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), place.Id);
                    if (place.Id == "north_tower")
                        Assert.That(path.corners.Any(p => Mathf.Abs(p.x) < 3 && p.z > 19 && p.z < 27), Is.True,
                            "The route to the north bank must use the bridge.");
                }
                foreach (var zone in Zones())
                {
                    var path = new NavMeshPath();
                    Assert.That(NavMesh.CalculatePath(start, zone.Entrance.position, NavMesh.AllAreas, path), Is.True, zone.Id);
                    Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), zone.Id + " entrance");
                }
            }
            finally { instance.Remove(); }
        }

        [Test]
        public void TheVillageHasTheEvaluationWorldAndItsRestrictedZones()
        {
            var ids = Targetables().Select(t => t.Id).ToList();
            foreach (var id in EvaluationTargets)
                Assert.That(ids.Count(x => x == id), Is.EqualTo(1), id);
            Assert.That(ids, Is.Unique);
            var zones = Zones();
            Assert.That(zones.Select(z => z.Id), Is.EquivalentTo(new[] { "armory", "lords_keep", "locked_cave" }));
            foreach (var zone in zones)
            {
                Assert.That(zone.GetComponent<BoxCollider>().isTrigger, Is.True, zone.Id);
                Assert.That(zone.Entrance, Is.Not.Null, zone.Id);
                Assert.That(zone.GetComponent<Targetable>().Id, Is.EqualTo(zone.Id));
            }
        }

        [Test]
        public void SceneWiringAndLauncherRegistrationAreComplete()
        {
            Assert.That(commands.Input.Player.transform, Is.EqualTo(commands.Player));
            Assert.That(commands.Input.Overlay, Is.EqualTo(commands.Overlay));
            Assert.That(commands.Senses, Is.Not.Null);
            Assert.That(commands.Senses.Wolf, Is.Not.Null);
            Assert.That(commands.GetComponent<DecisionHistoryPanel>().Commands, Is.EqualTo(commands));
            Assert.That(commands.Guard.GetComponent<NavMeshAgent>(), Is.Not.Null);
            Assert.That(commands.Guard.Alarm.Villagers, Has.Length.EqualTo(4));
            Assert.That(commands.Guard.PatrolRoute, Has.All.Not.Null);
            foreach (var path in new[] { DemoLauncher.VillagePath, DemoLauncher.LauncherPath })
                Assert.That(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == path), Is.True, path);
            // The game reaches the framework only through its public assembly; the shared helpers stay framework-free.
            Assert.That(typeof(VillageCommands).Assembly.GetReferencedAssemblies().Any(a => a.Name == "Agenerela"), Is.True);
            Assert.That(typeof(DecisionDebugOverlay).Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("Agenerela")), Is.False);
        }

        // Asks the guard the way the game does for each source: the player's typed line, or the event
        // GuardSenses raises.
        private async Task Ask(string source)
        {
            switch (source)
            {
                case "player":
                    await commands.SubmitAsync("Hello");
                    break;
                case "zone":
                    await commands.ReportEventAsync("zone", "Event", "The player has walked into the armory, which is off limits.",
                        DecisionPriority.PlayerFacing);
                    break;
                case "wolf":
                    await commands.ReportEventAsync("wolf", "Event", "A wolf has come out of the woods.", DecisionPriority.Background);
                    break;
                case "quiet":
                    await commands.ReportEventAsync("quiet", "Time", "Time passes quietly at your post.", DecisionPriority.Background);
                    break;
                default:
                    throw new ArgumentException(source);
            }
        }

        private Targetable[] Targetables() =>
            scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Targetable>(true)).ToArray();

        private RestrictedZone[] Zones() =>
            scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RestrictedZone>(true)).ToArray();
    }
}
