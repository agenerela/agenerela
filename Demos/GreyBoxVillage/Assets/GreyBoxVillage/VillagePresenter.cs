using System.Collections.Generic;
using System.Linq;
using Demos.Shared;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// The presenter's keys: F1 resets the village, F2 brings the wolf, F3 the thief, F4 a fire and
    /// F5 jumps to dusk. F12 shows or hides the small panel that lists them. Each situation ends where
    /// an agent is asked within a few seconds, so a talk never waits on the village.
    /// </summary>
    /// <remarks>
    /// The keys work while the framework view is open, and not while the player is typing or the
    /// menu is open (<see cref="Accepts"/>). Every key is a public method too, which the presenter
    /// script's test and the capture tool call directly. Nothing here talks to the framework: each
    /// situation only sets the scene, and the game asks the agents as it always does (DR-008).
    /// </remarks>
    public sealed class VillagePresenter : MonoBehaviour
    {
        public VillageCommands Village;
        public VillageWolf Wolf;
        public VillageThief Thief;
        public VillageFire Fire;
        public NightWatch Watch;
        public VillageAlarm Alarm;
        [Tooltip("Where the wolf comes out for F2, and where it walks to: outside the gate, in the gate guard's sight and within reach of the bell.")]
        public Transform WolfFrom;
        public Transform WolfTo;
        [Tooltip("The hour F5 jumps to, as DayNightCycle.TimeOfDay: just past the moment the cycle calls it night, so the watch changes hands in the next frame.")]
        public float DuskTimeOfDay = 0.7605f;
        [Tooltip("The hour F1 sets.")]
        public float MorningTimeOfDay = 0.36f;
        [Tooltip("Whether the panel shows when Play starts.")]
        public bool PanelShown;

        /// <summary>The keys and what each does, as the panel and the README list them.</summary>
        public static readonly IReadOnlyList<(string key, string what)> Keys = System.Array.AsReadOnly(new[]
        {
            ("F1", "Reset the village"), ("F2", "The wolf"), ("F3", "The thief"), ("F4", "A fire"), ("F5", "Jump to dusk"),
            ("F12", "Hide this panel")
        });

        private static readonly Key[] Listened = { Key.F1, Key.F2, Key.F3, Key.F4, Key.F5, Key.F12 };

        /// <summary>The last key's result, as the panel shows it; empty before the first.</summary>
        public string Status { get; private set; } = "";

        /// <summary>The panel's place on screen while it shows, so world labels keep out from under it.</summary>
        public Rect? Bounds => PanelShown && panel != null ? panel.worldBound : (Rect?)null;

        private VisualElement panel;
        private Label status;
        private readonly Dictionary<Transform, (Vector3 position, Quaternion rotation)> starts =
            new Dictionary<Transform, (Vector3, Quaternion)>();

        private void OnEnable()
        {
            var root = DemoUI.Create(gameObject, 12);
            // Opaque, like the other panels drawn over the world, so it reads over anything.
            panel = DemoUI.Panel(root);
            panel.name = "PresenterKeys";
            panel.style.position = Position.Absolute;
            panel.style.right = 14;
            panel.style.bottom = 14;
            panel.style.width = 230;
            panel.pickingMode = PickingMode.Ignore;
            DemoUI.Label(panel, "PRESENTER", "vui-subtitle");
            foreach (var (key, what) in Keys)
            {
                var row = DemoUI.KeyHint(panel, key, what);
                row.style.marginTop = 3;
            }
            status = DemoUI.Label(panel, Status, "vui-muted");
            status.style.marginTop = 6;
            status.style.whiteSpace = WhiteSpace.Normal;
            Apply();
        }

        private void Start() => Remember();

        // Where everyone stands when Play starts, for F1.
        private void Remember()
        {
            starts.Clear();
            var people = new List<Transform>();
            if (Village != null)
            {
                people.AddRange(Village.Minds.Where(m => m != null).Select(m => m.transform));
                if (Village.Player != null) people.Add(Village.Player);
            }
            if (Alarm != null) people.AddRange(Alarm.Villagers.Where(v => v != null).Select(v => v.transform));
            if (Wolf != null) people.Add(Wolf.transform);
            foreach (var t in people) starts[t] = (t.position, t.rotation);
        }

        /// <summary>Whether a key does anything now: not while the player types, nor behind the menu. The framework view may be open.</summary>
        public bool Accepts =>
            Village != null && !(Village.Input != null && Village.Input.IsTyping) && !(Village.Menu != null && Village.Menu.IsOpen);

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            foreach (var key in Listened)
                if (keyboard[key].wasPressedThisFrame) Press(key);
        }

        /// <summary>What a key does, if <see cref="Accepts"/>; returns whether it did anything.</summary>
        public bool Press(Key key)
        {
            if (!Accepts) return false;
            switch (key)
            {
                case Key.F1: ResetVillage(); return true;
                case Key.F2: SendWolf(); return true;
                case Key.F3: SendThief(); return true;
                case Key.F4: StartFire(); return true;
                case Key.F5: JumpToDusk(); return true;
                case Key.F12: TogglePanel(); return true;
                default: return false;
            }
        }

        public void TogglePanel()
        {
            PanelShown = !PanelShown;
            Apply();
        }

        private void Apply()
        {
            if (panel != null) DemoUI.Show(panel, PanelShown);
        }

        private void Say(string text)
        {
            Status = text;
            if (status != null) status.text = text;
            if (Village != null && Village.Hud != null) Village.Hud.Toast(text, 4f);
        }

        // ---- The keys ----------------------------------------------------------------------------

        /// <summary>
        /// F1: the village as Play started it, morning, with fresh agents whose memories are empty,
        /// and the wolf resting in its den for a minute. A decision still with the model is dropped
        /// when it comes back.
        /// </summary>
        public void ResetVillage()
        {
            if (Village == null) return;
            Village.ResetAgents();
            if (Watch != null) Watch.ResetState();
            if (Thief != null) Thief.ResetState();
            if (Fire != null) Fire.ResetState();
            if (Alarm != null) Alarm.Silence();
            foreach (var mind in Village.Minds.Where(m => m != null))
            {
                mind.Body.ResetState();
                if (mind.Senses != null) mind.Senses.ResetState();
                Return(mind.transform);
            }
            if (Alarm != null)
                foreach (var villager in Alarm.Villagers.Where(v => v != null))
                    if (starts.TryGetValue(villager.transform, out var start)) villager.ResetTo(start.position);
            // The wolf rests in its den for a minute, so the next stage is not interrupted by it.
            if (Wolf != null) Wolf.Rest(Wolf.Den != null ? Wolf.Den.position : starts.TryGetValue(Wolf.transform, out var home) ? home.position : Wolf.transform.position);
            if (Village.Day != null)
            {
                Village.Day.TimeOfDay = MorningTimeOfDay;
                Village.Day.Apply();
            }
            if (Village.Inventory != null) Village.Inventory.Reset(8);
            if (Village.Quests != null) Village.Quests.Define();
            if (Village.Player != null) Return(Village.Player);
            Say("F1: the village is reset. Morning, and every agent starts afresh.");
        }

        /// <summary>F2: the wolf comes up the road to the gate, where the gate guard sees it close enough to ring the bell.</summary>
        public void SendWolf()
        {
            if (Wolf == null || WolfFrom == null || WolfTo == null) return;
            Wolf.Approach(WolfFrom.position, WolfTo.position, 25f);
            // Each guard sees it afresh, whatever its last sighting.
            if (Village != null)
                foreach (var mind in Village.Minds.Where(m => m != null && m.Senses != null)) mind.Senses.ForgetWolf();
            Say("F2: the wolf is coming up the road to the gate.");
        }

        /// <summary>F3: a thief slips up to Odile's counter, takes a ware and runs for the eastern road.</summary>
        public void SendThief()
        {
            if (Thief == null) return;
            if (Thief.Merchant != null && Thief.Merchant.FirstOnCounter() == null)
            {
                Say("F3: Odile's counter is bare, so there is nothing to steal. Press F1 first.");
                return;
            }
            Thief.Begin();
            Say("F3: a hooded stranger is making for Odile's stall.");
        }

        /// <summary>F4: the bread stall catches fire.</summary>
        public void StartFire()
        {
            if (Fire == null) return;
            if (Fire.IsBurning)
            {
                Say("F4: the fire is already burning.");
                return;
            }
            Fire.Ignite();
            Say("F4: fire at " + Fire.Where + ".");
        }

        /// <summary>F5: dusk, just as night falls: the gate shuts and the watch changes hands.</summary>
        public void JumpToDusk()
        {
            if (Village == null || Village.Day == null) return;
            if (Village.Day.IsNight)
            {
                Say("F5: it is night already. F1 brings the morning back.");
                return;
            }
            Village.Day.TimeOfDay = DuskTimeOfDay;
            Village.Day.Apply();
            Say("F5: dusk. The gate shuts for the night, and Wren takes the watch.");
        }

        private void Return(Transform t)
        {
            if (!starts.TryGetValue(t, out var start)) return;
            var nav = t.GetComponent<NavMeshAgent>();
            var controller = t.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            if (nav != null && nav.isActiveAndEnabled && nav.isOnNavMesh) nav.Warp(start.position);
            else t.position = start.position;
            t.rotation = start.rotation;
            if (controller != null) controller.enabled = true;
        }
    }
}
