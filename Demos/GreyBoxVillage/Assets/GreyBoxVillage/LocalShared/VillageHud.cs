using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Demos.Shared
{
    /// <summary>A speech bubble to draw over someone's head this frame.</summary>
    public struct BubbleInfo
    {
        public Vector3 World;
        public string Text;
        /// <summary>"say", "refusal", "error" or "thinking".</summary>
        public string Kind;
        /// <summary>For a refusal: the guard that stopped the decision.</summary>
        public string Tag;
    }

    /// <summary>A label to draw over a person, or over a place in the framework view.</summary>
    public struct TagInfo
    {
        public Vector3 World;
        public string Text, Detail;
        public bool Warning;
    }

    /// <summary>
    /// Everything drawn over the world: the clock and coins, the quest tracker, the interaction
    /// prompt, toasts, key hints, speech bubbles, name tags and (in the framework view) the target
    /// id of every place. Framework-free and string-driven: the game fills it each frame.
    /// </summary>
    public sealed class VillageHud : MonoBehaviour
    {
        private VisualElement root, world, quests, toasts, hints, clockPanel, column;
        private Label clock, coins, prompt, promptKey, alert;
        private VisualElement phase;
        private VisualElement promptRow, coinsRow;
        private readonly List<VisualElement> bubbles = new List<VisualElement>();
        private readonly List<Label> nameTags = new List<Label>();
        private readonly List<Label> placeTags = new List<Label>();
        private readonly List<(VisualElement element, float until)> liveToasts = new List<(VisualElement, float)>();
        private readonly List<int> order = new List<int>();
        private readonly List<Rect> taken = new List<Rect>();
        private readonly List<Rect> covered = new List<Rect>();
        // Panel pixels between a speaker's bubble and the name tag beneath it.
        private const float BubbleLift = 18f;

        /// <summary>The toasts shown so far, newest last, for tests.</summary>
        public List<string> ToastLog { get; } = new List<string>();

        private void OnEnable()
        {
            root = DemoUI.Create(gameObject, 0);
            world = new VisualElement { name = "World" };
            world.pickingMode = PickingMode.Ignore;
            world.style.position = Position.Absolute;
            world.style.left = world.style.right = world.style.top = world.style.bottom = 0;
            root.Add(world);

            var time = clockPanel = DemoUI.Panel(root, "vui-panel--soft");
            time.style.position = Position.Absolute;
            time.style.left = 14;
            time.style.top = 12;
            time.style.flexDirection = FlexDirection.Row;
            time.style.alignItems = Align.Center;
            time.pickingMode = PickingMode.Ignore;
            // The sun or the moon: a disc, so no font has to carry the glyph.
            phase = new VisualElement { pickingMode = PickingMode.Ignore };
            phase.style.width = phase.style.height = 18;
            phase.style.borderTopLeftRadius = phase.style.borderTopRightRadius =
                phase.style.borderBottomLeftRadius = phase.style.borderBottomRightRadius = 9;
            phase.style.marginRight = 10;
            time.Add(phase);
            var times = new VisualElement();
            time.Add(times);
            clock = DemoUI.Label(times, "", "vui-title");
            coins = null;
            coinsRow = DemoUI.Row(times);
            coins = DemoUI.Label(coinsRow, "", "vui-subtitle");

            // The right-hand column: the quest tracker, and the toasts under it, out of the top
            // centre where speech bubbles rise.
            column = new VisualElement { name = "RightColumn", pickingMode = PickingMode.Ignore };
            column.style.position = Position.Absolute;
            column.style.right = 14;
            column.style.top = 12;
            column.style.width = 250;
            column.style.alignItems = Align.FlexEnd;
            root.Add(column);
            quests = DemoUI.Panel(column, "vui-panel--soft");
            quests.name = "QuestTracker";
            quests.style.alignSelf = Align.Stretch;
            quests.style.marginBottom = 8;
            quests.pickingMode = PickingMode.Ignore;
            DemoUI.Show(quests, false);

            alert = DemoUI.Label(root, "", "alert");
            alert.name = "Alert";
            alert.style.position = Position.Absolute;
            alert.style.top = 12;
            alert.style.alignSelf = Align.Center;
            DemoUI.Show(alert, false);

            toasts = new VisualElement { name = "Toasts" };
            toasts.pickingMode = PickingMode.Ignore;
            toasts.style.alignItems = Align.FlexEnd;
            toasts.style.alignSelf = Align.Stretch;
            column.Add(toasts);

            promptRow = DemoUI.Panel(root, "vui-panel--soft");
            promptRow.style.position = Position.Absolute;
            promptRow.style.bottom = 170;
            promptRow.style.alignSelf = Align.Center;
            promptRow.style.flexDirection = FlexDirection.Row;
            promptRow.style.alignItems = Align.Center;
            promptRow.pickingMode = PickingMode.Ignore;
            promptKey = DemoUI.Label(promptRow, "E", "vui-key");
            prompt = DemoUI.Label(promptRow, "", "vui-text");
            DemoUI.Show(promptRow, false);

            // On a soft panel of its own, so it reads over bright grass and snow alike.
            hints = DemoUI.Panel(root, "vui-panel--soft");
            hints.name = "KeyHints";
            hints.AddToClassList("vui-row");
            hints.style.position = Position.Absolute;
            hints.style.left = 14;
            hints.style.bottom = 10;
            hints.style.paddingTop = hints.style.paddingBottom = 4;
            hints.style.paddingLeft = hints.style.paddingRight = 8;
            hints.pickingMode = PickingMode.Ignore;
        }

        /// <summary>
        /// What else is on screen, so the HUD keeps out of its way: the key hints step aside for the
        /// dialogue panel, and the toasts and the quest tracker for the book and the menu.
        /// </summary>
        public void SetCovered(bool dialogue, bool overlay)
        {
            if (hints == null) return;
            hints.style.visibility = dialogue || overlay ? Visibility.Hidden : Visibility.Visible;
            toasts.style.visibility = overlay ? Visibility.Hidden : Visibility.Visible;
            quests.style.visibility = overlay ? Visibility.Hidden : Visibility.Visible;
        }

        private void OnDisable() => root?.Clear();

        public void SetClock(string time, string dayPhase, int day, bool night)
        {
            if (clock == null) return;
            phase.style.backgroundColor = night ? new Color(0.8f, 0.86f, 1f) : new Color(1f, 0.8f, 0.3f);
            phase.style.borderRightWidth = night ? 5 : 0;
            phase.style.borderRightColor = new Color(0.1f, 0.1f, 0.15f, 0.9f);
            clock.text = time + "   " + Cap(dayPhase) + ", day " + day;
        }

        /// <summary>What the HUD shows about a problem the whole village has, such as no model running; null hides it.</summary>
        public string Alert => alert != null && alert.style.display != DisplayStyle.None ? alert.text : null;

        /// <summary>A problem the whole village has, shown top centre until it is cleared with null.</summary>
        public void SetAlert(string text)
        {
            if (alert == null) return;
            DemoUI.Show(alert, !string.IsNullOrEmpty(text));
            alert.text = text ?? "";
        }

        /// <summary>The player's purse; a negative amount hides it.</summary>
        public void SetCoins(int amount)
        {
            if (coins == null) return;
            DemoUI.Show(coinsRow, amount >= 0);
            coins.text = amount + " coins";
            coins.style.color = new Color(0.95f, 0.8f, 0.4f);
        }

        /// <summary>The key hints along the bottom, as (key, what) pairs.</summary>
        public void SetHints(IReadOnlyList<(string key, string what)> pairs)
        {
            if (hints == null || hints.childCount == pairs.Count) return;
            hints.Clear();
            foreach (var (key, what) in pairs) DemoUI.KeyHint(hints, key, what);
        }

        /// <summary>The interaction prompt, such as ("E", "Talk to Bram"); a null text hides it.</summary>
        public void SetPrompt(string key, string text)
        {
            if (prompt == null) return;
            DemoUI.Show(promptRow, !string.IsNullOrEmpty(text));
            promptKey.text = key;
            prompt.text = text ?? "";
        }

        /// <summary>How many toasts show at once; a new one pushes out the oldest.</summary>
        public const int MaxToasts = 3;

        public void Toast(string text, float seconds = 3.5f)
        {
            ToastLog.Add(text);
            if (toasts == null) return;
            while (liveToasts.Count >= MaxToasts)
            {
                liveToasts[0].element.RemoveFromHierarchy();
                liveToasts.RemoveAt(0);
            }
            var label = DemoUI.Label(toasts, text, "toast");
            // Wraps inside the column rather than running off the screen.
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.maxWidth = Length.Percent(100);
            liveToasts.Add((label, Time.unscaledTime + seconds));
        }

        /// <summary>The quest tracker: each quest's title and its next step. Empty hides it.</summary>
        public void SetQuests(IReadOnlyList<(string title, string step, bool ready)> active)
        {
            if (quests == null) return;
            DemoUI.Show(quests, active.Count > 0);
            if (quests.childCount == active.Count * 2 + 1 && active.Count > 0)
            {
                for (int i = 0; i < active.Count; i++)
                {
                    ((Label)quests[1 + i * 2]).text = (active[i].ready ? "READY  " : "• ") + active[i].title;
                    ((Label)quests[2 + i * 2]).text = active[i].step;
                }
                return;
            }
            quests.Clear();
            DemoUI.Label(quests, "QUESTS", "vui-subtitle");
            foreach (var (title, step, ready) in active)
            {
                var t = DemoUI.Label(quests, (ready ? "READY  " : "• ") + title, "vui-text");
                t.style.unityFontStyleAndWeight = FontStyle.Bold;
                t.style.color = new Color(0.97f, 0.87f, 0.6f);
                DemoUI.Label(quests, step, "vui-muted").style.marginBottom = 4;
            }
        }

        // ---- Over the world ----------------------------------------------------------------------

        public void SetBubbles(Camera camera, IReadOnlyList<BubbleInfo> list)
        {
            if (world == null || camera == null) return;
            while (bubbles.Count < list.Count)
            {
                var b = new VisualElement();
                b.AddToClassList("bubble");
                b.pickingMode = PickingMode.Ignore;
                // Wraps inside the bubble's max-width; a long line otherwise ran out of the bubble.
                var line = new Label { pickingMode = PickingMode.Ignore };
                line.style.whiteSpace = WhiteSpace.Normal;
                b.Add(line);
                var tag = new Label { pickingMode = PickingMode.Ignore };
                tag.AddToClassList("bubble__tag");
                b.Add(tag);
                world.Add(b);
                bubbles.Add(b);
            }
            for (int i = 0; i < bubbles.Count; i++)
            {
                var b = bubbles[i];
                if (i >= list.Count || !Place(b, camera, list[i].World, true, clamp: true) || UnderPanel(b))
                {
                    DemoUI.Show(b, false);
                    continue;
                }
                DemoUI.Show(b, true);
                var info = list[i];
                ((Label)b[0]).text = info.Text;
                ((Label)b[1]).text = string.IsNullOrEmpty(info.Tag) ? "" : "stopped by " + info.Tag;
                DemoUI.Show(b[1], !string.IsNullOrEmpty(info.Tag));
                b.EnableInClassList("bubble--refusal", info.Kind == "refusal");
                b.EnableInClassList("bubble--thinking", info.Kind == "thinking");
                b.EnableInClassList("bubble--error", info.Kind == "error");
            }
        }

        // A bubble that would fall under a panel is hidden, as a world label is: with four agents
        // talking at once, the far ones' bubbles otherwise piled over the clock and the agent list.
        // Its size is last frame's, which is enough to tell.
        private bool UnderPanel(VisualElement bubble)
        {
            float w = bubble.resolvedStyle.width, h = bubble.resolvedStyle.height;
            if (float.IsNaN(w) || float.IsNaN(h)) return false;
            var rect = new Rect(bubble.style.left.value.value, bubble.style.top.value.value, w, h);
            foreach (var c in covered)
                if (c.Overlaps(rect)) return true;
            return false;
        }

        /// <summary>
        /// Where other documents' panels sit over the world this frame, in panel pixels: the agent
        /// list or the framework view, the dialogue, the book, the menu. A world label or a speech
        /// bubble that would fall under one of them, or under the HUD's own panels, is hidden. Set it
        /// before the bubbles and the tags.
        /// </summary>
        public void SetCoveredRects(IEnumerable<Rect> rects)
        {
            covered.Clear();
            covered.AddRange(rects);
            foreach (var own in new[] { clockPanel, column, hints })
                if (own != null && own.resolvedStyle.display != DisplayStyle.None && own.resolvedStyle.visibility == Visibility.Visible)
                    covered.Add(own.worldBound);
        }

        /// <summary>The name tags. Call before <see cref="SetPlaceTags"/>, which keeps clear of them.</summary>
        public void SetNameTags(Camera camera, IReadOnlyList<TagInfo> list)
        {
            taken.Clear();
            Tags(nameTags, "nametag", camera, list, 22f);
        }

        /// <summary>The framework view's place labels: every target id the agents can name.</summary>
        public void SetPlaceTags(Camera camera, IReadOnlyList<TagInfo> list) => Tags(placeTags, "placetag", camera, list, 120f);

        // Nearest first: where two labels would overlap, the nearer one is shown and the other hidden,
        // as is any label under an open panel. Hidden by visibility, not display, so each keeps its
        // size and the choice does not flicker from frame to frame.
        private void Tags(List<Label> pool, string cls, Camera camera, IReadOnlyList<TagInfo> list, float maxDistance)
        {
            if (world == null || camera == null) return;
            while (pool.Count < list.Count)
            {
                var l = new Label { pickingMode = PickingMode.Ignore };
                l.AddToClassList(cls);
                l.enableRichText = true;
                world.Add(l);
                pool.Add(l);
            }
            for (int i = list.Count; i < pool.Count; i++) DemoUI.Show(pool[i], false);
            var from = camera.transform.position;
            order.Clear();
            for (int i = 0; i < list.Count; i++) order.Add(i);
            order.Sort((a, b) => Vector3.Distance(from, list[a].World).CompareTo(Vector3.Distance(from, list[b].World)));
            foreach (int i in order)
            {
                var l = pool[i];
                bool placed = Vector3.Distance(from, list[i].World) < maxDistance && Place(l, camera, list[i].World, false);
                DemoUI.Show(l, placed);
                if (!placed) continue;
                var info = list[i];
                l.text = string.IsNullOrEmpty(info.Detail) ? info.Text
                    : info.Text + "\n<size=9>" + info.Detail + "</size>";
                if (cls == "placetag") l.EnableInClassList("placetag--restricted", info.Warning);
                var r = RectOf(l);
                bool clear = !covered.Exists(c => c.Overlaps(r)) && !taken.Exists(t => t.Overlaps(r));
                l.style.visibility = clear ? Visibility.Visible : Visibility.Hidden;
                if (clear) taken.Add(r);
            }
        }

        private static Rect RectOf(VisualElement e)
        {
            float w = e.resolvedStyle.width, h = e.resolvedStyle.height;
            return new Rect(e.style.left.value.value, e.style.top.value.value, float.IsNaN(w) ? 0 : w, float.IsNaN(h) ? 0 : h);
        }

        // Puts an element over a world point; false when the point is behind the camera. From the
        // viewport and the panel's own size, not the screen's: a panel drawn into a RenderTexture
        // (the capture tool's) has its size, not the screen's, and the two agree on a screen.
        // With clamp, the element is kept wholly on screen: a bubble whose speaker stands at the edge
        // was cut off there.
        private bool Place(VisualElement element, Camera camera, Vector3 worldPoint, bool above, bool clamp = false)
        {
            var viewport = camera.WorldToViewportPoint(worldPoint);
            if (viewport.z <= 0.1f) return false;
            var size = root.layout.size;
            if (float.IsNaN(size.x) || size.x <= 0) return false;
            var rect = camera.rect;
            var panelPoint = new Vector2((rect.x + viewport.x * rect.width) * size.x, (1f - (rect.y + viewport.y * rect.height)) * size.y);
            float w = element.resolvedStyle.width, h = element.resolvedStyle.height;
            if (float.IsNaN(w)) w = 0;
            if (float.IsNaN(h)) h = 0;
            float left = panelPoint.x - w / 2f;
            // A bubble rises clear of the name tag under it.
            float top = panelPoint.y - (above ? h + BubbleLift : h / 2f);
            if (clamp)
            {
                const float margin = 6f;
                left = Mathf.Clamp(left, margin, Mathf.Max(margin, size.x - w - margin));
                top = Mathf.Clamp(top, margin, Mathf.Max(margin, size.y - h - margin));
            }
            element.style.left = left;
            element.style.top = top;
            return true;
        }

        private void Update()
        {
            for (int i = liveToasts.Count - 1; i >= 0; i--)
            {
                var (element, until) = liveToasts[i];
                float left = until - Time.unscaledTime;
                element.style.opacity = Mathf.Clamp01(left / 0.5f);
                if (left > 0) continue;
                element.RemoveFromHierarchy();
                liveToasts.RemoveAt(i);
            }
        }

        private static string Cap(string s) => string.IsNullOrEmpty(s) ? "" : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
