using Demos.Shared;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// Oswin, the hermit by his hut north of the river: a scripted villager with fixed lines, said in
    /// turn when the player presses E beside him. Not an agent and not a target, so he adds no
    /// request to the one queue the four agents share, and nothing to any schema.
    /// </summary>
    public sealed class Hermit : MonoBehaviour
    {
        public string[] Lines =
        {
            "Mind the den past the glade. The wolf only bites when she's cornered.",
            "The moonpetals open after dark. Odile pays well for them.",
            "That shrine was old before the keep was built. Nobody remembers whose it was.",
            "From the lookout you can count every boat at the docks.",
            "Fog's the worst of it. The guards can't see past their own spears."
        };
        [Tooltip("How close the player must be to talk to him, in metres.")]
        public float Reach = 3.5f;

        /// <summary>The line above his head right now, or null.</summary>
        public string Speech => Time.time < speechUntil ? said : null;
        /// <summary>How many lines he has said; the next is <c>Lines[Said % Lines.Length]</c>.</summary>
        public int Said { get; private set; }

        private string said;
        private float speechUntil = float.NegativeInfinity;

        /// <summary>Says his next line, for a few seconds, and returns it.</summary>
        public string Talk()
        {
            if (Lines == null || Lines.Length == 0) return null;
            said = Lines[Said % Lines.Length];
            Said++;
            speechUntil = Time.time + 6f;
            var figure = GetComponent<SegmentedFigure>();
            if (figure != null) figure.Talk(Mathf.Clamp(said.Length * 0.06f, 1f, 4f));
            return said;
        }

        /// <summary>Whether the player stands close enough to talk to him.</summary>
        public bool InReach(Vector3 player) => Vector3.Distance(player, transform.position) <= Reach;

        /// <summary>Back to his first line, silent. The presenter's reset.</summary>
        public void ResetState()
        {
            Said = 0;
            speechUntil = float.NegativeInfinity;
        }
    }
}
