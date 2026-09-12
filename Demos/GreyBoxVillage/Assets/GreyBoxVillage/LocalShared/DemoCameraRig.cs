using UnityEngine;

namespace Demos.Shared
{
    public sealed class DemoCameraRig : MonoBehaviour
    {
        public Transform Player;
        public Vector3 Offset = new Vector3(0, 24, -24);

        private void LateUpdate()
        {
            if (Player == null) return;
            // Keep the village legible while still tracking exploration.
            var focus = new Vector3(Mathf.Clamp(Player.position.x, -5, 5), 0,
                Mathf.Clamp(Player.position.z, -2, 7));
            transform.position = focus + Offset;
            transform.LookAt(focus);
        }
    }
}
