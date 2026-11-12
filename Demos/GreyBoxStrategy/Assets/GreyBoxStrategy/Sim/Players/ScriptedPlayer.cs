using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>How a <see cref="ScriptedPlayer"/> weighs risk. Two are defined; a test may make more.</summary>
    public sealed class Temperament
    {
        public string Name;
        /// <summary>Attack a province when our army is at least this many times its expected defence.</summary>
        public double AttackMargin;
        /// <summary>Declare war when our army is this many times the defence of the rival's weakest province next to us.</summary>
        public double DeclareMargin;
        /// <summary>Most wars at once it will start; it never refuses one declared on it.</summary>
        public int MaxWars;
        /// <summary>Offer peace when an enemy next to us has this many times our army.</summary>
        public double OfferPeaceRatio;
        /// <summary>Accept peace when the country offering it has this many times our army.</summary>
        public double AcceptPeaceRatio;
        /// <summary>Recruit until our army is this many times the strongest rival next to us.</summary>
        public double ArmyPerThreat;
        /// <summary>While at war and rich (gold for a fort and four full levies), build a fort on the front facing the strongest enemy before recruiting.</summary>
        public bool FortifyAtWar;
        /// <summary>Raise taxes only at this stability or more.</summary>
        public int TaxMinStability;

        /// <summary>Attacks and declares war at narrower odds, never builds forts, and rarely makes peace.</summary>
        public static Temperament Expansionist => new Temperament
        {
            Name = "expansionist",
            AttackMargin = 1.3,
            DeclareMargin = 1.3,
            MaxWars = 1,
            OfferPeaceRatio = 1.7,
            AcceptPeaceRatio = 1.3,
            ArmyPerThreat = 1.2,
            FortifyAtWar = false,
            TaxMinStability = 3,
        };

        /// <summary>Attacks only with a wide margin, fortifies the front while at war, and makes peace readily.</summary>
        public static Temperament Cautious => new Temperament
        {
            Name = "cautious",
            AttackMargin = 1.5,
            DeclareMargin = 1.8,
            MaxWars = 1,
            OfferPeaceRatio = 1.1,
            AcceptPeaceRatio = 0.8,
            ArmyPerThreat = 1.2,
            FortifyAtWar = true,
            TaxMinStability = 4,
        };
    }

    /// <summary>
    /// A competent rule-based player. It reads the state the way a careful human would: expected
    /// strengths, who borders whom, who is winning. It never reads the dice. Its rules, in order:
    /// make peace when outmatched, take the best province the odds allow, spend spare gold on a
    /// fort facing the strongest enemy (cautious only), keep up with the strongest neighbour, start a war
    /// when there is no free land left and a weak neighbour, recruit, raise taxes when broke, hold.
    /// </summary>
    /// <remarks>
    /// Every country also joins a war on a runaway leader that borders it, so a game does not
    /// end with one country rolling over passive neighbours.
    /// </remarks>
    public sealed class ScriptedPlayer : IPlayer
    {
        public ScriptedPlayer(Temperament temperament)
        {
            Temperament = temperament ?? throw new ArgumentNullException(nameof(temperament));
        }

        public Temperament Temperament { get; }
        public string Kind => Temperament.Name;

        /// <summary>Turns to wait before offering the same enemy peace again.</summary>
        public const int OfferCooldown = 3;

        // The one thing it remembers between turns: when it last offered each enemy peace, so it
        // does not send an envoy every turn to a country that keeps refusing. One player plays one
        // country in one game.
        private readonly Dictionary<string, int> lastOffer = new Dictionary<string, int>(StringComparer.Ordinal);

        public Move Decide(CountryRequest request) =>
            request.Moment == Moment.Choice ? request.DefaultMove ?? request.Legal[0]
            : request.Moment == Moment.Envoy ? AnswerPeace(request) : Council(request);

        private Move Council(CountryRequest req)
        {
            var w = req.World;
            var me = w.Country(req.CountryId);
            var t = Temperament;
            var rules = w.Rules;
            Move Legal(string action, string target = null) =>
                req.Legal.FirstOrDefault(m => m.Action == action && m.Target == target);

            var rivals = w.Alive.Where(c => c.Id != me.Id && w.Borders(me.Id, c.Id)).ToList();
            var enemies = rivals.Where(c => w.AtWar(me.Id, c.Id)).OrderByDescending(c => c.Army).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
            string leader = Leader(w);

            // 1. Peace when outmatched by an enemy on our border.
            foreach (var e in enemies)
            {
                var offer = Legal(Actions.OfferPeace, e.Id);
                bool recently = lastOffer.TryGetValue(e.Id, out int when) && req.Turn - when < OfferCooldown;
                if (offer != null && !recently && e.Army >= me.Army * t.OfferPeaceRatio)
                {
                    lastOffer[e.Id] = req.Turn;
                    return offer;
                }
            }

            // 2. The best province the odds allow.
            var attack = req.Legal
                .Where(m => m.Action == Actions.Attack)
                .Select(m => (move: m, p: w.Province(m.Target)))
                .Select(x => (x.move, x.p, margin: me.Army / Math.Max(1.0, ExpectedDefence(w, x.p))))
                .Select(x => (x.move, x.margin, value: x.margin
                    + (x.p.IsCapital ? 0.1 : 0)
                    + (x.p.Owner != null && x.p.Owner == leader ? 0.2 : 0)))
                .OrderByDescending(x => x.value).ThenBy(x => x.move.Target, StringComparer.Ordinal)
                .FirstOrDefault();
            if (attack.move != null && attack.margin >= t.AttackMargin) return attack.move;

            // 3. Fortify the front, if the temperament does, with gold for four full levies left over.
            var strongest = enemies.FirstOrDefault();
            int fullLevyCost = (rules.LevyBase + rules.LevyPerProvince * w.ProvinceCount(me.Id)) * rules.GoldPerSoldier;
            if (t.FortifyAtWar && strongest != null && me.Gold >= rules.FortCost + 4 * fullLevyCost)
            {
                var fort = req.Legal
                    .Where(m => m.Action == Actions.Fortify)
                    .Select(m => w.Province(m.Target))
                    .Where(p => w.Neighbours(p).Any(n => n.Owner == strongest.Id))
                    .OrderBy(p => p.Forts).ThenByDescending(p => p.IsCapital).ThenBy(p => p.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (fort != null && fort.Forts == 0) return Legal(Actions.Fortify, fort.Id);
            }

            // 4. Keep up with the strongest neighbour.
            double threat = rivals.Select(c => c.Army * (w.AtWar(me.Id, c.Id) ? 1.0 : w.RelationBetween(me.Id, c.Id) == Relation.Truce ? 0.5 : 0.8))
                .DefaultIfEmpty(0).Max();
            var recruit = Legal(Actions.Recruit);
            if (recruit != null && me.Army < threat * t.ArmyPerThreat) return recruit;

            // 5. War: on a runaway leader always; otherwise when no free land is left.
            int wars = w.EnemiesOf(me.Id).Count();
            if (leader != null && leader != me.Id)
            {
                var onLeader = Legal(Actions.DeclareWar, leader);
                if (onLeader != null && me.Army >= w.Country(leader).Army * 0.5) return onLeader;
            }
            bool freeLand = req.Legal.Any(m => m.Action == Actions.Attack && w.Province(m.Target).IsNeutral);
            if (!freeLand && wars < t.MaxWars)
            {
                var war = req.Legal
                    .Where(m => m.Action == Actions.DeclareWar)
                    .Select(m => (move: m, weakest: WeakestFront(w, me.Id, m.Target)))
                    .Where(x => me.Army >= t.DeclareMargin * x.weakest)
                    .OrderBy(x => x.weakest).ThenBy(x => x.move.Target, StringComparer.Ordinal)
                    .Select(x => x.move)
                    .FirstOrDefault();
                if (war != null) return war;
            }

            // 6. Recruit with what is left; 7. taxes when broke; 8. hold.
            if (recruit != null) return recruit;
            var taxes = Legal(Actions.RaiseTaxes);
            if (taxes != null && me.Gold < rules.MinLevy * rules.GoldPerSoldier * 3 && me.Stability >= t.TaxMinStability) return taxes;
            return Move.Hold;
        }

        private Move AnswerPeace(CountryRequest req)
        {
            var w = req.World;
            var me = w.Country(req.CountryId);
            var them = w.Country(req.OfferedBy);
            var accept = new Move(Actions.AcceptPeace);
            var refuse = new Move(Actions.RefusePeace);
            if (them == null) return refuse;
            if (them.Id == Leader(w)) return refuse;
            if (them.Army >= me.Army * Temperament.AcceptPeaceRatio) return accept;

            bool canWin = w.ProvincesOf(them.Id).Any(p => w.Touches(me.Id, p)
                && me.Army >= Temperament.AttackMargin * ExpectedDefence(w, p));
            return canWin ? refuse : accept;
        }

        /// <summary>What a province would field on average, with no luck either way.</summary>
        public static double ExpectedDefence(World w, Province p)
        {
            var r = w.Rules;
            if (p.Owner == null) return p.Garrison + p.Forts * r.FortBonus;
            var owner = w.Country(p.Owner);
            return owner.Army * r.DefenceShare + p.Forts * r.FortBonus + (p.IsCapital ? r.CapitalBonus : 0);
        }

        /// <summary>The cheapest of a rival's provinces that touch ours.</summary>
        private static double WeakestFront(World w, string me, string rival) =>
            w.ProvincesOf(rival).Where(p => w.Touches(me, p)).Select(p => ExpectedDefence(w, p)).DefaultIfEmpty(double.MaxValue).Min();

        /// <summary>A country within four provinces of winning, if one is.</summary>
        public static string Leader(World w)
        {
            var best = w.Alive.OrderByDescending(c => w.ProvinceCount(c.Id)).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
            return best != null && w.ProvinceCount(best.Id) >= w.Rules.ProvincesToWin - 4 ? best.Id : null;
        }
    }
}
