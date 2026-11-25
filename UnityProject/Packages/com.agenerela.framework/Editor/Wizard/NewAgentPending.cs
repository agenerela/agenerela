using UnityEditor;
using UnityEngine;

namespace Agenerela.Editor
{
    /// <summary>
    /// Remembers which GameObject to add a new agent to while Unity recompiles. Unity keeps these
    /// fields through a recompile and clears them when the Editor closes.
    /// </summary>
    internal sealed class NewAgentPending : ScriptableSingleton<NewAgentPending>
    {
        /// <summary>The new script's path, or empty when nothing is waiting.</summary>
        [SerializeField] internal string ScriptPath;

        [SerializeField] internal AgentProfile Profile;
        [SerializeField] internal GameObject Target;

        internal void Clear()
        {
            ScriptPath = null;
            Profile = null;
            Target = null;
        }
    }
}
