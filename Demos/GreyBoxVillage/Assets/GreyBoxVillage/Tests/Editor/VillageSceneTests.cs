using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    /// <see cref="Hold"/> set, the next request waits until <see cref="Release"/>. Every agent in the
    /// village shares one instance, through <see cref="FakeVillageConfig"/>.
    /// </summary>
    internal sealed class FakeVillageProvider : ILLMProvider
    {
        public string Reply;
        /// <summary>The token probabilities the reply carries, as Ollama reports them; null for none.</summary>
        public IReadOnlyList<TokenProbability> TokenProbabilities;
        public DecisionRequest LastRequest;
        public readonly List<DecisionRequest> Requests = new List<DecisionRequest>();
        public int Calls;
        public bool Hold;
        private AwaitableCompletionSource<ProviderResult> held;

        public FakeVillageProvider(string reply) => Reply = reply;

        public string Name => "fake";
        public ProviderCapabilities Capabilities { get; } = new ProviderCapabilities { SupportsConstrainedDecoding = true };

        public Awaitable<ProviderResult> RequestAsync(DecisionRequest req, CancellationToken ct)
        {
            LastRequest = req;
            Requests.Add(req);
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

        private ProviderResult Result() => new ProviderResult { Text = Reply, Model = "fake-model", TokenProbabilities = TokenProbabilities };

        public static string Decision(string action, string target, string statement = "Understood.") =>
            "{\"action\":\"" + action + "\",\"target\":\"" + target + "\",\"statement\":\"" + statement + "\"}";

        public string[] Offered(string field) => Offered(LastRequest, field);

        public static string[] Offered(DecisionRequest request, string field) =>
            request.Schema.Fields.Single(f => f.Name == field).AllowedValues.ToArray();
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
        private AgentMind bram, wren, odile, hale;
        private VillageGuard guard;
        private FakeVillageProvider provider;
        private FakeVillageConfig config;
        private ProviderConfig original;

        [SetUp]
        public void SetUp()
        {
            scene = SceneManager.GetSceneByPath(DemoLauncher.VillagePath);
            openedScene = !scene.isLoaded;
            if (openedScene) scene = EditorSceneManager.OpenScene(DemoLauncher.VillagePath, OpenSceneMode.Additive);
            commands = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<VillageCommands>()).Single();
            bram = commands.Mind("Bram");
            wren = commands.Mind("Wren");
            guard = (VillageGuard)bram.Body;
            original = bram.Behaviour.Provider;
            provider = new FakeVillageProvider(FakeVillageProvider.Decision("none", "no_target"));
            config = ScriptableObject.CreateInstance<FakeVillageConfig>();
            config.Fake = provider;
            commands.UseProvider(config);
            // The scene can stay loaded between tests, and the guards' state with it.
            foreach (var mind in commands.Minds) mind.Body.ResetState();
            guard.Alarm.Silence();
            commands.Inventory.Reset(8);
            commands.Quests.Define();
            odile = commands.Mind("Odile");
            hale = commands.Mind("Hale");
        }

        [TearDown]
        public void TearDown()
        {
            if (openedScene) EditorSceneManager.CloseScene(scene, true);
            // A scene that was already open goes back to the real provider config.
            else commands.UseProvider(original);
            UnityEngine.Object.DestroyImmediate(config);
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
            Assert.That(guard.Activity, Is.EqualTo(activity));
            Assert.That(bram.Last.Agent, Is.EqualTo("Bram"), "The player stands by the gate, so Bram is the one listening.");
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
            // The village's people and the other guard are on top of the evaluation world, and its
            // targets still fit under the cap.
            Assert.That(provider.Offered("target"), Does.Contain("wren").And.Contain("tomas"));
            Assert.That(provider.Offered("target"), Does.Not.Contain("bram"), "An agent never names itself.");
            Assert.That(bram.Last.TargetsDropped, Is.Empty);
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
            Assert.That(guard.IsPatrolling, Is.True);
            Assert.That(guard.Activity, Is.EqualTo("patrolling the village"));
        }

        [TestCase("player")]
        [TestCase("zone")]
        [TestCase("wolf")]
        public async Task AnythingButAQuietMomentBreaksOffAPatrol(string source)
        {
            provider.Reply = FakeVillageProvider.Decision("patrol", "no_target");
            await Ask("quiet");
            Assert.That(guard.IsPatrolling, Is.True);

            provider.Reply = FakeVillageProvider.Decision("none", "no_target");
            await Ask(source);
            Assert.That(guard.IsPatrolling, Is.False);
            Assert.That(guard.IsMoving, Is.False);
            Assert.That(provider.LastRequest.Observations, Does.Contain("You are standing still, your patrol broken off."));
            Assert.That(provider.Offered("action"), Does.Not.Contain("patrol"));
        }

        [TestCase("zone")]
        [TestCase("wolf")]
        public async Task OnlyThePlayerEndsAnEscort(string source)
        {
            provider.Reply = FakeVillageProvider.Decision("follow_player", "no_target");
            await Ask("player");
            Assert.That(guard.IsFollowing, Is.True);

            provider.Reply = FakeVillageProvider.Decision("stop_following", "no_target");
            await Ask(source);
            Assert.That(provider.Offered("action"), Does.Not.Contain("stop_following"));
            // The schema guard rejects it inside DecideAsync, and Execute runs nothing.
            var record = commands.Recorder.Records.Last();
            Assert.That(record.Outcome, Is.EqualTo("refused"));
            Assert.That(record.Action, Is.EqualTo("stop_following"));
            Assert.That(guard.IsFollowing, Is.True);
        }

        [Test]
        public async Task TheBellIsOfferedForAWolfCloseByAndNotForOneFarOff()
        {
            var wolf = bram.Senses.Wolf;
            var start = wolf.transform.position;
            var at = bram.transform.position;
            try
            {
                wolf.transform.position = at + new Vector3(20, 0, 0);
                Physics.SyncTransforms();
                await Ask("wolf");
                Assert.That(provider.LastRequest.Observations, Does.Contain("A wolf is 20 m from you."), "The guard must see the far wolf.");
                Assert.That(provider.Offered("action"), Does.Not.Contain("sound_alarm"));

                wolf.transform.position = at + new Vector3(0, 0, 5);
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
            Assert.That(guard.Alarm.IsSounding, Is.True);
            await commands.SubmitAsync("second");
            await Idle();
            var second = Records("Bram").Last();
            Assert.That(second.OfferedActions, Does.Not.Contain("sound_alarm"));
            Assert.That(second.Outcome, Is.EqualTo("refused"));
        }

        [Test]
        public async Task AnActionTheGuardCannotTakeIsRefused()
        {
            provider.Reply = FakeVillageProvider.Decision("follow_player", "no_target");
            await commands.SubmitAsync("first");
            // Already following, so follow_player is not offered. The schema guard rejects it inside
            // DecideAsync, and Execute returns false (hard rule 5).
            await commands.SubmitAsync("second");
            Assert.That(commands.Overlay.Details, Does.Contain("REFUSED: the action was not offered; SchemaLegalityGuard said:"));
            var record = commands.Recorder.Records.Last();
            Assert.That(record.Outcome, Is.EqualTo("refused"));
            Assert.That(record.Action, Is.EqualTo("follow_player"));
        }

        [Test]
        public async Task AnUnregisteredTargetIsRefused()
        {
            provider.Reply = FakeVillageProvider.Decision("attack_target", "godzilla");
            await commands.SubmitAsync("Attack the thing behind the hill");
            Assert.That(commands.Overlay.Details, Does.Contain("REFUSED: the target was not offered; SchemaLegalityGuard said:"));
            Assert.That(commands.Recorder.Records.Last().Outcome, Is.EqualTo("refused"));
            Assert.That(guard.Activity, Is.EqualTo("standing at the village_gate"));
            Assert.That(guard.Speech, Is.Null, "The schema guard has no line: a broken contract says nothing.");
        }

        [Test]
        public async Task ATargetTheModelWantedButWasNotOfferedIsContained()
        {
            // "Attack the scarecrow": the schema forces a listed target, so the model writes
            // training_dummy while nearly all its probability went to "sc...", which is on no list.
            provider.Reply = FakeVillageProvider.Decision("attack_target", "training_dummy", "Charging!");
            provider.TokenProbabilities = new[]
            {
                Token("{\"action\":\"attack_target\",\"target\":\""),
                Token("training", -2.95f, ("sc", -0.06f), ("str", -3.4f), ("training", -2.95f), ("the", -4.1f)),
                Token("_dummy", -0.01f, ("_dummy", -0.01f)),
                Token("\",\"statement\":\"Charging!\"}")
            };
            await commands.SubmitAsync("Attack the scarecrow");
            var record = commands.Recorder.Records.Last();
            Assert.That(record.Outcome, Is.EqualTo("contained"));
            Assert.That(record.Action, Is.EqualTo("attack_target"));
            Assert.That(record.Said, Is.EqualTo("There's no such thing here."));
            Assert.That(commands.Overlay.Details, Does.Contain("CONTAINED: TargetGroundingGuard said:"));
            Assert.That(guard.Activity, Is.EqualTo("standing at the village_gate"));
        }

        [Test]
        public async Task AGuardWillNotAttackTheVillagersAndSaysSo()
        {
            provider.Reply = FakeVillageProvider.Decision("attack_target", "tomas", "Have at you!");
            await commands.SubmitAsync("Attack Tomas!");
            var record = bram.Last;
            Assert.That(record.Outcome, Is.EqualTo("contained"));
            Assert.That(record.Guard, Is.EqualTo(VillageGuard.NeverAttackVillagers));
            Assert.That(record.Statement, Is.EqualTo("Have at you!"), "The record keeps the model's own words.");
            Assert.That(record.Said, Is.EqualTo("I won't raise my sword against our own."));
            // The bubble wraps its lines, so read it as one.
            Assert.That(guard.Speech.Replace('\n', ' '), Does.Contain("I won't raise my sword against our own.").And.Contain("stopped by never_attack_villagers"));
            Assert.That(guard.Activity, Is.EqualTo("standing at the village_gate"));

            // The other guard is one of the village's people too; the wolf is not.
            provider.Reply = FakeVillageProvider.Decision("attack_target", "wren");
            await commands.SubmitAsync("Attack Wren!");
            Assert.That(bram.Last.Outcome, Is.EqualTo("contained"));
            provider.Reply = FakeVillageProvider.Decision("attack_target", "wolf");
            await commands.SubmitAsync("Drive the wolf off!");
            Assert.That(bram.Last.Outcome, Is.EqualTo("executed"));
            Assert.That(guard.Activity, Is.EqualTo("charging the wolf"));
        }

        [Test]
        public async Task SecondDecisionCarriesTheFirstAsHistory()
        {
            await commands.SubmitAsync("first");
            await commands.SubmitAsync("second");
            Assert.That(commands.Overlay.Details, Does.Contain("history sent 1"));
        }

        [Test]
        public async Task EachGuardRemembersOnlyItsOwnExchanges()
        {
            await commands.SubmitAsync("Bram, first words.");
            await wren.ReportEventAsync("wolf", "Event", "A wolf is by the river.", DecisionPriority.Background);
            await commands.SubmitAsync("Bram, second words.");
            var bramHistory = string.Join("\n", provider.LastRequest.History);
            Assert.That(bramHistory, Does.Contain("Bram, first words."));
            Assert.That(bramHistory, Does.Not.Contain("A wolf is by the river."));

            await wren.ReportEventAsync("wolf", "Event", "The wolf is back.", DecisionPriority.Background);
            var wrenHistory = string.Join("\n", provider.LastRequest.History);
            Assert.That(provider.LastRequest.SystemPrompt, Does.Contain("Wren"));
            Assert.That(wrenHistory, Does.Contain("A wolf is by the river."));
            Assert.That(wrenHistory, Does.Not.Contain("Bram, first words."));
        }

        [Test]
        public async Task TheGuardsShareOneQueue()
        {
            Assert.That(bram.Agent, Is.Not.SameAs(wren.Agent));
            provider.Hold = true;
            var first = wren.ReportEventAsync("quiet", "Time", "Time passes quietly at your post.", DecisionPriority.Background);
            var second = bram.ReportEventAsync("quiet", "Time", "Time passes quietly at your post.", DecisionPriority.Background);
            for (int i = 0; i < 50 && provider.Calls == 0; i++) await Task.Delay(10);
            await Task.Delay(50);
            Assert.That(provider.Calls, Is.EqualTo(1), "Bram's request must wait while Wren's is with the provider.");
            provider.Release();
            await first;
            await second;
            Assert.That(provider.Calls, Is.EqualTo(2));
            Assert.That(wren.Last.QueuedAhead, Is.EqualTo(0));
            Assert.That(bram.Last.QueuedAhead, Is.EqualTo(1));
        }

        [Test]
        public async Task EachRecordKeepsItsOwnAgentsRequestUnderMixedPriorities()
        {
            provider.Hold = true;
            var background = wren.ReportEventAsync("wolf", "Event", "Something moves across the river.", DecisionPriority.Background);
            var playerFacing = commands.SubmitAsync("Bram, are you there?");
            for (int i = 0; i < 50 && provider.Calls == 0; i++) await Task.Delay(10);
            provider.Release();
            await background;
            await playerFacing;
            var w = Records("Wren").Single();
            var b = Records("Bram").Single();
            Assert.That(w.SystemPrompt, Does.Contain("Wren").And.Not.Contain("Bram"));
            Assert.That(w.StimulusTurn, Does.Contain("Something moves across the river."));
            Assert.That(w.Priority, Is.EqualTo("Background"));
            Assert.That(b.SystemPrompt, Does.Contain("Bram").And.Not.Contain("Wren"));
            Assert.That(b.StimulusTurn, Does.Contain("Bram, are you there?"));
            Assert.That(b.Priority, Is.EqualTo("PlayerFacing"));
        }

        [Test]
        public async Task AnEventReachesOnlyTheAgentsThatSenseIt()
        {
            var events = commands.GetComponent<VillageEvents>();
            var told = events.Raise(new VillageEvent
            {
                Text = "A cart has tipped over by the gate.",
                Where = bram.transform.position,
                HearingRadius = 5f
            });
            Assert.That(told, Is.EqualTo(new[] { bram }));
            await Idle();
            Assert.That(commands.Recorder.Records.Select(r => r.Agent), Is.EqualTo(new[] { "Bram" }));
        }

        [Test]
        public async Task TheBellReachesTheOtherGuardButNotTheOneWhoRangIt()
        {
            provider.Reply = FakeVillageProvider.Decision("sound_alarm", "no_target", "To arms!");
            await commands.SubmitAsync("Raiders at the wall!");
            await Idle();
            Assert.That(Records("Bram"), Has.Count.EqualTo(1));
            var heard = Records("Wren").Single();
            Assert.That(heard.Source, Is.EqualTo("bell"));
            Assert.That(heard.Stimulus, Is.EqualTo("The alarm bell is ringing. Bram rang it."));
            Assert.That(heard.OfferedActions, Does.Contain("move_to").And.Not.Contain("sound_alarm"));
        }

        [Test]
        public async Task DistantPlayerCannotSpeakToAGuard()
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
        public async Task ThePlayerSpeaksToTheNearestGuard()
        {
            var start = commands.Player.position;
            try
            {
                commands.Player.position = wren.transform.position + new Vector3(0, 0, -3);
                await commands.SubmitAsync("Quiet night?");
                Assert.That(Records("Wren").Single().Label, Is.EqualTo("Player"));
                Assert.That(Records("Bram"), Is.Empty);
            }
            finally { commands.Player.position = start; }
        }

        [Test]
        public async Task AnEventIsAskedWithItsOwnLabelAndPriority()
        {
            await bram.ReportEventAsync("zone", "Event", "The player has walked into the armory, which is off limits.",
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
            await bram.ReportEventAsync("wolf", "Event", "A wolf has come out of the woods.", DecisionPriority.Background);
            Assert.That(provider.Calls, Is.EqualTo(1), "The event must not be sent while the first decision runs.");
            provider.Release();
            await first;
            Assert.That(provider.Calls, Is.EqualTo(2));
            Assert.That(provider.LastRequest.Stimulus, Does.StartWith("Event: "));
        }

        [Test]
        public async Task AnEventStillRunsWhenTheDecisionBeforeItFails()
        {
            provider.Hold = true;
            var first = commands.SubmitAsync("first");
            for (int i = 0; i < 50 && provider.Calls == 0; i++) await Task.Delay(10);
            await bram.ReportEventAsync("wolf", "Event", "A wolf has come out of the woods.", DecisionPriority.Background);
            Assert.That(bram.HasPending, Is.True);
            provider.Reply = "this is not json";
            provider.Release();
            await first;
            Assert.That(provider.Calls, Is.EqualTo(2), "The waiting event must be asked after the failed decision.");
            Assert.That(bram.HasPending, Is.False, "A stranded event would silence the quiet timer for good.");
            Assert.That(Records("Bram").Select(r => r.Outcome), Is.EqualTo(new[] { "error", "error" }));
        }

        [Test]
        public async Task AGuardDestroyedMidDecisionAsksNothingMore()
        {
            // A copy of Wren, so the scene, open in the Editor or not, keeps its guards.
            var copy = UnityEngine.Object.Instantiate(wren.gameObject, wren.transform.position, wren.transform.rotation);
            try
            {
                var mind = copy.GetComponent<AgentMind>();
                mind.UseProvider(config);
                provider.Hold = true;
                var first = mind.ReportEventAsync("quiet", "Time", "Time passes quietly at your post.", DecisionPriority.Background);
                for (int i = 0; i < 50 && provider.Calls == 0; i++) await Task.Delay(10);
                await mind.ReportEventAsync("wolf", "Event", "A wolf is by the river.", DecisionPriority.Background);
                Assert.That(mind.HasPending, Is.True);
                UnityEngine.Object.DestroyImmediate(copy);
                provider.Release();
                await first;
                Assert.That(provider.Calls, Is.EqualTo(1), "A destroyed guard must not ask its waiting event.");
            }
            finally
            {
                if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        [Test]
        public async Task AQuietMomentAsksInTheBackgroundWithTheTimeLabel()
        {
            var senses = bram.Senses;
            float quiet = senses.QuietSeconds;
            try
            {
                senses.QuietSeconds = -1f;
                senses.Sense();
                for (int i = 0; i < 200 && (bram.IsDeciding || provider.Calls == 0); i++) await Task.Delay(10);
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
            Assert.That(record.Agent, Is.EqualTo("Bram"));
            Assert.That(record.RawReply, Is.EqualTo(provider.Reply));
            Assert.That(record.SystemPrompt, Does.Contain("Bram"));
            Assert.That(record.Observations, Does.Contain("village_gate"));
            Assert.That(record.OfferedActions, Does.Contain("patrol"));
            Assert.That(record.SchemaJson, Does.Contain("\"patrol\""));
            Assert.That(record.Outcome, Is.EqualTo("executed"));
            Assert.That(DecisionHistoryPanel.Describe(record), Does.Contain("== RAW REPLY").And.Contain("agent Bram"));
        }

        [Test]
        public async Task TheRecorderWritesOneJsonLinePerDecision()
        {
            commands.UseProvider(config, writeLogFile: true);
            string path = commands.Recorder.FilePath;
            try
            {
                await commands.SubmitAsync("hello");
                var lines = File.ReadAllLines(path);
                Assert.That(lines, Has.Length.EqualTo(1));
                var read = JsonUtility.FromJson<DecisionRecord>(lines[0]);
                Assert.That(read.Agent, Is.EqualTo("Bram"));
                Assert.That(read.RawReply, Is.EqualTo(provider.Reply));
                Assert.That(read.SystemPrompt, Does.Contain("Bram"));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public async Task NoFewShotExampleIsAnEvaluationPrompt()
        {
            // Hard rule 4: demonstrating a phrase and then scoring on it invalidates the result.
            // Every action's examples are only built while it is offered, so ask each guard in each
            // state: a quiet moment at its post (patrol), then the player, then following the player
            // (stop_following).
            var start = commands.Player.position;
            try
            {
                foreach (var mind in commands.Minds.Where(m => m.Body is VillageGuard))
                {
                    await mind.ReportEventAsync("quiet", "Time", "Time passes quietly at your post.", DecisionPriority.Background);
                    Assert.That(Records(mind.Name).Last().OfferedActions, Does.Contain("patrol"), mind.Name);
                    commands.Player.position = mind.transform.position + new Vector3(0, 0, -3);
                    provider.Reply = FakeVillageProvider.Decision("follow_player", "no_target");
                    await commands.SubmitAsync("Hello");
                    provider.Reply = FakeVillageProvider.Decision("none", "no_target");
                    await commands.SubmitAsync("Hello again");
                    Assert.That(Records(mind.Name).Last().OfferedActions, Does.Contain("stop_following"), mind.Name);
                }
                // The merchant and the captain, with every quest and stall action they offer.
                foreach (var mind in new[] { odile, hale })
                {
                    commands.Player.position = mind.transform.position + mind.transform.forward * 2.5f;
                    commands.Inventory.Add("moonpetal", 3);
                    commands.Inventory.Add("pelt");
                    provider.Reply = FakeVillageProvider.Decision("none", "no_target");
                    await commands.SubmitAsync("Hello");
                    commands.Quests.Get("quest_moonpetals").State = QuestState.Active;
                    commands.Quests.Get("quest_wolf").State = QuestState.Active;
                    await commands.SubmitAsync("Hello again");
                    Assert.That(Records(mind.Name).Last().OfferedActions, Does.Contain("complete_quest"), mind.Name);
                    commands.Quests.Define();
                }
            }
            finally { commands.Player.position = start; }
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../UnityProject/Assets/Evaluation/prompts"));
            var prompts = Directory.GetFiles(folder, "*.json")
                .SelectMany(f => JsonUtility.FromJson<PromptFile>(File.ReadAllText(f)).prompts)
                .Select(p => p.stimulus.Trim().TrimEnd('.', '!', '?').ToLowerInvariant())
                .Where(p => p.Length > 0)
                .ToList();
            Assert.That(prompts.Count, Is.GreaterThan(40), "The evaluation prompts were not found at " + folder);
            foreach (var record in commands.Recorder.Records)
            {
                string examples = record.FewShotBlock.ToLowerInvariant();
                Assert.That(examples, Is.Not.Empty, record.Agent);
                foreach (var prompt in prompts)
                    Assert.That(Regex.IsMatch(examples, @"\b" + Regex.Escape(prompt) + @"\b"), Is.False,
                        record.Agent + "'s examples contain the evaluation prompt \"" + prompt + "\"");
            }
        }

        [Serializable]
        private sealed class PromptFile
        {
            public List<Prompt> prompts = new List<Prompt>();
        }

        [Serializable]
        private sealed class Prompt
        {
            public string stimulus = "";
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
                foreach (var mind in commands.Minds)
                {
                    var start = mind.transform.position;
                    foreach (var place in places)
                    {
                        var path = new NavMeshPath();
                        Assert.That(NavMesh.CalculatePath(start, place.transform.position, NavMesh.AllAreas, path), Is.True, mind.Name + " to " + place.Id);
                        Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), mind.Name + " to " + place.Id);
                        if (mind == bram && place.Id == "north_tower")
                            Assert.That(path.corners.Any(p => Mathf.Abs(p.x) < 3 && p.z > 19 && p.z < 27), Is.True,
                                "The route to the north bank must use the bridge.");
                    }
                    foreach (var zone in Zones())
                    {
                        var path = new NavMeshPath();
                        Assert.That(NavMesh.CalculatePath(start, zone.Entrance.position, NavMesh.AllAreas, path), Is.True, zone.Id);
                        Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), mind.Name + " to the " + zone.Id + " entrance");
                    }
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
            Assert.That(Targetables().Where(t => t.Category == "person").Select(t => t.Id),
                Is.EquivalentTo(new[] { "bram", "wren", "odile", "hale", "tomas", "ada", "pell", "mira" }));
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
            Assert.That(commands.Board, Is.Not.Null);
            Assert.That(commands.GetComponent<DecisionHistoryPanel>().Commands, Is.EqualTo(commands));
            Assert.That(commands.GetComponent<VillageEvents>().Alarm, Is.EqualTo(guard.Alarm));
            Assert.That(commands.Minds.Select(m => m.Name), Is.EqualTo(new[] { "Bram", "Wren", "Odile", "Hale" }));
            foreach (var mind in commands.Minds)
            {
                Assert.That(mind.Village, Is.EqualTo(commands), mind.Name);
                Assert.That(mind.Behaviour.Validate(out string problem), Is.True, mind.Name + ": " + problem);
            }
            Assert.That(((VillageMerchant)odile.Body).Stock.Select(t => t.Id), Is.EqualTo(new[] { "bread", "apple", "rope", "torch", "lantern" }));
            Assert.That(commands.Inventory, Is.Not.Null);
            Assert.That(commands.Quests.Inventory, Is.EqualTo(commands.Inventory));
            foreach (var mind in commands.Minds.Where(m => m.Body is VillageGuard))
            {
                var body = (VillageGuard)mind.Body;
                Assert.That(mind.Village, Is.EqualTo(commands), mind.Name);
                Assert.That(mind.Senses.Mind, Is.EqualTo(mind), mind.Name);
                Assert.That(mind.Senses.Wolf, Is.Not.Null, mind.Name);
                Assert.That(body.GetComponent<NavMeshAgent>(), Is.Not.Null, mind.Name);
                Assert.That(body.Alarm, Is.EqualTo(guard.Alarm), mind.Name);
                Assert.That(body.Post, Is.Not.Null, mind.Name);
                Assert.That(body.PatrolRoute, Has.All.Not.Null, mind.Name);
            }
            // Both guards ask one provider config asset, so they share one queue.
            Assert.That(original, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(original), Is.Not.Empty);
            Assert.That(guard.Alarm.Villagers, Has.Length.EqualTo(4));
            foreach (var path in new[] { DemoLauncher.VillagePath, DemoLauncher.LauncherPath })
                Assert.That(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == path), Is.True, path);
            // The game reaches the framework only through its public assembly; the shared helpers stay framework-free.
            Assert.That(typeof(VillageCommands).Assembly.GetReferencedAssemblies().Any(a => a.Name == "Agenerela"), Is.True);
            Assert.That(typeof(AgentBoard).Assembly, Is.EqualTo(typeof(DecisionDebugOverlay).Assembly));
            Assert.That(typeof(DecisionDebugOverlay).Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("Agenerela")), Is.False);
        }

        [Test]
        public void BothGuardsInTheSceneAskTheSameProviderConfig()
        {
            var scenePath = scene.path;
            Assert.That(scenePath, Is.EqualTo(DemoLauncher.VillagePath));
            // UseProvider in SetUp replaced the components, so read the asset the scene was saved with.
            var saved = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), scenePath));
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original));
            Assert.That(Regex.Matches(saved, "Provider: \\{fileID: \\d+, guid: " + guid).Count, Is.EqualTo(commands.Minds.Length));
        }

        // ---- The market, the captain and the quests -------------------------------------------

        // Stands the player before an agent, so that agent is the one listening.
        private async Task SayTo(AgentMind mind, string text)
        {
            var start = commands.Player.position;
            try
            {
                commands.Player.position = mind.transform.position + mind.transform.forward * 2.5f;
                await commands.SubmitAsync(text);
            }
            finally { commands.Player.position = start; }
        }

        private Targetable Stock(string id) => ((VillageMerchant)odile.Body).Stock.Single(t => t.Id == id);

        [Test]
        public async Task OdileSellsWhatIsOnHerCounter()
        {
            provider.Reply = FakeVillageProvider.Decision("sell", "bread", "Fresh this morning.");
            await SayTo(odile, "I'll take a loaf.");
            Assert.That(odile.Last.Outcome, Is.EqualTo("executed"));
            Assert.That(commands.Inventory.Count("bread"), Is.EqualTo(1));
            Assert.That(commands.Inventory.Coins, Is.EqualTo(6));
            Assert.That(Stock("bread").gameObject.activeSelf, Is.False);
            // Sold out: the next request no longer offers it.
            provider.Reply = FakeVillageProvider.Decision("none", "no_target");
            await SayTo(odile, "Anything else?");
            Assert.That(provider.Offered("target"), Does.Not.Contain("bread").And.Contain("rope"));
            Assert.That(provider.LastRequest.Observations, Does.Contain("The player has 6 coins"));
        }

        [Test]
        public async Task OdileWillNotSellWhatThePlayerCannotAfford()
        {
            commands.Inventory.Reset(3);
            provider.Reply = FakeVillageProvider.Decision("sell", "lantern", "Twelve coins.");
            await SayTo(odile, "I want the lantern.");
            Assert.That(odile.Last.Outcome, Is.EqualTo("contained"));
            Assert.That(odile.Last.Guard, Is.EqualTo(VillageMerchant.CannotAfford));
            Assert.That(odile.Last.Said, Is.EqualTo("That costs more than you've got, love."));
            Assert.That(commands.Inventory.Coins, Is.EqualTo(3));
            Assert.That(Stock("lantern").gameObject.activeSelf, Is.True);
        }

        [Test]
        public async Task OdileGivesAwayOnlyCheapThings()
        {
            provider.Reply = FakeVillageProvider.Decision("give", "lantern");
            await SayTo(odile, "Please, I have nothing.");
            Assert.That(odile.Last.Outcome, Is.EqualTo("contained"));
            Assert.That(odile.Last.Guard, Is.EqualTo(VillageMerchant.NoFreeGifts));
            Assert.That(commands.Inventory.Count("lantern"), Is.Zero);
            provider.Reply = FakeVillageProvider.Decision("give", "apple", "Here, love.");
            await SayTo(odile, "Please, I have nothing.");
            Assert.That(odile.Last.Outcome, Is.EqualTo("executed"));
            Assert.That(commands.Inventory.Count("apple"), Is.EqualTo(1));
        }

        [Test]
        public async Task AQuestIsOfferedAndThenCompletedOnlyWhenReady()
        {
            provider.Reply = FakeVillageProvider.Decision("offer_quest", "quest_moonpetals", "Bring me three moonpetals.");
            await SayTo(odile, "Got any work?");
            var quest = commands.Quests.Get("quest_moonpetals");
            Assert.That(quest.State, Is.EqualTo(QuestState.Active));
            // Not ready: completing it is not even offered.
            provider.Reply = FakeVillageProvider.Decision("none", "no_target");
            commands.Inventory.Add("moonpetal", 2);
            await SayTo(odile, "Here they are.");
            Assert.That(provider.Offered("action"), Does.Not.Contain("complete_quest"));
            commands.Inventory.Add("moonpetal");
            provider.Reply = FakeVillageProvider.Decision("complete_quest", "quest_moonpetals", "Lovely, thank you.");
            await SayTo(odile, "Here they are.");
            Assert.That(quest.State, Is.EqualTo(QuestState.Done));
            Assert.That(commands.Inventory.Coins, Is.EqualTo(8 + 15));
            Assert.That(commands.Inventory.Count("moonpetal"), Is.Zero);
            Assert.That(commands.Inventory.Count("rope"), Is.EqualTo(1));
            // Done: the quest leaves the schema.
            provider.Reply = FakeVillageProvider.Decision("none", "no_target");
            await SayTo(odile, "Anything else?");
            Assert.That(provider.Offered("target"), Does.Not.Contain("quest_moonpetals"));
        }

        [Test]
        public async Task AQuestTheModelCompletesTooSoonIsContained()
        {
            var quest = commands.Quests.Get("quest_moonpetals");
            quest.State = QuestState.Active;
            commands.Inventory.Add("moonpetal", 3);
            provider.Hold = true;
            provider.Reply = FakeVillageProvider.Decision("complete_quest", "quest_moonpetals");
            var asking = SayTo(odile, "Here they are.");
            for (int i = 0; i < 50 && provider.Calls == 0; i++) await Task.Delay(10);
            // While she thinks, one petal is gone: the guard sees the quest is no longer ready.
            commands.Inventory.Remove("moonpetal");
            provider.Release();
            await asking;
            Assert.That(odile.Last.Outcome, Is.EqualTo("contained"));
            Assert.That(odile.Last.Guard, Is.EqualTo(VillageBody.QuestNotReady));
            Assert.That(quest.State, Is.EqualTo(QuestState.Active));
            Assert.That(commands.Inventory.Coins, Is.EqualTo(8));
        }

        [Test]
        public async Task AnActionAimedAtTheWrongKindOfTargetIsContained()
        {
            provider.Reply = FakeVillageProvider.Decision("offer_quest", "market");
            await SayTo(odile, "Got any work?");
            Assert.That(odile.Last.Outcome, Is.EqualTo("contained"));
            Assert.That(odile.Last.Guard, Is.EqualTo(VillageBody.TargetFitsAction));
            Assert.That(odile.Last.Said, Is.EqualTo("I don't follow you."));
        }

        [Test]
        public async Task TheCaptainsOrderBecomesAGuardsStimulus()
        {
            provider.Reply = FakeVillageProvider.Decision("send_guard", "docks", "Get down there and look sharp.");
            await SayTo(hale, "Something is wrong at the docks.");
            Assert.That(hale.Last.Outcome, Is.EqualTo("executed"));
            await Idle();
            var order = commands.Recorder.Records.Single(r => r.Source == "captain");
            Assert.That(order.Agent, Is.EqualTo("Wren"), "Wren is the nearer free guard to the docks.");
            Assert.That(order.Label, Is.EqualTo("Captain"));
            Assert.That(order.Stimulus, Does.Contain("docks").And.Contain("Get down there and look sharp."));
        }

        [Test]
        public void TheWolfLeavesItsPeltOnlyDuringTheQuest()
        {
            var wolf = bram.Senses.Wolf;
            Assert.That(wolf.Pelt, Is.Not.Null);
            var start = wolf.transform.position;
            try
            {
                wolf.Pelt.gameObject.SetActive(false);
                wolf.Flee();
                Assert.That(wolf.Pelt.gameObject.activeSelf, Is.False, "No quest, no pelt.");
                wolf.ResetState();
                commands.Quests.Get("quest_wolf").State = QuestState.Active;
                wolf.Flee();
                Assert.That(wolf.Pelt.gameObject.activeSelf, Is.True);
                Assert.That(Vector3.Distance(wolf.Pelt.transform.position, wolf.transform.position), Is.LessThan(0.01f));
            }
            finally
            {
                wolf.Pelt.gameObject.SetActive(false);
                wolf.ResetState();
                wolf.transform.position = start;
            }
        }

        [Test]
        public void ThePlayerPicksUpWhatIsWithinReach()
        {
            var start = commands.Player.position;
            var petal = UnityEngine.Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None).First(p => p.ItemId == "moonpetal");
            try
            {
                commands.Player.position = petal.transform.position + new Vector3(1, 0, 0);
                Assert.That(commands.NearestPickup(), Is.EqualTo(petal));
                petal.Collect(commands.Inventory);
                Assert.That(commands.Inventory.Count("moonpetal"), Is.EqualTo(1));
                Assert.That(commands.NearestPickup(), Is.Null);
            }
            finally
            {
                petal.gameObject.SetActive(true);
                commands.Player.position = start;
            }
        }

        [Test]
        public async Task TheGuardsAreNeverOfferedTheStallsWares()
        {
            await commands.SubmitAsync("Hello");
            Assert.That(provider.Offered("target"), Does.Not.Contain("bread").And.Not.Contain("lantern"));
            Assert.That(provider.Offered("action"), Does.Not.Contain("offer_quest"), "Bram has no quests to give.");
        }

        // Asks Bram the way the game does for each source: the player's typed line, or the event
        // GuardSenses raises.
        private async Task Ask(string source)
        {
            switch (source)
            {
                case "player":
                    await commands.SubmitAsync("Hello");
                    break;
                case "zone":
                    await bram.ReportEventAsync("zone", "Event", "The player has walked into the armory, which is off limits.",
                        DecisionPriority.PlayerFacing);
                    break;
                case "wolf":
                    await bram.ReportEventAsync("wolf", "Event", "A wolf has come out of the woods.", DecisionPriority.Background);
                    break;
                case "quiet":
                    await bram.ReportEventAsync("quiet", "Time", "Time passes quietly at your post.", DecisionPriority.Background);
                    break;
                default:
                    throw new ArgumentException(source);
            }
        }

        // Until no agent is deciding or waiting: an event the game raised runs on by itself.
        private async Task Idle()
        {
            for (int i = 0; i < 300 && commands.Minds.Any(m => m.IsDeciding || m.HasPending); i++) await Task.Delay(10);
            Assert.That(commands.IsDeciding, Is.False, "An agent never finished deciding.");
        }

        private List<DecisionRecord> Records(string agent) => commands.Recorder.Records.Where(r => r.Agent == agent).ToList();

        private static TokenProbability Token(string text, float logProbability = 0f,
            params (string Text, float LogProbability)[] alternatives) =>
            new TokenProbability
            {
                Text = text,
                LogProbability = logProbability,
                Alternatives = alternatives.Select(a => new TokenAlternative { Text = a.Text, LogProbability = a.LogProbability }).ToList()
            };

        private Targetable[] Targetables() =>
            scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Targetable>(true)).ToArray();

        private RestrictedZone[] Zones() =>
            scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RestrictedZone>(true)).ToArray();
    }
}
