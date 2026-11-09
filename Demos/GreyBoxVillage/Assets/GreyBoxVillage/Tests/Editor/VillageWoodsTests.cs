using System.Linq;
using System.Threading.Tasks;
using Agenerela;
using Agenerela.Providers;
using Demos.Shared;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Demos.GreyBoxVillage.Tests
{
    /// <summary>
    /// Slice 3 of #153, second part, in Edit mode: the woods north of the river as places to find,
    /// Oswin the hermit, and the village's sound and its volume. None of it is an agent or a target:
    /// Bram, the evaluation agent, is offered what he was before, wherever the player walks. Each
    /// rule's test was seen failing with the rule undone (the PR lists them).
    /// </summary>
    public sealed class VillageWoodsTests
    {
        private Scene scene;
        private bool openedScene;
        private VillageCommands commands;
        private AgentMind bram;
        private Exploration exploration;
        private Hermit hermit;
        private VillageSound sound;
        private ScriptedProvider provider;
        private ScriptedProviderConfig config;
        private ProviderConfig original;
        private float hour;
        private Vector3 playerAt;
        private float volume;

        [SetUp]
        public void SetUp()
        {
            scene = SceneManager.GetSceneByPath(DemoLauncher.VillagePath);
            openedScene = !scene.isLoaded;
            if (openedScene) scene = EditorSceneManager.OpenScene(DemoLauncher.VillagePath, OpenSceneMode.Additive);
            commands = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<VillageCommands>()).Single();
            bram = commands.Mind("Bram");
            exploration = commands.Exploration;
            hermit = commands.Hermit;
            sound = commands.Sound;
            original = bram.Behaviour.Provider;
            config = ScriptableObject.CreateInstance<ScriptedProviderConfig>();
            provider = config.Scripted.Otherwise(ScriptedReply.Decide("none", "no_target", "Understood."));
            commands.UseProvider(config);
            hour = commands.Day.TimeOfDay;
            playerAt = commands.Player.position;
            volume = commands.Menu.Volume;
            VillageSituationTests.ResetVillage(commands);
            exploration.ResetState();
            hermit.ResetState();
        }

        [TearDown]
        public void TearDown()
        {
            commands.Day.TimeOfDay = hour;
            commands.Weather.Set(WeatherKind.Clear);
            commands.Player.position = playerAt;
            commands.Menu.Volume = volume;
            commands.Menu.Muted = false;
            ((VillageGuard)bram.Body).Alarm.Silence();
            sound.Release();
            Physics.SyncTransforms();
            exploration.ResetState();
            hermit.ResetState();
            VillageSituationTests.ResetVillage(commands);
            if (openedScene) EditorSceneManager.CloseScene(scene, true);
            else commands.UseProvider(original);
            Object.DestroyImmediate(config);
        }

        private void Stand(Vector3 at)
        {
            commands.Player.position = at;
            Physics.SyncTransforms();
        }

        // ---- The woods -----------------------------------------------------------------------------

        [Test]
        public void WalkingToAPlaceInTheWoodsRecordsItInTheJournalOnce()
        {
            Assert.That(exploration.Total, Is.EqualTo(6));
            Assert.That(exploration.Places.Select(p => p.Title), Is.EquivalentTo(new[]
                { "The old wood path", "The moonpetal glade", "The wolf's den", "The ruined shrine", "The lookout", "The hermit's hut" }));
            Stand(new Vector3(0, 0.05f, -18f));
            exploration.Check();
            Assert.That(exploration.Count, Is.Zero, "Found from the gate.");

            int toasts = commands.Hud.ToastLog.Count;
            var glade = exploration.Places.Single(p => p.Title == "The moonpetal glade");
            Stand(glade.transform.position + new Vector3(2f, 0, 1f));
            exploration.Check();
            exploration.Check();
            Assert.That(exploration.Count, Is.EqualTo(1));
            Assert.That(exploration.HasFound(glade), Is.True);
            Assert.That(commands.Hud.ToastLog.Skip(toasts), Is.EqualTo(new[] { "Found: The moonpetal glade (1 of 6)" }), "One toast, once.");

            var journal = commands.JournalPlaces();
            Assert.That(VillageBook.PlacesHeading(journal), Is.EqualTo("Places found: 1 of 6"));
            Assert.That(journal.Where(p => p.Found).Select(p => p.Title), Is.EqualTo(new[] { "The moonpetal glade" }));

            foreach (var place in exploration.Places)
            {
                Stand(place.transform.position);
                exploration.Check();
            }
            Assert.That(VillageBook.PlacesHeading(commands.JournalPlaces()), Is.EqualTo("Places found: 6 of 6"));
            exploration.ResetState();
            Assert.That(exploration.Count, Is.Zero);
        }

        [Test]
        public void EveryPlaceInTheWoodsIsOnTheNavMesh()
        {
            var navigation = scene.GetRootGameObjects().Select(g => g.GetComponent<VillageNavigation>()).Single(n => n != null);
            var instance = NavMesh.AddNavMeshData(navigation.BakedData);
            try
            {
                var from = new Vector3(0, 0, 18f);
                var ends = exploration.Places.Select(p => (p.Title, p.transform.position)).ToList();
                ends.Add(("Oswin", hermit.transform.position + hermit.transform.forward * 1.5f));
                ends.Add(("the wolf's den", bram.Senses.Wolf.Den.position));
                foreach (var (name, at) in ends)
                {
                    Assert.That(NavMesh.SamplePosition(at, out var hit, 1.5f, NavMesh.AllAreas), Is.True, name + " is off the NavMesh.");
                    var path = new NavMeshPath();
                    Assert.That(NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, path), Is.True, name);
                    Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), "Over the bridge to " + name);
                }
            }
            finally { instance.Remove(); }
        }

        [Test]
        public void TheHermitSaysHisLinesInTurnAndIsNeitherAnAgentNorATarget()
        {
            Assert.That(hermit.GetComponent<AgentMind>(), Is.Null);
            Assert.That(hermit.GetComponent<AgentBehaviour>(), Is.Null);
            Assert.That(hermit.GetComponentInChildren<Targetable>(true), Is.Null, "Oswin is a target.");
            Assert.That(commands.Minds.Any(m => m.gameObject == hermit.gameObject), Is.False);
            Assert.That(hermit.Lines.Length, Is.GreaterThanOrEqualTo(3));

            Assert.That(hermit.InReach(hermit.transform.position + Vector3.forward * 2f), Is.True);
            Assert.That(hermit.InReach(hermit.transform.position + Vector3.forward * 8f), Is.False);
            for (int i = 0; i < hermit.Lines.Length + 1; i++)
            {
                string said = hermit.Talk();
                Assert.That(said, Is.EqualTo(hermit.Lines[i % hermit.Lines.Length]), "Line " + i);
                Assert.That(hermit.Speech, Is.EqualTo(said));
            }
            hermit.ResetState();
            Assert.That(hermit.Talk(), Is.EqualTo(hermit.Lines[0]), "A reset starts him at his first line again.");
        }

        // Nothing north of the river is a target or an observation: whatever the player finds, Bram
        // is asked with the same schema.
        [Test]
        public async Task WhereverThePlayerWalksInTheWoodsBramIsOfferedTheSameTargets()
        {
            var spots = exploration.Places.Select(p => p.transform.position).Append(hermit.transform.position).ToList();
            foreach (var at in spots)
            {
                Stand(at);
                await bram.HearPlayerAsync("Anything to report?");
                Assert.That(bram.Last.OfferedTargets, Is.EquivalentTo(VillageExploreTests.BramsTargets), "Player at " + at);
                foreach (var word in new[] { "shrine", "glade", "lookout", "hermit", "Oswin", "wolf's den", "old wood" })
                    Assert.That(bram.Last.Observations, Does.Not.Contain(word), "Player at " + at);
            }
        }

        // ---- The sound -----------------------------------------------------------------------------

        [Test]
        public void BirdsSingByDayCricketsByNightAndRainHushesThem()
        {
            sound.Build();
            commands.Day.TimeOfDay = 12f / 24f;
            sound.Tick();
            Assert.That(Volume("birds"), Is.GreaterThan(0.2f));
            Assert.That(Volume("crickets"), Is.Zero);
            Assert.That(Volume("rain"), Is.Zero);
            float birds = Volume("birds");

            commands.Day.TimeOfDay = 0f;
            sound.Tick();
            Assert.That(Volume("birds"), Is.Zero, "Birds at midnight.");
            Assert.That(Volume("crickets"), Is.GreaterThan(0.2f));

            commands.Day.TimeOfDay = 12f / 24f;
            commands.Weather.Set(WeatherKind.Rain);
            commands.Weather.Snap();
            sound.Tick();
            Assert.That(Volume("rain"), Is.GreaterThan(0.4f));
            Assert.That(Volume("birds"), Is.LessThan(birds * 0.5f), "The rain does not hush the birds.");
            commands.Weather.Set(WeatherKind.Clear);
            commands.Weather.Snap();
        }

        [Test]
        public void TheBellIsHeardOnlyWhileItRings()
        {
            sound.Build();
            var alarm = ((VillageGuard)bram.Body).Alarm;
            sound.Tick();
            Assert.That(Volume("bell"), Is.Zero);
            alarm.Ring();
            sound.Tick();
            Assert.That(Volume("bell"), Is.GreaterThan(0.5f));
            Assert.That(sound.Sources["bell"].transform.IsChildOf(alarm.transform), Is.True, "The bell is heard from the bell.");
            alarm.Silence();
            sound.Tick();
            Assert.That(Volume("bell"), Is.Zero);
        }

        [Test]
        public void TheMenuScalesEverySoundAndMuteSilencesThem()
        {
            sound.Build();
            commands.Day.TimeOfDay = 12f / 24f;
            commands.Menu.Volume = 1f;
            sound.Tick();
            float birds = Volume("birds"), campfire = Volume("campfire");
            Assert.That(birds, Is.GreaterThan(0f));

            commands.Menu.SetVolume(0.5f);
            sound.Tick();
            Assert.That(Volume("birds"), Is.EqualTo(birds * 0.5f).Within(1e-4f));
            Assert.That(Volume("campfire"), Is.EqualTo(campfire * 0.5f).Within(1e-4f));

            commands.Menu.ToggleMute();
            sound.Tick();
            Assert.That(sound.Sources.Values.Select(s => s.volume), Has.All.EqualTo(0f), "Muted, something still plays.");
            Assert.That(sound.Effects.Select(s => s.volume), Has.All.EqualTo(0f), "Muted, an effect still plays.");
            commands.Menu.ToggleMute();
            sound.Tick();
            Assert.That(Volume("birds"), Is.EqualTo(birds * 0.5f).Within(1e-4f), "Unmuted, the slider's volume comes back.");
        }

        // #207's Codex review: an effect played through PlayOneShot kept the volume it started with.
        [Test]
        public void AnEffectAlreadyPlayingFollowsTheMenusVolumeAndMute()
        {
            var gate = sound.Gate;
            var clip = AudioClip.Create("test gate", 44100 * 3, 1, 44100, false);
            try
            {
                sound.Gate = clip;
                sound.Build();
                commands.Menu.Volume = 1f;
                sound.Tick();
                sound.GateDoors.Close();
                sound.Tick();
                var voice = sound.Effects.Single(s => s.clip == clip);
                float loud = voice.volume;
                Assert.That(loud, Is.GreaterThan(0.5f), "The gate is not heard shutting.");

                commands.Menu.ToggleMute();
                sound.Tick();
                Assert.That(voice.volume, Is.Zero, "Muted while the gate creaks, it still creaks.");
                commands.Menu.ToggleMute();
                commands.Menu.SetVolume(0.5f);
                sound.Tick();
                Assert.That(voice.volume, Is.EqualTo(loud * 0.5f).Within(1e-4f), "A new volume never reaches the creak.");
            }
            finally
            {
                sound.Gate = gate;
                Object.DestroyImmediate(clip);
            }
        }

        // #207's Codex review: every effect moved one shared source to where it was heard.
        [Test]
        public void TwoEffectsAtOnceEachPlayFromTheirOwnPlace()
        {
            var gate = sound.Gate;
            var splash = sound.Splash;
            var creak = AudioClip.Create("test gate", 44100 * 3, 1, 44100, false);
            var bucket = AudioClip.Create("test splash", 44100 * 2, 1, 44100, false);
            // Nobody is asked about this fire: only its buckets are wanted.
            var events = sound.StallFire.Events;
            try
            {
                sound.StallFire.Events = null;
                sound.Gate = creak;
                sound.Splash = bucket;
                sound.Build();
                sound.Tick();
                sound.GateDoors.Close();
                sound.Tick();
                sound.StallFire.Ignite();
                sound.StallFire.Douse();
                sound.Tick();

                var heardGate = sound.Effects.Single(s => s.clip == creak);
                var heardSplash = sound.Effects.Single(s => s.clip == bucket);
                Assert.That(heardGate, Is.Not.SameAs(heardSplash), "The splash took the gate's source.");
                Assert.That(Vector3.Distance(heardGate.transform.position, sound.GateDoors.transform.position), Is.LessThan(0.01f),
                    "The gate's creak moved to the fire.");
                Assert.That(Vector3.Distance(heardSplash.transform.position, sound.StallFire.transform.position), Is.LessThan(0.01f));
            }
            finally
            {
                sound.StallFire.Events = events;
                sound.Gate = gate;
                sound.Splash = splash;
                Object.DestroyImmediate(creak);
                Object.DestroyImmediate(bucket);
            }
        }

        [Test]
        public void FootstepsAreOnPlanksOnTheBridgeAndTheDocksAndOnDirtElsewhere()
        {
            Assert.That(sound.OnWood(new Vector3(0, 0, 23f)), Is.True, "the bridge");
            Assert.That(sound.OnWood(new Vector3(30f, 0, 22f)), Is.True, "the docks");
            Assert.That(sound.OnWood(new Vector3(0, 0, -2f)), Is.False, "the square");
            Assert.That(sound.OnWood(new Vector3(37f, 0, 39f)), Is.False, "the glade");
        }

        [Test]
        public void TheWoodsTheHermitAndTheSoundAreInTheSceneAndWired()
        {
            Assert.That(exploration.Player, Is.EqualTo(commands.Player));
            Assert.That(exploration.Places, Has.All.Not.Null);
            Assert.That(commands.Presenter.Exploration, Is.EqualTo(exploration));
            Assert.That(commands.Presenter.Hermit, Is.EqualTo(hermit));
            foreach (var place in exploration.Places)
                Assert.That(place.transform.position.z, Is.GreaterThan(26f), place.Title + " is not north of the river.");
            Assert.That(sound.Menu, Is.EqualTo(commands.Menu));
            Assert.That(new Object[] { sound.Day, sound.Weather, sound.Routine, sound.Alarm, sound.GateDoors, sound.StallFire, sound.Wolf, sound.Player },
                Has.All.Not.Null);
            Assert.That(sound.RiverAt, Is.Not.Empty);
            // With the server's clips in Audio/, every one is wired; with none, the village is silent.
            var clips = new[] { sound.Birds, sound.Crickets, sound.River, sound.Market, sound.Campfire, sound.Rain, sound.Owl,
                sound.Bell, sound.StepsDirt, sound.StepsWood, sound.Gate, sound.Splash, sound.Howl, sound.FireRoar };
            bool any = clips.Any(c => c != null);
            if (any) Assert.That(clips, Has.All.Not.Null, "Some clips are wired and some are not.");
        }

        private float Volume(string name) => sound.Sources[name].volume;
    }
}
