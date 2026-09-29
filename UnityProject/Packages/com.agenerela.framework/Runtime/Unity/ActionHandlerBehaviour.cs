using UnityEngine;

namespace Agenerela
{
    /// <summary>Base class for scene components that execute registered actions.</summary>
    public abstract class ActionHandlerBehaviour : MonoBehaviour, IActionHandler
    {
        public abstract bool IsAvailable(AgentContext ctx);

        public abstract void Execute(AgentContext ctx, AgentDecision decision);
    }
}
