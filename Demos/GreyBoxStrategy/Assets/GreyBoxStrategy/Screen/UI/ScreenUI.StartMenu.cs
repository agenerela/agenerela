using System;
using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The start menu (#20): the title screen over the living map, with its buttons for the keyboard and the mouse and
    /// the model status line at its foot; and the windows it opens that the older menus did not have: Load a replay,
    /// Credits, and a confirmation. New game, Settings and the pause menu are in <c>ScreenUI.Menu.cs</c>.
    /// </summary>
    public sealed partial class ScreenUI
    {
        private readonly List<Button> menuItems = new List<Button>();
        private int menuSel = -1;
        private Label menuHint, statusLabel;
        private VisualElement statusDot;
        private ModelStatus modelStatus;
        private readonly List<VisualElement> hudOff = new List<VisualElement>();

        /// <summary>True while the title screen itself is up (not one of its windows).</summary>
        public bool TitleOpen => menu != null && menu.ClassListContains("title-screen");

        /// <summary>For the keys, the Unity CLI and the tests: the open menu's big buttons, top to bottom.</summary>
        public IReadOnlyList<string> MenuItems => menuItems.Select(b => b.Q<Label>()?.text ?? b.text).ToList();

        /// <summary>The button the keys are on, or null.</summary>
        public string MenuSelected => menuSel >= 0 && menuSel < menuItems.Count ? MenuItems[menuSel] : null;

        /// <summary>For the tests and the Unity CLI: every line of text on the open menu page.</summary>
        public List<string> MenuText() => menu == null ? new List<string>() : menu.Query<Label>().ToList().Select(l => l.text).Where(t => !string.IsNullOrEmpty(t)).ToList();

        /// <summary>The model status line as last set, on whichever page shows it.</summary>
        public ModelStatus ModelStatus => modelStatus;

        // ---- the title screen ----

        /// <summary>
        /// The game's first screen: the title, the buttons, a hint for the one the keys are on, and the model status
        /// line. The map behind it is the director's; everything else on the screen is put away until a game starts.
        /// </summary>
        public void ShowTitle(IReadOnlyList<StartMenu.Item> items, Action<string> chose, string version)
        {
            HideMenu();
            HideHud(true);
            menu = new VisualElement { name = "title" }; menu.AddToClassList("title-screen");
            menu.Add(new TitleVeil());
            MenuBack = null;

            var col = new VisualElement(); col.AddToClassList("title-col");
            col.Add(new CompassRose { style = { width = 84, height = 84, marginBottom = 8 } });
            var name = new Label(StartMenu.Title.ToUpperInvariant()); name.AddToClassList("title-name"); col.Add(name);
            var sub = new Label(StartMenu.Subtitle); sub.AddToClassList("title-sub"); col.Add(sub);
            col.Add(new GiltRule());
            var list = new VisualElement(); list.AddToClassList("title-list");
            foreach (var item in items)
            {
                string id = item.Id;
                var b = new Button(() => chose(id)) { name = "title-" + id, userData = new MenuEntry { Note = item.Note, Press = () => chose(id) } }; b.AddToClassList("title-btn");
                var mark = new VisualElement { pickingMode = PickingMode.Ignore }; mark.AddToClassList("title-mark"); b.Add(mark);
                b.Add(new Label(item.Label) { pickingMode = PickingMode.Ignore });
                list.Add(b);
            }
            col.Add(list);
            menuHint = new Label(); menuHint.AddToClassList("title-hint"); col.Add(menuHint);
            menu.Add(col);

            var foot = new VisualElement(); foot.AddToClassList("title-foot");
            statusDot = new VisualElement(); statusDot.AddToClassList("title-dot"); foot.Add(statusDot);
            statusLabel = new Label(); statusLabel.AddToClassList("title-status"); foot.Add(statusLabel);
            var ver = new Label($"{StartMenu.FrameworkLine(version)} · working title"); ver.AddToClassList("title-version"); foot.Add(ver);
            menu.Add(foot);
            Root.Add(menu);
            TakeMenuItems(list, "title-btn");
            MenuMove(0);
            SetModelStatus(modelStatus);
        }

        /// <summary>
        /// A dark veil that is nearly solid behind the buttons and clears toward the right, so the map shows: one strip
        /// of texture, dark to light, stretched over the screen (UI Toolkit draws no gradients of its own).
        /// </summary>
        private sealed class TitleVeil : VisualElement
        {
            private static Texture2D ramp;

            public TitleVeil()
            {
                pickingMode = PickingMode.Ignore;
                AddToClassList("title-veil");
                style.backgroundImage = new StyleBackground(Ramp());
                style.backgroundSize = new BackgroundSize(BackgroundSizeType.Length) { x = Length.Percent(100), y = Length.Percent(100) };
            }

            /// <summary>Solid to 30% of the width, then easing to a light wash at the right edge.</summary>
            public static float Alpha(float x)
            {
                float t = Mathf.Clamp01((x - 0.30f) / 0.45f);
                return Mathf.Lerp(0.90f, 0.22f, t * t * (3 - 2 * t));
            }

            private static Texture2D Ramp()
            {
                if (ramp != null) return ramp;
                const int w = 256;
                ramp = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
                var px = new Color32[w];
                for (int i = 0; i < w; i++) px[i] = new Color32(9, 7, 5, (byte)Mathf.RoundToInt(255 * Alpha(i / (float)(w - 1))));
                ramp.SetPixels32(px);
                ramp.Apply();
                return ramp;
            }
        }

        // ---- the keys: any page's big buttons, up and down, Enter ----

        /// <summary>Makes a page's big buttons the ones the arrow keys walk and Enter presses; the mouse moves the same mark.</summary>
        private void TakeMenuItems(VisualElement within, string className)
        {
            menuItems.Clear();
            menuSel = -1;
            foreach (var b in within.Query<Button>(className: className).ToList())
            {
                if (!b.enabledSelf) continue;
                int k = menuItems.Count;
                menuItems.Add(b);
                b.RegisterCallback<MouseEnterEvent>(_ => SelectMenuItem(k));
            }
        }

        private void ClearMenuItems()
        {
            menuItems.Clear();
            menuSel = -1;
            menuHint = null; statusLabel = null; statusDot = null;
        }

        private void SelectMenuItem(int k)
        {
            if (menuItems.Count == 0) return;
            menuSel = ((k % menuItems.Count) + menuItems.Count) % menuItems.Count;
            for (int i = 0; i < menuItems.Count; i++) menuItems[i].EnableInClassList("sel", i == menuSel);
            if (menuHint != null) menuHint.text = (menuItems[menuSel].userData as MenuEntry)?.Note ?? "";
            // A long list (the replays) keeps the chosen row in view.
            var scroll = menuItems[menuSel].GetFirstAncestorOfType<ScrollView>();
            if (scroll != null && menuItems[menuSel].panel != null) scroll.ScrollTo(menuItems[menuSel]);
        }

        /// <summary>The arrow keys: the mark moves by <paramref name="by"/>, wrapping at either end. 0 puts it on the first button.</summary>
        public void MenuMove(int by)
        {
            if (menuItems.Count == 0) return;
            SelectMenuItem(menuSel < 0 || by == 0 ? 0 : menuSel + by);
        }

        /// <summary>Enter: presses the marked button. False when no page with big buttons is open.</summary>
        public bool MenuPress()
        {
            if (menuSel < 0 || menuSel >= menuItems.Count) return false;
            // A button the mouse just clicked holds the focus, and the panel sends it Enter itself: never twice.
            if (Root.panel?.focusController?.focusedElement is Button) return false;
            var press = (menuItems[menuSel].userData as MenuEntry)?.Press;
            if (press == null) return false;
            press();
            return true;
        }

        /// <summary>What a big button carries for the keys: its hint, and what pressing it does.</summary>
        private sealed class MenuEntry
        {
            public string Note;
            public Action Press;
        }

        // ---- the model status line ----

        /// <summary>Sets the status line wherever it is shown: the title's foot, and New game's Model group.</summary>
        public void SetModelStatus(ModelStatus status)
        {
            modelStatus = status;
            if (status == null) return;
            if (statusLabel != null)
            {
                statusLabel.text = status.Line;
                statusLabel.tooltip = status.Detail;
                foreach (string tone in new[] { "good", "warn", "muted" }) statusDot.EnableInClassList(tone, tone == status.Tone);
            }
            newGameModelStatus?.Invoke(status);
        }

        private Action<ModelStatus> newGameModelStatus;

        // ---- putting the game's own screen away behind the title ----

        /// <summary>
        /// Hides everything on the screen but the menu (the top bar, the panels, the map's labels) while the title and
        /// its windows are up, and brings it back when a game starts.
        /// </summary>
        public void HideHud(bool hide)
        {
            if (hide)
            {
                foreach (var child in Root.Children())
                {
                    if (child == menu || child == notice || hudOff.Contains(child)) continue;
                    child.AddToClassList("hud-off");
                    hudOff.Add(child);
                }
                return;
            }
            foreach (var child in hudOff) child.RemoveFromClassList("hud-off");
            hudOff.Clear();
        }

        public bool HudHidden => hudOff.Count > 0;

        // ---- Load a replay ----

        /// <summary>The finished games, newest first, each with when it ended, how long it ran and who won.</summary>
        public void ShowReplays(IReadOnlyList<SavedReplay> replays, Action<SavedReplay> play, Action back)
        {
            var card = Page("replays", back);
            card.Add(Heading("LOAD A REPLAY"));
            var note = new Label(replays.Any(r => !r.Bundled)
                ? "A game played or watched to its end is kept here, the newest first. A replay needs no model and no server."
                : "No game of yours is saved yet: one played or watched to its end is kept here. The game that ships with Painted Realm is below.");
            note.AddToClassList("why-no"); card.Add(note);
            card.Add(new GiltRule());
            var list = new VisualElement(); list.AddToClassList("replay-list");
            foreach (var r in replays)
            {
                var saved = r;
                var row = new Button(() => play(saved)) { userData = new MenuEntry { Press = () => play(saved) } }; row.AddToClassList("replay-row");
                var text = new VisualElement { pickingMode = PickingMode.Ignore }; text.AddToClassList("replay-text");
                var title = new Label(r.Title) { pickingMode = PickingMode.Ignore }; title.AddToClassList("replay-title"); text.Add(title);
                var line = new Label(r.Line()) { pickingMode = PickingMode.Ignore }; line.AddToClassList("replay-line"); text.Add(line);
                row.Add(text);
                var side = new VisualElement { pickingMode = PickingMode.Ignore }; side.AddToClassList("replay-side");
                if (!string.IsNullOrEmpty(r.World)) { var w = new Label(r.World) { pickingMode = PickingMode.Ignore }; w.AddToClassList("replay-world"); side.Add(w); }
                if (!string.IsNullOrEmpty(r.Played)) { var p = new Label(r.Played) { pickingMode = PickingMode.Ignore }; p.AddToClassList("replay-line"); side.Add(p); }
                row.Add(side);
                list.Add(row);
            }
            card.Add(list);
            menuStatus = new Label(); menuStatus.AddToClassList("menu-status"); card.Add(menuStatus);
            var backB = new Button(back) { text = "Back" }; backB.AddToClassList("menu-button"); backB.AddToClassList("quiet");
            var foot = Row(backB); foot.style.marginTop = 10; card.Add(foot);
            Ornament();
            TakeMenuItems(list, "replay-row");
            MenuMove(0);
        }

        // ---- Credits ----

        public void ShowCredits(string version, Action back)
        {
            var card = Page("credits", back);
            card.Add(new CompassRose { style = { width = 64, height = 64, alignSelf = Align.Center, marginBottom = 4 } });
            var name = new Label(StartMenu.Title.ToUpperInvariant()); name.AddToClassList("menu-title"); name.style.alignSelf = Align.Center; card.Add(name);
            var sub = new Label(StartMenu.Subtitle); sub.AddToClassList("menu-sub"); card.Add(sub);
            card.Add(new GiltRule());
            card.Add(Section2("THE TEAM", StartMenu.Course));
            foreach (var (person, part) in StartMenu.Team)
            {
                var row = new VisualElement(); row.AddToClassList("credit-row");
                var who = new Label(person); who.AddToClassList("credit-name"); row.Add(who);
                var what = new Label(part); what.AddToClassList("credit-part"); row.Add(what);
                card.Add(row);
            }
            card.Add(Section2("THE FRAMEWORK"));
            var fw = new Label($"{StartMenu.FrameworkLine(version)}. Every ruler, marshal, advisor and storyteller here is a framework agent: a language model that chooses from the moves the game registered, checked by the game before anything happens.");
            fw.AddToClassList("why-no"); card.Add(fw);
            card.Add(Section2("LICENCE"));
            var lic = new Label(StartMenu.Licence + ". The full text is LICENSE.md in the repository."); lic.AddToClassList("why-no"); card.Add(lic);
            var backB = new Button(back) { text = "Back" }; backB.AddToClassList("menu-button"); backB.AddToClassList("quiet");
            var foot = Row(backB); foot.style.marginTop = 10; card.Add(foot);
            Ornament();
        }

        // ---- New game: your ruler's face ----

        /// <summary>The art pack's four neutral faces for your own realm, the one you wear framed (<see cref="YourFace"/>).</summary>
        private VisualElement FacePicker()
        {
            var row = new VisualElement(); row.AddToClassList("set-row"); row.AddToClassList("face-row");
            row.Add(new Label("Your face") { style = { width = 130 } });
            var faces = new List<Button>();
            for (int i = 0; i < ArtCatalogue.PlayerFaces; i++)
            {
                int k = i;
                var b = new Button { tooltip = "Wear this face when you rule" }; b.AddToClassList("face-pick");
                var tex = ArtCatalogue.Portrait($"you-{i + 1}");
                if (tex != null) b.style.backgroundImage = new StyleBackground(tex);
                else b.text = (i + 1).ToString();
                b.clicked += () => { YourFace = k; for (int j = 0; j < faces.Count; j++) faces[j].EnableInClassList("pick", j == k); };
                b.EnableInClassList("pick", i == YourFace);
                faces.Add(b); row.Add(b);
            }
            return row;
        }

        // ---- a confirmation ----

        /// <summary>A question with two answers, the safe one first and under the keys: Esc and <paramref name="no"/> go back.</summary>
        public void ShowConfirm(string title, string text, string yes, string no, Action confirmed, Action back)
        {
            var card = Page("pause", back);
            card.Add(Heading(title));
            var body = new Label(text); body.AddToClassList("why-no"); body.style.marginBottom = 10; card.Add(body);
            card.Add(BigButton(no, "Esc", back));
            card.Add(BigButton(yes, null, confirmed));
            Ornament();
            TakeMenuItems(card, "menu-big");
            MenuMove(0);
        }
    }
}
