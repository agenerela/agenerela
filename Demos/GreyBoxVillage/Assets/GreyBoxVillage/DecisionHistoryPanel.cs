using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// Every decision this session, newest first: press H. Selecting one shows exactly what was
    /// sent (system prompt, examples, observations, history, stimulus, the offered actions and
    /// targets, the schema), the raw reply, and what the game did with it.
    /// </summary>
    public sealed class DecisionHistoryPanel : MonoBehaviour
    {
        public VillageCommands Commands;
        public bool Open;

        private Vector2 listScroll, detailScroll;
        private int selected = -1;
        private GUIStyle mono, row;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.hKey.wasPressedThisFrame && (Commands.Input == null || !Commands.Input.IsTyping))
                Open = !Open;
        }

        private void OnGUI()
        {
            float scale = Mathf.Max(0.5f, Mathf.Min(Screen.width / 1000f, Screen.height / 720f));
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;

            if (!Open)
            {
                if (GUI.Button(new Rect(width - 156, 56, 140, 28), "History (H)")) Open = true;
                GUI.matrix = oldMatrix;
                return;
            }

            mono ??= new GUIStyle(GUI.skin.textArea) { wordWrap = true, fontSize = 12, richText = false };
            row ??= new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, wordWrap = false, fontSize = 12 };

            var area = new Rect(width * 0.30f, 16, width * 0.70f - 16, height - 32);
            GUI.Box(area, "");
            GUILayout.BeginArea(new Rect(area.x + 8, area.y + 8, area.width - 16, area.height - 16));
            GUILayout.BeginHorizontal();
            GUILayout.Label("REQUEST HISTORY", new GUIStyle(GUI.skin.label) { fontSize = 18 });
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close (H)", GUILayout.Width(100))) Open = false;
            GUILayout.EndHorizontal();

            var recorder = Commands.Recorder;
            GUILayout.Label(recorder == null ? "Nothing asked yet." :
                "Log file: " + (recorder.FilePath ?? "(not written)"));

            var records = recorder?.Records.Reverse().ToList();
            GUILayout.BeginHorizontal();
            listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Width(area.width * 0.38f));
            if (records != null)
                foreach (var r in records)
                {
                    string line = "#" + r.Number + " " + r.Time + "  " + r.Label + ": " + Short(r.Stimulus, 28) + "\n      → " +
                        (r.Done ? r.Action + " / " + r.Target + "  " + r.Outcome : "deciding...");
                    var style = new GUIStyle(row) { normal = { textColor = Colour(r) } };
                    if (GUILayout.Toggle(selected == r.Number, line, style, GUILayout.Height(40))) selected = r.Number;
                }
            GUILayout.EndScrollView();

            detailScroll = GUILayout.BeginScrollView(detailScroll);
            var chosen = records?.FirstOrDefault(r => r.Number == selected) ?? records?.FirstOrDefault();
            if (chosen != null) GUILayout.TextArea(Describe(chosen), mono);
            GUILayout.EndScrollView();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            GUI.matrix = oldMatrix;
        }

        private static Color Colour(DecisionRecord r) =>
            !r.Done ? Color.yellow :
            r.Outcome == "executed" ? new Color(0.6f, 1f, 0.6f) :
            r.Outcome == "none" ? new Color(0.8f, 0.8f, 0.8f) :
            new Color(1f, 0.55f, 0.5f);

        private static string Short(string s, int n) => string.IsNullOrEmpty(s) || s.Length <= n ? s : s.Substring(0, n - 1) + "…";

        public static string Describe(DecisionRecord r)
        {
            var b = new StringBuilder();
            b.AppendLine("#" + r.Number + "  " + r.Time + "  source " + r.Source + "  label " + r.Label + "  priority " + r.Priority);
            b.AppendLine();
            b.AppendLine("== RESULT");
            if (!r.Done) b.AppendLine("deciding...");
            else
            {
                b.AppendLine("action " + r.Action + "   target " + r.Target + "   outcome " + r.Outcome);
                if (!string.IsNullOrEmpty(r.Statement)) b.AppendLine("statement \"" + r.Statement + "\"");
                if (!string.IsNullOrEmpty(r.Detail)) b.AppendLine(r.Detail);
                b.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "model {0}   latency {1:0.00} s   queued {2:0.00} s   tokens {3} in / {4} out   history sent {5}",
                    r.Model, r.LatencySeconds, r.QueueWaitSeconds, r.PromptTokens, r.CompletionTokens, r.HistoryTurnsSent));
                if (r.TargetsDropped.Count > 0) b.AppendLine("targets dropped by the cap: " + string.Join(", ", r.TargetsDropped));
            }
            if (!r.Sent)
            {
                b.AppendLine();
                b.AppendLine("Nothing reached the provider: the request failed while it was being prepared.");
                return b.ToString();
            }
            Section(b, "RAW REPLY", r.RawReply ?? r.ProviderError ?? "(none)");
            Section(b, "OFFERED", "actions: " + string.Join(", ", r.OfferedActions) + "\ntargets: " + string.Join(", ", r.OfferedTargets));
            Section(b, "SYSTEM PROMPT", r.SystemPrompt);
            Section(b, "FEW-SHOT EXAMPLES", r.FewShotBlock);
            Section(b, "HISTORY (" + r.History.Count / 2 + " exchanges)", r.History.Count == 0 ? "(none)" : string.Join("\n", r.History));
            Section(b, "OBSERVATIONS", r.Observations);
            Section(b, "STIMULUS", r.StimulusTurn);
            Section(b, "SCHEMA (constrains sampling; a local model never reads it)", r.SchemaJson);
            return b.ToString();
        }

        private static void Section(StringBuilder b, string title, string body)
        {
            b.AppendLine();
            b.AppendLine("== " + title);
            b.AppendLine(string.IsNullOrEmpty(body) ? "(empty)" : body);
        }
    }
}
