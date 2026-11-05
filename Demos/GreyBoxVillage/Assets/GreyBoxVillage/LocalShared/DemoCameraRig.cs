using UnityEngine;
using UnityEngine.InputSystem;

namespace Demos.Shared
{
    /// <summary>
    /// A third-person camera behind the player. Hold the right mouse button (or use the right stick)
    /// to orbit, scroll to zoom. It eases after the player and slides in front of walls rather than
    /// through them. Movement is camera-relative, so W walks away from it.
    /// </summary>
    public sealed class DemoCameraRig : MonoBehaviour
    {
        public Transform Player;
        public float Distance = 7.5f;
        public float MinDistance = 2.5f, MaxDistance = 22f;
        public float Pitch = 22f;
        public float MinPitch = 2f, MaxPitch = 75f;
        public float Yaw;
        public float DragDegreesPerPixel = 0.22f;
        public float StickDegreesPerSecond = 140f;
        public float FollowSharpness = 12f;
        public float FocusHeight = 1.55f;
        [Tooltip("What the camera may not pass through.")]
        public LayerMask Obstacles = ~0;

        private Vector3 focus;
        private float shownDistance;
        private bool placed;

        private void LateUpdate()
        {
            if (Player == null) return;
            float dt = Time.unscaledDeltaTime;
            ReadInput(dt);

            var target = Player.position + Vector3.up * FocusHeight;
            focus = placed ? Vector3.Lerp(focus, target, 1f - Mathf.Exp(-FollowSharpness * dt)) : target;

            var rotation = Quaternion.Euler(Pitch, Yaw, 0);
            var back = rotation * Vector3.back;
            float wanted = Distance;
            // Slide in front of anything between the player and the camera.
            var hits = Physics.SphereCastAll(focus, 0.25f, back, Distance, Obstacles, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(Player) || hit.distance <= 0f) continue;
                if (hit.collider.GetComponent<CharacterController>() != null) continue;
                wanted = Mathf.Min(wanted, Mathf.Max(hit.distance - 0.1f, MinDistance * 0.5f));
            }
            shownDistance = placed ? Mathf.Lerp(shownDistance, wanted, 1f - Mathf.Exp(-(wanted < shownDistance ? 25f : 6f) * dt)) : wanted;
            placed = true;
            transform.SetPositionAndRotation(focus + back * shownDistance, rotation);
        }

        private void ReadInput(float dt)
        {
            var player = Player.GetComponent<DemoPlayerController>();
            bool typing = player != null && player.InputBlocked;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.rightButton.isPressed)
                {
                    var delta = mouse.delta.ReadValue();
                    Yaw += delta.x * DragDegreesPerPixel;
                    Pitch = Mathf.Clamp(Pitch - delta.y * DragDegreesPerPixel, MinPitch, MaxPitch);
                }
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    Distance = Mathf.Clamp(Distance * (1f - Mathf.Sign(scroll) * 0.1f), MinDistance, MaxDistance);
            }
            var pad = Gamepad.current;
            if (pad != null && !typing)
            {
                var look = pad.rightStick.ReadValue();
                Yaw += look.x * StickDegreesPerSecond * dt;
                Pitch = Mathf.Clamp(Pitch - look.y * StickDegreesPerSecond * 0.6f * dt, MinPitch, MaxPitch);
            }
        }
    }
}
