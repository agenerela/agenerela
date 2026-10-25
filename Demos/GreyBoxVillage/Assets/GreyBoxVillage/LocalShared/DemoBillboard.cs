using UnityEngine;

namespace Demos.Shared
{
    /// <summary>Keeps a world-space label turned toward the camera as it orbits.</summary>
    public sealed class DemoBillboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera != null) transform.rotation = camera.transform.rotation;
        }
    }
}
