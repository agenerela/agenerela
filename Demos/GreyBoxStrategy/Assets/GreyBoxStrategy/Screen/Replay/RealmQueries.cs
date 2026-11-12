using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy.Replay
{
    /// <summary>
    /// The questions the screen asks of a moment in the replay: which provinces a country may
    /// attack and why not, how strongly a province defends, and how often an attack wins. They read
    /// the rules from <see cref="GameRules"/> and the map from <see cref="MapDefinition"/>, so the
    /// reach map mode and the panels say what <c>Legality</c> and the battle would say.
    /// </summary>
    public sealed class RealmQueries
    {
        private readonly ReplayGame game;
        private readonly MapDefinition map;
        private readonly GameRules rules;
        private readonly Dictionary<string, string> capitalOf;

        public RealmQueries(ReplayGame game, MapDefinition map = null, GameRules rules = null)
        {
            this.game = game;
            this.map = map ?? MapDefinition.Standard();
            this.rules = rules ?? GameRules.Standard;
            capitalOf = this.map.Provinces.Where(p => p.IsCapital).ToDictionary(p => p.StartOwner, p => p.Id, StringComparer.Ordinal);
        }

        public GameRules Rules => rules;

        /// <summary>The capital province id of a country.</summary>
        public string CapitalOf(string country) => country != null && capitalOf.TryGetValue(country, out var p) ? p : null;
        public int CapitalIndex(string country) => game.IndexOf(CapitalOf(country));
        public bool IsCapital(int province) => capitalOf.ContainsValue(game.ProvinceIds[province]);

        public IEnumerable<int> Neighbours(int province) => map.Neighbours(game.ProvinceIds[province]).Select(game.IndexOf);

        /// <summary>Why <paramref name="country"/> may not attack the province, or null when it may: <c>Legality</c>'s rule for <c>attack</c>.</summary>
        public string WhyNotAttack(ReplayState s, string country, int province)
        {
            string owner = s.Owners[province], id = game.ProvinceIds[province];
            if (owner == country) return "it is ours";
            if (!Neighbours(province).Any(q => s.Owners[q] == country)) return $"{id} is not next to our land";
            if (s.Countries.TryGetValue(country, out var c) && c.Army < rules.MinArmyToAttack) return $"our army is under {rules.MinArmyToAttack}";
            if (owner == null) return null;
            var r = s.Relation(country, owner);
            if (r == null || r.Kind != "war") return r?.Kind == "truce" ? $"truce with {owner}" : $"we are not at war with {owner}";
            return null;
        }

        /// <summary>Every province <paramref name="country"/> may attack at this moment.</summary>
        public List<int> Attackable(ReplayState s, string country) =>
            Enumerable.Range(0, s.Owners.Length).Where(p => WhyNotAttack(s, country, p) == null).ToList();

        /// <summary>The defence at a given roll, as the battle computes it.</summary>
        public int Defence(ReplayState s, int province, double roll = 1.0)
        {
            string owner = s.Owners[province];
            int forts = s.Forts[province] * rules.FortBonus;
            if (owner == null) return (int)Math.Round(s.Garrisons[province] * roll) + forts;
            int army = s.Countries.TryGetValue(owner, out var c) ? c.Army : 0;
            return (int)Math.Round(army * rules.DefenceShare * roll) + forts + (IsCapital(province) ? rules.CapitalBonus : 0);
        }

        /// <summary>
        /// The share of dice rolls in which <paramref name="attacker"/>'s attack on the province wins:
        /// both rolls uniform between <see cref="GameRules.RollMin"/> and <see cref="GameRules.RollMax"/>,
        /// on a 41 × 41 grid.
        /// </summary>
        public double WinChance(ReplayState s, string attacker, int province)
        {
            int army = s.Countries.TryGetValue(attacker, out var c) ? c.Army : 0;
            int wins = 0, n = 0;
            for (int i = 0; i <= 40; i++)
                for (int j = 0; j <= 40; j++)
                {
                    double ra = rules.RollMin + (rules.RollMax - rules.RollMin) * i / 40.0;
                    double rd = rules.RollMin + (rules.RollMax - rules.RollMin) * j / 40.0;
                    n++;
                    if ((int)Math.Round(army * ra) > Defence(s, province, rd)) wins++;
                }
            return wins / (double)n;
        }

        /// <summary>The diplomacy view: the relation of every pair, with whether they share a border.</summary>
        public (string kind, int since, int left, bool border) Between(ReplayState s, string a, string b)
        {
            var r = s.Relation(a, b);
            bool border = Enumerable.Range(0, s.Owners.Length).Any(p => s.Owners[p] == a && Neighbours(p).Any(q => s.Owners[q] == b));
            return (r?.Kind ?? "peace", r?.Since ?? 0, r?.TruceTurnsLeft ?? 0, border);
        }
    }
}
