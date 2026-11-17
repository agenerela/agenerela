using UnityEngine;

namespace LivingVale.Unity
{
    [RequireComponent(typeof(Camera))]
    public sealed class OrbitCamera : MonoBehaviour
    {
        [SerializeField] private ValeBootstrap bootstrap;
        [SerializeField] private Transform target;
        [SerializeField, Min(0.1f)] private float distance = 4.5f;
        [SerializeField, Min(0.01f)] private float sensitivity = 0.12f;
        [SerializeField] private float minPitch = -15f;
        [SerializeField] private float maxPitch = 65f;
        [SerializeField] private LayerMask obstructionLayers = ~(1 << 2);

        private float yaw;
        private float pitch = 20f;
        private Camera view;

        public void Configure(ValeBootstrap owner, Transform followTarget)
        {
            bootstrap = owner;
            target = followTarget;
        }

        private void Awake()
        {
            view = GetComponent<Camera>();
            yaw = target.eulerAngles.y;
        }

        private void LateUpdate()
        {
            Vector2 delta = bootstrap.OrbitDelta;
            yaw += delta.x * sensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * sensitivity, minPitch, maxPitch);
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * 1.5f;
            Vector3 backward = rotation * Vector3.back;

            // Cover the near plane as well as the camera centre to prevent wall clipping.
            float halfHeight = view.nearClipPlane * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float radius = Mathf.Max(0.22f, halfHeight * Mathf.Sqrt(1f + view.aspect * view.aspect));
            float safeDistance = distance;
            if (Physics.SphereCast(pivot, radius, backward, out var hit, distance,
                obstructionLayers, QueryTriggerInteraction.Ignore))
            {
                safeDistance = Mathf.Max(0f, hit.distance - 0.05f);
            }

            // No outward interpolation, so a newly intervening wall is resolved in this frame.
            transform.SetPositionAndRotation(pivot + backward * safeDistance, rotation);
        }
    }
}
