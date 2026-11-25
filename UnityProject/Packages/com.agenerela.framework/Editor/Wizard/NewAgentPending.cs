using UnityEditor;
using UnityEngine;

namespace Agenerela.Editor
{
    /// <summary>
    /// What the New Agent wizard still has to do once Unity has compiled the script it wrote: add that
    /// script and <see cref="AgentBehaviour"/> to <see cref="Target"/>. Unity keeps a
    /// ScriptableSingleton's serialized fields through a domain reload, a link to a scene object
    /// included, and forgets them when the Editor closes, so nothing is written to disk.
    /// </summary>
    internal sealed class NewAgentPending : ScriptableSingleton<NewAgentPending>
    {
        /// <summary>The new script's path. Empty when nothing is waiting.</summary>
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
