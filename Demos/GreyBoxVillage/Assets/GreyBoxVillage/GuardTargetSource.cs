using System.Linq;
using Agenerela;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// Offers the village's guards as targets, by their ids, wherever they are: whom Odile can call
    /// (<c>call_guard</c>). A custom <see cref="ITargetSource"/>, like the quests', because a
    /// proximity source keys on a category, and the guards share theirs, <c>person</c>, with every
    /// villager, whom the guard's rule protects.
    /// </summary>
    /// <remarks>
    /// Odile's proximity source leaves people out, so the guards reach her schema through this one
    /// alone. A guard some earlier source already registered is skipped, since the registry refuses
    /// an id twice.
    /// </remarks>
    public sealed class GuardTargetSource : ITargetSource
    {
        private readonly VillageCommands village;

        public GuardTargetSource(VillageCommands village) => this.village = village;

        public void Collect(AgentContext ctx, TargetRegistry into)
        {
            if (village == null) return;
            foreach (var guard in village.Minds.Where(m => m != null && m.Body is VillageGuard).Select(m => m.Body))
            {
                var targetable = guard.GetComponent<Targetable>();
                if (targetable != null && !into.Contains(targetable.Id)) into.Register(targetable.Id, guard.transform);
            }
        }
    }
}
