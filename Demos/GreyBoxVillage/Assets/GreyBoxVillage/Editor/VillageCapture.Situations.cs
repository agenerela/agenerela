using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Demos.Shared;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Demos.GreyBoxVillage.Editor
{
    /// <summary>
    /// The capture tool's situations: the presenter's keys played in Play mode, each caught at the
    /// moment an audience would look. The agents answer from the presenter script's replies
    /// (<see cref="PresenterScript"/>), never a model, and the game's senses and events run as they do
    /// in a talk.
    /// </summary>
    /// <remarks>
    /// <c>Unity -batchmode -projectPath Demos/GreyBoxVillage -executeMethod
    /// Demos.GreyBoxVillage.Editor.VillageCapture.CaptureSituationsFromCommandLine -captureOut DIR
    /// [-captureSizes 1920x1080]</c>, with no <c>-quit</c>. <c>CaptureUiFromCommandLine</c> takes
    /// these too, after the UI views.
    /// </remarks>
    public static partial class VillageCapture
    {
        public static void CaptureSituationsFromCommandLine() =>
            Run(SituationShots(Arg("-captureOut") ?? Path.GetFullPath("Logs/captures"), Sizes()), true);

        private static IEnumerator SituationShots(string folder, (int w, int h)[] sizes)
        {
            Directory.CreateDirectory(folder);
            Application.targetFrameRate = 30;
            var commands = Object.FindFirstObjectByType<VillageCommands>();
            var config = ScriptableObject.CreateInstance<CaptureProviderConfig>();
            var panel = Resources.Load<PanelSettings>("VillagePanelSettings");
            var camera = Camera.main;
            var saved = (panel.targetTexture, panel.clearColor, panel.colorClearValue);
            try
            {
                foreach (var (w, h) in sizes)
                {
                    var (world, ui) = Targets(camera, panel, w, h);
                    foreach (var x in Situations(commands, config, folder, "_" + w + "x" + h, world, ui)) yield return x;
                    camera.targetTexture = null;
                    Release(world, ui, panel);
                }
            }
            finally
            {
                (panel.targetTexture, panel.clearColor, panel.colorClearValue) = saved;
                Application.targetFrameRate = -1;
                Object.DestroyImmediate(config);
            }
        }

        // The wolf at the gate, the thief chased, the fire fought, and night at the shut gate, with
        // the presenter's panel showing throughout.
        private static IEnumerable<object> Situations(VillageCommands commands, CaptureProviderConfig config, string folder,
            string size, RenderTexture world, RenderTexture ui)
        {
            var presenter = commands.Presenter;
            var scripted = config.Scripted;
            scripted.Fail = null;
            scripted.Answer = PresenterScript.Answer;
            var events = commands.GetComponent<VillageEvents>();
            if (events != null) events.enabled = true;
            foreach (var mind in commands.Minds)
                if (mind.Senses != null)
                {
                    mind.Senses.enabled = true;
                    mind.Senses.QuietTimerEnabled = false;
                }
            commands.Day.Running = true;
            commands.UseProvider(config);
            if (commands.Board.Expanded) commands.Board.Toggle();
            commands.Book.Show(false);
            commands.Menu.Close();
            if (!presenter.PanelShown) presenter.TogglePanel();
            var merchant = (VillageMerchant)commands.Mind("Odile").Body;
            var guards = commands.Minds.Select(m => m.Body).OfType<VillageGuard>().ToList();
            IEnumerable<object> Snap(string name) => Shoot(folder, name + size, world, ui);

            // F2: the wolf comes up the road to the gate; Bram rings the bell.
            presenter.Press(Key.F1);
            // On the road outside, looking north: the wolf ahead, Bram in the gateway beyond it.
            foreach (var x in Place(commands, new Vector3(-3f, 0, -28f), new Vector3(2f, 0, -16f))) yield return x;
            presenter.Press(Key.F2);
            foreach (var x in Until(() => guards[0].Alarm.IsSounding, 300)) yield return x;
            foreach (var x in Frames(20)) yield return x;
            foreach (var x in Snap("situation_wolf")) yield return x;

            // F3: the thief takes the bread and runs for the road; a guard gives chase. Seen from
            // the square, looking east along the road, then with the framework view open, as in a talk.
            presenter.Press(Key.F1);
            foreach (var x in Place(commands, new Vector3(8f, 0, -4.5f), new Vector3(30f, 0, 1f))) yield return x;
            presenter.Press(Key.F3);
            foreach (var x in Until(() => guards.Any(g => g.IsChasing) && merchant.Thief.transform.position.x > 22f, 600)) yield return x;
            foreach (var x in Snap("situation_thief_chase")) yield return x;
            commands.Board.Toggle();
            foreach (var x in Snap("situation_thief_board")) yield return x;
            commands.Board.Toggle();
            foreach (var x in Until(() => merchant.Thief.State != VillageThief.Phase.Running, 600)) yield return x;
            foreach (var x in Frames(10)) yield return x;
            foreach (var x in Snap("situation_thief_caught")) yield return x;

            // F4: the bread stall burns; the guards carry water from the well.
            presenter.Press(Key.F1);
            foreach (var x in Place(commands, new Vector3(4f, 0, -7.5f), new Vector3(10f, 0, 2.5f))) yield return x;
            presenter.Press(Key.F4);
            foreach (var x in Until(() => guards.Any(g => g.Bucket != null && g.Bucket.activeSelf), 600)) yield return x;
            foreach (var x in Frames(15)) yield return x;
            foreach (var x in Snap("situation_fire")) yield return x;
            foreach (var x in Until(() => !merchant.Fire.IsBurning, 1500)) yield return x;
            foreach (var x in Frames(10)) yield return x;
            foreach (var x in Snap("situation_fire_out")) yield return x;

            // F5: dusk. The gate shuts, Wren takes the watch, and lets the player in.
            presenter.Press(Key.F1);
            var watch = commands.Mind("Wren").GetComponent<NightWatch>();
            presenter.Press(Key.F5);
            foreach (var x in Until(() => watch.OnWatch && !watch.Guard.IsMoving, 600)) yield return x;
            foreach (var x in Place(commands, watch.Gate.transform.position + new Vector3(0.6f, 0, -1.9f), watch.Gate.transform.position)) yield return x;
            foreach (var x in Frames(30)) yield return x;
            foreach (var x in Snap("situation_night_gate_shut")) yield return x;
            var asking = commands.SubmitAsync(PresenterScript.LetMeIn).GetAwaiter();
            foreach (var x in Until(() => asking.IsCompleted, 300)) yield return x;
            foreach (var x in Frames(60)) yield return x;
            foreach (var x in Snap("situation_night_gate_open")) yield return x;

            presenter.Press(Key.F1);
            if (presenter.PanelShown) presenter.TogglePanel();
            scripted.Answer = null;
            foreach (var mind in commands.Minds)
                if (mind.Senses != null) mind.Senses.enabled = false;
            if (events != null) events.enabled = false;
            commands.Day.Running = false;
        }
    }
}
