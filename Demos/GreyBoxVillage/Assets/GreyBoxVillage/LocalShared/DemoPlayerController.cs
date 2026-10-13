using UnityEngine;
using UnityEngine.InputSystem;

namespace Demos.Shared
{
    /// <summary>Temporary #14 seam: camera-aligned movement, suspended while typing.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class DemoPlayerController : MonoBehaviour
    {
        public bool InputBlocked { get; set; }
        public float Speed = 5f;
        private CharacterController controller;
        private float verticalSpeed;

        private void Awake() => controller = GetComponent<CharacterController>();

        private void Update()
        {
            var keyboard = Keyboard.current;
            var input = Vector2.zero;
            if (!InputBlocked && keyboard != null)
            {
                input.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                input.y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0)
                    - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
            }
            Move(input, Time.deltaTime);
        }

        public void Move(Vector2 input, float deltaTime)
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            var forward = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
            forward.y = 0;
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            var direction = Vector3.ClampMagnitude(right * input.x + forward * input.y, 1f);
            verticalSpeed = controller.isGrounded ? -2f : Mathf.Max(verticalSpeed - 20f * deltaTime, -30f);
            controller.Move((direction * Speed + Vector3.up * verticalSpeed) * deltaTime);
            if (direction.sqrMagnitude > 0.01f) transform.forward = direction;
        }
    }
}
