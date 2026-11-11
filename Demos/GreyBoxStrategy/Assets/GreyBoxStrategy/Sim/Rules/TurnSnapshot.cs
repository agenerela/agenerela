using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>
    /// The map as every country saw it when the council decided: armies, province counts and
    /// owners, after upkeep and before any move landed. The scorecard reads it to judge a move by
    /// the state it was chosen on ("attacked a weaker neighbour"), since <see cref="World"/> has moved
    /// on by the time anyone looks.
    /// </summary>
    public sealed class TurnSnapshot
    {
        public TurnSnapshot(World world)
        {
            Turn = world.Turn;
            Army = world.Alive.ToDictionary(c => c.Id, c => c.Army, StringComparer.Ordinal);
            Provinces = world.Alive.ToDictionary(c => c.Id, c => world.ProvinceCount(c.Id), StringComparer.Ordinal);
            Owners = world.ProvinceList.ToDictionary(p => p.Id, p => p.Owner, StringComparer.Ordinal);
        }

        public int Turn { get; }
        /// <summary>Soldiers per living country.</summary>
        public IReadOnlyDictionary<string, int> Army { get; }
        /// <summary>Provinces per living country.</summary>
        public IReadOnlyDictionary<string, int> Provinces { get; }
        /// <summary>Owner per province id; null for a neutral one.</summary>
        public IReadOnlyDictionary<string, string> Owners { get; }

        public string OwnerOf(string provinceId) => provinceId != null && Owners.TryGetValue(provinceId, out var o) ? o : null;
        public int ArmyOf(string countryId) => countryId != null && Army.TryGetValue(countryId, out int a) ? a : 0;
    }
}
