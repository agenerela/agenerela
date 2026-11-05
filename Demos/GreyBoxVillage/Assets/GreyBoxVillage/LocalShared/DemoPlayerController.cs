using UnityEngine;
using UnityEngine.InputSystem;

namespace Demos.Shared
{
    /// <summary>
    /// Camera-relative movement: WASD or arrows (or the left stick), Shift to run. The character
    /// turns smoothly to where it walks. Suspended while the player types or a menu is open.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class DemoPlayerController : MonoBehaviour
    {
        public bool InputBlocked { get; set; }
        public float Speed = 4.2f;
        public float RunSpeed = 7.5f;
        public float TurnSharpness = 12f;
        private CharacterController controller;
        private float verticalSpeed;
        private Vector3 velocity;

        private void Awake() => controller = GetComponent<CharacterController>();

        private void Update()
        {
            var input = Vector2.zero;
            bool run = false;
            if (!InputBlocked)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    input.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                        - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                    input.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                        - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
                    run = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                }
                var pad = Gamepad.current;
                if (pad != null)
                {
                    var stick = pad.leftStick.ReadValue();
                    if (stick.sqrMagnitude > input.sqrMagnitude) input = stick;
                    run |= pad.leftStickButton.isPressed || pad.rightTrigger.isPressed;
                }
            }
            Move(input, Time.deltaTime, run);
        }

        public void Move(Vector2 input, float deltaTime, bool run = false)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            var forward = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
            forward.y = 0;
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            var direction = Vector3.ClampMagnitude(right * input.x + forward * input.y, 1f);
            var wanted = direction * (run ? RunSpeed : Speed);
            velocity = Vector3.Lerp(velocity, wanted, 1f - Mathf.Exp(-14f * deltaTime));
            verticalSpeed = controller.isGrounded ? -2f : Mathf.Max(verticalSpeed - 20f * deltaTime, -30f);
            controller.Move((velocity + Vector3.up * verticalSpeed) * deltaTime);
            if (direction.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction),
                    1f - Mathf.Exp(-TurnSharpness * deltaTime));
        }
    }
}
