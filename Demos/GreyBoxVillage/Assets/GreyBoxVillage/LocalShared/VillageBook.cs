using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Demos.Shared
{
    /// <summary>
    /// The player's book: the inventory (I) and the quest journal (J), one panel with two pages.
    /// Framework-free: the game passes strings, and item icons come from <c>Resources/Icons/&lt;id&gt;</c>.
    /// </summary>
    public sealed class VillageBook : MonoBehaviour
    {
        public struct Entry
        {
            public string Id, Name, Description;
            public int Count;
        }

        /// <summary>A place to find, for the journal: its name and note once found.</summary>
        public struct PlaceEntry
        {
            public string Title, Note;
            public bool Found;
        }

        public struct QuestEntry
        {
            public string Title, Giver, Text, Step;
            /// <summary>"active", "ready" or "done".</summary>
            public string State;
        }

        public bool IsOpen { get; private set; }
        /// <summary>Where the book sits, in panel pixels, while it is open.</summary>
        public Rect? Bounds => IsOpen && panel != null ? panel.worldBound : (Rect?)null;
        /// <summary>"inventory" or "journal".</summary>
        public string Page { get; private set; } = "inventory";

        private VisualElement root, panel, grid, journal, detail;
        private Label title, coins, detailName, detailText;
        private Button inventoryTab, journalTab;
        private int shownVersion = -1;

        private void OnEnable()
        {
            root = DemoUI.Create(gameObject, 15);
            panel = DemoUI.Panel(root);
            panel.name = "Book";
            panel.style.position = Position.Absolute;
            panel.style.right = Length.Percent(18);
            panel.style.left = Length.Percent(18);
            panel.style.top = Length.Percent(14);
            panel.style.bottom = Length.Percent(20);
            // Opaque: even a few per cent of a bright bubble behind it shows through its dark wood.
            panel.style.backgroundColor = new Color(0.1f, 0.08f, 0.06f, 1f);
            panel.pickingMode = PickingMode.Position;
            var tabs = DemoUI.Row(panel);
            inventoryTab = DemoUI.Button(tabs, "Inventory  (I)", () => Open("inventory"));
            journalTab = DemoUI.Button(tabs, "Journal  (J)", () => Open("journal"));
            title = DemoUI.Label(tabs, "", "vui-title");
            title.style.flexGrow = 1;
            title.style.unityTextAlign = TextAnchor.MiddleRight;
            coins = DemoUI.Label(tabs, "", "vui-title");
            coins.style.marginLeft = 12;
            coins.style.color = new Color(0.95f, 0.8f, 0.4f);

            var body = DemoUI.Row(panel);
            body.style.flexGrow = 1;
            body.style.alignItems = Align.FlexStart;
            body.style.marginTop = 8;
            grid = new VisualElement { pickingMode = PickingMode.Ignore };
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.flexGrow = 1;
            body.Add(grid);
            detail = DemoUI.Panel(body, "vui-panel--soft");
            detail.style.width = 210;
            detailName = DemoUI.Label(detail, "", "vui-title");
            detailText = DemoUI.Label(detail, "", "vui-text");

            journal = new ScrollView(ScrollViewMode.Vertical);
            journal.style.flexGrow = 1;
            journal.style.marginTop = 8;
            panel.Add(journal);
            DemoUI.Label(panel, "I / J switch pages   ·   Esc or the same key closes", "vui-muted").style.marginTop = 6;
            Show(false);
        }

        private void OnDisable() => root?.Clear();

        public void Open(string page)
        {
            Page = page;
            shownVersion = -1;
            Show(true);
        }

        /// <summary>Opens on the page, or closes when that page is already open.</summary>
        public void Toggle(string page)
        {
            if (IsOpen && Page == page) Show(false);
            else Open(page);
        }

        public void Show(bool open)
        {
            IsOpen = open;
            if (panel != null) DemoUI.Show(panel, open);
        }

        /// <summary>Refreshes the open page. Rebuilds only when <paramref name="version"/> changes.</summary>
        public void Fill(int version, int purse, IReadOnlyList<Entry> items, IReadOnlyList<QuestEntry> quests,
            IReadOnlyList<PlaceEntry> places = null)
        {
            if (panel == null || !IsOpen || version == shownVersion) return;
            shownVersion = version;
            bool inv = Page == "inventory";
            inventoryTab.EnableInClassList("vui-chip", false);
            inventoryTab.style.opacity = inv ? 1f : 0.6f;
            journalTab.style.opacity = inv ? 0.6f : 1f;
            title.text = inv ? "Your pack" : "Your quests";
            coins.text = purse + " coins";
            DemoUI.Show(grid.parent, inv);
            DemoUI.Show(journal, !inv);
            if (inv) FillInventory(items);
            else FillJournal(quests, places);
        }

        private void FillInventory(IReadOnlyList<Entry> items)
        {
            grid.Clear();
            detailName.text = items.Count == 0 ? "Empty" : "";
            detailText.text = items.Count == 0 ? "Pick things up with E; Odile sells the rest." : "Hover an item to read about it.";
            foreach (var item in items)
            {
                var slot = new VisualElement();
                slot.AddToClassList("slot");
                var icon = new Image { image = Resources.Load<Texture2D>("Icons/" + item.Id), scaleMode = ScaleMode.ScaleToFit };
                icon.AddToClassList("slot__icon");
                slot.Add(icon);
                var count = new Label(item.Count > 1 ? item.Count.ToString() : "");
                count.AddToClassList("slot__count");
                slot.Add(count);
                var captured = item;
                slot.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    detailName.text = captured.Name;
                    detailText.text = captured.Description;
                    slot.AddToClassList("slot--selected");
                });
                slot.RegisterCallback<PointerLeaveEvent>(_ => slot.RemoveFromClassList("slot--selected"));
                grid.Add(slot);
            }
        }

        /// <summary>"Places found: 4 of 6", as the journal heads its places.</summary>
        public static string PlacesHeading(IReadOnlyList<PlaceEntry> places)
        {
            int found = 0;
            foreach (var p in places) if (p.Found) found++;
            return "Places found: " + found + " of " + places.Count;
        }

        private void FillJournal(IReadOnlyList<QuestEntry> quests, IReadOnlyList<PlaceEntry> places)
        {
            journal.Clear();
            if (quests.Count == 0) DemoUI.Label(journal, "No quests yet. Ask around: \"Got any work?\"", "vui-muted");
            foreach (var q in quests)
            {
                var card = DemoUI.Panel(journal, "vui-panel--soft");
                card.style.marginBottom = 6;
                card.style.borderLeftWidth = 4;
                card.style.borderLeftColor = q.State == "done" ? new Color(0.33f, 0.59f, 0.33f)
                    : q.State == "ready" ? new Color(0.95f, 0.75f, 0.3f) : new Color(0.27f, 0.51f, 0.78f);
                var head = DemoUI.Row(card);
                var t = DemoUI.Label(head, q.Title, "vui-title");
                t.style.flexGrow = 1;
                DemoUI.Label(head, q.State == "done" ? "DONE" : q.State == "ready" ? "READY" : "ACTIVE", "vui-chip");
                DemoUI.Label(card, "From " + q.Giver, "vui-subtitle");
                DemoUI.Label(card, q.Text, "vui-text");
                if (q.State != "done") DemoUI.Label(card, q.Step, "vui-muted");
            }
            if (places == null || places.Count == 0) return;
            var heading = DemoUI.Label(journal, PlacesHeading(places).ToUpperInvariant(), "vui-subtitle");
            heading.style.marginTop = 10;
            heading.style.marginBottom = 4;
            foreach (var p in places)
            {
                var card = DemoUI.Panel(journal, "vui-panel--soft");
                card.style.marginBottom = 4;
                card.style.borderLeftWidth = 4;
                card.style.borderLeftColor = p.Found ? new Color(0.45f, 0.75f, 0.85f) : new Color(0.35f, 0.3f, 0.24f);
                DemoUI.Label(card, p.Found ? p.Title : "Somewhere north of the river...", p.Found ? "vui-title" : "vui-muted");
                if (p.Found) DemoUI.Label(card, p.Note, "vui-text");
            }
        }
    }
}
