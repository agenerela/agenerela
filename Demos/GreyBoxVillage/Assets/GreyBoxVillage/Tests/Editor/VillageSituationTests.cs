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
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Demos.GreyBoxVillage.Tests
{
    /// <summary>
    /// The situations of #153 in Edit mode, where nothing walks: the thief and <c>call_guard</c>,
    /// <c>chase</c>, night at the gate, the fire and <c>fetch_water</c>, the bell, and the presenter's
    /// reset. Each rule's test was seen failing with the rule undone (the PR lists them). The whole
    /// presenter script, walking included, runs in Play mode in <see cref="VillagePresenterScriptTests"/>.
    /// </summary>
    public sealed class VillageSituationTests
    {
        private Scene scene;
        private bool openedScene;
        private VillageCommands commands;
        private AgentMind bram, wren, odile;
        private VillageGuard bramBody, wrenBody;
        private VillageMerchant merchant;
        private VillageThief thief;
        private VillageFire fire;
        private NightWatch watch;
        private VillageGate gate;
        private ScriptedProvider provider;
        private ScriptedProviderConfig config;
        private ProviderConfig original;
        private float hour;

        [SetUp]
        public void SetUp()
        {
            scene = SceneManager.GetSceneByPath(DemoLauncher.VillagePath);
            openedScene = !scene.isLoaded;
            if (openedScene) scene = EditorSceneManager.OpenScene(DemoLauncher.VillagePath, OpenSceneMode.Additive);
            commands = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<VillageCommands>()).Single();
            bram = commands.Mind("Bram");
            wren = commands.Mind("Wren");
            odile = commands.Mind("Odile");
            bramBody = (VillageGuard)bram.Body;
            wrenBody = (VillageGuard)wren.Body;
            merchant = (VillageMerchant)odile.Body;
            thief = merchant.Thief;
            fire = merchant.Fire;
            watch = wren.GetComponent<NightWatch>();
            gate = watch.Gate;
            original = bram.Behaviour.Provider;
            config = ScriptableObject.CreateInstance<ScriptedProviderConfig>();
            provider = config.Scripted.Otherwise(ScriptedReply.Decide("none", "no_target", "Understood."));
            commands.UseProvider(config);
            hour = commands.Day.TimeOfDay;
            commands.Day.TimeOfDay = 0.45f;
            ResetVillage(commands);
        }

        /// <summary>Everything a situation changes, back as the scene was saved. The scene can stay loaded between tests.</summary>
        internal static void ResetVillage(VillageCommands commands)
        {
            var wren = commands.Mind("Wren");
            var merchant = (VillageMerchant)commands.Mind("Odile").Body;
            wren.GetComponent<NightWatch>().ResetState();
            merchant.Thief.ResetState();
            merchant.Fire.ResetState();
            foreach (var mind in commands.Minds)
            {
                mind.Body.ResetState();
                if (mind.Senses != null) mind.Senses.ResetState();
            }
            ((VillageGuard)wren.Body).Alarm.Silence();
            commands.Inventory.Reset(8);
            commands.Quests.Define();
        }

        [TearDown]
        public void TearDown()
        {
            commands.Day.TimeOfDay = hour;
            ResetVillage(commands);
            if (openedScene) EditorSceneManager.CloseScene(scene, true);
            else commands.UseProvider(original);
            UnityEngine.Object.DestroyImmediate(config);
        }

        // ---- The thief ---------------------------------------------------------------------------

        [Test]
        public async Task TheThiefRobsOdileWhoCallsAGuardWithHerOwnWords()
        {
            provider.When("Odile", null, ScriptedReply.Decide("call_guard", "wren", "Wren, he has my bread!"));
            thief.Begin();
            thief.Grab();
            Assert.That(thief.IsRunning, Is.True);
            Assert.That(thief.Loot.Id, Is.EqualTo("bread"), "The thief takes the first ware on the counter.");
            Assert.That(Stock("bread").gameObject.activeSelf, Is.False, "The stolen ware leaves the counter.");
            await Idle();

            var seen = Records("Odile").Single();
            Assert.That(seen.Source, Is.EqualTo("thief"));
            Assert.That(seen.Stimulus, Does.Contain("snatched the bread from your counter"));
            Assert.That(seen.OfferedActions, Does.Contain("call_guard"));
            Assert.That(seen.OfferedTargets, Does.Contain("bram").And.Contain("wren"), "The guards come from GuardTargetSource.");
            Assert.That(seen.OfferedTargets, Does.Not.Contain("tomas"), "Odile offers no villager: her proximity source leaves people out.");
            Assert.That(seen.Outcome, Is.EqualTo("executed"));

            Assert.That(Records("Wren"), Has.Count.EqualTo(1), "Odile's call never reached Wren.");
            var called = Records("Wren").Single();
            Assert.That(called.Source, Is.EqualTo("merchant"));
            Assert.That(called.Label, Is.EqualTo("Merchant"));
            Assert.That(called.Stimulus, Does.Contain("Odile shouts for you").And.Contain("bread").And.Contain("Wren, he has my bread!"));
            Assert.That(Records("Bram").Where(r => r.Source == "merchant"), Is.Empty, "Only the guard she named is called.");
        }

        [Test]
        public async Task CallGuardIsOfferedOnlyWhileThereIsTroubleAtTheMarket()
        {
            await SayTo(odile, "Hello there.");
            Assert.That(odile.Last.OfferedActions, Does.Not.Contain("call_guard"), "No thief, no fire: nothing to call a guard for.");

            thief.Begin();
            thief.Grab();
            await Idle();
            await SayTo(odile, "Did you see that?");
            Assert.That(odile.Last.OfferedActions, Does.Contain("call_guard"), "A thief is running.");

            thief.Catch(bramBody);
            await SayTo(odile, "Is it over?");
            Assert.That(odile.Last.OfferedActions, Does.Not.Contain("call_guard"), "The thief was caught.");

            fire.Ignite();
            await Idle();
            await SayTo(odile, "What now?");
            Assert.That(odile.Last.OfferedActions, Does.Contain("call_guard"), "A fire burns.");
        }

        [Test]
        public async Task ForAFireOdileCanCallOnlyAGuardWhoCanCarryWater()
        {
            fire.Ignite();
            await Idle();
            var call = Records("Odile").Single(r => r.Source == "fire");
            Assert.That(call.OfferedActions, Does.Contain("call_guard"));
            Assert.That(call.OfferedTargets, Does.Contain("wren").And.Not.Contain("bram"), "Bram cannot fetch water, so he is not offered for a fire.");
            Assert.That(call.Observations, Does.Contain("Guards you can call: wren (").And.Not.Contain("bram ("));

            // With a thief running too, every guard can be called: Bram catches one with attack_target.
            thief.Begin();
            thief.Grab();
            await Idle();
            Assert.That(Records("Odile").Last().OfferedTargets, Does.Contain("wren").And.Contain("bram"));
        }

        // G13: a guard an earlier source already offers is one target, offered once, as that source
        // offered it; the same id for another object is still refused.
        [Test]
        public void AGuardOfferedTwiceIsOneTarget()
        {
            var wrenTargetable = wren.GetComponent<Targetable>();
            var registry = new TargetRegistry();
            registry.Register(wrenTargetable.Id, wren.transform, wrenTargetable.Category);
            new GuardTargetSource(merchant).Collect(null, registry);
            Assert.That(registry.Ids, Is.EquivalentTo(new[] { "wren", "bram" }));
            Assert.That(registry.CategoryOf("wren"), Is.EqualTo(wrenTargetable.Category), "The first offer is the one kept.");
            Assert.That(registry.CategoryOf("bram"), Is.EqualTo(GuardTargetSource.Category));

            var other = new TargetRegistry();
            other.Register("wren", odile.transform);
            Assert.Throws<System.ArgumentException>(() => new GuardTargetSource(merchant).Collect(null, other),
                "Two objects answering to one id were both offered.");
        }

        // Codex, L1: a guard whose agent cannot be built must not stop Odile deciding about a fire.
        [Test]
        public async Task AGuardThatCannotBeBuiltDoesNotStopOdile()
        {
            var events = fire.Events;
            var profile = bram.Behaviour.Profile;
            try
            {
                bram.UseProvider(config);
                bram.Behaviour.Profile = null;
                fire.Events = null;
                fire.Ignite();
                await SayTo(odile, "Is that smoke?");
                var record = Records("Odile").Single();
                Assert.That(record.Outcome, Is.EqualTo("none"), "Odile's decision did not finish.");
                Assert.That(record.OfferedTargets, Does.Contain("wren").And.Not.Contain("bram"));
            }
            finally
            {
                fire.Events = events;
                bram.Behaviour.Profile = profile;
                bram.UseProvider(config);
            }
        }

        // Codex, L1: the observations name what the proximity source offers, by its own categories when
        // it names some, so they never list a target the schema does not offer.
        [Test]
        public async Task TheObservationsNameWhatTheSourceOffers()
        {
            var own = wren.Behaviour.Targets.Categories;
            try
            {
                wren.Behaviour.Targets.Categories = new[] { "place" };
                wren.UseProvider(config);
                await SayTo(wren, "Anything to report?");
                Assert.That(wren.Last.OfferedTargets, Does.Contain("bridge").And.Not.Contain("tomas"));
                Assert.That(wren.Last.Observations, Does.Contain("bridge, ").And.Not.Contain("away: Tomas, a villager"));
            }
            finally
            {
                wren.Behaviour.Targets.Categories = own;
                wren.UseProvider(config);
            }
        }

        [Test]
        public async Task CallGuardTakesOnlyAGuard()
        {
            thief.Begin();
            provider.Otherwise(ScriptedReply.Decide("call_guard", "apple", "Help!"));
            thief.Grab();
            await Idle();
            var record = Records("Odile").Single();
            Assert.That(record.Outcome, Is.EqualTo("contained"));
            Assert.That(record.Guard, Is.EqualTo(nameof(TargetCategoryGuard)));
            Assert.That(record.Said, Is.EqualTo("I don't follow you."));
            Assert.That(Records("Wren").Concat(Records("Bram")), Is.Empty, "A call at no guard calls nobody.");
        }

        [Test]
        public async Task ChaseIsOfferedOnlyWhileAThiefOrTheWolfIsInSight()
        {
            await wren.HearPlayerAsync("Hello.");
            Assert.That(wren.Last.OfferedActions, Does.Not.Contain("chase"));

            thief.Begin();
            thief.Grab();
            await Idle();
            // Behind the cottage south of the bridge: running, but out of Wren's sight.
            thief.transform.position = new Vector3(10f, 0, 6.5f);
            Physics.SyncTransforms();
            await wren.HearPlayerAsync("Hello?");
            Assert.That(wren.Last.OfferedActions, Does.Not.Contain("chase"), "The thief is behind the cottage.");

            thief.transform.position = wren.transform.position + new Vector3(0, 0, -8f);
            Physics.SyncTransforms();
            await wren.HearPlayerAsync("Look!");
            Assert.That(wren.Last.OfferedActions, Does.Contain("chase"), "The thief is in plain sight.");
            Assert.That(wren.Last.OfferedTargets, Does.Contain("thief"));
            Assert.That(wren.Last.Observations, Does.Contain("A thief is running for the eastern_road, 8 m from you."));

            thief.ResetState();
            var wolf = wren.Senses.Wolf;
            var start = wolf.transform.position;
            try
            {
                wolf.transform.position = wren.transform.position + new Vector3(0, 0, 5f);
                Physics.SyncTransforms();
                await wren.HearPlayerAsync("A wolf!");
                Assert.That(wren.Last.OfferedActions, Does.Contain("chase"), "The wolf is in sight.");
            }
            finally
            {
                wolf.transform.position = start;
                Physics.SyncTransforms();
            }
        }

        [Test]
        public async Task ACaughtThiefDropsTheWareAndItGoesBackOnTheCounter()
        {
            thief.Begin();
            thief.Grab();
            await Idle();
            thief.transform.position = wren.transform.position + new Vector3(0, 0, -8f);
            Physics.SyncTransforms();
            provider.Otherwise(ScriptedReply.Decide("chase", "thief", "Stop, thief!"));
            await wren.HearPlayerAsync("After him!");
            Assert.That(wren.Last.Outcome, Is.EqualTo("executed"));
            Assert.That(wrenBody.IsChasing, Is.True);
            Assert.That(wrenBody.Activity, Is.EqualTo("chasing the thief"));

            thief.Catch(wrenBody);
            Assert.That(thief.State, Is.EqualTo(VillageThief.Phase.Caught));
            Assert.That(thief.CaughtBy, Is.EqualTo(wrenBody));
            Assert.That(thief.Loot, Is.Null);
            Assert.That(Stock("bread").gameObject.activeSelf, Is.True, "The ware is back on the counter.");
            Assert.That(commands.Hud.ToastLog.Last(), Does.Contain("caught the thief"));
        }

        // Played live, qwen3.5:2b answered the thief with attack_target as often as chase: a guard
        // that walked to where the thief had been let it get away.
        [Test]
        public async Task AttackingARunningThiefRunsAfterIt()
        {
            thief.Begin();
            thief.Grab();
            await Idle();
            thief.transform.position = bram.transform.position + new Vector3(0, 0, 8f);
            Physics.SyncTransforms();
            provider.Otherwise(ScriptedReply.Decide("attack_target", "thief", "Stop!"));
            await bram.HearPlayerAsync("Get him!");
            Assert.That(bram.Last.Outcome, Is.EqualTo("executed"));
            Assert.That(bramBody.IsChasing, Is.True, "An attack on a running thief must run after it.");
            Assert.That(bramBody.GetComponent<UnityEngine.AI.NavMeshAgent>().speed, Is.EqualTo(bramBody.ChaseSpeed));
        }

        // Played live, Wren was asked about a wolf already running for its den: no longer a target,
        // so the grounding guard contained her answer.
        [Test]
        public async Task AFleeingWolfIsNoLongerNews()
        {
            var wolf = bram.Senses.Wolf;
            var start = wolf.transform.position;
            try
            {
                wolf.transform.position = bram.transform.position + new Vector3(0, 0, 5f);
                Physics.SyncTransforms();
                wolf.Flee();
                Assert.That(wolf.IsFleeing, Is.True);
                bram.Senses.ForgetWolf();
                bram.Senses.Sense();
                await Idle();
                Assert.That(Records("Bram").Where(r => r.Source == "wolf"), Is.Empty, "A wolf running for its den was reported.");
                await bram.HearPlayerAsync("Where did it go?");
                Assert.That(bram.Last.Observations, Does.Not.Contain("A wolf is"));
                Assert.That(bram.Last.OfferedActions, Does.Not.Contain("chase"));
            }
            finally
            {
                wolf.ResetState();
                wolf.transform.position = start;
                Physics.SyncTransforms();
            }
        }

        [Test]
        public void AThiefWhoGetsAwayTakesTheWareUntilTheStallRestocks()
        {
            thief.Begin();
            thief.Grab();
            thief.GetAway();
            Assert.That(thief.gameObject.activeSelf, Is.False, "Gone down the road.");
            Assert.That(Stock("bread").gameObject.activeSelf, Is.False, "The ware went with it.");
            Assert.That(commands.Hud.ToastLog.Last(), Does.Contain("got away with the bread"));
            // It left the counter as a sale does, so it restocks as a sold ware would.
            Assert.That(merchant.FirstOnCounter().Id, Is.EqualTo("apple"));
        }

        // ---- Night at the gate -----------------------------------------------------------------------

        // Bram is the evaluation agent: what he is offered must not change with the hour, whoever asks.
        [TestCase("player")]
        [TestCase("zone")]
        [TestCase("wolf")]
        [TestCase("quiet")]
        public async Task BramIsOfferedTheSameActionsByNightAsByDay(string source)
        {
            await Ask(bram, source);
            var day = bram.Last;
            Assert.That(day.Observations, Does.Contain("It is daytime."));

            Night(true);
            await Ask(bram, source);
            var night = bram.Last;
            Assert.That(night.Observations, Does.Contain("It is night."));
            Assert.That(night.OfferedActions, Is.EqualTo(day.OfferedActions), "Bram's actions changed at night.");
            Assert.That(night.TargetsDropped, Is.Empty);
            Assert.That(bram.GetComponent<NightWatch>(), Is.Null, "The gate actions are never Bram's.");
            Assert.That(bram.Agent.Actions.Definitions.Select(d => d.Id), Does.Not.Contain("open_gate").And.Not.Contain("close_gate"));
        }

        // #153 keeps Bram's actions: whatever happens, he is offered nothing he could not be offered
        // before the situations, and he registers no other action at all: his component's Only
        // Actions are these. Wren alone has chase and fetch_water, and the gate (NightWatch).
        private static readonly string[] BramsOwnActions =
            { "follow_player", "stop_following", "move_to", "attack_target", "warn_player", "block_path", "sound_alarm", "patrol", "none" };

        [Test]
        public async Task BramIsOfferedNothingOutsideHisOwnActionsWhateverHappens()
        {
            Assert.That(bram.Behaviour.OnlyActions, Is.EquivalentTo(BramsOwnActions.Where(a => a != "none")));
            Assert.That(bram.Agent.Actions.Definitions.Select(d => d.Id),
                Is.EquivalentTo(BramsOwnActions.Where(a => a != "none")),
                "Bram registers an action he did not have before the situations.");
            var wolf = bram.Senses.Wolf;
            var start = wolf.transform.position;
            try
            {
                // Everything at once: the wolf close, a thief running past, the stall on fire.
                wolf.transform.position = bram.transform.position + new Vector3(0, 0, 5f);
                thief.Begin();
                thief.Grab();
                thief.transform.position = bram.transform.position + new Vector3(3f, 0, 8f);
                Physics.SyncTransforms();
                fire.Ignite();
                await Idle();
                foreach (bool night in new[] { false, true })
                {
                    Night(night);
                    foreach (var source in new[] { "player", "zone", "wolf", "quiet", "thief", "merchant", "fire", "bell", "captain" })
                    {
                        if (source == "player") await bram.HearPlayerAsync("What now?");
                        else await bram.ReportEventAsync(source, "Event", "Something is happening (" + source + ").", DecisionPriority.Background);
                        await Idle();
                        var offered = bram.Last.OfferedActions;
                        Assert.That(offered, Is.SubsetOf(BramsOwnActions), (night ? "night, " : "day, ") + source);
                        if (source == "thief") Assert.That(bram.Last.OfferedTargets, Does.Contain("thief"), "He can still go after the thief.");
                    }
                }
            }
            finally
            {
                wolf.ResetState();
                wolf.transform.position = start;
                Physics.SyncTransforms();
            }
        }

        [Test]
        public void NightfallShutsTheGateAndHandsTheWatchToWren()
        {
            Assert.That(gate.IsOpen, Is.True);
            Night(true);
            Assert.That(gate.IsOpen, Is.False);
            Assert.That(gate.Blocker.enabled, Is.True, "A shut gate stops the player.");
            Assert.That(gate.Obstacle.enabled, Is.True, "A shut gate stops what walks the NavMesh.");
            Assert.That(watch.OnWatch, Is.True);
            Assert.That(wrenBody.CurrentPost, Is.EqualTo(watch.WatchPost));
            Assert.That(wrenBody.Activity, Does.Contain("take the night watch"));
            Assert.That(bramBody.CurrentPost, Is.EqualTo(watch.RelievedPost), "Bram goes off watch, so he is not the one at the gate.");
            Assert.That(wren.RoleNow, Is.EqualTo("the night watch at the gate"), "Her card says what she keeps at night.");
            Assert.That(wren.GetComponent<NameTag>().Detail, Is.EqualTo("night watch"));
            Assert.That(bram.RoleNow, Is.EqualTo(bram.Role));

            Night(false);
            Assert.That(gate.IsOpen, Is.True);
            Assert.That(gate.Blocker.enabled, Is.False);
            Assert.That(watch.OnWatch, Is.False);
            Assert.That(wrenBody.CurrentPost, Is.EqualTo(wrenBody.Post));
            Assert.That(bramBody.CurrentPost, Is.EqualTo(bramBody.Post));
            Assert.That(wren.RoleNow, Is.EqualTo(wren.Role));
            Assert.That(wren.GetComponent<NameTag>().Detail, Is.Not.EqualTo("night watch"));
        }

        [Test]
        public void TheWatchSeesThroughTheShutGate()
        {
            Night(true);
            Assert.That(gate.Blocker.enabled, Is.True);
            var wolf = wren.Senses.Wolf;
            var (wolfAt, wrenAt) = (wolf.transform.position, wren.transform.position);
            try
            {
                wren.transform.position = watch.WatchPost.position;
                wolf.transform.position = gate.transform.position + new Vector3(1f, 0, -4f);
                Physics.SyncTransforms();
                Assert.That(wren.Senses.CanSee(wolf.transform), Is.True, "The shut gate hid the wolf from the watch.");
            }
            finally
            {
                wolf.transform.position = wolfAt;
                wren.transform.position = wrenAt;
                Physics.SyncTransforms();
            }
        }

        [Test]
        public async Task OnlyTheNightWatchOpensAndShutsTheGate()
        {
            await wren.HearPlayerAsync("Open the gate.");
            Assert.That(wren.Last.OfferedActions, Does.Not.Contain("open_gate").And.Not.Contain("close_gate"), "By day nobody keeps the gate.");

            Night(true);
            await wren.ReportEventAsync("quiet", "Time", "Time passes quietly at your post.", DecisionPriority.Background);
            Assert.That(wren.Last.OfferedActions, Does.Not.Contain("open_gate"), "A quiet moment never opens the gate.");
            Assert.That(wren.Last.OfferedActions, Does.Not.Contain("patrol"), "The watch stays at the gate.");
            Assert.That(wren.Last.Observations, Does.Contain("The village gate is shut for the night."));

            provider.Otherwise(ScriptedReply.Decide("open_gate", "no_target", "In you come."));
            await wren.HearPlayerAsync("Let me in!");
            Assert.That(wren.Last.OfferedActions, Does.Contain("open_gate").And.Not.Contain("close_gate"));
            Assert.That(wren.Last.Outcome, Is.EqualTo("executed"));
            Assert.That(gate.IsOpen, Is.True);

            provider.Otherwise(ScriptedReply.Decide("close_gate", "no_target", "Barred."));
            await wren.HearPlayerAsync("Shut it again.");
            Assert.That(wren.Last.OfferedActions, Does.Contain("close_gate").And.Not.Contain("open_gate"));
            Assert.That(gate.IsOpen, Is.False);
        }

        // ---- The fire --------------------------------------------------------------------------------

        [Test]
        public async Task WhoeverSeesTheSmokeIsAskedAndFetchWaterIsOfferedOnlyWhileItBurns()
        {
            await wren.HearPlayerAsync("Hello.");
            Assert.That(wren.Last.OfferedActions, Does.Not.Contain("fetch_water"));

            provider.When("Wren", null, ScriptedReply.Decide("fetch_water", "no_target", "Water!"));
            fire.Ignite();
            Assert.That(fire.IsBurning, Is.True);
            await Idle();
            var told = commands.Recorder.Records.Where(r => r.Source == "fire").Select(r => r.Agent).ToList();
            Assert.That(told, Does.Contain("Odile"), "Odile stands by the stall and hears it.");
            Assert.That(told, Does.Contain("Bram").And.Contain("Wren"), "Both guards see the smoke from their posts.");
            Assert.That(told, Does.Not.Contain("Hale"), "The captain, behind the keep's wall, neither sees nor hears it.");
            var guard = Records("Wren").Single(r => r.Source == "fire");
            Assert.That(guard.Stimulus, Is.EqualTo(fire.Stimulus));
            Assert.That(guard.OfferedActions, Does.Contain("fetch_water"));
            Assert.That(guard.Outcome, Is.EqualTo("executed"));
            Assert.That(wrenBody.IsFetchingWater, Is.True);
            Assert.That(wrenBody.Activity, Is.EqualTo("running to the well for water"));
            Assert.That(Records("Bram").Single(r => r.Source == "fire").OfferedActions, Does.Not.Contain("fetch_water"),
                "Bram is told of the fire, but carrying water is not his.");
        }

        [Test]
        public async Task EnoughBucketsPutTheFireOut()
        {
            fire.Ignite();
            await Idle();
            for (int i = 1; i < fire.TripsToPutOut; i++)
            {
                Assert.That(fire.Douse(), Is.False);
                Assert.That(fire.IsBurning, Is.True, "Still burning after " + i + " bucket(s).");
            }
            Assert.That(fire.Douse(), Is.True);
            Assert.That(fire.IsBurning, Is.False);
            Assert.That(fire.Effects.All(e => !e.activeSelf), Is.True, "The flames and smoke go out with it.");
            await wren.HearPlayerAsync("Is it out?");
            Assert.That(wren.Last.OfferedActions, Does.Not.Contain("fetch_water"));
        }

        // ---- The bell ------------------------------------------------------------------------------

        // #153's bell_cooldown: "the same threat cannot ring the bell twice within 30 s". Availability
        // already enforces it, so there is no guard: the bell is not offered while it rings, it rings
        // for at least 30 s, and an answer the model gave before it rang is refused by Execute's
        // re-check. This test holds both guards' answers until both were asked with the bell offered.
        [Test]
        public async Task TwoGuardsAnsweringTheBellAtOnceRingItOnce()
        {
            var alarm = bramBody.Alarm;
            Assert.That(alarm.RingSeconds, Is.GreaterThanOrEqualTo(30f), "The bell must ring at least the 30 s #153 asks between two rings.");
            int rang = 0;
            void Count(VillageBody _) => rang++;
            alarm.Rang += Count;
            try
            {
                provider.Otherwise(ScriptedReply.Decide("sound_alarm", "no_target", "To arms!"));
                provider.Hold = true;
                var first = bram.HearPlayerAsync("Raiders!");
                for (int i = 0; i < 50 && provider.RequestCount == 0; i++) await Task.Delay(10);
                var second = wren.HearPlayerAsync("Raiders!");
                provider.Hold = false;
                provider.Release();
                await first;
                await second;
                await Idle();
                string seen = string.Join("; ", commands.Recorder.Records.Select(r => r.Agent + " " + r.Source + " " + r.Action + " " + r.Outcome +
                    " [" + string.Join(",", r.OfferedActions) + "] " + r.Guard + " " + r.GuardReason));
                Assert.That(rang, Is.EqualTo(1), "The bell rang twice for one raid. " + seen);
                // Both were offered the bell; whichever answer came back second found it ringing.
                var answers = commands.Recorder.Records.Where(r => r.Source == "player").ToList();
                Assert.That(answers.Select(r => r.Agent), Is.EquivalentTo(new[] { "Bram", "Wren" }));
                Assert.That(answers.All(r => r.OfferedActions.Contains("sound_alarm")), Is.True, "Both guards must have been offered the bell before it rang. " + seen);
                Assert.That(answers.Select(r => r.Outcome), Is.EquivalentTo(new[] { "executed", "refused" }), seen);
                var late = answers.Single(r => r.Outcome == "refused");
                Assert.That(late.Guard, Is.EqualTo("SchemaLegalityGuard@Execute"), "Execute's re-check refuses a bell that is already ringing. " + seen);
            }
            finally { alarm.Rang -= Count; }
        }

        // ---- The presenter -------------------------------------------------------------------------

        // G12: the reset replaces an agent's AgentBehaviour, and the new one must keep every setting of
        // the old, the ones #208 added (Target Categories, Only Actions) and any the framework adds
        // later. On an agent of its own, every setting away from its default: the scene's agents have
        // already been through SetUp's reset, so they cannot show what one loses.
        [Test]
        public void AResetKeepsEveryAgentSetting()
        {
            var probe = new GameObject("reset probe");
            try
            {
                var old = probe.AddComponent<AgentBehaviour>();
                old.Profile = bram.Behaviour.Profile;
                old.Provider = config;
                old.FindTargetsNearby = false;
                old.Targets = new ProximityTargetSource { Radius = 12f, Cap = 3, RequireLineOfSight = true, Categories = new[] { "place" } };
                old.NoticeNearby = true;
                old.Observations = new ProximityObservationSource { Radius = 7f, Cap = 2, RequireLineOfSight = true };
                old.OnlyActions = new[] { "patrol" };
                old.TargetCategories = new[] { "place", "creature" };
                var mind = probe.AddComponent<AgentMind>();
                string before = JsonUtility.ToJson(old);

                mind.UseProvider(config);
                Assert.That(mind.Behaviour, Is.Not.SameAs(old), "The reset kept the old component, so its agent and memory.");
                Assert.That(JsonUtility.ToJson(mind.Behaviour), Is.EqualTo(before), "The reset lost a setting.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        [Test]
        public async Task AResetDropsADecisionStillWithTheModel()
        {
            provider.Otherwise(ScriptedReply.Decide("follow_player", "no_target", "Understood."));
            provider.Hold = true;
            var asking = bram.HearPlayerAsync("Walk with me.");
            for (int i = 0; i < 50 && provider.RequestCount == 0; i++) await Task.Delay(10);
            commands.ResetAgents();
            // The reset cancels the old request, so the provider is left nothing to answer.
            Assert.That(provider.HeldCount, Is.Zero, "The reset left the old agent's request with the model.");
            provider.Hold = false;
            await asking;
            Assert.That(bramBody.IsFollowing, Is.False, "An answer for the agent before the reset must not run.");
            Assert.That(bram.IsDeciding, Is.False);
            Assert.That(bram.Last, Is.Null, "The fresh agent has asked nothing yet.");
        }

        // The landing review of #157: F1 cancels what the old agents still have with the model, so
        // the one shared slot is free at once. Before, the next stage waited for the old answer.
        [Test]
        public async Task AResetFreesTheQueueWithoutWaitingForTheOldAnswer()
        {
            provider.Otherwise(ScriptedReply.Decide("follow_player", "no_target", "Understood."));
            provider.Hold = true;
            var stale = bram.HearPlayerAsync("Walk with me.");
            for (int i = 0; i < 50 && provider.RequestCount == 0; i++) await Task.Delay(10);
            Assert.That(provider.RequestCount, Is.EqualTo(1), "The first line never reached the provider.");
            commands.ResetAgents();

            provider.Hold = false;
            provider.Otherwise(ScriptedReply.Decide("none", "no_target", "Morning."));
            var fresh = bram.HearPlayerAsync("Hello?").GetAwaiter();
            for (int i = 0; i < 200 && !fresh.IsCompleted; i++) await Task.Delay(10);
            Assert.That(fresh.IsCompleted, Is.True, "The fresh line waited behind the old agent's request, which was never released.");
            Assert.That(provider.RequestCount, Is.EqualTo(2));
            Assert.That(bram.Last.Outcome, Is.EqualTo("none"));
            await stale;
            Assert.That(bramBody.IsFollowing, Is.False, "The old agent's answer ran after the reset.");
        }

        // F3 pressed again while a thief runs with a ware puts the ware back before the next one comes.
        [Test]
        public void SendingAThiefAgainPutsBackWhatTheFirstTook()
        {
            var bread = merchant.Stock.Single(t => t.Id == "bread");
            thief.Begin();
            thief.Grab();
            Assert.That(bread.gameObject.activeSelf, Is.False, "The first thief took nothing.");
            thief.Begin();
            Assert.That(bread.gameObject.activeSelf, Is.True, "The ware the first thief held is lost until the stall restocks.");
        }

        [Test]
        public void TheSituationsAreInTheSceneAndWired()
        {
            var presenter = commands.Presenter;
            Assert.That(presenter, Is.Not.Null);
            Assert.That(presenter.Village, Is.EqualTo(commands));
            Assert.That(new object[] { presenter.Wolf, presenter.Thief, presenter.Fire, presenter.Watch, presenter.Alarm, presenter.WolfFrom, presenter.WolfTo },
                Has.All.Not.Null);
            Assert.That(VillagePresenter.Keys.Select(k => k.key), Is.EqualTo(new[] { "F1", "F2", "F3", "F4", "F5", "F6", "F12" }));
            Assert.That(commands.Minds.Where(m => m.GetComponent<NightWatch>() != null), Is.EqualTo(new[] { wren }), "Wren alone keeps the night watch.");
            Assert.That(commands.Minds.Where(m => m.Agent.Actions.TryGet("chase", out _, out _)), Is.EqualTo(new[] { wren }), "Wren alone chases.");
            Assert.That(commands.Minds.Where(m => m.Agent.Actions.TryGet("fetch_water", out _, out _)), Is.EqualTo(new[] { wren }), "Wren alone carries water.");
            Assert.That(wrenBody.Bucket, Is.Not.Null);
            Assert.That(watch.Relieved, Is.EqualTo(bramBody));
            Assert.That(watch.Day, Is.EqualTo(commands.Day));
            Assert.That(gate.gameObject.layer, Is.EqualTo(2), "The gate's blocker must not hide anything from a guard's sight.");
            foreach (var guard in new[] { bramBody, wrenBody })
            {
                Assert.That(guard.Fire, Is.EqualTo(fire), guard.name);
                Assert.That(guard.GetComponent<GuardSenses>().Thief, Is.EqualTo(thief), guard.name);
                Assert.That(guard.GetComponent<GuardSenses>().Fire, Is.EqualTo(fire), guard.name);
            }
            Assert.That(thief.Merchant, Is.EqualTo(merchant));
            Assert.That(thief.GetComponent<Targetable>().Category, Is.EqualTo("stranger"));
            Assert.That(fire.Well.GetComponent<Targetable>().Id, Is.EqualTo("well"));
            Assert.That(wren.Behaviour.Validate(out string problem), Is.True, problem);

            // As the scene saves them: the thief away, the fire out, the gate open.
            string saved = File.ReadAllText(DemoLauncher.VillagePath).Replace("\r\n", "\n");
            Assert.That(Regex.IsMatch(saved, @"m_Name: thief\n(?:  .*\n)*?  m_IsActive: 0"), Is.True, "The thief must be saved inactive.");
        }

        [Test]
        public async Task TheNewActionsExamplesAreNotEvaluationPrompts()
        {
            // Every action's few-shot example is only built while it is offered, so offer each.
            thief.Begin();
            thief.Grab();
            await Idle();
            thief.transform.position = wren.transform.position + new Vector3(0, 0, -8f);
            Physics.SyncTransforms();
            await wren.HearPlayerAsync("Look!");
            fire.Ignite();
            await Idle();
            Night(true);
            await wren.HearPlayerAsync("Let me in!");
            var offered = commands.Recorder.Records.SelectMany(r => r.OfferedActions).ToList();
            Assert.That(offered, Does.Contain("chase").And.Contain("call_guard").And.Contain("fetch_water").And.Contain("open_gate"));
            provider.Otherwise(ScriptedReply.Decide("open_gate", "no_target", "Understood."));
            await wren.HearPlayerAsync("Let me in!");
            await wren.HearPlayerAsync("Thanks.");
            Assert.That(wren.Last.OfferedActions, Does.Contain("close_gate"));

            var prompts = EvaluationPrompts();
            foreach (var record in commands.Recorder.Records)
            {
                string examples = record.FewShotBlock.ToLowerInvariant();
                foreach (var prompt in prompts)
                    Assert.That(Regex.IsMatch(examples, @"\b" + Regex.Escape(prompt) + @"\b"), Is.False,
                        record.Agent + "'s examples contain the evaluation prompt \"" + prompt + "\"");
            }
        }

        // ---- Helpers ---------------------------------------------------------------------------------

        // Night falls or ends as the cycle would say it: the hour, then the event the watch hears.
        private void Night(bool night)
        {
            commands.Day.TimeOfDay = night ? 0.9f : 0.45f;
            watch.Nightfall(night);
        }

        private async Task Ask(AgentMind mind, string source)
        {
            switch (source)
            {
                case "player":
                    await mind.HearPlayerAsync("Hello");
                    break;
                case "zone":
                    await mind.ReportEventAsync("zone", "Event", "The player has walked into the armory, which is off limits.", DecisionPriority.PlayerFacing);
                    break;
                case "wolf":
                    await mind.ReportEventAsync("wolf", "Event", "A wolf has come out of the woods.", DecisionPriority.Background);
                    break;
                case "quiet":
                    await mind.ReportEventAsync("quiet", "Time", "Time passes quietly at your post.", DecisionPriority.Background);
                    break;
                default:
                    throw new ArgumentException(source);
            }
        }

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

        private Targetable Stock(string id) => merchant.Stock.Single(t => t.Id == id);

        private async Task Idle()
        {
            for (int i = 0; i < 300 && commands.Minds.Any(m => m.IsDeciding || m.HasPending); i++) await Task.Delay(10);
            Assert.That(commands.IsDeciding, Is.False, "An agent never finished deciding.");
        }

        private List<DecisionRecord> Records(string agent) => commands.Recorder.Records.Where(r => r.Agent == agent).ToList();

        internal static List<string> EvaluationPrompts()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../UnityProject/Assets/Evaluation/prompts"));
            var prompts = Directory.GetFiles(folder, "*.json")
                .SelectMany(f => JsonUtility.FromJson<PromptFile>(File.ReadAllText(f)).prompts)
                .Select(p => p.stimulus.Trim().TrimEnd('.', '!', '?').ToLowerInvariant())
                .Where(p => p.Length > 0)
                .ToList();
            Assert.That(prompts.Count, Is.GreaterThan(40), "The evaluation prompts were not found at " + folder);
            return prompts;
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
    }
}
