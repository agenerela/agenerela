using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    public sealed class VillageSceneTests
    {
        // What every agent answers unless a test scripts otherwise.
        private static readonly ScriptedReply DefaultReply = ScriptedReply.Decide("none", "no_target", "Understood.");

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
        private ScriptedProvider provider;
        private ScriptedProviderConfig config;
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
            config = ScriptableObject.CreateInstance<ScriptedProviderConfig>();
            provider = config.Scripted.Otherwise(DefaultReply);
            commands.UseProvider(config);
            // The scene can stay loaded between tests, and the guards' state with it.
            foreach (var mind in commands.Minds) mind.Body.ResetState();
            // And whatever a situation left: the thief, the fire, the night watch.
            VillageSituationTests.ResetVillage(commands);
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
            provider.Otherwise(ScriptedReply.Decide(action, target, "On it."));
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
            var request = provider.LastRequest.Request;
            Assert.That(request.Stimulus, Is.EqualTo("Player: \"Would you mind heading over to the bridge?\""));
            Assert.That(request.Observations, Does.Contain("You are not following the player."));
            Assert.That(request.Observations, Does.Contain("Off limits to the player: armory, locked_cave, lords_keep."));
            Assert.That(provider.LastRequest.OfferedActions, Is.EqualTo(new[]
                { "follow_player", "move_to", "attack_target", "warn_player", "block_path", "sound_alarm", "none" }));
            foreach (var id in EvaluationTargets)
            {
                Assert.That(provider.LastRequest.OfferedTargets, Does.Contain(id), id);
                // A local model never reads the schema: the id has to be in the prompt itself.
                Assert.That(request.Observations, Does.Contain(id + ", "), id);
            }
            // The village's people and the other guard are on top of the evaluation world, and its
            // targets still fit under the cap.
            Assert.That(provider.LastRequest.OfferedTargets, Does.Contain("wren").And.Contain("tomas"));
            Assert.That(provider.LastRequest.OfferedTargets, Does.Not.Contain("bram"), "An agent never names itself.");
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
            Assert.That(provider.LastRequest.OfferedActions, Is.EqualTo(offered.Split(' ')));
            Assert.That(commands.Recorder.Records.Last().OfferedActions, Is.EqualTo(offered.Split(' ')));
        }

        [Test]
        public async Task AQuietMomentCanStartAPatrol()
        {
            provider.Otherwise(ScriptedReply.Decide("patrol", "no_target", "On my rounds."));
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
            provider.Otherwise(ScriptedReply.Decide("patrol", "no_target", "Understood."));
            await Ask("quiet");
            Assert.That(guard.IsPatrolling, Is.True);

            provider.Otherwise(ScriptedReply.Decide("none", "no_target", "Understood."));
            await Ask(source);
            Assert.That(guard.IsPatrolling, Is.False);
            Assert.That(guard.IsMoving, Is.False);
            Assert.That(provider.LastRequest.Request.Observations, Does.Contain("You are standing still, your patrol broken off."));
            Assert.That(provider.LastRequest.OfferedActions, Does.Not.Contain("patrol"));
        }

        [TestCase("zone")]
        [TestCase("wolf")]
        public async Task OnlyThePlayerEndsAnEscort(string source)
        {
            provider.Otherwise(ScriptedReply.Decide("follow_player", "no_target", "Understood."));
            await Ask("player");
            Assert.That(guard.IsFollowing, Is.True);

            provider.Otherwise(ScriptedReply.Decide("stop_following", "no_target", "Understood."));
            await Ask(source);
            Assert.That(provider.LastRequest.OfferedActions, Does.Not.Contain("stop_following"));
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
                Assert.That(provider.LastRequest.Request.Observations, Does.Contain("A wolf is 20 m from you."), "The guard must see the far wolf.");
                Assert.That(provider.LastRequest.OfferedActions, Does.Not.Contain("sound_alarm"));

                wolf.transform.position = at + new Vector3(0, 0, 5);
                Physics.SyncTransforms();
                await Ask("wolf");
                Assert.That(provider.LastRequest.Request.Observations, Does.Contain("A wolf is 5 m from you."), "The guard must see the near wolf.");
                Assert.That(provider.LastRequest.OfferedActions, Does.Contain("sound_alarm"));
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
            provider.Otherwise(ScriptedReply.Decide("follow_player", "no_target", "Understood."));
            await commands.SubmitAsync("first");
            await commands.SubmitAsync("second");
            Assert.That(provider.LastRequest.OfferedActions, Does.Contain("stop_following"));
            Assert.That(provider.LastRequest.OfferedActions, Does.Not.Contain("follow_player"));
            Assert.That(provider.LastRequest.OfferedActions, Does.Not.Contain("patrol"));
        }

        [Test]
        public async Task TheAlarmCannotBeRungTwice()
        {
            provider.Otherwise(ScriptedReply.Decide("sound_alarm", "no_target", "Understood."));
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
            provider.Otherwise(ScriptedReply.Decide("follow_player", "no_target", "Understood."));
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
            provider.Otherwise(ScriptedReply.Decide("attack_target", "godzilla", "Understood."));
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
            provider.Otherwise(ScriptedReply.Decide("attack_target", "training_dummy", "Charging!").WithTokens(new[]
            {
                Token("{\"action\":\"attack_target\",\"target\":\""),
                Token("training", -2.95f, ("sc", -0.06f), ("str", -3.4f), ("training", -2.95f), ("the", -4.1f)),
                Token("_dummy", -0.01f, ("_dummy", -0.01f)),
                Token("\",\"statement\":\"Charging!\"}")
            }));
            await commands.SubmitAsync("Attack the scarecrow");
            var record = commands.Recorder.Records.Last();
            Assert.That(record.Outcome, Is.EqualTo("contained"));
            Assert.That(record.Action, Is.EqualTo("attack_target"));
            Assert.That(record.Said, Is.EqualTo("There's no such thing here."));
            Assert.That(commands.Overlay.Details, Does.Contain("CONTAINED: TargetGroundingGuard said:"));
            Assert.That(guard.Activity, Is.EqualTo("standing at the village_gate"));
        }

        // Lab decision 4 (10 November): Odile said "I don't sell that." to a thief when the grounding
        // guard contained her call. Its line answers what someone said, so it is said to the player
        // and to nobody else; asked about a thief, she says nothing.
        [Test]
        public async Task AGroundingLineAnswersOnlyThePlayer()
        {
            var merchant = (VillageMerchant)odile.Body;
            // A call whose target the model wanted elsewhere: "the..." was on no list.
            var unsure = ScriptedReply.Decide("call_guard", "wren", "Wren! Help!").WithTokens(new[]
            {
                Token("{\"action\":\"call_guard\",\"target\":\""),
                Token("wren", -3.2f, ("the", -0.05f), ("wren", -3.2f)),
                Token("\",\"statement\":\"Wren! Help!\"}")
            });
            provider.Otherwise(unsure);
            // Muted, so the thief's own report does not ask her first: the test asks once, itself.
            commands.EventsMuted = true;
            try
            {
                merchant.Thief.Begin();
                merchant.Thief.Grab();
                await odile.AskAsync("thief", "Event", VillageThief.SnatchedStimulus("bread"), DecisionPriority.Background);
            }
            finally { commands.EventsMuted = false; }
            var record = Records("Odile").Last();
            Assert.That(record.Source, Is.EqualTo("thief"));
            Assert.That(record.Outcome, Is.EqualTo("contained"));
            Assert.That(record.Guard, Is.EqualTo("TargetGroundingGuard"));
            Assert.That(record.Said, Is.Empty, "Odile answered a thief as if the player had asked for a ware.");
            Assert.That(odile.Body.Speech, Is.Null);

            // Asked by the player for a ware she does not have, the line is hers again. (The thief
            // has the bread, so the apple is what the schema forces.)
            provider.Otherwise(ScriptedReply.Decide("sell", "apple", "Here you are.").WithTokens(new[]
            {
                Token("{\"action\":\"sell\",\"target\":\""),
                Token("apple", -3.2f, ("sw", -0.05f), ("apple", -3.2f)),
                Token("\",\"statement\":\"Here you are.\"}")
            }));
            await odile.HearPlayerAsync("Sell me a cutlass.");
            record = Records("Odile").Last();
            Assert.That(record.Outcome, Is.EqualTo("contained"));
            Assert.That(record.Said, Is.EqualTo("I don't sell that."));
        }

        [Test]
        public async Task AGuardWillNotAttackTheVillagersAndSaysSo()
        {
            provider.Otherwise(ScriptedReply.Decide("attack_target", "tomas", "Have at you!"));
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
            provider.Otherwise(ScriptedReply.Decide("attack_target", "wren", "Understood."));
            await commands.SubmitAsync("Attack Wren!");
            Assert.That(bram.Last.Outcome, Is.EqualTo("contained"));
            provider.Otherwise(ScriptedReply.Decide("attack_target", "wolf", "Understood."));
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
            var bramHistory = string.Join("\n", provider.LastRequest.Request.History);
            Assert.That(bramHistory, Does.Contain("Bram, first words."));
            Assert.That(bramHistory, Does.Not.Contain("A wolf is by the river."));

            await wren.ReportEventAsync("wolf", "Event", "The wolf is back.", DecisionPriority.Background);
            var wrenHistory = string.Join("\n", provider.LastRequest.Request.History);
            Assert.That(provider.LastRequest.Request.SystemPrompt, Does.Contain("Wren"));
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
            for (int i = 0; i < 50 && provider.RequestCount == 0; i++) await Task.Delay(10);
            await Task.Delay(50);
            Assert.That(provider.RequestCount, Is.EqualTo(1), "Bram's request must wait while Wren's is with the provider.");
            provider.Hold = false;
            provider.Release();
            await first;
            await second;
            Assert.That(provider.RequestCount, Is.EqualTo(2));
            Assert.That(wren.Last.QueuedAhead, Is.EqualTo(0));
            Assert.That(bram.Last.QueuedAhead, Is.EqualTo(1));
        }

        [Test]
        public async Task EachRecordKeepsItsOwnAgentsRequestUnderMixedPriorities()
        {
            provider.Hold = true;
            var background = wren.ReportEventAsync("wolf", "Event", "Something moves across the river.", DecisionPriority.Background);
            var playerFacing = commands.SubmitAsync("Bram, are you there?");
            for (int i = 0; i < 50 && provider.RequestCount == 0; i++) await Task.Delay(10);
            provider.Hold = false;
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
            provider.Otherwise(ScriptedReply.Decide("sound_alarm", "no_target", "To arms!"));
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
                Assert.That(provider.RequestCount, Is.Zero);
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
            Assert.That(provider.LastRequest.Request.Stimulus, Is.EqualTo("Event: \"The player has walked into the armory, which is off limits.\""));
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
            Assert.That(provider.RequestCount, Is.EqualTo(1), "The event must not be sent while the first decision runs.");
            provider.Hold = false;
            provider.Release();
            await first;
            Assert.That(provider.RequestCount, Is.EqualTo(2));
            Assert.That(provider.LastRequest.Request.Stimulus, Does.StartWith("Event: "));
        }

        [Test]
        public async Task AnEventStillRunsWhenTheDecisionBeforeItFails()
        {
            // A held request's reply is chosen when it is asked, so both get the unreadable one.
            provider.Otherwise(ScriptedReply.Raw("this is not json"));
            provider.Hold = true;
            var first = commands.SubmitAsync("first");
            for (int i = 0; i < 50 && provider.RequestCount == 0; i++) await Task.Delay(10);
            await bram.ReportEventAsync("wolf", "Event", "A wolf has come out of the woods.", DecisionPriority.Background);
            Assert.That(bram.HasPending, Is.True);
            provider.Hold = false;
            provider.Release();
            await first;
            Assert.That(provider.RequestCount, Is.EqualTo(2), "The waiting event must be asked after the failed decision.");
            Assert.That(bram.HasPending, Is.False, "A stranded event would silence the quiet timer for good.");
            Assert.That(Records("Bram").Select(r => r.Outcome), Is.EqualTo(new[] { "error", "error" }));
        }

        [TestCase(ProviderErrorKind.Unreachable, "cannot be reached")]
        [TestCase(ProviderErrorKind.ModelNotFound, "ollama pull")]
        public async Task WithNoModelTheVillageSaysSoAndStopsAskingOnItsOwn(ProviderErrorKind failure, string told)
        {
            provider.Otherwise(ScriptedReply.Fail(failure));
            await bram.HearPlayerAsync("Open the gate.");
            Assert.That(Records("Bram").Last().Outcome, Is.EqualTo("error"));
            Assert.That(commands.ModelProblem, Does.Contain(told), "The HUD must say what is wrong with the model.");
            Assert.That(bram.Body.SpeechType, Is.EqualTo(SpeechKind.Error), "Bram must say no answer came, not stay silent.");
            Assert.That(bram.Body.Speech, Does.Contain("No answer"));
            Assert.That(bram.Conversation.Last().Text, Is.EqualTo("(no answer: no model is answering)"),
                "The dialogue log must say it in the bubble's words, not name an enum.");
            Assert.That(bram.IsDeciding, Is.False, "Bram must not stay thinking.");

            // What the game notices by itself waits for the model; four agents asking a stopped
            // server every few seconds would fill the console.
            int calls = provider.RequestCount;
            await wren.ReportEventAsync("wolf", "Event", "A wolf is by the river.", DecisionPriority.Background);
            var senses = bram.Senses;
            float quiet = senses.QuietSeconds;
            try
            {
                senses.QuietSeconds = -1f;
                senses.Sense();
                for (int i = 0; i < 20; i++) await Task.Delay(5);
            }
            finally { senses.QuietSeconds = quiet; }
            Assert.That(provider.RequestCount, Is.EqualTo(calls), "An agent asked a model that is down, on its own.");
            Assert.That(commands.MayAskOnItsOwn, Is.False);

            // The player can always ask, and an answer ends it.
            provider.Otherwise(ScriptedReply.Decide("none", "no_target", "Understood."));
            await bram.HearPlayerAsync("Hello?");
            Assert.That(commands.ModelProblem, Is.Null);
            Assert.That(commands.MayAskOnItsOwn, Is.True);
        }

        [Test]
        public async Task AnyReplyEndsTheNoModelWarningEvenAnUnreadableOne()
        {
            provider.Otherwise(ScriptedReply.Fail(ProviderErrorKind.Unreachable));
            await bram.HearPlayerAsync("Open the gate.");
            Assert.That(commands.ModelProblem, Is.Not.Null);
            provider.Otherwise(ScriptedReply.Decide("none", "no_target", "Understood."));
            provider.Otherwise(ScriptedReply.Raw("this is not json"));
            await bram.HearPlayerAsync("Open the gate.");
            Assert.That(commands.ModelProblem, Is.Null, "A model answered, so the warning must go, readable or not.");
            Assert.That(commands.MayAskOnItsOwn, Is.True);
            Assert.That(bram.Body.Speech, Does.Contain("could not be read"));
        }

        [Test]
        public async Task AnUnreadableReplyIsNotTakenForAMissingModel()
        {
            provider.Otherwise(ScriptedReply.Raw("this is not json"));
            await bram.HearPlayerAsync("Open the gate.");
            Assert.That(Records("Bram").Last().Outcome, Is.EqualTo("error"));
            Assert.That(commands.ModelProblem, Is.Null);
            Assert.That(commands.MayAskOnItsOwn, Is.True);
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
                for (int i = 0; i < 50 && provider.RequestCount == 0; i++) await Task.Delay(10);
                await mind.ReportEventAsync("wolf", "Event", "A wolf is by the river.", DecisionPriority.Background);
                Assert.That(mind.HasPending, Is.True);
                UnityEngine.Object.DestroyImmediate(copy);
                provider.Hold = false;
                provider.Release();
                await first;
                Assert.That(provider.RequestCount, Is.EqualTo(1), "A destroyed guard must not ask its waiting event.");
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
                for (int i = 0; i < 200 && (bram.IsDeciding || provider.RequestCount == 0); i++) await Task.Delay(10);
                Assert.That(provider.LastRequest.Request.Stimulus, Is.EqualTo("Time: \"Time passes quietly at your post.\""));
                Assert.That(commands.Recorder.Records.Last().Priority, Is.EqualTo("Background"));
            }
            finally { senses.QuietSeconds = quiet; }
        }

        [Test]
        public async Task TheRecorderKeepsWhatWasSentAndWhatCameBack()
        {
            var reply = ScriptedReply.Decide("patrol", "no_target", "I will walk the rounds.");
            provider.Otherwise(reply);
            await Ask("quiet");
            var record = commands.Recorder.Records.Single();
            Assert.That(record.Sent, Is.True);
            Assert.That(record.Agent, Is.EqualTo("Bram"));
            Assert.That(record.RawReply, Is.EqualTo(reply.Text));
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
                Assert.That(read.RawReply, Is.EqualTo(DefaultReply.Text));
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
                    provider.Otherwise(ScriptedReply.Decide("follow_player", "no_target", "Understood."));
                    await commands.SubmitAsync("Hello");
                    provider.Otherwise(ScriptedReply.Decide("none", "no_target", "Understood."));
                    await commands.SubmitAsync("Hello again");
                    Assert.That(Records(mind.Name).Last().OfferedActions, Does.Contain("stop_following"), mind.Name);
                }
                // The merchant and the captain, with every quest and stall action they offer.
                foreach (var mind in new[] { odile, hale })
                {
                    commands.Player.position = mind.transform.position + mind.transform.forward * 2.5f;
                    commands.Inventory.Add("moonpetal", 3);
                    commands.Inventory.Add("pelt");
                    provider.Otherwise(ScriptedReply.Decide("none", "no_target", "Understood."));
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
            // SetUp gave every agent a scripted config, so read the asset the scene was saved with.
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
            provider.Otherwise(ScriptedReply.Decide("sell", "bread", "Fresh this morning."));
            await SayTo(odile, "I'll take a loaf.");
            Assert.That(odile.Last.Outcome, Is.EqualTo("executed"));
            Assert.That(commands.Inventory.Count("bread"), Is.EqualTo(1));
            Assert.That(commands.Inventory.Coins, Is.EqualTo(6));
            Assert.That(Stock("bread").gameObject.activeSelf, Is.False);
            // Sold out: the next request no longer offers it.
            provider.Otherwise(ScriptedReply.Decide("none", "no_target", "Understood."));
            await SayTo(odile, "Anything else?");
            Assert.That(provider.LastRequest.OfferedTargets, Does.Not.Contain("bread").And.Contain("rope"));
            Assert.That(provider.LastRequest.Request.Observations, Does.Contain("The player has 6 coins"));
        }

        [Test]
        public async Task OdileWillNotSellWhatThePlayerCannotAfford()
        {
            commands.Inventory.Reset(3);
            provider.Otherwise(ScriptedReply.Decide("sell", "lantern", "Twelve coins."));
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
            provider.Otherwise(ScriptedReply.Decide("give", "lantern", "Understood."));
            await SayTo(odile, "Please, I have nothing.");
            Assert.That(odile.Last.Outcome, Is.EqualTo("contained"));
            Assert.That(odile.Last.Guard, Is.EqualTo(VillageMerchant.NoFreeGifts));
            Assert.That(commands.Inventory.Count("lantern"), Is.Zero);
            provider.Otherwise(ScriptedReply.Decide("give", "apple", "Here, love."));
            await SayTo(odile, "Please, I have nothing.");
            Assert.That(odile.Last.Outcome, Is.EqualTo("executed"));
            Assert.That(commands.Inventory.Count("apple"), Is.EqualTo(1));
        }

        [Test]
        public async Task AQuestIsOfferedAndThenCompletedOnlyWhenReady()
        {
            provider.Otherwise(ScriptedReply.Decide("offer_quest", "quest_moonpetals", "Bring me three moonpetals."));
            await SayTo(odile, "Got any work?");
            var quest = commands.Quests.Get("quest_moonpetals");
            Assert.That(quest.State, Is.EqualTo(QuestState.Active));
            // Not ready: completing it is not even offered.
            provider.Otherwise(ScriptedReply.Decide("none", "no_target", "Understood."));
            commands.Inventory.Add("moonpetal", 2);
            await SayTo(odile, "Here they are.");
            Assert.That(provider.LastRequest.OfferedActions, Does.Not.Contain("complete_quest"));
            commands.Inventory.Add("moonpetal");
            provider.Otherwise(ScriptedReply.Decide("complete_quest", "quest_moonpetals", "Lovely, thank you."));
            await SayTo(odile, "Here they are.");
            Assert.That(quest.State, Is.EqualTo(QuestState.Done));
            Assert.That(commands.Inventory.Coins, Is.EqualTo(8 + 15));
            Assert.That(commands.Inventory.Count("moonpetal"), Is.Zero);
            Assert.That(commands.Inventory.Count("rope"), Is.EqualTo(1));
            // Done: the quest leaves the schema.
            provider.Otherwise(ScriptedReply.Decide("none", "no_target", "Understood."));
            await SayTo(odile, "Anything else?");
            Assert.That(provider.LastRequest.OfferedTargets, Does.Not.Contain("quest_moonpetals"));
        }

        [Test]
        public async Task AQuestTheModelCompletesTooSoonIsContained()
        {
            var quest = commands.Quests.Get("quest_moonpetals");
            quest.State = QuestState.Active;
            commands.Inventory.Add("moonpetal", 3);
            provider.Hold = true;
            provider.Otherwise(ScriptedReply.Decide("complete_quest", "quest_moonpetals", "Understood."));
            var asking = SayTo(odile, "Here they are.");
            for (int i = 0; i < 50 && provider.RequestCount == 0; i++) await Task.Delay(10);
            // While she thinks, one petal is gone: the guard sees the quest is no longer ready.
            commands.Inventory.Remove("moonpetal");
            provider.Hold = false;
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
            provider.Otherwise(ScriptedReply.Decide("offer_quest", "market", "Understood."));
            await SayTo(odile, "Got any work?");
            Assert.That(odile.Last.Outcome, Is.EqualTo("contained"));
            Assert.That(odile.Last.Guard, Is.EqualTo(nameof(TargetCategoryGuard)));
            Assert.That(odile.Last.Said, Is.EqualTo("I don't follow you."));
        }

        // The rule's other half: an action for the world never takes a quest. Wren gives one, so her
        // move_to is offered with it, and a quest is no place to walk to.
        [Test]
        public async Task AQuestIsNoPlaceToWalkTo()
        {
            provider.Otherwise(ScriptedReply.Decide("move_to", "quest_lantern", "Understood."));
            await SayTo(wren, "Go and look for it.");
            Assert.That(wren.Last.OfferedTargets, Does.Contain("quest_lantern"));
            Assert.That(wren.Last.Outcome, Is.EqualTo("contained"));
            Assert.That(wren.Last.Guard, Is.EqualTo(nameof(TargetCategoryGuard)));
            Assert.That(wren.Last.Said, Is.EqualTo("I don't follow you."));
        }

        [Test]
        public async Task TheCaptainsOrderBecomesAGuardsStimulus()
        {
            provider.Otherwise(ScriptedReply.Decide("send_guard", "docks", "Get down there and look sharp."));
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
            Assert.That(provider.LastRequest.OfferedTargets, Does.Not.Contain("bread").And.Not.Contain("lantern"));
            Assert.That(provider.LastRequest.OfferedActions, Does.Not.Contain("offer_quest"), "Bram has no quests to give.");
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
