using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// A place north of the river worth finding: the glade, the den, the shrine and the rest. Walking
    /// within <see cref="Radius"/> of it records it in the journal (<see cref="Exploration"/>).
    /// </summary>
    /// <remarks>
    /// Not a <c>Targetable</c>: nothing here reaches an agent's schema or its observations, so Bram,
    /// the evaluation agent, is offered exactly what he was before the woods were built.
    /// </remarks>
    public sealed class PlaceToFind : MonoBehaviour
    {
        public string Title = "";
        [Tooltip("One line for the journal once it is found.")]
        public string Note = "";
        [Tooltip("How close the player must come, in metres, ignoring height.")]
        public float Radius = 4.5f;

        /// <summary>Whether the player is close enough to find it.</summary>
        public bool Reaches(Vector3 player)
        {
            var d = player - transform.position;
            d.y = 0;
            return d.magnitude <= Radius;
        }
    }
}
