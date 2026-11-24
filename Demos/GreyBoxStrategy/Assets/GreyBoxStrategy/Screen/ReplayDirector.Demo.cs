using System;
using System.Collections;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// What the demo director (<c>Screen/Demo/DemoDirector.cs</c>) asks of the screen: load a recording over the
    /// menu, hold before or after an event, pan the map on its own clock, and find a province or a capital on the
    /// panel. Nothing here runs unless a demo is started.
    /// </summary>
    public sealed partial class ReplayDirector
    {
        /// <summary>Checked before each event lands: true holds playback there, once.</summary>
        public Func<ReplayEvent, bool> DemoHoldBefore;
        /// <summary>Checked after each event has landed: true holds playback there, once.</summary>
        public Func<ReplayEvent, bool> DemoHoldAfter;
        /// <summary>True while playback waits at an event for play or step.</summary>
        public bool AtGate { get; private set; }
        public bool IsPlaying => playing;
        public bool Started => started;
        public ScreenUI Ui => ui;
        public Camera MapCamera => cam;
        public VisualElement UiRoot => document?.rootVisualElement;

        /// <summary>Plays a recording from its first turn, from the menu or after a game: the demo's <c>load</c> step.</summary>
        public string LoadForDemo(Recording rec)
        {
            if (started) return "A game is running.";
            sourceRecording = rec;
            game = ReplayBuilder.Build(rec);
            ui.Players = game.Players;
            // A take shows the game, not its first-turn hints.
            ui.HintSeen = _ => true;
            ui.DismissHint = _ => { };
            Show(game.Initial, null);
            StartReplay();
            return $"replaying {rec.Title}, {game.Turns.Count} turns";
        }

        private float demoUiScale = 1;

        /// <summary>A demo take's UI size: this times the size the settings give, never saved.</summary>
        public void DemoUiScale(float times) { demoUiScale = times; ApplyScale(); }

        /// <summary>Plays on from where playback holds.</summary>
        public void DemoPlay() { playing = true; ui?.SetPlaying(true, speed); }

        /// <summary>The turn being played, or -1 before the first and after the last.</summary>
        public int CurrentTurn => game != null && turnIndex < game.Turns.Count ? game.Turns[turnIndex].Turn : -1;

        /// <summary>A province's index by id, or -1.</summary>
        public int ProvinceIndexOf(string id)
        {
            if (game == null) return -1;
            for (int i = 0; i < game.ProvinceIds.Count; i++) if (game.ProvinceIds[i] == id) return i;
            return -1;
        }

        /// <summary>A country's capital, or -1.</summary>
        public int CapitalOf(string country) => ui == null ? -1 : ui.CapitalIndex(country);

        /// <summary>Where a province sits on the panel, in panel units.</summary>
        public Vector2 ProvinceOnPanel(int province) => ProvinceToPanel(province);

        /// <summary>
        /// Eases the camera over <paramref name="seconds"/> of game time so a province sits in the middle of the free map
        /// area, its view <paramref name="zoom"/> times the size it has now (below 1 is closer). A province of -1 fits the
        /// whole land again.
        /// </summary>
        public IEnumerator DemoPan(int province, float zoom, float seconds)
        {
            Vector3 from = cam.transform.position, to;
            float fromSize = cam.orthographicSize, toSize;
            if (province < 0)
            {
                FitCamera();
                to = cam.transform.position; toSize = cam.orthographicSize;
                cam.transform.position = from; cam.orthographicSize = fromSize;
            }
            else
            {
                toSize = Mathf.Clamp(fromSize * zoom, zoomMin, zoomMax);
                float scale = Screen.height / Mathf.Max(1f, document.rootVisualElement.layout.height);
                float left = ui.FreeLeft * scale, right = (ui.InspectorOpen ? 536 : 356) * scale, top = 50 * scale, bottom = 46 * scale;
                float upp = toSize * 2 / Screen.height;
                float cx = (left + (Screen.width - left - right) / 2) - Screen.width / 2f, cy = (bottom + (Screen.height - top - bottom) / 2) - Screen.height / 2f;
                var target = map.ProvinceWorld(province);
                to = new Vector3(target.x - cx * upp, target.y - cy * upp, from.z);
            }
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float e = DemoDefaults.Ease(t / seconds);
                cam.transform.position = Vector3.Lerp(from, to, e);
                cam.orthographicSize = Mathf.Lerp(fromSize, toSize, e);
                yield return null;
            }
            cam.transform.position = to;
            cam.orthographicSize = toSize;
        }

        /// <summary>For the demo's <c>inspect</c> step: a decision of a turn and a country, as its chronicle line would open it.</summary>
        public string DemoInspect(int turn, string country) => Inspect(turn, country);

        /// <summary>The events played so far, for a demo waiting on one.</summary>
        public System.Collections.Generic.IReadOnlyList<ReplayEvent> Played => played;
    }
}
