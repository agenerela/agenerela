using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Demos.Shared
{
    /// <summary>
    /// The Esc menu: resume, the framework view, how fast the day runs, the sound's volume and mute,
    /// the controls, restart and the title screen. Esc while typing only leaves the text field; a
    /// second Esc opens this.
    /// </summary>
    public sealed class DemoMenu : MonoBehaviour
    {
        public DemoCommandInput Input;
        public AgentBoard Board;
        public DayNightCycle Day;
        [Tooltip("Esc closes the book first, when it is open.")]
        public VillageBook Book;
        [Range(0f, 1f), Tooltip("How loud the game's sound is, set with the menu's slider.")]
        public float Volume = 0.8f;
        public bool Muted;

        /// <summary>What every sound is scaled by: <see cref="Volume"/>, or nothing while muted.</summary>
        public float Gain => Muted ? 0f : Mathf.Clamp01(Volume);

        public bool IsOpen { get; private set; }
        /// <summary>Where the menu sits, in panel pixels, while it is open.</summary>
        public Rect? Bounds => IsOpen && panel != null ? panel.worldBound : (Rect?)null;

        private VisualElement root, panel, controls;
        private Button dayButton, boardButton, muteButton;
        private Slider volumeSlider;
        private int typingEndedFrame = -10;
        private bool wasTyping;

        private static readonly (string label, float seconds)[] DaySpeeds =
            { ("Normal (10 min)", 600f), ("Fast (3 min)", 180f), ("Slow (30 min)", 1800f), ("Stopped", 0f) };
        private int daySpeed;

        private void OnEnable()
        {
            root = DemoUI.Create(gameObject, 20);
            panel = DemoUI.Panel(root);
            panel.name = "Menu";
            panel.style.position = Position.Absolute;
            panel.style.left = Length.Percent(35);
            panel.style.right = Length.Percent(35);
            panel.style.top = Length.Percent(18);
            panel.style.paddingTop = 16;
            panel.style.paddingBottom = 16;
            panel.style.backgroundColor = new Color(0.1f, 0.08f, 0.06f, 1f);
            panel.pickingMode = PickingMode.Position;
            var title = DemoUI.Label(panel, "ASHFORD", "vui-title");
            title.style.fontSize = 24;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            var sub = DemoUI.Label(panel, "A village run by language models, through Agenerela", "vui-subtitle");
            sub.style.unityTextAlign = TextAnchor.MiddleCenter;
            sub.style.marginBottom = 10;
            DemoUI.Button(panel, "Resume", Close, "Resume");
            boardButton = DemoUI.Button(panel, "", () => { Board?.Toggle(); Refresh(); }, "ToggleBoard");
            dayButton = DemoUI.Button(panel, "", CycleDay, "DaySpeed");
            var sound = DemoUI.Row(panel);
            sound.name = "Sound";
            sound.style.marginTop = 4;
            sound.style.marginBottom = 4;
            DemoUI.Label(sound, "Sound", "vui-text").style.width = 56;
            volumeSlider = new Slider(0f, 1f) { name = "Volume", value = Volume };
            volumeSlider.AddToClassList("vui-slider");
            volumeSlider.style.flexGrow = 1;
            volumeSlider.RegisterValueChangedCallback(e => SetVolume(e.newValue));
            sound.Add(volumeSlider);
            muteButton = DemoUI.Button(sound, "", ToggleMute, "Mute");
            muteButton.style.width = 84;
            DemoUI.Button(panel, "Controls", () => ShowControls(controls.style.display == DisplayStyle.None), "Controls");
            controls = DemoUI.Panel(panel, "vui-panel--soft");
            foreach (var (key, what) in new[]
            {
                ("WASD", "walk, Shift to run"), ("Mouse (right button)", "look around"), ("Wheel", "zoom"),
                ("E / Enter", "talk to whoever is near"), ("Tab", "the framework view: agents, queue, target ids"),
                ("H", "every request in full"), ("Esc", "this menu")
            })
                DemoUI.KeyHint(controls, key, what).style.marginBottom = 3;
            DemoUI.Show(controls, false);
            DemoUI.Button(panel, "Restart the village", () => SceneManager.LoadScene(SceneManager.GetActiveScene().name), "Restart");
            DemoUI.Button(panel, "Title screen", () => SceneManager.LoadScene(DemoLauncher.LauncherPath), "OpenLauncher");
            Refresh();
            DemoUI.Show(panel, false);
        }

        private void OnDisable() => root?.Clear();

        private void CycleDay()
        {
            daySpeed = (daySpeed + 1) % DaySpeeds.Length;
            if (Day != null)
            {
                Day.Running = DaySpeeds[daySpeed].seconds > 0;
                if (Day.Running) Day.DayLengthSeconds = DaySpeeds[daySpeed].seconds;
            }
            Refresh();
        }

        /// <summary>The slider's volume, 0 to 1.</summary>
        public void SetVolume(float volume)
        {
            Volume = Mathf.Clamp01(volume);
            Refresh();
        }

        /// <summary>Mutes the sound, or brings it back at the slider's volume.</summary>
        public void ToggleMute()
        {
            Muted = !Muted;
            Refresh();
        }

        private void Refresh()
        {
            if (volumeSlider != null)
            {
                volumeSlider.SetValueWithoutNotify(Volume);
                volumeSlider.SetEnabled(!Muted);
            }
            if (muteButton != null) muteButton.text = Muted ? "Unmute" : "Mute";
            if (dayButton == null) return;
            dayButton.text = "Day: " + DaySpeeds[daySpeed].label;
            boardButton.text = Board != null && Board.Expanded ? "Hide the framework view" : "Show the framework view";
        }

        /// <summary>Shows or hides the list of controls under the menu's buttons.</summary>
        public void ShowControls(bool shown)
        {
            if (controls != null) DemoUI.Show(controls, shown);
        }

        public void Open()
        {
            IsOpen = true;
            Refresh();
            DemoUI.Show(panel, true);
            if (Input != null && Input.Player != null) Input.Player.Paused = true;
        }

        public void Close()
        {
            IsOpen = false;
            DemoUI.Show(panel, false);
            if (Input != null && Input.Player != null) Input.Player.Paused = false;
        }

        private void Update()
        {
            bool typing = Input != null && Input.IsTyping;
            if (wasTyping && !typing) typingEndedFrame = Time.frameCount;
            wasTyping = typing;
            var keyboard = Keyboard.current;
            if (keyboard == null || typing || !keyboard.escapeKey.wasPressedThisFrame) return;
            // The Esc that left the text field does not also open the menu.
            if (Time.frameCount - typingEndedFrame <= 1) return;
            if (Book != null && Book.IsOpen)
            {
                Book.Show(false);
                return;
            }
            if (IsOpen) Close();
            else Open();
        }
    }
}
