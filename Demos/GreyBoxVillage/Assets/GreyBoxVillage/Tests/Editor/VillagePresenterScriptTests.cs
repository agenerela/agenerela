using System;
using System.Collections;
using System.Linq;
using Demos.GreyBoxVillage.Editor;
using Demos.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Demos.GreyBoxVillage.Tests
{
    /// <summary>
    /// The README's five-minute presenter script, played headless in Play mode against the scripted
    /// replies (<see cref="PresenterScript"/>), stage by stage: the wolf, the thief, the fire and
    /// dusk at the gate. Everything walks, runs and burns as it does in front of an audience; only
    /// the model is scripted. Each key must leave an agent asked within a few seconds.
    /// </summary>
    public sealed class VillagePresenterScriptTests
    {
        // How long a key may take to get an agent asked, in game seconds ("a few seconds").
        private const float AskedWithin = 6f;

        private FakeVillageConfig config;
        private VillageCommands commands;
        private VillagePresenter presenter;

        [UnityTest]
        public IEnumerator ThePresenterScriptPlaysEndToEnd()
        {
            EditorSceneManager.OpenScene(DemoLauncher.VillagePath);
            yield return new EnterPlayMode();
            yield return null;
            // The script is its own enumerator, made once Play mode has started. Written inline, with
            // its locals hoisted into this one across the domain reload, it failed with a
            // NullReferenceException at its first statements; and a failure inside it is logged here
            // with its whole stack.
            var script = Script();
            while (true)
            {
                bool more;
                try { more = script.MoveNext(); }
                catch (Exception e)
                {
                    Debug.Log("[presenter test] " + e);
                    throw;
                }
                if (!more) break;
                yield return script.Current;
            }
            yield return new ExitPlayMode();
        }

        private IEnumerator Script()
        {
            commands = UnityEngine.Object.FindFirstObjectByType<VillageCommands>();
            Assert.That(commands, Is.Not.Null, "No village in Play mode.");
            presenter = commands.Presenter;
            Assert.That(presenter, Is.Not.Null, "The village has no presenter.");
            Assert.That(commands.Minds, NUnit.Framework.Has.All.Not.Null);
            config = ScriptableObject.CreateInstance<FakeVillageConfig>();
            config.Fake = new FakeVillageProvider(FakeVillageProvider.Decision("none", "no_target")) { Script = PresenterScript.Answer };
            commands.UseProvider(config);
            // The quiet timers would only add "none"s between the stages.
            foreach (var mind in commands.Minds)
                if (mind.Senses != null) mind.Senses.QuietTimerEnabled = false;
            Time.timeScale = 3f;
            var bram = commands.Mind("Bram");
            var wren = commands.Mind("Wren");
            var odile = commands.Mind("Odile");
            var hale = commands.Mind("Hale");
            var merchant = (VillageMerchant)odile.Body;
            // Every F1 starts a new recorder, so each stage's records are kept before the next F1.
            var all = new System.Collections.Generic.List<DecisionRecord>();
            void Keep()
            {
                foreach (var r in commands.Recorder.Records)
                    if (!all.Contains(r)) all.Add(r);
            }

            // 0. The framework view is open for the whole talk, and the keys still work.
            commands.Board.Toggle();
            Assert.That(commands.Board.Expanded, Is.True);
            Assert.That(presenter.Press(Key.F12), Is.True, "F12 must work with the framework view open.");
            Assert.That(presenter.PanelShown, Is.True);
            yield return null;
            Assert.That(presenter.Bounds, Is.Not.Null, "The panel must say where it is, so world labels keep out from under it.");

            // But not while the player types: F1 to F5 are letters to a text field.
            var field = commands.Input.GetComponent<UIDocument>().rootVisualElement.Q<TextField>("VillageCommand");
            Stand(bram.transform.position + bram.transform.forward * 3f);
            for (int i = 0; i < 3; i++) yield return null;
            commands.Input.StartTyping();
            field.Focus();
            yield return null;
            Assert.That(commands.Input.IsTyping, Is.True);
            Assert.That(presenter.Press(Key.F3), Is.False, "A key reached the village while the player was typing.");
            Assert.That(merchant.Thief.gameObject.activeSelf, Is.False);
            using (var e = KeyDownEvent.GetPooled('\0', KeyCode.Escape, EventModifiers.None)) field.SendEvent(e);
            yield return null;
            Assert.That(commands.Input.IsTyping, Is.False);

            // F1 puts back what the player picked up, or a restarted quest could not be finished.
            var lantern = UnityEngine.Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None).Single(p => p.ItemId == "old_lantern");
            var lanternAt = lantern.transform.position;
            lantern.Collect(commands.Inventory);
            Assert.That(commands.Inventory.Count("old_lantern"), Is.EqualTo(1));

            // 1. F2, the wolf: Bram sees it close and rings the bell, Hale hears it and sends a guard.
            Assert.That(presenter.Press(Key.F1), Is.True);
            Assert.That(commands.Inventory.Count("old_lantern"), Is.Zero);
            Assert.That(lantern.gameObject.activeSelf, Is.True, "F1 left the lantern picked up, and Wren's quest could not be finished again.");
            Assert.That(Vector3.Distance(lantern.transform.position, lanternAt), Is.LessThan(0.01f));
            yield return null;
            int before = commands.Recorder.Records.Count;
            presenter.Press(Key.F2);
            yield return Until(() => Asked(bram, "wolf"), AskedWithin);
            Assert.That(Asked(bram, "wolf"), Is.True, "F2: Bram was not asked about the wolf within " + AskedWithin + " s.");
            yield return Until(() => Asked(hale, "bell", "send_guard") && commands.Recorder.Records.Any(r => r.Source == "captain"), 20f);
            var rang = One(bram, "wolf");
            Assert.That(rang.Action, Is.EqualTo("sound_alarm"));
            Assert.That(rang.Outcome, Is.EqualTo("executed"));
            Assert.That(One(hale, "bell").Action, Is.EqualTo("send_guard"), "Hale heard the bell and sent a guard.");
            var order = commands.Recorder.Records.Skip(before).First(r => r.Source == "captain");
            Assert.That(order.Label, Is.EqualTo("Captain"));
            Assert.That(order.Stimulus, Does.Contain("village_gate"));
            yield return Idle();
            Keep();

            // 2. F3, the thief: Odile calls Wren, a guard who sees it running chases it, and the
            // bread goes back on the counter.
            presenter.Press(Key.F1);
            yield return null;
            Assert.That(merchant.FirstOnCounter().Id, Is.EqualTo("bread"));
            presenter.Press(Key.F3);
            yield return Until(() => Asked(odile, "thief"), AskedWithin + 4f);
            Assert.That(Asked(odile, "thief"), Is.True, "F3: Odile was not asked within a few seconds of the thief reaching her counter.");
            yield return Until(() => merchant.Thief.State != VillageThief.Phase.Running && merchant.Thief.State != VillageThief.Phase.Approaching, 40f);
            var call = One(odile, "thief");
            Assert.That(call.Action, Is.EqualTo("call_guard"));
            Assert.That(call.Target, Is.EqualTo("wren"));
            Assert.That(call.Outcome, Is.EqualTo("executed"));
            Assert.That(Asked(wren, "merchant"), Is.True, "Odile's call became Wren's stimulus.");
            Assert.That(commands.Recorder.Records.Any(r => (r.Action == "chase" || r.Action == "attack_target") && r.Target == "thief" && r.Outcome == "executed"), Is.True,
                "No guard went after the thief.");
            Assert.That(merchant.Thief.State, Is.EqualTo(VillageThief.Phase.Caught).Or.EqualTo(VillageThief.Phase.Hidden),
                "The thief got away: " + merchant.Thief.State);
            Assert.That(merchant.Thief.CaughtBy, Is.Not.Null, "The thief got away.");
            Assert.That(merchant.Stock.Single(t => t.Id == "bread").gameObject.activeSelf, Is.True, "The bread is back on the counter.");
            yield return Idle();
            Keep();

            // 3. F4, a fire: whoever sees the smoke fetches water until it is out.
            presenter.Press(Key.F1);
            yield return null;
            var fire = merchant.Fire;
            presenter.Press(Key.F4);
            Assert.That(fire.IsBurning, Is.True);
            yield return Until(() => commands.Recorder.Records.Any(r => r.Source == "fire"), AskedWithin);
            Assert.That(commands.Recorder.Records.Any(r => r.Source == "fire"), Is.True, "F4: nobody was asked about the fire.");
            yield return Until(() => !fire.IsBurning, 90f);
            Assert.That(fire.IsBurning, Is.False, "The fire still burns after " + fire.Trips + " bucket(s).");
            Assert.That(commands.Recorder.Records.Any(r => r.Action == "fetch_water" && r.Outcome == "executed"), Is.True);
            Assert.That(Asked(odile, "fire", "call_guard"), Is.True, "Odile called a guard to the fire.");
            yield return Idle();
            Keep();

            // 4. F5, dusk: the gate shuts, Wren hurries to take the watch and is asked there, then lets
            // the player in and shuts it again. Bram goes off watch.
            presenter.Press(Key.F1);
            yield return null;
            var watch = wren.GetComponent<NightWatch>();
            presenter.Press(Key.F5);
            yield return Until(() => watch.OnWatch, 3f);
            Assert.That(watch.OnWatch, Is.True, "F5: night did not fall.");
            Assert.That(watch.Gate.IsOpen, Is.False);
            yield return Until(() => Asked(wren, "night"), AskedWithin);
            Assert.That(Asked(wren, "night"), Is.True, "F5: Wren was not asked at the gate within " + AskedWithin + " s.");
            Assert.That(One(wren, "night").Observations, Does.Contain("It is night."));
            Stand(watch.Gate.transform.position + new Vector3(0.6f, 0, -1.9f));
            yield return null;
            Assert.That(commands.Nearest(), Is.EqualTo(wren), "At night Wren, not Bram, is the one at the gate.");
            yield return Say(PresenterScript.LetMeIn);
            Assert.That(wren.Last.Action, Is.EqualTo("open_gate"));
            Assert.That(watch.Gate.IsOpen, Is.True);
            yield return Say(PresenterScript.ShutIt);
            Assert.That(wren.Last.Action, Is.EqualTo("close_gate"));
            Assert.That(watch.Gate.IsOpen, Is.False);
            Assert.That(((VillageGuard)bram.Body).CurrentPost, Is.EqualTo(watch.RelievedPost), "Bram is off watch by the campfire.");
            // The villagers skipped the hours F5 jumped: they are at the campfire, inside the gate.
            var routine = commands.Routine;
            foreach (var v in routine.Villagers)
            {
                Assert.That(v.Place, Is.EqualTo(VillageRoutine.Campfire), v.name + " at dusk.");
                Assert.That(routine.SpotOf(v)?.Id, Is.EqualTo(VillageRoutine.Campfire), v.name + " is not by the campfire after F5.");
            }

            // 5. F6, the weather: round to fog, which shortens a guard's sight and every agent is told.
            var weather = presenter.Weather;
            Assert.That(weather.Current, Is.EqualTo(WeatherKind.Clear), "The weather changed during the script.");
            for (int i = 0; i < 3; i++) Assert.That(presenter.Press(Key.F6), Is.True, "F6 must work with the framework view open.");
            Assert.That(weather.Current, Is.EqualTo(WeatherKind.Fog));
            Assert.That(presenter.Status, Does.Contain("fog"));
            yield return Say("Can you see anything out there?");
            Assert.That(wren.Last.Observations, Does.Contain("Fog lies over the village; you can see about 15 m."));
            Assert.That(presenter.Press(Key.F1), Is.True);
            Assert.That(weather.Current, Is.EqualTo(WeatherKind.Clear), "F1 left the fog in.");
            yield return null;

            // Every decision of the run, every stage's, followed the script: nothing was refused or failed.
            Keep();
            Assert.That(all.Count, Is.GreaterThan(10), "The run kept too few records to check.");
            var bad = all.Where(r => r.Outcome == "refused" || r.Outcome == "error").Select(r => r.Agent + " " + r.Source + " " + r.Action).ToList();
            Assert.That(bad, Is.Empty);
        }

        private bool Asked(AgentMind mind, string source, string action = null) =>
            commands.Recorder.Records.Any(r => r.Agent == mind.Name && r.Source == source && r.Done && (action == null || r.Action == action));

        private DecisionRecord One(AgentMind mind, string source) =>
            commands.Recorder.Records.Last(r => r.Agent == mind.Name && r.Source == source);

        private IEnumerator Say(string line)
        {
            var asking = commands.SubmitAsync(line).GetAwaiter();
            yield return Until(() => asking.IsCompleted, 5f);
            Assert.That(asking.IsCompleted, Is.True, "\"" + line + "\" was never answered.");
        }

        private void Stand(Vector3 at)
        {
            var controller = commands.Player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            commands.Player.position = at;
            if (controller != null) controller.enabled = true;
        }

        private static IEnumerator Until(Func<bool> done, float gameSeconds)
        {
            float until = Time.time + gameSeconds;
            while (!done() && Time.time < until) yield return null;
        }

        private IEnumerator Idle()
        {
            yield return Until(() => !commands.Minds.Any(m => m.IsDeciding || m.HasPending), 10f);
            Assert.That(commands.IsDeciding, Is.False, "An agent never finished deciding.");
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            Time.timeScale = 1f;
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }
    }
}
