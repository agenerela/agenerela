using System;
using System.Globalization;
using Demos.Shared;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>Explicit demo shortcuts, deliberately not a natural-language decision system.</summary>
    public sealed class VillageCommands : MonoBehaviour
    {
        public DemoCommandInput Input;
        public DecisionDebugOverlay Overlay;
        public Transform Player;
        public VillageGuard Guard;
        public Transform Tower;
        public Transform Bridge;
        public Transform TrainingDummy;
        public float SpeakingDistance = 5f;

        private void OnEnable() => Input.Submitted += Submit;
        private void OnDisable() => Input.Submitted -= Submit;

        public string[] Observations() => new[]
        {
            "It is daytime.",
            "The gate is open.",
            "You are standing at the gate.",
            "You are not following the player.",
            "The player is " + Vector3.Distance(Player.position, Guard.transform.position)
                .ToString("0.0", CultureInfo.InvariantCulture) + " m away.",
            "The tower and bridge can be reached on foot.",
            "The training_dummy is in the training yard."
        };

        public void Submit(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            string command = text.Trim().TrimEnd('.', '!').ToLowerInvariant();
            if (Vector3.Distance(Player.position, Guard.transform.position) > SpeakingDistance)
            {
                Overlay.ShowPlaceholder(text, "none", "no_target", "Move within 5 m of the guard to speak.");
                return;
            }
            if (command == "follow me")
                Overlay.ShowPlaceholder(text, "follow_player", "no_target", Guard.Follow(Player));
            else if (command == "go to the tower")
                Overlay.ShowPlaceholder(text, "move_to", "tower", Guard.MoveTo(Tower));
            else if (command == "go to the bridge")
                Overlay.ShowPlaceholder(text, "move_to", "bridge", Guard.MoveTo(Bridge));
            else if (command == "attack the training dummy" || command == "attack training_dummy")
                Overlay.ShowPlaceholder(text, "attack_target", "training_dummy", Guard.Attack(TrainingDummy));
            else
                Overlay.ShowPlaceholder(text, "none", "no_target",
                    "No placeholder shortcut matched. Try: Follow me / Go to the tower / Go to the bridge / Attack the training dummy.");
        }

        private void OnGUI()
        {
            float scale = Mathf.Max(0.5f, Mathf.Min(Screen.width / 1000f, Screen.height / 720f));
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUILayout.BeginArea(new Rect(16, 16, 630, 100), GUI.skin.box);
            GUILayout.Label("GREYBOX VILLAGE / #19", new GUIStyle(GUI.skin.label) { fontSize = 22 });
            GUILayout.Label("Blue: player   |   Gold: guard   |   Flat blocks: future action targets");
            GUILayout.Label("Guard distance: " + Vector3.Distance(Player.position, Guard.transform.position).ToString("0.0") +
                " m   |   Speak within 5 m   |   No AI connected");
            GUILayout.EndArea();
            GUI.matrix = oldMatrix;
        }
    }
}
