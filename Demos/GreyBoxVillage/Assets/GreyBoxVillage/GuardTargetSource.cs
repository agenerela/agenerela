using Agenerela;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// Offers the guards Odile can call (<c>call_guard</c>) as targets, by their ids, wherever they
    /// are: <see cref="VillageMerchant.CallableGuards"/>, which for a fire is only those who can
    /// carry water. A custom <see cref="ITargetSource"/>, like the quests', because a proximity
    /// source keys on a category, and the guards share theirs, <c>person</c>, with every villager,
    /// whom the guard's rule protects.
    /// </summary>
    /// <remarks>
    /// Odile's proximity source leaves people out, so the guards reach her schema through this one
    /// alone. A guard some earlier source already registered is skipped, since the registry refuses
    /// an id twice.
    /// </remarks>
    public sealed class GuardTargetSource : ITargetSource
    {
        /// <summary>The category the guards are offered in here, the one <c>call_guard</c> takes.</summary>
        public const string Category = "guard";

        private readonly VillageMerchant merchant;

        public GuardTargetSource(VillageMerchant merchant) => this.merchant = merchant;

        public void Collect(AgentContext ctx, TargetRegistry into)
        {
            if (merchant == null) return;
            foreach (var guard in merchant.CallableGuards())
            {
                var targetable = guard.GetComponent<Targetable>();
                if (targetable != null && !into.Contains(targetable.Id)) into.Register(targetable.Id, guard.transform, Category);
            }
        }
    }
}
