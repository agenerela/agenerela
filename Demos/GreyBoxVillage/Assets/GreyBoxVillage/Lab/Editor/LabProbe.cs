using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Agenerela;
using UnityEngine;

namespace Demos.GreyBoxVillage.Lab
{
    public static partial class LabBench
    {
        /// <summary>The probe's lines, kept off every scenario by the rule 4 test.</summary>
        public static readonly string[] ProbeLines = { "Tell me about the road east of here.", "Still with me after all that?" };

        private sealed partial class Session
        {
            /// <summary>
            /// F1 pressed while Bram's request is with the model, on the last arm's provider: did the
            /// request end, did the shared slot free up, did a late answer reach anyone? For the
            /// framework tech lead (docs/live-runs.md): a reset aborting an in-flight Ollama request
            /// had not been seen live.
            /// </summary>
            public IEnumerable<object> ResetProbe()
            {
                var arm = settings.Arms.Last();
                var bram = commands.Mind("Bram");
                foreach (var x in Ready(arm, bram)) yield return x;
                commands.UseProvider(arm.Config);
                presenter.ResetVillage();
                tuning.Apply(arm.Variant);
                yield return null;

                var agent = bram.Agent;
                var late = new List<DecisionFeedEntry>();
                void Watch(DecisionFeedEntry e)
                {
                    if (e.Agent == agent && e.Result?.Request?.Stimulus != null && e.Result.Request.Stimulus.Contains(ProbeLines[0])) late.Add(e);
                }
                DecisionFeed.Recorded += Watch;
                try
                {
                    Stand(bram, "");
                    float asked = Time.realtimeSinceStartup;
                    var stale = bram.HearPlayerAsync(ProbeLines[0]).GetAwaiter();
                    // Long enough for the request to reach the model, well short of its answer.
                    foreach (var x in Until(() => stale.IsCompleted, 0.25f)) yield return x;
                    bool answeredFirst = stale.IsCompleted;
                    float pressed = Time.realtimeSinceStartup;
                    presenter.ResetVillage();
                    tuning.Apply(arm.Variant);
                    foreach (var x in Until(() => stale.IsCompleted, 5f)) yield return x;
                    float ended = Time.realtimeSinceStartup - pressed;

                    // The next request, at once: does it wait behind the one F1 dropped?
                    Stand(bram, "");
                    var fresh = bram.HearPlayerAsync(ProbeLines[1]).GetAwaiter();
                    foreach (var x in Until(() => fresh.IsCompleted, TimeoutFor(arm))) yield return x;
                    var next = bram.Last;
                    // Anything still to come back for the dropped request comes in this time.
                    foreach (var x in Until(() => false, 5f)) yield return x;

                    var timed = run.Results.Where(r => r.arm == arm.Name && r.latencySeconds > 0).Select(r => r.latencySeconds).ToList();
                    string Typical() => timed.Count == 0 ? "no other decision on this arm" :
                        "this arm's median " + LabScore.Percentile(timed, 0.5).ToString("0.00", CultureInfo.InvariantCulture) + " s";
                    run.Notes.Add(answeredFirst
                        ? "F1 probe: the answer came back within 0.25 s, before F1 could be pressed, so the probe saw nothing; run it again."
                        : string.Format(CultureInfo.InvariantCulture,
                            "F1 probe, on {0} ({1}): F1 pressed {2:0.00} s after asking Bram. The call {3}. The next request {4}: queued {5:0.00} s, latency {6:0.00} s ({7}). " +
                            "A late answer to the dropped request {8}. Bram's line after it: \"{9}\".",
                            arm.Name, arm.Model ?? "scripted", pressed - asked,
                            stale.IsCompleted ? "ended " + ended.ToString("0.00", CultureInfo.InvariantCulture) + " s after F1" : "had not ended 5 s after F1",
                            next != null && next.Done && next.Outcome != "error" ? "was answered" : "failed: " + (next?.ProviderError ?? "no record"),
                            next?.QueueWaitSeconds ?? 0, next?.LatencySeconds ?? 0, Typical(),
                            late.Count == 0 ? "reached nobody: the feed raised nothing for it" : "was raised by the feed " + late.Count + " time(s), stage " + string.Join(", ", late.Select(e => e.Stage)),
                            next?.Said ?? ""));
                }
                finally
                {
                    DecisionFeed.Recorded -= Watch;
                }
            }
        }
    }
}
