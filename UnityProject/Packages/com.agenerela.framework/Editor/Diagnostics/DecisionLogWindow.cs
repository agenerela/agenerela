using System;
using System.IO;
using Agenerela.Editor.UI;
using UnityEditor;
using UnityEngine;

namespace Agenerela.Editor.Diagnostics
{
    /// <summary>
    /// <b>Window → Agenerela → Decision Log</b>: why did my agent do that? (#172, screen 3). Every
    /// decision any agent makes in Play mode becomes a row; selecting one tells its whole story.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rows live in <see cref="DecisionLogStore"/>, read from the <see cref="DecisionFeed"/>
    /// (#166) only while this window is open and recording, and kept across leaving Play mode. The
    /// tree is <see cref="DecisionLogUi"/>, from <c>DecisionLogWindow.uxml</c> and <c>.uss</c> on the
    /// #167 kit. This class holds the filters and the selection, and hands the tree all of it
    /// whenever one changes.
    /// </para>
    /// <para>
    /// Extension point: <b>Add to evaluation prompts</b> belongs beside <b>Copy as JSON</b> once the
    /// Prompt Set window can take a prompt from here (#160); it is a follow-up, not part of #172.
    /// </para>
    /// </remarks>
    public sealed class DecisionLogWindow : EditorWindow
    {
        // How often new rows reach the list at most, so a burst of decisions redraws it once.
        private const long RefreshMilliseconds = 100;

        private readonly DecisionLogFilter filter = new DecisionLogFilter();
        private DecisionLogStore store;
        private DecisionLogUi ui;
        private DecisionLogRow selected;
        private bool dirty;

        // Kept across a domain reload, so the window comes back as it was left.
        [SerializeField] private string agentFilter;
        [SerializeField] private string searchFilter;
        [SerializeField] private int hiddenOutcomes;
        [SerializeField] private long selectedSequence = -1;
        [SerializeField] private long selectedTicks;

        [MenuItem("Window/Agenerela/Decision Log")]
        public static void Open()
        {
            var window = GetWindow<DecisionLogWindow>();
            window.titleContent = new GUIContent("Decision Log", OutcomeIcon.Icon(Outcome.Contained));
            window.minSize = new Vector2(620, 320);
            window.Show();
        }

        /// <summary>The store the window shows; the session's unless a test gave it another.</summary>
        internal DecisionLogStore Store => store ?? (store = DecisionLogSession.Store);

        /// <summary>Gives the window a store of a test's own. Call before the window is shown.</summary>
        internal void UseStore(DecisionLogStore own)
        {
            Store.Changed -= OnStoreChanged;
            Store.Detach(this);
            store = own;
            Store.Changed += OnStoreChanged;
            Store.Attach(this);
            Refresh();
        }

        private void OnEnable()
        {
            filter.Agent = agentFilter;
            filter.Search = searchFilter;
            foreach (var outcome in DecisionLogFilter.Outcomes)
            {
                if ((hiddenOutcomes & (1 << (int)outcome)) != 0)
                {
                    filter.Hidden.Add(outcome);
                }
            }

            Store.Changed += OnStoreChanged;
            Store.Attach(this);
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Store.Changed -= OnStoreChanged;

            // With no window open, the feed has no subscriber from the log.
            Store.Detach(this);
        }

        private void CreateGUI()
        {
            ui = new DecisionLogUi(DecisionLogUi.Load());
            rootVisualElement.Add(ui.Root);

            ui.RecordingChanged += on =>
            {
                Store.Recording = on;
                Refresh();
            };
            ui.ClearOnPlayChanged += on => Store.ClearOnPlay = on;
            ui.ClearClicked += () =>
            {
                Store.Clear();
                selected = null;
            };
            ui.ExportClicked += Export;
            ui.AgentChanged += agent =>
            {
                filter.Agent = agentFilter = agent;
                Refresh();
            };
            ui.OutcomeToggled += (outcome, shown) =>
            {
                if (shown) filter.Hidden.Remove(outcome); else filter.Hidden.Add(outcome);
                hiddenOutcomes = 0;
                foreach (var hidden in filter.Hidden) hiddenOutcomes |= 1 << (int)hidden;
                Refresh();
            };
            ui.SearchChanged += text =>
            {
                filter.Search = searchFilter = text;
                Refresh();
            };
            ui.SelectionChanged += row =>
            {
                selected = row;
                selectedSequence = row?.Line.Sequence ?? -1;
                selectedTicks = row?.Line.Time.Ticks ?? 0;
                Refresh();
            };
            ui.SelectInScene += SelectAgent;
            ui.CopyJson += row =>
            {
                EditorGUIUtility.systemCopyBuffer = DecisionLogStore.ToIndentedJson(row.Line);
                ShowNotification(new GUIContent("Copied"), 1);
            };

            rootVisualElement.schedule.Execute(() =>
            {
                if (dirty)
                {
                    Refresh();
                }
            }).Every(RefreshMilliseconds);

            Refresh();
        }

        private void OnStoreChanged()
        {
            dirty = true;
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode && Store.ClearOnPlay)
            {
                Store.Clear();
                selected = null;
            }

            // The feed dropped every subscriber as Play mode started (#190, G8).
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                Store.Resubscribe();
            }

            dirty = true;
        }

        internal void Refresh()
        {
            dirty = false;
            if (ui == null)
            {
                return;
            }

            var rows = Store.Rows;
            if (selected == null && selectedSequence >= 0)
            {
                // After a domain reload, the row selected before it.
                foreach (var row in rows)
                {
                    if (row.Line.Sequence == selectedSequence && row.Line.Time.Ticks == selectedTicks)
                    {
                        selected = row;
                    }
                }
            }

            if (selected != null && selected.Removed)
            {
                selected = null;
            }

            var shown = filter.Apply(rows);
            ui.Show(new DecisionLogUi.State
            {
                Shown = shown,
                All = rows,
                Filter = filter,
                Counts = filter.Counts(rows),
                Agents = DecisionLogFilter.Agents(rows),
                Recording = Store.Recording,
                ClearOnPlay = Store.ClearOnPlay,
                Playing = EditorApplication.isPlaying,
                Selected = selected != null && shown.Contains(selected) ? selected : null,
            });
        }

        private void Export()
        {
            var rows = filter.Apply(Store.Rows);
            if (rows.Count == 0)
            {
                ShowNotification(new GUIContent("Nothing to export"), 1);
                return;
            }

            var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs");
            var path = EditorUtility.SaveFilePanel("Export decisions", folder, "decision-log-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), "jsonl");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            // Oldest first, as a DecisionLog file is.
            rows.Reverse();
            File.WriteAllText(path, DecisionLogStore.ToJsonLines(rows), new System.Text.UTF8Encoding(false));
            ShowNotification(new GUIContent($"Exported {rows.Count} decision{(rows.Count == 1 ? "" : "s")}"), 2);
        }

        private void SelectAgent(DecisionLogRow row)
        {
            var target = EditorUtility.EntityIdToObject(row.GameObject) as GameObject;
            if (target == null)
            {
                ShowNotification(new GUIContent("That GameObject no longer exists"), 2);
                return;
            }

            Selection.activeGameObject = target;
            EditorGUIUtility.PingObject(target);
        }
    }
}
