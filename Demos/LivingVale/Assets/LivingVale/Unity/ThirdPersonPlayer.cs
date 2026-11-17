using UnityEngine;

namespace LivingVale.Unity
{
    [DefaultExecutionOrder(200)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class ThirdPersonPlayer : MonoBehaviour
    {
        [SerializeField] private ValeBootstrap bootstrap;
        [SerializeField] private Transform view;
        [SerializeField, Min(0f)] private float walkSpeed = 3f;
        [SerializeField, Min(0f)] private float sprintSpeed = 5.5f;
        [SerializeField, Min(0f)] private float turnSpeed = 540f;
        [SerializeField] private float gravity = -20f;

        private CharacterController controller;
        private float verticalSpeed;

        public void Configure(ValeBootstrap owner, Transform cameraTransform)
        {
            bootstrap = owner;
            view = cameraTransform;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            Vector2 input = bootstrap.Movement;
            Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 direction = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
            float speed = bootstrap.Sprinting ? sprintSpeed : walkSpeed;

            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
            }

            if (controller.isGrounded && verticalSpeed < 0f)
            {
                verticalSpeed = -2f;
            }

            // Gravity continues while typing, including when the player steps off a box.
            verticalSpeed = Mathf.Max(verticalSpeed + gravity * Time.deltaTime, -40f);
            controller.Move((direction * speed + Vector3.up * verticalSpeed) * Time.deltaTime);
        }
    }
}
