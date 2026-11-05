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
        /// <summary>"say", "refusal" or "thinking".</summary>
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
        private VisualElement root, world, quests, toasts, hints;
        private Label clock, coins, prompt, promptKey;
        private VisualElement phase;
        private VisualElement promptRow, coinsRow;
        private readonly List<VisualElement> bubbles = new List<VisualElement>();
        private readonly List<Label> nameTags = new List<Label>();
        private readonly List<Label> placeTags = new List<Label>();
        private readonly List<(VisualElement element, float until)> liveToasts = new List<(VisualElement, float)>();

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

            var time = DemoUI.Panel(root, "vui-panel--soft");
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

            quests = DemoUI.Panel(root, "vui-panel--soft");
            quests.name = "QuestTracker";
            quests.style.position = Position.Absolute;
            quests.style.right = 14;
            quests.style.top = 12;
            quests.style.width = 250;
            quests.pickingMode = PickingMode.Ignore;
            DemoUI.Show(quests, false);

            toasts = new VisualElement { name = "Toasts" };
            toasts.pickingMode = PickingMode.Ignore;
            toasts.style.position = Position.Absolute;
            toasts.style.top = 70;
            toasts.style.left = 0;
            toasts.style.right = 0;
            toasts.style.alignItems = Align.Center;
            root.Add(toasts);

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

            hints = DemoUI.Row(root);
            hints.style.position = Position.Absolute;
            hints.style.left = 14;
            hints.style.bottom = 10;
            hints.style.opacity = 0.85f;
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

        public void Toast(string text, float seconds = 3.5f)
        {
            ToastLog.Add(text);
            if (toasts == null) return;
            var label = DemoUI.Label(toasts, text, "toast");
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
                b.Add(new Label { pickingMode = PickingMode.Ignore });
                var tag = new Label { pickingMode = PickingMode.Ignore };
                tag.AddToClassList("bubble__tag");
                b.Add(tag);
                world.Add(b);
                bubbles.Add(b);
            }
            for (int i = 0; i < bubbles.Count; i++)
            {
                var b = bubbles[i];
                if (i >= list.Count || !Place(b, camera, list[i].World, true))
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
            }
        }

        public void SetNameTags(Camera camera, IReadOnlyList<TagInfo> list) => Tags(nameTags, "nametag", camera, list, 22f);

        /// <summary>The framework view's place labels: every target id the agents can name.</summary>
        public void SetPlaceTags(Camera camera, IReadOnlyList<TagInfo> list) => Tags(placeTags, "placetag", camera, list, 120f);

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
            for (int i = 0; i < pool.Count; i++)
            {
                var l = pool[i];
                bool shown = i < list.Count && Vector3.Distance(camera.transform.position, list[i].World) < maxDistance
                             && Place(l, camera, list[i].World, false);
                DemoUI.Show(l, shown);
                if (!shown) continue;
                var info = list[i];
                l.text = string.IsNullOrEmpty(info.Detail) ? info.Text
                    : info.Text + "\n<size=9>" + info.Detail + "</size>";
                if (cls == "placetag") l.EnableInClassList("placetag--restricted", info.Warning);
            }
        }

        // Puts an element over a world point; false when the point is behind the camera.
        private bool Place(VisualElement element, Camera camera, Vector3 worldPoint, bool above)
        {
            var viewport = camera.WorldToViewportPoint(worldPoint);
            if (viewport.z <= 0.1f) return false;
            var panelPoint = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, worldPoint, camera);
            float w = element.resolvedStyle.width, h = element.resolvedStyle.height;
            if (float.IsNaN(w)) w = 0;
            if (float.IsNaN(h)) h = 0;
            element.style.left = panelPoint.x - w / 2f;
            element.style.top = panelPoint.y - (above ? h : h / 2f);
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
