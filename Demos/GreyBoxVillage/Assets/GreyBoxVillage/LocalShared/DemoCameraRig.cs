using UnityEngine;
using UnityEngine.InputSystem;

namespace Demos.Shared
{
    /// <summary>
    /// A high third-person camera that orbits the player. Right-drag (or Q/E) turns it, dragging up
    /// and down tilts it, the scroll wheel zooms. Movement is camera-relative, so W walks away from it.
    /// </summary>
    public sealed class DemoCameraRig : MonoBehaviour
    {
        public Transform Player;
        public float Distance = 18f;
        public float MinDistance = 6f, MaxDistance = 40f;
        public float Pitch = 50f;
        public float MinPitch = 25f, MaxPitch = 80f;
        public float Yaw;
        public float DragDegreesPerPixel = 0.25f;
        public float KeyDegreesPerSecond = 90f;
        public float FollowSharpness = 10f;
        public float FocusHeight = 1f;

        private Vector3 focus;
        private bool placed;

        private void LateUpdate()
        {
            if (Player == null) return;
            ReadInput(Time.unscaledDeltaTime);

            var target = Player.position + Vector3.up * FocusHeight;
            focus = placed ? Vector3.Lerp(focus, target, 1f - Mathf.Exp(-FollowSharpness * Time.unscaledDeltaTime)) : target;
            placed = true;

            var rotation = Quaternion.Euler(Pitch, Yaw, 0);
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * Distance, rotation);
        }

        private void ReadInput(float dt)
        {
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

            // Q and E turn it too, unless the player is typing into a text field.
            var keyboard = Keyboard.current;
            var player = Player.GetComponent<DemoPlayerController>();
            if (keyboard != null && (player == null || !player.InputBlocked))
            {
                if (keyboard.qKey.isPressed) Yaw -= KeyDegreesPerSecond * dt;
                if (keyboard.eKey.isPressed) Yaw += KeyDegreesPerSecond * dt;
            }
        }
    }
}
