using System;
using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>What the New game page chose.</summary>
    public sealed class NewGameChoice
    {
        public bool Rule;
        public string Country = "kesh";
        public string Model = "qwen3.5:4b";
        public int Seed = 101;
        /// <summary>A generated world instead of the Classic board (the drawn-chart spike: scripted rulers, watch only).</summary>
        public bool Generated;
        public MapSize Size = MapSize.Medium;
        public LandShape Shape = LandShape.Continent;
        /// <summary>"off", or a storyteller preset's name (<see cref="StorytellerSettings.Presets"/>).</summary>
        public string Storyteller = "off";
        /// <summary>Rules v2's writs turn: armies as pieces, occupation and treaties (R1). The one-move game stays the default.</summary>
        public bool Writs;
        /// <summary>The writs game's victory preset: <see cref="GameSetup.ConquestPreset"/> or <see cref="GameSetup.DominationPreset"/>.</summary>
        public string Victory = GameSetup.ConquestPreset;
    }

    /// <summary>What the settings page needs to know about the screen it runs on.</summary>
    public sealed class DisplayInfo
    {
        /// <summary>In the editor the Game view decides the size and mode; the page says so instead of offering them.</summary>
        public bool InEditor;
        public List<Vector2Int> Resolutions = new List<Vector2Int>();
        public Vector2Int Native;
        /// <summary>What automatic UI scale gives on this screen now.</summary>
        public float AutoScale = 1;
    }

    /// <summary>
    /// The menus, over the painted map: the main menu (the game's first screen), New game,
    /// Settings, the pause menu (Esc in a game), the display countdown, and a notice line.
    /// </summary>
    public sealed partial class ScreenUI
    {
        private VisualElement menu, notice;
        private Label menuStatus, noticeText;
        private Button startButton;

        /// <summary>What Esc does on the page that is open: back one page, or resume. Null when no page is open.</summary>
        public Action MenuBack { get; private set; }
        public bool MenuOpen => menu != null;

        private VisualElement Page(string cls, Action back)
        {
            HideMenu();
            menu = new VisualElement(); menu.AddToClassList("menu-veil");
            var card = new VisualElement(); card.AddToClassList("panel"); card.AddToClassList("menu"); card.AddToClassList(cls);
            menu.Add(card);
            Root.Add(menu);
            MenuBack = back;
            // A tall page scrolls inside the card at 1366 x 768 rather than squeezing its fields.
            var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("menu-scroll");
            card.Add(scroll);
            return scroll.contentContainer;
        }

        public void HideMenu() { menu?.RemoveFromHierarchy(); menu = null; MenuBack = null; menuStatus = null; }

        private static Button BigButton(string text, string note, Action pressed)
        {
            var b = new Button(pressed); b.AddToClassList("menu-big");
            b.Add(new Label(text) { pickingMode = PickingMode.Ignore });
            if (note != null) { var n = new Label(note) { pickingMode = PickingMode.Ignore }; n.AddToClassList("menu-big-note"); b.Add(n); }
            return b;
        }

        private static string WatchNote(bool writs) => writs
            ? "Five model rulers play the writs turn, a court and a marshal each; you follow, inspect and pause"
            : "Five model rulers with personas play; you follow, inspect and pause";

        private static Label Heading(string text) { var l = new Label(text); l.AddToClassList("menu-head"); return l; }

        private static VisualElement Row(params VisualElement[] items)
        {
            var r = new VisualElement(); r.AddToClassList("menu-row");
            foreach (var i in items) r.Add(i);
            return r;
        }

        // ---- the main menu ----

        public void ShowMainMenu(string replayTitle, Action newGame, Action replay, Action settings, Action quit)
        {
            var card = Page("main", null);
            card.Add(new CompassRose { style = { width = 112, height = 112, alignSelf = Align.Center, marginBottom = 6 } });
            var title = new Label("PAINTED REALM"); title.AddToClassList("menu-title"); card.Add(title);
            var sub = new Label("Five rulers, each a language model playing through the Agenerela framework, contend for twenty-five provinces.");
            sub.AddToClassList("menu-sub"); card.Add(sub);
            card.Add(new GiltRule());
            card.Add(BigButton("New game", "Watch five model rulers, or rule a country against four", newGame));
            card.Add(BigButton("Replay", replayTitle + " · no model or server needed", replay));
            card.Add(BigButton("Settings", "Display, UI scale, speed, sound", settings));
            card.Add(BigButton("Quit", null, quit));
            card.Add(new GiltRule());
            var foot = new Label("A demo of the Agenerela framework · GreyBoxStrategy · Esc opens the pause menu in a game"); foot.AddToClassList("src"); foot.style.alignSelf = Align.Center;
            card.Add(foot);
        }

        // ---- New game ----

        public void ShowNewGame(NewGameChoice choice, Action<NewGameChoice> start, Action back)
        {
            var card = Page("newgame", back);
            card.Add(Heading("NEW GAME"));

            card.Add(Section2("MODE"));
            var watchB = BigButton("Watch", WatchNote(choice.Writs), null);
            var ruleB = BigButton("Rule", "You rule one country against four model rulers", null);
            var crests = new VisualElement(); crests.AddToClassList("menu-row");
            void Mode(bool rule)
            {
                choice.Rule = rule;
                watchB.EnableInClassList("pick", !rule); ruleB.EnableInClassList("pick", rule);
                crests.SetEnabled(rule);
            }
            watchB.clicked += () => Mode(false); ruleB.clicked += () => Mode(true);
            var modes = Row(watchB, ruleB); modes.AddToClassList("menu-modes"); card.Add(modes);

            card.Add(Section2("YOUR COUNTRY", "rule mode"));
            foreach (var id in CountryIds)
            {
                var look = CountryLook.Of(id);
                var cb = new Button { tooltip = $"Rule {look.Name}" }; cb.AddToClassList("menu-crest");
                cb.Add(new Crest(look, 22)); cb.Add(new Label(look.Name));
                string country = id;
                cb.clicked += () => { choice.Country = country; foreach (var x in crests.Children()) x.RemoveFromClassList("pick"); cb.AddToClassList("pick"); };
                if (id == choice.Country) cb.AddToClassList("pick");
                crests.Add(cb);
            }
            card.Add(crests);

            card.Add(Section2("THE RULERS", "on the server in " + Gateway.UrlVariable));
            var model = new TextField("Model") { value = choice.Model }; model.AddToClassList("menu-field");
            var seed = new IntegerField("Seed") { value = choice.Seed }; seed.AddToClassList("menu-field"); seed.style.maxWidth = 190;
            var dice = new Button(() => seed.value = UnityEngine.Random.Range(1, 10000)) { text = "Roll", tooltip = "A new seed: a new map of events and rolls" }; dice.AddToClassList("tbtn"); dice.style.width = 48;
            card.Add(Row(model, seed, dice));

            card.Add(Section2("MAP", "a generated world is a first look: scripted rulers play it, you watch"));
            var sizeRow = Steps("Size", new[] { "Small", "Medium", "Large", "Huge" }, (int)choice.Size, i => choice.Size = (MapSize)i);
            var shapeRow = Steps("Shape", new[] { "Continent", "Two lands", "Inland sea", "Peninsula", "Long coast" }, (int)choice.Shape, i => choice.Shape = (LandShape)i);
            void World(bool generated)
            {
                choice.Generated = generated;
                sizeRow.SetEnabled(generated); shapeRow.SetEnabled(generated);
            }
            card.Add(Steps("World", new[] { "Classic, painted", "Generated, drawn" }, choice.Generated ? 1 : 0, i => World(i == 1)));
            card.Add(sizeRow); card.Add(shapeRow);
            World(choice.Generated);

            card.Add(Section2("THE TURN", "rules v2: armies on the map, occupation, treaties"));
            // The lead's choice (13 November): both, conquest the default. Lengths are scripted games' (R1b's tuning).
            var victoryNote = new Label(); victoryNote.AddToClassList("src"); victoryNote.style.whiteSpace = WhiteSpace.Normal;
            void Victory(int i)
            {
                choice.Victory = i == 1 ? GameSetup.DominationPreset : GameSetup.ConquestPreset;
                victoryNote.text = i == 1
                    ? "Hold 75%: the first realm to own three provinces in four wins. Shorter: about 63 turns on Classic, 144 on a medium world, in scripted games."
                    : "Conquest: the last realm standing wins, and land changes hands only by treaty. About 78 turns on Classic, 178 on a medium world, in scripted games.";
            }
            var victoryRow = Steps("Victory", new[] { "Conquest", "Hold 75%" }, choice.Victory == GameSetup.DominationPreset ? 1 : 0, Victory);
            Victory(choice.Victory == GameSetup.DominationPreset ? 1 : 0);
            var turnNote = new Label(); turnNote.AddToClassList("src"); turnNote.style.whiteSpace = WhiteSpace.Normal;
            VisualElement storytellerRow = null;
            void Turn(bool writs)
            {
                choice.Writs = writs;
                victoryRow.SetEnabled(writs);
                victoryNote.style.display = writs ? DisplayStyle.Flex : DisplayStyle.None;
                watchB.Q<Label>(className: "menu-big-note").text = WatchNote(writs);

                turnNote.text = writs
                    ? "Writs: each ruler gives a few orders a turn, to its court and to armies awaiting them; armies march, fight and occupy; land changes hands by treaty. Model rulers play it, a court and a marshal each; you watch (rule mode on it comes next)."
                    : "One move: each ruler makes one move a turn, as every measured game so far.";
            }
            card.Add(Steps("Turn", new[] { "One move", "Writs and armies" }, choice.Writs ? 1 : 0, i => Turn(i == 1)));
            card.Add(victoryRow);
            card.Add(victoryNote);
            card.Add(turnNote);
            Turn(choice.Writs);

            card.Add(Section2("COMING", "not in the game yet"));
            card.Add(Coming("Persona mix", "Which personas rule, and how hard they play"));
            card.Add(Section2("STORYTELLER", "a model that makes things happen: events, dilemmas, tales"));
            var presets = StorytellerSettings.Presets;
            int sel = Math.Max(0, presets.ToList().FindIndex(p => p.Name == choice.Storyteller) + 1);
            var presetNote = new Label(); presetNote.AddToClassList("src"); presetNote.style.whiteSpace = WhiteSpace.Normal;
            void Note(int i) => presetNote.text = i == 0
                ? "Off: the random event table (harvests, bandits, unrest), as every measured game so far."
                : presets[i - 1].Description + " Dilemmas, events on many, planned tales, and a second call that titles each event.";
            storytellerRow = Steps("Storyteller", new[] { "Off" }.Concat(presets.Select(p => p.Label)).ToArray(), sel,
                i => { choice.Storyteller = i == 0 ? "off" : presets[i - 1].Name; Note(i); });
            card.Add(storytellerRow);
            Note(sel);
            card.Add(presetNote);
            Turn(choice.Writs);
            card.Add(Coming("Advisor", "A model that counsels you in rule mode: on by default, off in a click"));

            menuStatus = new Label(); menuStatus.AddToClassList("menu-status"); card.Add(menuStatus);
            var backB = new Button(back) { text = "Back" }; backB.AddToClassList("menu-button"); backB.AddToClassList("quiet");
            startButton = new Button(() => { choice.Model = model.value; choice.Seed = seed.value; start(choice); }) { text = "Start" }; startButton.AddToClassList("menu-button");
            var foot = Row(backB, new VisualElement { style = { flexGrow = 1 } }, startButton); foot.style.marginTop = 10;
            card.Add(foot);
            Mode(choice.Rule);
        }

        private static VisualElement Section2(string title, string note = null)
        {
            var h = new VisualElement(); h.AddToClassList("sec-head"); h.style.marginTop = 10;
            h.Add(new Label(title));
            if (note != null) { var n = new Label(note); n.AddToClassList("sec-note"); h.Add(n); }
            return h;
        }

        private static VisualElement Coming(string name, string what)
        {
            var r = new VisualElement(); r.AddToClassList("menu-coming");
            r.Add(new Label(name) { style = { width = 110, unityFontStyleAndWeight = FontStyle.Bold } });
            r.Add(new Label(what) { style = { flexGrow = 1, flexShrink = 1, whiteSpace = WhiteSpace.Normal } });
            var tag = new Label("COMING"); tag.AddToClassList("tag"); r.Add(tag);
            return r;
        }

        /// <summary>The New game page's status line: why a game cannot start, or that it is starting.</summary>
        public void ShowMenuStatus(string text, bool busy = false)
        {
            if (menuStatus == null) return;
            menuStatus.text = text ?? "";
            menuStatus.EnableInClassList("busy", busy);
            startButton?.SetEnabled(!busy);
        }

        // ---- Settings ----

        /// <summary>Settings: every change applies at once through <paramref name="changed"/>, which also saves it.</summary>
        public void ShowSettings(GameSettings current, DisplayInfo display, Action<GameSettings> changed, Action back)
        {
            var s = current.Clone();
            var card = Page("settings", back);
            card.Add(Heading("SETTINGS"));
            void Changed() => changed(s.Clone());

            card.Add(Section2("DISPLAY"));
            if (display.InEditor)
            {
                var n = new Label("In the editor the Game view decides the size and the window: choose Free Aspect and Scale 1x in its menus. Display mode and resolution apply in a built game.");
                n.AddToClassList("why-no"); card.Add(n);
            }
            var modeRow = Steps("Mode", new[] { "Windowed", "Borderless", "Fullscreen" }, (int)s.Display, i => { s.Display = (DisplayMode)i; Changed(); });
            modeRow.SetEnabled(!display.InEditor); card.Add(modeRow);
            var sizes = display.Resolutions.Count > 0 ? display.Resolutions : new List<Vector2Int> { display.Native };
            int idx = Math.Max(0, sizes.FindIndex(r => r.x == s.Width && r.y == s.Height));
            if (s.Width == 0) idx = Math.Max(0, sizes.FindIndex(r => r == display.Native));
            var resRow = Stepper("Resolution", sizes.Select(r => $"{r.x} × {r.y}" + (r == display.Native ? "  (display)" : "")).ToList(), idx,
                i => { s.Width = sizes[i].x; s.Height = sizes[i].y; Changed(); });
            resRow.SetEnabled(!display.InEditor); card.Add(resRow);
            var scales = GameSettings.UiScaleSteps;
            card.Add(Steps("UI scale", scales.Select(x => x == 0 ? $"Auto ({Mathf.RoundToInt(display.AutoScale * 100)}%)" : $"{Mathf.RoundToInt(x * 100)}%").ToArray(),
                Array.IndexOf(scales, s.UiScale), i => { s.UiScale = scales[i]; Changed(); }));

            card.Add(Section2("PERFORMANCE"));
            card.Add(Steps("VSync", new[] { "Off", "On" }, s.VSync ? 1 : 0, i => { s.VSync = i == 1; Changed(); }));
            var caps = GameSettings.FrameCaps;
            card.Add(Steps("Frame cap", caps.Select(c => c == 0 ? "None" : c.ToString()).ToArray(), Array.IndexOf(caps, s.FrameCap), i => { s.FrameCap = caps[i]; Changed(); }));

            card.Add(Section2("GAME"));
            var speeds = GameSettings.Speeds;
            card.Add(Steps("Playback speed", speeds.Select(x => $"{x:0}×").ToArray(), Array.IndexOf(speeds, s.PlaybackSpeed), i => { s.PlaybackSpeed = speeds[i]; Changed(); }));

            card.Add(Section2("SOUND", "no sound plays yet; these are kept for when it does"));
            card.Add(Volume("Master", s.MasterVolume, v => { s.MasterVolume = v; Changed(); }));
            card.Add(Volume("Music", s.MusicVolume, v => { s.MusicVolume = v; Changed(); }));
            card.Add(Volume("Effects", s.EffectsVolume, v => { s.EffectsVolume = v; Changed(); }));
            card.Add(Volume("Voices", s.VoiceVolume, v => { s.VoiceVolume = v; Changed(); }));

            var src = new Label("Saved as you change them, in settings.json in the game's data folder."); src.AddToClassList("src"); card.Add(src);
            var backB = new Button(back) { text = "Back" }; backB.AddToClassList("menu-button"); backB.AddToClassList("quiet");
            var foot = Row(backB); foot.style.marginTop = 10; card.Add(foot);
        }

        private static VisualElement Steps(string label, string[] options, int selected, Action<int> chose)
        {
            var row = new VisualElement(); row.AddToClassList("set-row");
            row.Add(new Label(label) { style = { width = 130 } });
            var group = new VisualElement(); group.AddToClassList("set-steps");
            var buttons = new List<Button>();
            for (int i = 0; i < options.Length; i++)
            {
                int k = i;
                var b = new Button { text = options[i] }; b.AddToClassList("order-move");
                b.clicked += () => { for (int j = 0; j < buttons.Count; j++) buttons[j].EnableInClassList("pick", j == k); chose(k); };
                b.EnableInClassList("pick", i == selected);
                buttons.Add(b); group.Add(b);
            }
            row.Add(group);
            return row;
        }

        private static VisualElement Stepper(string label, List<string> options, int selected, Action<int> chose)
        {
            var row = new VisualElement(); row.AddToClassList("set-row");
            row.Add(new Label(label) { style = { width = 130 } });
            int i = Mathf.Clamp(selected, 0, Math.Max(0, options.Count - 1));
            var value = new Label(options.Count > 0 ? options[i] : "—") { style = { minWidth = 190, unityTextAlign = TextAnchor.MiddleCenter } };
            var prev = new Button { text = "‹" }; prev.AddToClassList("tbtn");
            var next = new Button { text = "›" }; next.AddToClassList("tbtn");
            prev.clicked += () => { if (i > 0) { i--; value.text = options[i]; chose(i); } };
            next.clicked += () => { if (i < options.Count - 1) { i++; value.text = options[i]; chose(i); } };
            row.Add(prev); row.Add(value); row.Add(next);
            return row;
        }

        private static VisualElement Volume(string label, float value, Action<float> changed)
        {
            var row = new VisualElement(); row.AddToClassList("set-row");
            row.Add(new Label(label) { style = { width = 130 } });
            var slider = new Slider(0, 1) { value = value }; slider.AddToClassList("set-slider");
            var pct = new Label($"{Mathf.RoundToInt(value * 100)}%") { style = { width = 44, unityTextAlign = TextAnchor.MiddleRight } };
            slider.RegisterValueChangedCallback(e => { pct.text = $"{Mathf.RoundToInt(e.newValue * 100)}%"; changed(e.newValue); });
            row.Add(slider); row.Add(pct);
            return row;
        }

        // ---- the display countdown ----

        private VisualElement confirm;

        /// <summary>After a display change: keep it, or it reverts by itself when the countdown ends.</summary>
        public void ShowDisplayConfirm(int seconds, Action keep, Action revert)
        {
            HideDisplayConfirm();
            confirm = new VisualElement(); confirm.AddToClassList("panel"); confirm.AddToClassList("offer");
            var head = new VisualElement(); head.AddToClassList("panel-head");
            var t = new Label("KEEP THIS DISPLAY?"); t.AddToClassList("panel-title"); head.Add(t);
            confirm.Add(head);
            var text = new Label(); text.AddToClassList("why-no"); text.style.marginLeft = 12; text.style.marginRight = 12; text.style.marginTop = 10;
            confirm.Add(text);
            var revertB = new Button(() => { HideDisplayConfirm(); revert(); }) { text = "Revert" }; revertB.AddToClassList("menu-button"); revertB.AddToClassList("quiet");
            var keepB = new Button(() => { HideDisplayConfirm(); keep(); }) { text = "Keep" }; keepB.AddToClassList("menu-button"); keepB.style.marginLeft = 8;
            var row = Row(new VisualElement { style = { flexGrow = 1 } }, revertB, keepB); row.style.paddingLeft = 12; row.style.paddingRight = 12; row.style.paddingBottom = 12;
            confirm.Add(row);
            Root.Add(confirm);
            int left = seconds;
            void Tick()
            {
                if (confirm == null) return;
                text.text = $"The display changed. It goes back to how it was in {left} s unless you keep it.";
                if (left-- <= 0) { HideDisplayConfirm(); revert(); return; }
                confirm.schedule.Execute(Tick).StartingIn(1000);
            }
            Tick();
        }

        public void HideDisplayConfirm() { confirm?.RemoveFromHierarchy(); confirm = null; }
        public bool DisplayConfirmOpen => confirm != null;

        // ---- the pause menu ----

        public void ShowPause(Action resume, Action settings, Action mainMenu, string note = null)
        {
            var card = Page("pause", resume);
            card.Add(Heading("PAUSED"));
            card.Add(BigButton("Resume", "Esc", resume));
            card.Add(BigButton("Settings", null, settings));
            card.Add(BigButton("Main menu", "Ends this game; a live or rule game lets the RTX 5080 go", mainMenu));
            if (note != null) { var n = new Label(note); n.AddToClassList("src"); n.style.alignSelf = Align.Center; card.Add(n); }
        }

        // ---- the notice line ----

        /// <summary>A line across the top of the map: what went wrong, or what is happening.</summary>
        public void ShowNotice(string text)
        {
            if (notice == null)
            {
                notice = new VisualElement(); notice.AddToClassList("notice");
                noticeText = new Label(); notice.Add(noticeText);
                var close = new Button(() => notice.style.display = DisplayStyle.None) { text = "×" }; close.AddToClassList("tbtn"); notice.Add(close);
                Root.Add(notice);
            }
            noticeText.text = text;
            notice.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // ---- drawn by code ----

        /// <summary>A thin gilt rule with a diamond at its centre, between a menu's parts.</summary>
        private sealed class GiltRule : VisualElement
        {
            public GiltRule()
            {
                style.height = 14; style.marginTop = 6; style.marginBottom = 6;
                generateVisualContent += ctx =>
                {
                    var r = contentRect; float y = r.height / 2, cx = r.width / 2;
                    var p = ctx.painter2D;
                    p.strokeColor = new Color32(201, 164, 90, 160); p.lineWidth = 1;
                    p.BeginPath(); p.MoveTo(new Vector2(r.width * 0.08f, y)); p.LineTo(new Vector2(cx - 10, y)); p.Stroke();
                    p.BeginPath(); p.MoveTo(new Vector2(cx + 10, y)); p.LineTo(new Vector2(r.width * 0.92f, y)); p.Stroke();
                    p.fillColor = new Color32(210, 173, 98, 255);
                    p.BeginPath(); p.MoveTo(new Vector2(cx, y - 5)); p.LineTo(new Vector2(cx + 5, y)); p.LineTo(new Vector2(cx, y + 5)); p.LineTo(new Vector2(cx - 5, y)); p.ClosePath(); p.Fill();
                };
            }
        }

        /// <summary>A sea-chart compass rose: rings, eight points in gilt and shadow, and the north mark.</summary>
        private sealed class CompassRose : VisualElement
        {
            public CompassRose()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext ctx)
            {
                var r = contentRect;
                var c = r.center; float R = Mathf.Min(r.width, r.height) / 2 - 2;
                var p = ctx.painter2D;
                Color gold = new Color32(210, 173, 98, 255), pale = new Color32(243, 226, 184, 255), dark = new Color32(60, 46, 28, 255);
                p.strokeColor = gold; p.lineWidth = 1.2f;
                p.BeginPath(); p.Arc(c, R * 0.92f, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
                p.lineWidth = 0.8f;
                p.BeginPath(); p.Arc(c, R * 0.8f, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
                for (int i = 0; i < 32; i++)
                {
                    float a = i * Mathf.PI * 2 / 32, len = i % 4 == 0 ? 0.12f : 0.06f;
                    var d = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
                    p.BeginPath(); p.MoveTo(c + d * R * 0.8f); p.LineTo(c + d * R * (0.8f + len)); p.Stroke();
                }
                // Eight points: the four winds long, the four between short; each half lit, half in shadow.
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4;
                    float len = i % 2 == 0 ? R * 0.95f : R * 0.55f, w = i % 2 == 0 ? R * 0.16f : R * 0.12f;
                    var d = new Vector2(Mathf.Sin(a), -Mathf.Cos(a)); var n = new Vector2(-d.y, d.x);
                    p.fillColor = i % 2 == 0 ? gold : pale;
                    p.BeginPath(); p.MoveTo(c); p.LineTo(c + d * len); p.LineTo(c + n * w); p.ClosePath(); p.Fill();
                    p.fillColor = dark;
                    p.BeginPath(); p.MoveTo(c); p.LineTo(c + d * len); p.LineTo(c - n * w); p.ClosePath(); p.Fill();
                }
                p.fillColor = gold;
                p.BeginPath(); p.Arc(c, R * 0.07f, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
                // The north mark: a small fleur above the ring.
                var top = c + new Vector2(0, -R);
                p.BeginPath(); p.MoveTo(top + new Vector2(0, -2)); p.LineTo(top + new Vector2(4, 4)); p.LineTo(top + new Vector2(-4, 4)); p.ClosePath(); p.Fill();
            }
        }
    }
}
