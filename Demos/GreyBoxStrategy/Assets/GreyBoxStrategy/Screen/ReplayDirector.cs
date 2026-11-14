using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.InputSystem;

using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// Watch mode, playing back a recorded game: builds the replay, the map and the screen, then
    /// lands the events one by one with the timings of the design page ("How the colour change
    /// should feel"). No model is called.
    /// </summary>
    public sealed partial class ReplayDirector : MonoBehaviour
    {
        [SerializeField] private TextAsset recording;
        [SerializeField] private MapView map;
        [SerializeField] private StyleSheet style;
        [SerializeField] private ThemeStyleSheet theme;
        [SerializeField] private Texture2D portraits;
        [SerializeField] private bool playOnStart = true;
        [Tooltip("Show the start menu (replay or live). Off, or a live game configured before Play, starts at once.")]
        [SerializeField] private bool showMenu = true;
        [Tooltip("Playback speed at start: 1, 2 or 4.")]
        [SerializeField] private float speed = 1;
        [Tooltip("When it is set and playing live, the screen follows a live game instead of the recording.")]
        [SerializeField] private LiveGame live;

        private ReplayGame game;
        private ScreenUI ui;
        private Camera cam;
        private UIDocument document;
        private ReplayState shown;
        private bool playing;
        private bool stepOnce;
        private int turnIndex, eventIndex;
        private Vector3 dragStart;
        private bool dragging;
        private Vector2Int lastSize;
        private PanelSettings panelSettings;

        /// <summary>
        /// In the editor: a warning when the Game view draws the game smaller than its own window and
        /// stretches it, as a fixed resolution such as 1920 × 1080 does on a larger screen. The game
        /// cannot change the editor's Game view itself; the person can, in a click.
        /// </summary>
        private static string GameViewWarning()
        {
#if UNITY_EDITOR
            var type = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            var views = type == null ? null : Resources.FindObjectsOfTypeAll(type);
            if (views == null || views.Length == 0) return null;
            var view = (UnityEditor.EditorWindow)views[0];
            float ppp = UnityEditor.EditorGUIUtility.pixelsPerPoint;
            float windowW = view.position.width * ppp, windowH = (view.position.height - 21) * ppp; // less the Game view's toolbar
            if (Screen.width >= windowW * 0.9f || Screen.height >= windowH * 0.9f) return null;
            return $"The Game view draws the game at {Screen.width} × {Screen.height} and stretches it to fill {Mathf.RoundToInt(windowW)} × {Mathf.RoundToInt(windowH)}, so it looks soft. " +
                   "In the Game view's resolution menu choose Free Aspect, set Scale to 1x, and untick Low Resolution Aspect Ratios.";
#else
            return null;
#endif
        }

        /// <summary>For the Unity CLI: where the playback is, as "turn 3, event 4 of 9".</summary>
        public string LiveStatus => live == null ? "no live source" : live.Status + $", {game?.Turns.Count ?? 0} turns logged, log {live.LogPath}";
        public LiveGame Live => live;
        public string Position => game == null ? "not built" : $"turn {(turnIndex < game.Turns.Count ? game.Turns[turnIndex].Turn : game.Turns.Count)}, event {eventIndex}";
        public ReplayGame Game => game;

        private IEnumerator Start()
        {
            cam = Camera.main;
            // A generated world is made, played and drawn behind a loading card, off the main thread
            // where it can be, so the screen never freezes while it waits.
            if (pendingWorld != null) yield return LoadGeneratedWorld();
            else if (pendingClassic != null) yield return LoadClassicWrits();
            StartScreen();
        }

        private void StartScreen()
        {
            bool isLive = live != null && live.PlayLive;
            bool generated = world != null || prebuilt;
            if (generated) { started = true; isLive = false; }
            else if (isLive)
            {
                started = true;
                live.Begin();
                game = live.Replay();
            }
            else game = ReplayBuilder.Build(Recording.Parse(recording.text));
            if (world == null) { CountryLook.UseWorld(null); map.Build(); }
            FitCamera();

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panelSettings = panel;
            LoadSettings();
            panel.sortingOrder = 10;
            document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panel;

            var root = document.rootVisualElement;
            ApplyFonts(root);
            ui = new ScreenUI(root, style, portraits, game.CountryIds, game.ProvinceIds, game.Players, game.Title)
            {
                ProvinceToPanel = ProvinceToPanel,
                PlayPressed = () => { playing = !playing; ui.SetPlaying(playing, speed); },
                StepPressed = () => { playing = false; stepOnce = true; ui.SetPlaying(false, speed); },
                SpeedPressed = () => { speed = speed >= 4 ? 1 : speed * 2; ui.SetPlaying(playing, speed); },
                LinePressed = l =>
                {
                    if (l.Event.Kind == ReplayEventKind.Story) { OpenEvent(l.Event); return; }
                    if (l.Event.Kind == ReplayEventKind.Battle) { if (l.Event.ProvinceIndex >= 0) PanTo(l.Event.ProvinceIndex); ui.ShowBattle(l.Event); return; }
                    if (l.Event.ProvinceIndex >= 0) PanTo(l.Event.ProvinceIndex); else if (l.Country != null) PanTo(ui.CapitalIndex(l.Country));
                    if (CanInspect(l.Event)) ui.ShowCard(DecisionCard.From(l.Event));
                },
                CountryPressed = id => { OpenCountry(id); FrameCountry(id); },
                ProvincePressed = p => { PanTo(p); OpenProvince(p); },
            };
            queries = new RealmQueries(game, game.Map, game.Rules);
            ui.Queries = queries;
            ui.WinAt = game.Rules?.ProvincesToWin ?? 15;
            // Conquest (the writs game's default): the last realm standing wins, so there is no province count to reach.
            ui.Conquest = game.Rules != null && game.Rules.Victory == VictoryMode.Conquest;
            if (ui.Conquest) ui.WinAt = game.ProvinceIds.Count;
            ui.MaxTurns = game.Rules?.MaxTurns ?? 30;
            if (world != null) ui.ProvinceLabelSize = i => labelSizes[i];
            ui.Land = world?.Geometry != null ? world : null;
            ui.BuildPanels();
            ui.ModePressed = SetMode;
            ui.FollowPressed = Follow;
            ui.DiplomacyPressed = ToggleDiplomacy;
            ui.DecisionPressed = e => { if (CanInspect(e)) ui.ShowCard(DecisionCard.From(e)); };
            ui.AddMenuButton(OpenPause);
            ui.NextArmyPressed = NextArmyJump;
            ui.EdgeMarkPressed = m => { PanTo(m.Province, closer: true); if (m.Event != null) ui.ShowBattle(m.Event); };
            WireStory();
            WireWrits();
            DevTools.Director = this;
            ApplyScale();
            ui.monoFont = UnityEngine.TextCore.Text.FontAsset.CreateFontAsset("Consolas", "Regular");
            Show(game.Initial, null);
            ui.SetTurn(1, "Council");
            playing = playOnStart;
            ui.SetPlaying(playing, speed);
            if (ruleStart != null)
            {
                // A rule game on a generated world: the session begins on the screen just built for its world.
                var r = ruleStart.Value;
                ruleStart = null;
                started = false;
                BeginRuleOn(r.country, r.model, r.scripted, r.arm, r.seed, r.map, r.rules);
                // After the first frames: the first Update fits the camera to the window, which would undo it.
                ui.Root.schedule.Execute(() => FrameRealm(r.country)).StartingIn(150);
                return;
            }
            if (showMenu && !isLive && !generated)
            {
                // "New game" on the end screen: the scene reloads straight to New game.
                if (newGameNext) { newGameNext = false; ShowNewGame(); }
                else ShowMainMenu();
                return;
            }
            StartCoroutine(Play());
        }

        private bool started;

        /// <summary>The menu's first choice, and the Unity CLI's: play the recorded game.</summary>
        public void StartReplay()
        {
            if (started) return;
            started = true;
            ui.HideMenu();
            StartCoroutine(Play());
        }

        /// <summary>For the Unity CLI: the menu's live button.</summary>
        public void TryLive(string model, int gameSeed, string arm = null, int turns = 0) => StartCoroutine(StartLive(model, gameSeed, arm, turns));

        /// <summary>The menu's second choice: checks the environment, the lock and the server, then follows a live game.</summary>
        private IEnumerator StartLive(string model, int gameSeed, string arm = null, int turns = 0)
        {
            if (started || live == null) yield break;
            string why = LiveGame.WhyNotNow();
            if (why != null) { ui.ShowMenuStatus(why); yield break; }
            ui.ShowMenuStatus("Asking the server...", true);
            string serverWhy = null;
            yield return Gateway.CheckServer(w => serverWhy = w);
            if (serverWhy != null) { ui.ShowMenuStatus(serverWhy); yield break; }
            started = true;
            live.Configure(true, gameSeed, turns, model, arm ?? "default");
            live.Begin();
            if (live.Error != null) { ui.ShowMenuStatus(live.Error); started = false; yield break; }
            ui.Players = live.Players;
            game = live.Replay();
            ui.HideMenu();
            Show(game.Initial, null);
            StartCoroutine(Play());
        }

        /// <summary>Fonts from the OS, nothing bundled: a book serif for the screen. OFL fonts replace them later.</summary>
        internal static void ApplyFonts(VisualElement root)
        {
            foreach (var family in new[] { "Palatino Linotype", "Book Antiqua", "Georgia", "Cambria" })
            {
                var font = UnityEngine.TextCore.Text.FontAsset.CreateFontAsset(family, "Regular");
                if (font == null) continue;
                root.style.unityFontDefinition = FontDefinition.FromSDFFont(font);
                return;
            }
        }

        private void FitCamera()
        {
            // The land (about 90% of the painting each way) fills the space between the realm list
            // (248 px), the chronicle (356 px) and the top bar (50 px).
            var size = map.SizeUnits;
            float w = Screen.width, h = Screen.height, scale = CurrentUiScale();
            float left = 248 * scale, right = 356 * scale, top = 50 * scale, bottom = 46 * scale;
            float freeW = Mathf.Max(200, w - left - right), freeH = Mathf.Max(200, h - top - bottom);
            // world units per pixel needed so the land fits both ways
            var land = map.LandWorld;
            float upp = Mathf.Max(land.width * 1.04f / freeW, land.height * 1.04f / freeH);
            cam.orthographicSize = upp * h / 2;
            SetZoomRange(cam.orthographicSize);
            float cx = (left + freeW / 2) - w / 2, cy = (bottom + freeH / 2) - h / 2;
            cam.transform.position = new Vector3(land.center.x - cx * upp, land.center.y - cy * upp, -10);
        }

        /// <summary>For the Unity CLI and tests: play faster, or jump ahead to a turn without animating.</summary>
        public void SetSpeed(float s) { speed = Mathf.Clamp(s, 0.25f, 32); ui?.SetPlaying(playing, speed); }
        public void SkipToTurn(int turn) { skipUntilTurn = turn; playing = true; ui?.SetPlaying(true, speed); }
        public void Pause() { playing = false; ui?.SetPlaying(false, speed); }
        /// <summary>For the Unity CLI and the N key: play the next event, then hold.</summary>
        public void Step() { playing = false; stepOnce = true; ui?.SetPlaying(false, speed); }
        /// <summary>For the Unity CLI and the frames: play on, and hold before the next event of this kind.</summary>
        public void RunUntil(string kind) { pauseBefore = kind; playing = true; ui?.SetPlaying(true, speed); }
        private string pauseBefore;
        /// <summary>For the Unity CLI: the kind of the next event to play, or "" at a turn's end.</summary>
        public string NextKind => game == null || turnIndex >= game.Turns.Count || eventIndex >= game.Turns[turnIndex].Events.Count ? "" : game.Turns[turnIndex].Events[eventIndex].Kind.ToString();
        /// <summary>The kind of the event being played, for the CLI to wait on ("Capture").</summary>
        public string Current { get; private set; } = "";
        private int skipUntilTurn;

        private Vector2 ProvinceToPanel(int province)
        {
            if (province < 0 || document?.rootVisualElement?.panel == null) return Vector2.zero;
            var sp = cam.WorldToScreenPoint(map.ProvinceWorld(province));
            return RuntimePanelUtils.ScreenToPanel(document.rootVisualElement.panel, new Vector2(sp.x, Screen.height - sp.y));
        }

        private void Show(ReplayState s, string flip)
        {
            shown = s;
            map.SetOwners(s.Owners, id => CountryLook.Of(id).Colour, id => CountryLook.Of(id).Slot);
            ShowOccupiers(s);
            ui.ShowState(s, flip);
            ui.ShowFollowBar(s);
            ApplyMode();
            FitChartLabels();
            ui.PlaceLabels(s);
        }

        // ---- map modes, follow, panels ----

        private RealmQueries queries;
        private MapMode mode = MapMode.Political;
        private string followed;
        private readonly List<ReplayEvent> played = new List<ReplayEvent>();
        private Vector2 clickStart;
        private int selected = -1;

        /// <summary>For the Unity CLI: switch the map mode, follow a country (null for none), open a panel.</summary>
        public void SetMode(MapMode m) { mode = m; ApplyMode(); }
        public void Follow(string id)
        {
            followed = id; ui.Followed = id;
            ui.ShowFollowBar(shown); ApplyMode();
            if (id != null) { OpenCountry(id); FrameCountry(id); }
        }

        /// <summary>Centres a country's land in the free map area, so the panel does not hide it.</summary>
        public void FrameCountry(string id)
        {
            var mine = Enumerable.Range(0, game.ProvinceIds.Count).Where(q => shown.Owners[q] == id).ToList();
            if (mine.Count == 0) return;
            var c = Vector3.zero; foreach (int q in mine) c += map.ProvinceWorld(q); c /= mine.Count;
            StartCoroutine(PanRoutine(c));
        }
        public void OpenCountry(string id)
        {
            var persona = game.Players.TryGetValue(id, out var pl) ? pl.Persona : null;
            GreyBoxStrategy.Persona p = null;
            try { if (persona != null) p = GreyBoxStrategy.Personas.Named(persona); } catch (ArgumentException) { }
            sideCountry = id;
            ui.ShowCountry(id, shown, played, p?.Personality, p?.Knowledge);
        }

        /// <summary>The country whose panel is open, or null for a province's.</summary>
        private string sideCountry;

        /// <summary>Redraws the open side panel, as when rule mode's orders open or close.</summary>
        private void RefreshSide()
        {
            if (!ui.SideOpen) return;
            if (sideCountry != null) OpenCountry(sideCountry);
            else if (selected >= 0) OpenProvince(selected);
        }
        public void OpenProvince(int province)
        {
            selected = province; sideCountry = null; map.SetSelected(province);
            ui.ShowProvince(province, shown, played);
        }
        public void ToggleDiplomacy()
        {
            if (ui.DiplomacyOpen) { ui.CloseDiplomacy(); return; }
            int turn = turnIndex < game.Turns.Count ? game.Turns[turnIndex].Turn : game.Result.Turns;
            ui.ShowDiplomacy(shown, turn, played.Where(e => e.Turn == turn && (e.Kind == ReplayEventKind.PeaceOffered || e.Kind == ReplayEventKind.PeaceAccepted || e.Kind == ReplayEventKind.PeaceRefused || e.Kind == ReplayEventKind.PeaceMutual)));
        }

        private void ApplyMode()
        {
            if (shown == null || queries == null) return;
            HashSet<int> reach = null;
            if (rule != null && rule.Writs && ui.OrdersRequest != null && ui.SelectedArmy != null)
            {
                var lit = ui.Destinations();
                map.SetOverlay(null, lit, new HashSet<int>(Enumerable.Range(0, game.ProvinceIds.Count).Where(q => !lit.Contains(q))));
                ui.ShowMode(mode, shown, lit);
                return;
            }
            switch (mode)
            {
                case MapMode.Political: map.SetOverlay(null, null, null); break;
                case MapMode.Armies:
                {
                    int max = Math.Max(1, shown.Countries.Values.Max(c => c.Army));
                    map.SetOverlay(shown.Owners.Select(o => o == null ? 1f : 0.2f + 0.8f * Mathf.Min(1, shown.Countries[o].Army / (float)max)).ToList(), null, null);
                    break;
                }
                case MapMode.Forts:
                    // Rule mode: forts beyond your borders are not known, and show as none.
                    map.SetOverlay(shown.Forts.Select((f, q) => !ui.Fog.KnowsProvince(shown, q) || f == 0 ? 0.15f : f == 1 ? 0.6f : 1f).ToList(), null, null);
                    break;
                case MapMode.Reach:
                    if (followed == null) { map.SetOverlay(null, null, null); break; }
                    reach = new HashSet<int>(queries.Attackable(shown, followed));
                    var dim = new HashSet<int>(Enumerable.Range(0, game.ProvinceIds.Count).Where(q => !reach.Contains(q) && shown.Owners[q] != followed));
                    map.SetOverlay(null, reach, dim);
                    break;
            }
            ui.ShowMode(mode, shown, reach);
        }

        // ---- input: pan, zoom, keys ----

        private void UpdateScreen()
        {
            if (ui == null) return;
            if (Screen.width != lastSize.x || Screen.height != lastSize.y)
            {
                lastSize = new Vector2Int(Screen.width, Screen.height);
                ApplyScale();
                ChartResized();
                string soft = GameViewWarning();
                if (soft != null) ui.ShowNotice(soft);
            }
            var mouse = Mouse.current; var kb = Keyboard.current;
            if (kb != null && !ui.Typing)
            {
                if (kb.spaceKey.wasPressedThisFrame) ui.Root.schedule.Execute(() => { playing = !playing; ui.SetPlaying(playing, speed); });
                if (kb.nKey.wasPressedThisFrame)
                {
                    // Rule mode on the writs turn: N is the next army awaiting orders, as the rules page has it.
                    if (rule != null && rule.Writs && ui.OrdersRequest != null) NextArmyJump();
                    else { playing = false; stepOnce = true; ui.SetPlaying(false, speed); }
                }
                if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) { speed = speed >= 4 ? 1 : speed * 2; ui.SetPlaying(playing, speed); }
                if (kb.escapeKey.wasPressedThisFrame) { if (ui.DevOpen) ui.CloseDev(); else Escape(); }
                if (kb.f1Key.wasPressedThisFrame) ToggleDevMenu();
                if (kb.digit1Key.wasPressedThisFrame) SetMode(MapMode.Political);
                if (kb.digit2Key.wasPressedThisFrame) SetMode(MapMode.Armies);
                if (kb.digit3Key.wasPressedThisFrame) SetMode(MapMode.Forts);
                if (kb.digit4Key.wasPressedThisFrame) SetMode(MapMode.Reach);
                if (kb.dKey.wasPressedThisFrame) ToggleDiplomacy();
                if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && rule != null) EndTurn();
            }
            if (kb != null && !ui.Typing) CameraKeys(kb);
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                // The wheel over a panel scrolls the panel (a long chronicle, a world's lists), not the map.
                if (Mathf.Abs(wheel) > 0.01f && OverMap(mouse.position.ReadValue()))
                {
                    var before = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                    cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * Mathf.Exp(-wheel * 0.0015f), zoomMin, zoomMax);
                    var after = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                    cam.transform.position += before - after;
                }
                if (mouse.middleButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame) { dragging = true; dragStart = cam.ScreenToWorldPoint(mouse.position.ReadValue()); }
                if (mouse.middleButton.wasReleasedThisFrame || mouse.rightButton.wasReleasedThisFrame) dragging = false;
                if (dragging) cam.transform.position += dragStart - cam.ScreenToWorldPoint(mouse.position.ReadValue());
                // A left click on the map (not on a panel) opens the province's panel; a left drag pans, as the other buttons do.
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    clickStart = mouse.position.ReadValue();
                    leftDrag = OverMap(clickStart);
                    leftDragStart = cam.ScreenToWorldPoint(clickStart);
                }
                if (leftDrag && mouse.leftButton.isPressed && (mouse.position.ReadValue() - clickStart).sqrMagnitude >= 25)
                    cam.transform.position += leftDragStart - cam.ScreenToWorldPoint(mouse.position.ReadValue());
                if (mouse.leftButton.wasReleasedThisFrame) leftDrag = false;
                if (mouse.leftButton.wasReleasedThisFrame && (mouse.position.ReadValue() - clickStart).sqrMagnitude < 25)
                {
                    var sp = mouse.position.ReadValue();
                    var panelPos = RuntimePanelUtils.ScreenToPanel(document.rootVisualElement.panel, new Vector2(sp.x, Screen.height - sp.y));
                    if (document.rootVisualElement.panel.Pick(panelPos) == null)
                    {
                        int p = map.ProvinceAt(cam.ScreenToWorldPoint(sp));
                        if (p >= 0 && rule != null && rule.Writs && ui.OrdersRequest != null && ui.SelectedArmy != null)
                        {
                            // An army is chosen: the click orders its march, or says why it cannot go there.
                            string why = ui.OrderArmyTo(p);
                            ui.ShowNotice(why == null ? null : $"{Cap(game.ProvinceIds[p])}: {why}");
                        }
                        else if (p >= 0) OpenProvince(p); else { ui.CloseSide(); map.SetSelected(-1); }
                    }
                }
            }
            ClampCamera();
            ui.HostScale = HostScaleNow();
            FitChartLabels();
            ui.PlaceLabels(shown);
            PlaceEdgeMarks();
            RedrawChartIfDue();
            UpdateDetail();
            DevUpdate();
        }

        /// <summary><paramref name="closer"/>: on a far view of a big world, also zoom in until pieces read.</summary>
        private void PanTo(int province, bool closer = false)
        {
            if (province < 0) return;
            float size = cam.orthographicSize;
            if (closer && world != null)
            {
                float ppc = PanelPixelsPerCell();
                if (ppc < CloseEnough) size = Mathf.Clamp(size * ppc / CloseEnough, zoomMin, zoomMax);
            }
            StartCoroutine(PanRoutine(map.ProvinceWorld(province), size));
        }

        /// <summary>Panel pixels a grid cell that a jump to an army or a battle zooms in to, when the view is farther than that.</summary>
        public const float CloseEnough = 9;

        /// <summary>Rule mode, the N key and the council's button: the next army awaiting orders, the camera brought to it.</summary>
        private void NextArmyJump()
        {
            string a = ui.NextArmy();
            var at = a == null ? null : shown?.Army(a);
            if (at != null) PanTo(game.IndexOf(at.Location), closer: true);
            ApplyMode();
        }

        /// <summary>Pans so <paramref name="target"/> sits at the centre of the free map area, between the open panels.</summary>
        private IEnumerator PanRoutine(Vector3 target, float size = -1)
        {
            yield return null; // let a panel that just opened lay out first
            float scale = Screen.height / Mathf.Max(1f, document.rootVisualElement.layout.height);
            float left = ui.FreeLeft * scale, right = (ui.InspectorOpen ? 536 : 356) * scale, top = 50 * scale, bottom = 46 * scale;
            float fromSize = cam.orthographicSize, toSize = size > 0 ? size : fromSize;
            float upp = toSize * 2 / Screen.height;
            float cx = (left + (Screen.width - left - right) / 2) - Screen.width / 2f, cy = (bottom + (Screen.height - top - bottom) / 2) - Screen.height / 2f;
            var from = cam.transform.position; var to = new Vector3(target.x - cx * upp, target.y - cy * upp, from.z);
            for (float t = 0; t < 1; t += Time.deltaTime / 0.45f)
            {
                float e = t < 0.5f ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2;
                cam.transform.position = Vector3.Lerp(from, to, e);
                cam.orthographicSize = Mathf.Lerp(fromSize, toSize, e);
                yield return null;
            }
            cam.transform.position = to;
            cam.orthographicSize = toSize;
        }

        // ---- playback ----

        private IEnumerator Wait(float seconds)
        {
            for (float t = 0; t < seconds / speed; t += Time.deltaTime) yield return null;
        }

        private IEnumerator Gate()
        {
            while (!playing && !stepOnce) yield return null;
            stepOnce = false;
        }

        /// <summary>Reading time for a banner at 1×: 1.5 s plus 40 ms a character (the design page).</summary>
        private static float Reading(string line) => string.IsNullOrEmpty(line) ? 0.6f : 1.5f + 0.04f * line.Length;

        /// <summary>
        /// The turns ready to play. For a live game, a turn is ready once the next council has begun
        /// or the game is over: only then has every move of it landed and been logged.
        /// </summary>
        private int ReadyTurns()
        {
            if (rule != null) return RuleReadyTurns();
            if (live == null || !live.PlayLive) return game.Turns.Count;
            game = live.Replay();
            if (live.Error != null) ui.ShowNotice("The live game stopped: " + live.Error);
            if (live.Finished || game.Result != null) return game.Turns.Count;
            return Math.Max(0, game.Turns.Count - 1);
        }

        private IEnumerator Play()
        {
            for (turnIndex = 0; ; turnIndex++)
            {
                // Live: wait for the council, with the thinking indicator, until the turn has resolved.
                while (rule != null && turnIndex >= ReadyTurns() && !rule.Finished)
                {
                    var waits = rule.WaitingFor;
                    ui.SetTurn(turnIndex + 1, waits == RuleSession.Waiting.Envoy ? "Envoys" : waits == RuleSession.Waiting.Storyteller ? "Chronicler"
                        : waits == RuleSession.Waiting.Choice ? "Your choice" : "Council");
                    RuleTick();
                    yield return new WaitForSeconds(0.2f);
                }
                if (rule != null) { ReadyTurns(); RuleResolving(turnIndex + 1); }
                var councilSince = DateTime.UtcNow;
                bool slowShown = false;
                while (live != null && live.PlayLive && turnIndex >= ReadyTurns() && !live.Finished)
                {
                    if (live.Error != null) { ui.ShowNotice("The live game stopped: " + live.Error); break; }
                    // Never silent: the council's clock runs in the top bar, and a slow server is named.
                    var waited = DateTime.UtcNow - councilSince;
                    ui.SetTurn(turnIndex + 1, $"Council {(int)waited.TotalMinutes}:{waited.Seconds:00}");
                    ui.SetCard(live.HoldsCard ? "held" : null);
                    slowShown |= waited.TotalSeconds > 30;
                    if (waited.TotalSeconds > 30)
                        ui.ShowNotice($"The rulers have taken {(int)waited.TotalSeconds} s on turn {turnIndex + 1}. The server may be busy with another client. Esc, then Main menu, leaves the game and lets the RTX 5080 go.");
                    ui.SetThinking(true);
                    yield return new WaitForSeconds(1f);
                }
                if (live != null && live.PlayLive) ui.SetCard(live.HoldsCard ? "held" : null);
                if (slowShown && live?.Error == null) ui.ShowNotice(null);
                if (live != null && live.PlayLive) ReadyTurns();
                ui.SetThinking(false);
                if (turnIndex >= game.Turns.Count) break;
                var turn = game.Turns[turnIndex];
                ui.SetTurn(turn.Turn, "Upkeep");
                Show(turn.Start, null);
                if (turn.Turn >= skipUntilTurn && rule == null)
                {
                    // The council: every ruler asked at once, on one snapshot (decision 24's thinking indicator).
                    ui.SetTurn(turn.Turn, "Council");
                    ui.SetThinking(true);
                    yield return Wait(0.9f);
                    ui.SetThinking(false);
                }
                for (eventIndex = 0; eventIndex < turn.Events.Count; eventIndex++)
                {
                    var e = turn.Events[eventIndex];
                    if (e.Kind == ReplayEventKind.Upkeep || e.Kind == ReplayEventKind.Revolt || e.Kind == ReplayEventKind.TruceEnded) ui.SetTurn(turn.Turn, "Upkeep");
                    else if (e.Kind == ReplayEventKind.PeaceOffered || e.Kind == ReplayEventKind.PeaceAccepted || e.Kind == ReplayEventKind.PeaceRefused
                             || e.Kind == ReplayEventKind.TermOffered || e.Kind == ReplayEventKind.TermAccepted || e.Kind == ReplayEventKind.TermRefused || e.Kind == ReplayEventKind.Treaty) ui.SetTurn(turn.Turn, "Envoys");
                    else if (e.Kind == ReplayEventKind.ArmyOrder) ui.SetTurn(turn.Turn, "Orders");
                    else if (e.Kind == ReplayEventKind.March) ui.SetTurn(turn.Turn, "March");
                    else ui.SetTurn(turn.Turn, "Resolve");
                    played.Add(e);
                    if (turn.Turn < skipUntilTurn)
                    {
                        Show(e.After, null);
                        AddLine(e, Chronicle.Line(e, CountryLook.NameOf));
                        continue;
                    }
                    if (pauseBefore != null && e.Kind.ToString() == pauseBefore) { pauseBefore = null; playing = false; ui.SetPlaying(false, speed); }
                    yield return Gate();
                    Current = e.Kind.ToString();
                    yield return Land(e);
                    Current = "";
                }
            }
            ui.GameOver = true;
            if (rule != null) RuleOver();
            // The end screen, once the last turn has landed: every way of playing ends on it.
            if (game.Result != null && !replayingEnd) ShowEnd();
            replayingEnd = false;
            if (game.Result != null) ui.SetTurn(game.Result.Turns, $"{CountryLook.NameOf(game.Result.Winner)} wins");
            else ui.SetTurn(game.Turns.Count, live != null && live.Error != null ? "live game stopped: " + live.Error : "stopped");
            playing = false;
            ui.SetPlaying(false, speed);
        }

        private IEnumerator Land(ReplayEvent e)
        {
            var line = Chronicle.Line(e, CountryLook.NameOf);
            if (WritsKind(e.Kind)) { yield return LandWrits(e, line); yield break; }
            switch (e.Kind)
            {
                case ReplayEventKind.Upkeep:
                case ReplayEventKind.TruceEnded:
                case ReplayEventKind.Eliminated:
                    Show(e.After, null);
                    if (!AddLine(e, line)) break; // another country's harvest: not news to your court
                    yield return Wait(e.Kind == ReplayEventKind.Upkeep ? 0.35f : 1.2f);
                    break;

                case ReplayEventKind.Revolt:
                    map.SetFocus(e.ProvinceIndex, 1);
                    yield return Wait(0.5f);
                    Show(e.After, null);
                    ui.AddLine(line);
                    yield return Wait(1.0f);
                    map.SetFocus(-1, 0);
                    break;

                case ReplayEventKind.Capture:
                    yield return Capture(e, line);
                    break;

                case ReplayEventKind.Story:
                    yield return LandStory(e, line);
                    break;

                case ReplayEventKind.AttackFailed:
                case ReplayEventKind.Refused:
                {
                    var banner = ui.Say(e.Country, e.Statement, -1, Why(e));
                    yield return Wait(0.8f);
                    if (e.ProvinceIndex >= 0) yield return Failed(e);
                    Show(e.After, null);
                    ui.AddLine(line);
                    yield return Wait(Mathf.Max(0.4f, Reading(e.Statement) - 2.2f));
                    ui.Unsay(banner);
                    break;
                }

                default:
                {
                    // Envoys, wars, forts, levies, taxes, holds: the ruler speaks, the screen answers.
                    var banner = ui.Say(e.Country, e.Statement, -1, Why(e));
                    yield return Wait(0.5f);
                    if (e.Kind == ReplayEventKind.Fort && e.ProvinceIndex >= 0) { map.SetFocus(e.ProvinceIndex, 0.6f); }
                    Show(e.After, null);
                    ui.AddLine(line);
                    yield return Wait(Reading(e.Statement) - 0.5f);
                    map.SetFocus(-1, 0);
                    ui.Unsay(banner);
                    break;
                }
            }
        }

        /// <summary>
        /// The centrepiece, as the design page times it at 1×: focus 0.3 s, strike 0.4 s, fall 0.4 s
        /// (skipped on neutral land), spread 1.2 s with a wet front, settle 0.4 s, release 0.3 s.
        /// </summary>
        private IEnumerator Capture(ReplayEvent e, ChronicleLine line)
        {
            var banner = ui.Say(e.Country, e.Statement, -1, Why(e));
            yield return Wait(0.6f);

            int p = e.ProvinceIndex;
            int src = Enumerable.Range(0, game.ProvinceIds.Count).Where(q => shown.Owners[q] == e.Country)
                .OrderBy(q => (map.ProvinceWorld(q) - map.ProvinceWorld(p)).sqrMagnitude).FirstOrDefault();
            map.SetFocus(p, 0);
            for (float t = 0; t < 1; t += Time.deltaTime * speed / 0.3f) { map.SetFocus(p, Smooth(t)); yield return null; }
            map.SetFocus(p, 1);

            var arrow = ui.ShowArrow(src, p, CountryLook.Of(e.Country).Colour);
            for (float t = 0; t < 1; t += Time.deltaTime * speed / 0.4f)
            {
                ui.SetArrow(arrow, 1 - Mathf.Pow(1 - t, 2), false, t > 0.75f, t > 0.75f ? $"{e.Attack} vs {e.Defence}" : null);
                yield return null;
            }
            ui.SetArrow(arrow, 1, false, true, $"{e.Attack} vs {e.Defence}");

            map.BeginCapture(p, e.Country, e.Other, id => CountryLook.Of(id).Colour, id => CountryLook.Of(id).Slot, shown.Owners);
            float fallTime = e.Other != null ? 0.4f : 0f;
            for (float t = 0; t < 1 && fallTime > 0; t += Time.deltaTime * speed / fallTime) { map.SetCaptureProgress(p, t, 0); yield return null; }
            for (float t = 0; t < 1; t += Time.deltaTime * speed / 1.2f)
            {
                map.SetCaptureProgress(p, 1, 1 - Mathf.Pow(1 - t, 3));
                yield return null;
            }
            map.SetCaptureProgress(p, 1, 1);

            // Settle: the frontier is inked in (the shader redraws borders from the new owner), the count flips, the chronicle answers.
            yield return Wait(0.1f);
            map.EndCapture(p);
            Show(e.After, e.Country);
            ui.AddLine(line);
            yield return Wait(0.3f);
            ui.HideArrow(arrow);
            for (float t = 0; t < 1; t += Time.deltaTime * speed / 0.3f) { map.SetFocus(p, 1 - Smooth(t)); yield return null; }
            map.SetFocus(-1, 0);
            yield return Wait(Mathf.Max(0.2f, Reading(e.Statement) - 3.3f));
            ui.Unsay(banner);
        }

        private IEnumerator Failed(ReplayEvent e)
        {
            int p = e.ProvinceIndex;
            int src = Enumerable.Range(0, game.ProvinceIds.Count).Where(q => shown.Owners[q] == e.Country)
                .OrderBy(q => (map.ProvinceWorld(q) - map.ProvinceWorld(p)).sqrMagnitude).FirstOrDefault();
            map.SetFocus(p, 1);
            var arrow = ui.ShowArrow(src, p, CountryLook.Of(e.Country).Colour);
            string score = e.Kind == ReplayEventKind.Refused ? "refused" : $"{e.Attack} vs {e.Defence}";
            for (float t = 0; t < 1; t += Time.deltaTime * speed / 0.4f) { ui.SetArrow(arrow, t, true, t > 0.7f, score); yield return null; }
            ui.SetArrow(arrow, 1, true, true, score);
            yield return Wait(0.9f);
            ui.HideArrow(arrow);
            map.SetFocus(-1, 0);
        }

        /// <summary>A banner's click: open the decision behind it.</summary>
        private Action Why(ReplayEvent e) => !CanInspect(e) ? null : (Action)(() => ui.ShowCard(DecisionCard.From(e)));

        /// <summary>Whether a decision opens in the inspector: always in watch mode; in rule mode, a model ruler's once the game is over (fog of war).</summary>
        private bool CanInspect(ReplayEvent e) =>
            e?.Decision != null && e.Decision.ModelName != RuleSession.YourModel && ui.Fog.KnowsDecision(e.Decision.AgentId, ui.GameOver);

        /// <summary>A chronicle line, unless fog of war hides it. False when hidden.</summary>
        private bool AddLine(ReplayEvent e, ChronicleLine line)
        {
            if (!ui.Fog.KnowsUpkeep(e)) return false;
            ui.AddLine(line);
            return true;
        }

        /// <summary>For the Unity CLI: open the inspector on a decision, as a click on its chronicle line would.</summary>
        public string Inspect(int turn, string country)
        {
            var e = game.AllEvents.FirstOrDefault(x => x.Turn == turn && x.Country == country && x.Decision != null);
            if (e == null) return "no decision for " + country + " on turn " + turn;
            ui.ShowCard(DecisionCard.From(e));
            return e.Kind + ": " + e.Text;
        }

        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
    }
}
