using UnityEngine;
using UnityEngine.AI;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// The village gate's two doors. Night shuts them (<see cref="NightWatch"/>); the night watch
    /// can open and shut them again. Shut, they stop the player and carve the NavMesh, so nothing
    /// walks through, but a guard can still see through the bars.
    /// </summary>
    /// <remarks>
    /// The blocker sits on the Ignore Raycast layer, which <see cref="GuardSenses.CanSee"/> leaves
    /// out, so the gate never hides the wolf or a visitor from the watch. It is not part of the
    /// baked NavMesh: its obstacle carves the running one only while the gate is shut.
    /// </remarks>
    public sealed class VillageGate : MonoBehaviour
    {
        [Tooltip("The doors, each turning about its hinge, which is its pivot.")]
        public Transform[] Leaves = new Transform[0];
        [Tooltip("Each door's turn about the vertical while shut, in degrees.")]
        public float[] ShutYaw = new float[0];
        [Tooltip("How far each door swings open, in degrees; its sign is the direction it turns.")]
        public float[] OpenAngles = new float[0];
        public Collider Blocker;
        public NavMeshObstacle Obstacle;
        public float SwingSeconds = 1.6f;

        public bool IsOpen { get; private set; } = true;

        private float amount = 1f;

        private void Awake() => Apply(true);

        public void Open() => Set(true);

        public void Close() => Set(false);

        /// <summary>Open, at once. Used between tests and by the presenter's reset.</summary>
        public void ResetState()
        {
            IsOpen = true;
            Apply(true);
        }

        private void Set(bool open)
        {
            if (IsOpen == open) return;
            IsOpen = open;
            Report(open ? "The gate is opened." : "The gate is shut.");
            // Edit mode has no Update to swing the doors.
            Apply(!Application.isPlaying);
        }

        private void Update()
        {
            float target = IsOpen ? 1f : 0f;
            if (Mathf.Approximately(amount, target)) return;
            amount = Mathf.MoveTowards(amount, target, Time.deltaTime / Mathf.Max(0.01f, SwingSeconds));
            Pose();
        }

        private void Apply(bool instantly)
        {
            if (instantly) amount = IsOpen ? 1f : 0f;
            if (Blocker != null) Blocker.enabled = !IsOpen;
            if (Obstacle != null) Obstacle.enabled = !IsOpen;
            Pose();
        }

        private void Pose()
        {
            float eased = Mathf.SmoothStep(0f, 1f, amount);
            for (int i = 0; i < Leaves.Length; i++)
            {
                if (Leaves[i] == null) continue;
                float yaw = (i < ShutYaw.Length ? ShutYaw[i] : 0f) + (i < OpenAngles.Length ? OpenAngles[i] : 0f) * eased;
                Leaves[i].localRotation = Quaternion.Euler(0, yaw, 0);
            }
        }

        private void Report(string message) => Debug.Log("[GreyBoxVillage] " + message, this);
    }
}
