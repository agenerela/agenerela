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
    /// <summary>An army as it stood when the council decided.</summary>
    public sealed class ArmySnapshot
    {
        public ArmySnapshot(Army a)
        {
            Id = a.Id;
            Owner = a.Owner;
            Location = a.Location;
            Soldiers = a.Soldiers;
            Destination = a.Destination;
            Path = a.Path.ToList();
            StepTurnsLeft = a.StepTurnsLeft;
            Wake = a.Wake.ToList();
        }

        public string Id { get; }
        public string Owner { get; }
        public string Location { get; }
        public int Soldiers { get; }
        /// <summary>Where it was marching, or null while holding.</summary>
        public string Destination { get; }
        public IReadOnlyList<string> Path { get; }
        public int StepTurnsLeft { get; }
        /// <summary>Why it was waiting for orders; empty while it carried one out.</summary>
        public IReadOnlyList<string> Wake { get; }
    }

    public sealed class TurnSnapshot
    {
        public TurnSnapshot(World world)
        {
            Turn = world.Turn;
            Army = world.Alive.ToDictionary(c => c.Id, c => c.Army, StringComparer.Ordinal);
            Provinces = world.Alive.ToDictionary(c => c.Id, c => world.ProvinceCount(c.Id), StringComparer.Ordinal);
            Owners = world.ProvinceList.ToDictionary(p => p.Id, p => p.Owner, StringComparer.Ordinal);
            Occupiers = world.ProvinceList.Where(p => p.Occupier != null).ToDictionary(p => p.Id, p => p.Occupier, StringComparer.Ordinal);
            Armies = world.Armies.Select(a => new ArmySnapshot(a)).ToList();
        }

        /// <summary>The writs turn: who occupies each occupied province, by province id.</summary>
        public IReadOnlyDictionary<string, string> Occupiers { get; }
        /// <summary>The writs turn: every army as it stood, so a replay or the inspector can show any past turn.</summary>
        public IReadOnlyList<ArmySnapshot> Armies { get; }

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
