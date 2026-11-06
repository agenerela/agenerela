using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Demos.GreyBoxVillage.Editor
{
    /// <summary>
    /// The presenter script played once with the real model: the scene's own provider config, so
    /// Ollama answers every decision. It writes down what each agent was asked and decided, which
    /// guards fired and how long each took. One run is an anecdote, not a measurement (hard rule 2).
    /// </summary>
    /// <remarks>
    /// <c>Unity -batchmode -projectPath Demos/GreyBoxVillage -executeMethod
    /// Demos.GreyBoxVillage.Editor.VillageCapture.PresenterLiveFromCommandLine -captureOut DIR</c>, with
    /// no <c>-quit</c> and Ollama running. Take the shared Ollama lock first. It writes
    /// <c>live-run.md</c>, a table per stage, and <c>live-run.jsonl</c>, every record, to DIR.
    /// </remarks>
    public static partial class VillageCapture
    {
        public static void PresenterLiveFromCommandLine() =>
            Run(Live(Arg("-captureOut") ?? Path.GetFullPath("Logs/captures")), true);

        private sealed class LiveStage
        {
            public string Title;
            public readonly List<string> Notes = new List<string>();
            public readonly List<DecisionRecord> Records = new List<DecisionRecord>();
        }

        private static IEnumerator Live(string folder)
        {
            Directory.CreateDirectory(folder);
            var commands = Object.FindFirstObjectByType<VillageCommands>();
            var presenter = commands.Presenter;
            foreach (var mind in commands.Minds)
                if (mind.Senses != null) mind.Senses.QuietTimerEnabled = false;
            var stages = new List<LiveStage>();
            float started = Time.realtimeSinceStartup;
            var merchant = (VillageMerchant)commands.Mind("Odile").Body;
            var watch = commands.Mind("Wren").GetComponent<NightWatch>();

            // Each F1 starts a new recorder, so a stage keeps its records before the next one starts.
            LiveStage Begin(string title)
            {
                if (stages.Count > 0) Keep(commands, stages[stages.Count - 1]);
                var stage = new LiveStage { Title = title };
                stages.Add(stage);
                return stage;
            }

            IEnumerable<object> Wait(System.Func<bool> done, float seconds)
            {
                float until = Time.realtimeSinceStartup + seconds;
                while (!done() && Time.realtimeSinceStartup < until) yield return null;
            }

            IEnumerable<object> Idle() => Wait(() => !commands.Minds.Any(m => m.IsDeciding || m.HasPending), 90f);

            IEnumerable<object> Say(string line)
            {
                var asking = commands.SubmitAsync(line).GetAwaiter();
                return Wait(() => asking.IsCompleted, 60f);
            }

            IEnumerable<object> Before(string agent)
            {
                var t = commands.Mind(agent).transform;
                return Place(commands, t.position + t.forward * 3f, t.position);
            }

            // 0. One line first, so the model is loaded before the audience watches.
            Begin("Warm-up");
            foreach (var x in Before("Bram")) yield return x;
            foreach (var x in Say("Morning, Bram.")) yield return x;

            Begin("Bram at the gate");
            presenter.Press(Key.F1);
            foreach (var x in Before("Bram")) yield return x;
            foreach (var line in new[] { PresenterScript.BramHello, PresenterScript.BramWalk, PresenterScript.BramStay })
                foreach (var x in Say(line)) yield return x;

            var stage = Begin("F2: the wolf");
            presenter.Press(Key.F1);
            foreach (var x in Place(commands, new Vector3(-7f, 0, -11.5f), new Vector3(3f, 0, -19.5f))) yield return x;
            presenter.Press(Key.F2);
            var alarm = ((VillageGuard)commands.Mind("Bram").Body).Alarm;
            foreach (var x in Wait(() => alarm.IsSounding, 12f)) yield return x;
            foreach (var x in Idle()) yield return x;
            if (!alarm.IsSounding)
            {
                // The script's fallback: Bram chased the wolf, so the presenter asks for the bell.
                stage.Notes.Add("Bram did not ring the bell, so the presenter asked: \"" + PresenterScript.RingTheBell + "\"");
                foreach (var x in Before("Bram")) yield return x;
                foreach (var x in Say(PresenterScript.RingTheBell)) yield return x;
            }
            foreach (var x in Wait(() => false, 8f)) yield return x;
            foreach (var x in Idle()) yield return x;
            stage.Notes.Add("Bell " + (alarm.IsSounding ? "ringing" : "silent") + " at the end.");

            stage = Begin("F3: the thief");
            presenter.Press(Key.F1);
            foreach (var x in Place(commands, new Vector3(13f, 0, -7f), new Vector3(27f, 0, 1f))) yield return x;
            presenter.Press(Key.F3);
            foreach (var x in Wait(() => merchant.Thief.State != VillageThief.Phase.Approaching && !merchant.Thief.IsRunning, 45f)) yield return x;
            stage.Notes.Add("Thief: " + merchant.Thief.State + (merchant.Thief.CaughtBy != null ? ", caught by " + merchant.Thief.CaughtBy.name : "") + ".");
            foreach (var x in Idle()) yield return x;

            stage = Begin("F4: a fire");
            presenter.Press(Key.F1);
            foreach (var x in Place(commands, new Vector3(4f, 0, -7.5f), new Vector3(10f, 0, 2.5f))) yield return x;
            presenter.Press(Key.F4);
            foreach (var x in Wait(() => !merchant.Fire.IsBurning, 90f)) yield return x;
            stage.Notes.Add("Fire: " + (merchant.Fire.IsBurning ? "still burning" : "out") + " after " + merchant.Fire.Trips + " bucket(s).");
            foreach (var x in Idle()) yield return x;

            stage = Begin("F5: dusk at the gate");
            presenter.Press(Key.F1);
            presenter.Press(Key.F5);
            // Wren is asked on reaching the gate; the player waits for her answer, as a presenter
            // waits for her line, or the next words find her still answering.
            var wren = commands.Mind("Wren");
            foreach (var x in Wait(() => wren.Last != null && wren.Last.Source == "night" && wren.Last.Done, 30f)) yield return x;
            foreach (var x in Idle()) yield return x;
            // Outside the shut gate, within speaking distance of the watch (6 m), not of Bram.
            foreach (var x in Place(commands, watch.Gate.transform.position + new Vector3(0.6f, 0, -1.9f), watch.Gate.transform.position)) yield return x;
            stage.Notes.Add("Listening at the gate: " + (commands.Nearest() != null ? commands.Nearest().Name : "nobody") + ".");
            foreach (var line in new[] { PresenterScript.LetMeIn, PresenterScript.ShutIt })
            {
                foreach (var x in Say(line)) yield return x;
                stage.Notes.Add("Gate after \"" + line + "\": " + (watch.Gate.IsOpen ? "open" : "shut") + ".");
                foreach (var x in Wait(() => false, 2f)) yield return x;
            }
            foreach (var x in Before("Bram")) yield return x;
            foreach (var x in Say(PresenterScript.BramAtNight)) yield return x;
            Keep(commands, stage);

            var md = new List<string> { "# GreyBoxVillage presenter script, played live", "", "Provider: " + commands.ProviderLabel + ".", "" };
            var jsonl = new List<string>();
            foreach (var s in stages)
            {
                md.Add("## " + s.Title);
                md.Add("");
                foreach (var note in s.Notes) md.Add("- " + note);
                if (s.Notes.Count > 0) md.Add("");
                md.Add("| Agent | Asked by | Stimulus | Offered | Chose | Said | Outcome | Guard | Latency | Queued |");
                md.Add("|---|---|---|---|---|---|---|---|---|---|");
                foreach (var r in s.Records)
                {
                    md.Add(string.Format(CultureInfo.InvariantCulture, "| {0} | {1} | {2} | {3} | `{4}` / `{5}` | {6} | {7} | {8} | {9:0.00} s | {10:0.00} s |",
                        r.Agent, r.Label, Cell(r.Stimulus), string.Join(", ", r.OfferedActions), r.Action, r.Target, Cell(r.Said),
                        r.Outcome, string.IsNullOrEmpty(r.Guard) ? "" : r.Guard + ": " + Cell(r.GuardReason), r.LatencySeconds, r.QueueWaitSeconds));
                    jsonl.Add(JsonUtility.ToJson(r));
                }
                md.Add("");
            }
            md.Add(string.Format(CultureInfo.InvariantCulture, "Whole run: {0:0} s.", Time.realtimeSinceStartup - started));
            File.WriteAllLines(Path.Combine(folder, "live-run.md"), md);
            File.WriteAllLines(Path.Combine(folder, "live-run.jsonl"), jsonl);
            Debug.Log("[VillageCapture] live run written to " + folder);
        }

        private static void Keep(VillageCommands commands, LiveStage stage)
        {
            foreach (var r in commands.Recorder.Records)
                if (!stage.Records.Contains(r)) stage.Records.Add(r);
        }

        private static string Cell(string s) => (s ?? "").Replace("|", "/").Replace("\n", " ");
    }
}
