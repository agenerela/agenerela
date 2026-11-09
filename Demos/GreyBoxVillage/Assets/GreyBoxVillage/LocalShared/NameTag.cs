using UnityEngine;

namespace Demos.Shared
{
    /// <summary>Marks someone the HUD labels by name when the camera is near: a person or a creature.</summary>
    public sealed class NameTag : MonoBehaviour
    {
        public string Text = "";
        [Tooltip("A second, smaller line, such as a role.")]
        public string Detail = "";
        public float Height = 2.15f;

        public Vector3 Anchor => transform.position + Vector3.up * Height;
    }
}
