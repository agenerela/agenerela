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
    public sealed class ReplayDirector : MonoBehaviour
    {
        [SerializeField] private TextAsset recording;
        [SerializeField] private MapView map;
        [SerializeField] private StyleSheet style;
        [SerializeField] private ThemeStyleSheet theme;
        [SerializeField] private Texture2D portraits;
        [SerializeField] private bool playOnStart = true;
        [Tooltip("Playback speed at start: 1, 2 or 4.")]
        [SerializeField] private float speed = 1;

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

        /// <summary>For the Unity CLI: where the playback is, as "turn 3, event 4 of 9".</summary>
        public string Position => game == null ? "not built" : $"turn {(turnIndex < game.Turns.Count ? game.Turns[turnIndex].Turn : game.Turns.Count)}, event {eventIndex}";
        public ReplayGame Game => game;

        private void Start()
        {
            game = ReplayBuilder.Build(Recording.Parse(recording.text));
            cam = Camera.main;
            map.Build();
            FitCamera();

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.scale = Screen.height >= 2000 ? 2 : 1;
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
                LinePressed = l => { if (l.Event.ProvinceIndex >= 0) PanTo(l.Event.ProvinceIndex); else if (l.Country != null) PanTo(ui.CapitalIndex(l.Country)); },
                CountryPressed = id => PanTo(ui.CapitalIndex(id)),
            };
            Show(game.Initial, null);
            ui.SetTurn(1, "Council");
            playing = playOnStart;
            ui.SetPlaying(playing, speed);
            StartCoroutine(Play());
        }

        /// <summary>Fonts from the OS, nothing bundled: a book serif for the screen. OFL fonts replace them later.</summary>
        private static void ApplyFonts(VisualElement root)
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
            float w = Screen.width, h = Screen.height, scale = Screen.height >= 2000 ? 2 : 1;
            float left = 248 * scale, right = 356 * scale, top = 50 * scale, bottom = 46 * scale;
            float freeW = Mathf.Max(200, w - left - right), freeH = Mathf.Max(200, h - top - bottom);
            // world units per pixel needed so the land fits both ways
            float upp = Mathf.Max(size.x * 0.88f / freeW, size.y * 0.9f / freeH);
            cam.orthographicSize = upp * h / 2;
            float cx = (left + freeW / 2) - w / 2, cy = (bottom + freeH / 2) - h / 2;
            cam.transform.position = new Vector3(-cx * upp, -cy * upp, -10);
        }

        /// <summary>For the Unity CLI and tests: play faster, or jump ahead to a turn without animating.</summary>
        public void SetSpeed(float s) { speed = Mathf.Clamp(s, 0.25f, 8); ui?.SetPlaying(playing, speed); }
        public void SkipToTurn(int turn) { skipUntilTurn = turn; playing = true; ui?.SetPlaying(true, speed); }
        public void Pause() { playing = false; ui?.SetPlaying(false, speed); }
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
            ui.ShowState(s, flip);
            ui.PlaceLabels(s);
        }

        // ---- input: pan, zoom, keys ----

        private void Update()
        {
            if (ui == null) return;
            var mouse = Mouse.current; var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame) ui.Root.schedule.Execute(() => { playing = !playing; ui.SetPlaying(playing, speed); });
                if (kb.nKey.wasPressedThisFrame) { playing = false; stepOnce = true; ui.SetPlaying(false, speed); }
                if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) { speed = speed >= 4 ? 1 : speed * 2; ui.SetPlaying(playing, speed); }
            }
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    var before = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                    cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * Mathf.Exp(-wheel * 0.0015f), 1.6f, 7f);
                    var after = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                    cam.transform.position += before - after;
                }
                if (mouse.middleButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame) { dragging = true; dragStart = cam.ScreenToWorldPoint(mouse.position.ReadValue()); }
                if (mouse.middleButton.wasReleasedThisFrame || mouse.rightButton.wasReleasedThisFrame) dragging = false;
                if (dragging) cam.transform.position += dragStart - cam.ScreenToWorldPoint(mouse.position.ReadValue());
            }
            ui.PlaceLabels(shown);
        }

        private void PanTo(int province)
        {
            if (province < 0) return;
            StartCoroutine(PanRoutine(map.ProvinceWorld(province)));
        }

        private IEnumerator PanRoutine(Vector3 target)
        {
            var from = cam.transform.position; var to = new Vector3(target.x, target.y, from.z);
            for (float t = 0; t < 1; t += Time.deltaTime / 0.45f)
            {
                float e = t < 0.5f ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2;
                cam.transform.position = Vector3.Lerp(from, to, e);
                yield return null;
            }
            cam.transform.position = to;
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

        private IEnumerator Play()
        {
            for (turnIndex = 0; turnIndex < game.Turns.Count; turnIndex++)
            {
                var turn = game.Turns[turnIndex];
                ui.SetTurn(turn.Turn, "Upkeep");
                Show(turn.Start, null);
                for (eventIndex = 0; eventIndex < turn.Events.Count; eventIndex++)
                {
                    var e = turn.Events[eventIndex];
                    if (e.Kind == ReplayEventKind.Upkeep || e.Kind == ReplayEventKind.Revolt || e.Kind == ReplayEventKind.TruceEnded) ui.SetTurn(turn.Turn, "Upkeep");
                    else if (e.Kind == ReplayEventKind.PeaceOffered || e.Kind == ReplayEventKind.PeaceAccepted || e.Kind == ReplayEventKind.PeaceRefused) ui.SetTurn(turn.Turn, "Envoys");
                    else ui.SetTurn(turn.Turn, "Resolve");
                    if (turn.Turn < skipUntilTurn)
                    {
                        Show(e.After, null);
                        ui.AddLine(Chronicle.Line(e, CountryLook.NameOf));
                        continue;
                    }
                    yield return Gate();
                    Current = e.Kind.ToString();
                    yield return Land(e);
                    Current = "";
                }
            }
            ui.SetTurn(game.Result.Turns, $"{CountryLook.NameOf(game.Result.Winner)} wins");
            playing = false;
            ui.SetPlaying(false, speed);
        }

        private IEnumerator Land(ReplayEvent e)
        {
            var line = Chronicle.Line(e, CountryLook.NameOf);
            switch (e.Kind)
            {
                case ReplayEventKind.Upkeep:
                case ReplayEventKind.TruceEnded:
                case ReplayEventKind.Eliminated:
                    Show(e.After, null);
                    ui.AddLine(line);
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

                case ReplayEventKind.AttackFailed:
                case ReplayEventKind.Refused:
                {
                    var banner = ui.Say(e.Country, e.Statement);
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
                    var banner = ui.Say(e.Country, e.Statement);
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
            var banner = ui.Say(e.Country, e.Statement);
            yield return Wait(0.6f);

            int p = e.ProvinceIndex;
            int src = Enumerable.Range(0, 25).Where(q => shown.Owners[q] == e.Country)
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
            int src = Enumerable.Range(0, 25).Where(q => shown.Owners[q] == e.Country)
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

        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
    }
}
