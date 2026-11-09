using System.Collections.Generic;
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
    /// Slice 3 of #153, "more to explore", in Edit mode where nothing walks: the villagers' routine,
    /// the weather and what it does to a guard's sight, and that none of it gives Bram, the
    /// evaluation agent, an action or a target he did not have. Each rule's test was seen failing
    /// with the rule undone (the PR lists them).
    /// </summary>
    public sealed class VillageExploreTests
    {
        private Scene scene;
        private bool openedScene;
        private VillageCommands commands;
        private AgentMind bram, odile;
        private VillageRoutine routine;
        private VillageWeather weather;
        private VillageAlarm alarm;
        private ScriptedProvider provider;
        private ScriptedProviderConfig config;
        private ProviderConfig original;
        private float hour;
        private readonly List<(Transform t, Vector3 at)> moved = new List<(Transform, Vector3)>();

        // Every target id in the village as slice 2 left it. Nothing in slice 3 is a target: the
        // routine's places, the weather and the woods stay out of every schema.
        private static readonly string[] SliceTwoTargets =
        {
            "village_gate", "well", "market", "campfire", "training_dummy", "shelter", "eastern_road", "bridge", "docks",
            "north_tower", "armory", "lords_keep", "locked_cave", "wolf", "thief", "bram", "wren", "odile", "hale",
            "tomas", "ada", "pell", "mira", "bread", "apple", "rope", "torch", "lantern"
        };

        // What Bram is offered as targets when the player speaks to him, as slice 2 left it.
        internal static readonly string[] BramsTargets =
        {
            "village_gate", "well", "market", "campfire", "training_dummy", "shelter", "eastern_road", "bridge", "docks",
            "north_tower", "armory", "lords_keep", "locked_cave", "wolf", "wren", "odile", "hale",
            "tomas", "ada", "pell", "mira", "no_target"
        };

        [SetUp]
        public void SetUp()
        {
            scene = SceneManager.GetSceneByPath(DemoLauncher.VillagePath);
            openedScene = !scene.isLoaded;
            if (openedScene) scene = EditorSceneManager.OpenScene(DemoLauncher.VillagePath, OpenSceneMode.Additive);
            commands = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<VillageCommands>()).Single();
            bram = commands.Mind("Bram");
            odile = commands.Mind("Odile");
            routine = commands.Routine;
            weather = commands.Weather;
            alarm = ((VillageGuard)bram.Body).Alarm;
            original = bram.Behaviour.Provider;
            config = ScriptableObject.CreateInstance<ScriptedProviderConfig>();
            provider = config.Scripted.Otherwise(ScriptedReply.Decide("none", "no_target", "Understood."));
            commands.UseProvider(config);
            hour = commands.Day.TimeOfDay;
            VillageSituationTests.ResetVillage(commands);
            weather.Set(WeatherKind.Clear);
            moved.Clear();
            foreach (var v in routine.Villagers) Remember(v.transform);
        }

        [TearDown]
        public void TearDown()
        {
            commands.Day.TimeOfDay = hour;
            weather.Set(WeatherKind.Clear);
            alarm.Silence();
            alarm.Tick();
            foreach (var (t, at) in moved) t.position = at;
            foreach (var v in routine.Villagers) v.ResetTo(v.transform.position);
            Physics.SyncTransforms();
            VillageSituationTests.ResetVillage(commands);
            if (openedScene) EditorSceneManager.CloseScene(scene, true);
            else commands.UseProvider(original);
            Object.DestroyImmediate(config);
        }

        private void Remember(Transform t)
        {
            if (moved.All(m => m.t != t)) moved.Add((t, t.position));
        }

        private void At(float h) => commands.Day.TimeOfDay = h / 24f;

        private Villager Villager(string id) => routine.Villagers.Single(v => v.GetComponent<Targetable>().Id == id);

        private static float Flat(Vector3 a, Vector3 b)
        {
            var d = a - b;
            d.y = 0;
            return d.magnitude;
        }

        // ---- The routine -------------------------------------------------------------------------

        [TestCase(3f, VillageRoutine.Home)]
        [TestCase(6f, VillageRoutine.Well)]
        [TestCase(9f, "work")]
        [TestCase(12.5f, VillageRoutine.Square)]
        [TestCase(15f, "work")]
        [TestCase(18f, VillageRoutine.Campfire)]
        [TestCase(22f, VillageRoutine.Home)]
        public void EachVillagerGoesWhereItsScheduleSaysAtEachHour(float h, string expected)
        {
            At(h);
            foreach (var v in routine.Villagers)
            {
                v.ResetTo(v.transform.position);
                v.Step();
                string place = expected == "work" ? v.Job : expected;
                Assert.That(v.Place, Is.EqualTo(place), v.name + " at " + h);
                if (place == VillageRoutine.Home)
                    Assert.That(Flat(v.Goal, v.Home.position), Is.LessThan(0.01f), v.name + " walks to its door.");
                else
                    Assert.That(routine.PointsOf(place).Min(p => Flat(v.Goal, p.position)), Is.LessThan(1.8f),
                        v.name + " walks somewhere other than the " + place + ".");
            }
            Assert.That(routine.Villagers.Select(v => v.Job).Distinct(), Is.EquivalentTo(new[] { VillageRoutine.Fields, VillageRoutine.Market }),
                "Two work the fields and two the market.");
        }

        [Test]
        public void AfterDarkAVillagerGoesInAndTheLightByItsDoorComesOn()
        {
            var ada = Villager("ada");
            At(22f);
            ada.ResetTo(ada.transform.position);
            ada.Step();
            Assert.That(ada.IsHome, Is.False, "Not home until it reaches its door.");
            ada.transform.position = ada.Goal;
            ada.Step();
            Assert.That(ada.IsHome, Is.True);
            Assert.That(ada.HomeLight.enabled, Is.True, "The light by her door is out while she is home.");
            Assert.That(ada.GetComponentsInChildren<Renderer>().All(r => !r.enabled), Is.True, "She is indoors, out of sight.");
            Assert.That(ada.GetComponent<NameTag>().enabled, Is.False);
            Assert.That(ada.GetComponent<Targetable>().IsCurrentlyTargetable, Is.True, "Indoors she is still a person the guards can name.");

            At(6f);
            ada.Step();
            Assert.That(ada.IsHome, Is.False);
            Assert.That(ada.Place, Is.EqualTo(VillageRoutine.Well));
            Assert.That(ada.HomeLight.enabled, Is.False);
            Assert.That(ada.GetComponentsInChildren<Renderer>().All(r => r.enabled), Is.True);
        }

        [Test]
        public async Task TheBellInterruptsTheRoutineAndTheRoutineResumesAfterIt()
        {
            var tomas = Villager("tomas");
            At(9f);
            tomas.ResetTo(tomas.transform.position);
            tomas.Step();
            Assert.That(tomas.Place, Is.EqualTo(VillageRoutine.Fields));

            alarm.Ring();
            alarm.Tick();
            Assert.That(tomas.InCover, Is.True);
            Assert.That(tomas.Place, Is.EqualTo(VillageRoutine.Shelter));
            Assert.That(Flat(tomas.Goal, alarm.Shelter.position), Is.LessThan(3f), "He runs for the shelter.");
            At(12.5f);
            tomas.Step();
            Assert.That(tomas.Place, Is.EqualTo(VillageRoutine.Shelter), "The hour moved him while the bell rang.");

            alarm.Silence();
            alarm.Tick();
            Assert.That(tomas.InCover, Is.False, "The bell stopped and nobody told him.");
            tomas.Step();
            Assert.That(tomas.Place, Is.EqualTo(VillageRoutine.Square), "After the bell he goes back to his day, where the hour has it.");

            // At night, from his own house: out to the shelter, and home again after.
            At(22f);
            tomas.CatchUp();
            Assert.That(tomas.IsHome, Is.True);
            alarm.Ring();
            alarm.Tick();
            Assert.That(tomas.IsHome, Is.False, "He stayed indoors with the bell ringing.");
            Assert.That(tomas.InCover, Is.True);
            alarm.Silence();
            alarm.Tick();
            tomas.Step();
            Assert.That(tomas.Place, Is.EqualTo(VillageRoutine.Home));
            // Every agent in earshot was asked about the bell.
            await Idle();
        }

        [Test]
        public async Task TheAgentsAreToldWhoIsWhere()
        {
            At(9f);
            Stand("tomas", VillageRoutine.Fields, 0);
            Stand("pell", VillageRoutine.Fields, 2);
            Stand("ada", VillageRoutine.Market, 0);
            Stand("mira", VillageRoutine.Market, 1);
            await bram.HearPlayerAsync("Busy morning?");
            Assert.That(bram.Last.Observations,
                Does.Contain("In the village: the market is busy (ada, mira), the square is empty, tomas and pell are working the fields outside the gate."));

            At(12.5f);
            foreach (var (id, i) in new[] { ("tomas", 0), ("ada", 1), ("pell", 2), ("mira", 3) }) Stand(id, VillageRoutine.Square, i);
            await odile.HearPlayerAsync("Quiet at the stall?");
            Assert.That(odile.Last.Observations,
                Does.Contain("In the village: the market is empty, the square is busy (tomas, ada, pell, mira)."), "Odile is told too.");

            At(22f);
            foreach (var v in routine.Villagers) v.CatchUp();
            await bram.HearPlayerAsync("All quiet?");
            Assert.That(bram.Last.Observations, Does.Contain("In the village: everyone is at home for the night, and the streets are empty."));

            alarm.Ring();
            alarm.Tick();
            await Idle();
            await bram.HearPlayerAsync("Where is everyone?");
            Assert.That(bram.Last.Observations, Does.Contain("In the village: the villagers are taking cover in the shelter."));
        }

        private void Stand(string id, string place, int point)
        {
            var v = Villager(id);
            v.ResetTo(routine.PointsOf(place)[point].position);
        }

        [Test]
        public void EveryPlaceOfTheRoutineIsOnTheNavMesh()
        {
            var navigation = scene.GetRootGameObjects().Select(g => g.GetComponent<VillageNavigation>()).Single(n => n != null);
            var instance = NavMesh.AddNavMeshData(navigation.BakedData);
            try
            {
                var from = routine.PointsOf(VillageRoutine.Square)[0].position;
                var points = routine.Spots.SelectMany(s => s.Points.Select(p => (s.Id, p))).ToList();
                Assert.That(points.Count(x => x.Id == VillageRoutine.Home), Is.EqualTo(4), "Every villager has a door to go home to.");
                foreach (var (id, p) in points)
                {
                    var path = new NavMeshPath();
                    Assert.That(NavMesh.CalculatePath(from, p.position, NavMesh.AllAreas, path), Is.True, id + " " + p.name);
                    Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), id + " " + p.name);
                }
            }
            finally { instance.Remove(); }
        }

        // ---- The weather -------------------------------------------------------------------------

        [Test]
        public async Task FogShortensAGuardsSightAndHidesAWolfSeenInClearWeather()
        {
            var wolf = bram.Senses.Wolf;
            Remember(wolf.transform);
            wolf.transform.position = bram.transform.position + new Vector3(20, 0, 0);
            Physics.SyncTransforms();
            Assert.That(bram.Senses.CanSee(wolf.transform, bram.Senses.WolfSightRange), Is.True, "In clear weather Bram sees a wolf 20 m off.");
            bram.Senses.ForgetWolf();
            bram.Senses.Sense();
            await Idle();
            Assert.That(Wolves(), Is.EqualTo(1), "In clear weather the wolf at 20 m is reported.");

            weather.Set(WeatherKind.Rain);
            Assert.That(bram.Senses.CanSee(wolf.transform, bram.Senses.WolfSightRange), Is.True, "Rain lets him see 22 m.");

            weather.Set(WeatherKind.Fog);
            Assert.That(bram.Senses.CanSee(wolf.transform, bram.Senses.WolfSightRange), Is.False, "Fog did not shorten his sight.");
            bram.Senses.ForgetWolf();
            bram.Senses.Sense();
            await Idle();
            Assert.That(Wolves(), Is.EqualTo(1), "In fog the wolf at 20 m was still reported.");
            await bram.HearPlayerAsync("See anything?");
            Assert.That(bram.Last.Observations, Does.Not.Contain("A wolf is"));

            wolf.transform.position = bram.transform.position + new Vector3(0, 0, 10);
            Physics.SyncTransforms();
            bram.Senses.Sense();
            await Idle();
            Assert.That(Wolves(), Is.EqualTo(2), "In fog the wolf is still seen once it is close.");

            // The other things a guard watches for go the same way: the player 20 m off.
            Remember(commands.Player);
            commands.Player.position = bram.transform.position + new Vector3(-20, 0, 0);
            Physics.SyncTransforms();
            Assert.That(bram.Senses.CanSee(commands.Player), Is.False, "In fog Bram still saw the player 20 m off.");
            weather.Set(WeatherKind.Clear);
            Assert.That(bram.Senses.CanSee(commands.Player), Is.True);
        }

        private int Wolves() => commands.Recorder.Records.Count(r => r.Agent == "Bram" && r.Source == "wolf");

        [Test]
        public async Task EveryAgentIsToldTheWeatherAndHowFarItCanSee()
        {
            await bram.HearPlayerAsync("Nice day?");
            foreach (var line in new[] { "The sky is overcast", "It is raining", "Fog lies" })
                Assert.That(bram.Last.Observations, Does.Not.Contain(line), "Clear weather adds nothing to the prompt.");

            weather.Set(WeatherKind.Overcast);
            await bram.HearPlayerAsync("Nice day?");
            Assert.That(bram.Last.Observations, Does.Contain("The sky is overcast."));

            weather.Set(WeatherKind.Rain);
            await odile.HearPlayerAsync("Wet enough for you?");
            Assert.That(odile.Last.Observations, Does.Contain("It is raining; you can see about 22 m."));

            weather.Set(WeatherKind.Fog);
            await bram.HearPlayerAsync("Can you see the road?");
            Assert.That(bram.Last.Observations, Does.Contain("Fog lies over the village; you can see about 15 m."));
        }

        // Bram is the evaluation agent: the weather changes what he sees, never what he can do.
        [Test]
        public async Task WeatherNeverAddsOrTakesAwayAnyOfBramsActions()
        {
            var registered = bram.Agent.Actions.Definitions.Select(d => d.Id).ToList();
            var wolf = bram.Senses.Wolf;
            Remember(wolf.transform);
            var clear = new Dictionary<string, List<string>>();
            foreach (var kind in new[] { WeatherKind.Clear, WeatherKind.Overcast, WeatherKind.Rain, WeatherKind.Fog })
            {
                weather.Set(kind);
                // A wolf out of sight in any weather, or close enough to see in any weather and for the
                // bell: at 5 m, and at 14.99 m, just inside the bell's 15 m. From a guard's eyes that wolf
                // is 15.002 m off, so a fog limit measured from the eyes would hide it.
                foreach (var (source, metres) in new[] { ("player", 60f), ("zone", 60f), ("wolf", 5f), ("wolf", 14.99f), ("quiet", 60f), ("bell", 60f) })
                {
                    wolf.transform.position = bram.transform.position + new Vector3(metres, 0, 0);
                    Physics.SyncTransforms();
                    if (source == "player") await bram.HearPlayerAsync("What now?");
                    else await bram.ReportEventAsync(source, "Event", "Something is happening (" + source + ").", DecisionPriority.Background);
                    await Idle();
                    var offered = bram.Last.OfferedActions;
                    string key = source + " " + metres + " m";
                    if (kind == WeatherKind.Clear) clear[key] = offered;
                    else Assert.That(offered, Is.EqualTo(clear[key]), kind + ", " + key);
                    if (source == "wolf") Assert.That(offered, Does.Contain("sound_alarm"), kind + ", " + key + ": the bell is for a wolf this close.");
                    Assert.That(bram.Last.OfferedTargets, Is.EquivalentTo(BramsTargets), kind + ", " + key);
                }
            }
            Assert.That(bram.Agent.Actions.Definitions.Select(d => d.Id), Is.EqualTo(registered));
        }

        [Test]
        public void TheWeatherChangesByItselfAFewTimesADayAndF6GoesRoundItAll()
        {
            Assert.That(weather.Current, Is.EqualTo(WeatherKind.Clear));
            weather.Advance(weather.ChangeEveryHours - 0.1f);
            Assert.That(weather.Current, Is.EqualTo(WeatherKind.Clear));
            var seen = new List<WeatherKind>();
            for (int i = 0; i < VillageWeather.Pattern.Count; i++)
            {
                weather.Advance(weather.ChangeEveryHours);
                seen.Add(weather.Current);
            }
            Assert.That(seen.Distinct(), Is.EquivalentTo(new[] { WeatherKind.Clear, WeatherKind.Overcast, WeatherKind.Rain, WeatherKind.Fog }),
                "A day of changes leaves some weather out.");
            Assert.That(24f / weather.ChangeEveryHours, Is.InRange(3f, 6f), "A few changes a day.");

            weather.Changes = false;
            try
            {
                var now = weather.Current;
                weather.Advance(48f);
                Assert.That(weather.Current, Is.EqualTo(now), "Switched off, it still changed.");
            }
            finally { weather.Changes = true; }

            weather.Set(WeatherKind.Clear);
            var cycle = new List<WeatherKind>();
            for (int i = 0; i < 4; i++)
            {
                Assert.That(commands.Presenter.Press(UnityEngine.InputSystem.Key.F6), Is.True);
                cycle.Add(weather.Current);
            }
            Assert.That(cycle, Is.EqualTo(new[] { WeatherKind.Overcast, WeatherKind.Rain, WeatherKind.Fog, WeatherKind.Clear }));
            Assert.That(VillagePresenter.Keys.Select(k => k.key), Does.Contain("F6"), "The panel does not list F6.");
        }

        // ---- Nothing new reaches a schema ---------------------------------------------------------

        [Test]
        public void SliceThreeAddsNoTarget()
        {
            var ids = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Targetable>(true)).Select(t => t.Id).ToList();
            Assert.That(ids, Is.EquivalentTo(SliceTwoTargets), "A target was added or lost.");
        }

        [Test]
        public async Task BramIsOfferedTheSameTargetsAtEveryHourAndInEveryWeather()
        {
            foreach (float h in new[] { 6f, 9f, 12.5f, 18.5f, 22f })
                foreach (var kind in new[] { WeatherKind.Clear, WeatherKind.Rain, WeatherKind.Fog })
                {
                    At(h);
                    weather.Set(kind);
                    foreach (var v in routine.Villagers) v.CatchUp();
                    await bram.HearPlayerAsync("Anything to report?");
                    Assert.That(bram.Last.OfferedTargets, Is.EquivalentTo(BramsTargets), h + " h, " + kind);
                    Assert.That(bram.Last.TargetsDropped, Is.Empty, h + " h, " + kind);
                }
        }

        [Test]
        public void TheRoutineAndTheWeatherAreInTheSceneAndWired()
        {
            Assert.That(routine, Is.Not.Null);
            Assert.That(routine.Day, Is.EqualTo(commands.Day));
            Assert.That(routine.Villagers, Is.EquivalentTo(alarm.Villagers), "The bell's villagers and the routine's are the same four.");
            foreach (var v in routine.Villagers)
            {
                Assert.That(v.Routine, Is.EqualTo(routine), v.name);
                Assert.That(v.Home, Is.Not.Null, v.name);
                Assert.That(v.HomeLight, Is.Not.Null, v.name);
                Assert.That(v.HomeLight.enabled, Is.False, v.name + "'s light is saved on.");
            }
            foreach (var id in new[] { VillageRoutine.Well, VillageRoutine.Market, VillageRoutine.Fields, VillageRoutine.Square, VillageRoutine.Campfire })
                Assert.That(routine.PointsOf(id), Is.Not.Empty, id);
            Assert.That(weather, Is.Not.Null);
            Assert.That(weather.Day, Is.EqualTo(commands.Day));
            Assert.That(weather.Rain, Is.Not.Null);
            Assert.That(weather.Mist, Is.Not.Null);
            Assert.That(weather.Rain.emission.rateOverTime.constant, Is.Zero, "The rain is saved falling.");
            Assert.That(commands.Presenter.Weather, Is.EqualTo(weather));
            Assert.That(commands.Presenter.Routine, Is.EqualTo(routine));
            foreach (var mind in commands.Minds.Where(m => m.Senses != null))
                Assert.That(mind.Senses.Weather, Is.EqualTo(weather), mind.Name + " sees as far in fog as in sunshine.");
        }

        private async Task Idle()
        {
            for (int i = 0; i < 300 && commands.Minds.Any(m => m.IsDeciding || m.HasPending); i++) await Task.Delay(10);
            Assert.That(commands.IsDeciding, Is.False, "An agent never finished deciding.");
        }
    }
}
