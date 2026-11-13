using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>
    /// The scripted player's rules for the writs turn (rules v2, R1), one temperament or the other:
    /// the yardstick a model ruler is measured against, as the one-move scripts are today. It reads the
    /// world as a careful player would and never reads the dice.
    /// </summary>
    /// <remarks>
    /// <para><b>The court:</b> settle a war it is winning or losing badly, recruit to keep up with the
    /// strongest neighbour, start a war when no free land is left beside it and a neighbour is weak,
    /// fortify the front while at war (cautious), raise taxes when broke, or do nothing.</para>
    /// <para><b>An army:</b> march on the best target in reach its odds allow (our occupied land first,
    /// then an enemy's undefended land, then neutral land), else toward the nearest such target, else
    /// hold.</para>
    /// <para><b>A treaty:</b> take land offered; accept white peace unless winning; accept a demand
    /// only when losing badly.</para>
    /// </remarks>
    public static class WritScripted
    {
        public static Move Decide(Temperament t, CountryRequest r)
        {
            switch (r.Moment)
            {
                case Moment.Army: return Army(t, r);
                case Moment.Envoy: return Answer(t, r);
                case Moment.Choice: return r.DefaultMove ?? r.Legal[0];
                default: return Court(t, r);
            }
        }

        private static bool Cautious(Temperament t) => t.Name == Temperament.Cautious.Name;

        private static Move Court(Temperament t, CountryRequest r)
        {
            var w = r.World;
            string me = r.CountryId;
            var my = w.Country(me);
            Move Legal(string action, string target = null) =>
                r.Legal.FirstOrDefault(m => m.Action == action && m.Target == target && !r.Ordered.Contains(m.ToString()));

            // 1. Wars: settle one that is won, lost, or going nowhere.
            foreach (var enemy in w.EnemiesOf(me).OrderBy(c => c.Id, StringComparer.Ordinal))
            {
                int score = WarScore.Of(w, me, enemy.Id);
                int length = w.Turn - w.War(me, enemy.Id).StartedOnTurn;
                if (score >= (Cautious(t) ? 15 : 25) || (length >= 12 && score > 0))
                    if (Legal(Actions.DemandOccupied, enemy.Id) is Move demand) return demand;
                if (score <= (Cautious(t) ? -25 : -40))
                    if (Legal(Actions.CedeOccupied, enemy.Id) is Move cede) return cede;
                if (score <= (Cautious(t) ? -10 : -25) || (length >= 15 && Math.Abs(score) < 10))
                    if (Legal(Actions.OfferWhitePeace, enemy.Id) is Move peace) return peace;
            }

            // 2. War, once no free land is left beside us, on the weakest neighbour we outmatch. Its army
            // is spread over its land, so, as in the one-move game, the share defending a province counts.
            // The expansionist weighs war before arming; the cautious only after.
            Move War()
            {
                bool freeLand = w.ProvinceList.Any(p => p.Owner == null && w.Neighbours(p).Any(n => n.Controller == me));
                if (freeLand || w.EnemiesOf(me).Count() >= t.MaxWars) return null;
                var target = r.Legal.Where(m => m.Action == Actions.DeclareWar)
                    .Select(m => w.Country(m.Target))
                    .Where(c => my.Army >= c.Army * w.Rules.DefenceShare * t.DeclareMargin)
                    .OrderBy(c => c.Army).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
                return target == null ? null : Legal(Actions.DeclareWar, target.Id);
            }
            if (!Cautious(t) && War() is Move early) return early;

            // 3. Keep up with the strongest neighbour.
            int threat = w.Alive.Where(c => c.Id != me && w.Borders(me, c.Id)).Select(c => c.Army).DefaultIfEmpty(0).Max();
            if (my.Army < threat * t.ArmyPerThreat || !w.ArmiesOf(me).Any())
                if (Legal(Actions.Recruit) is Move recruit) return recruit;

            // 4. Fortify the front while at war (cautious only), then war for the cautious.
            if (t.FortifyAtWar && w.EnemiesOf(me).Any() && my.Gold >= w.Rules.FortCost + 40)
            {
                var front = r.Legal.Where(m => m.Action == Actions.Fortify)
                    .Select(m => w.Province(m.Target))
                    .Where(p => w.Neighbours(p).Any(n => n.Owner != null && w.AtWar(me, n.Owner)))
                    .OrderByDescending(p => p.IsCapital).ThenBy(p => p.Forts).ThenBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
                if (front != null && Legal(Actions.Fortify, front.Id) is Move fort) return fort;
            }
            if (Cautious(t) && War() is Move late) return late;

            // 5. Money.
            if (my.Gold < 20 && Legal(Actions.RaiseTaxes) is Move tax && my.Stability >= t.TaxMinStability) return tax;
            if (my.Gold >= 80 && Legal(Actions.Recruit) is Move more) return more;
            return Move.Hold;
        }

        private static Move Answer(Temperament t, CountryRequest r)
        {
            var w = r.World;
            string offer = r.OfferId ?? "";
            int score = WarScore.Of(w, r.CountryId, r.OfferedBy);
            bool yes;
            if (offer.Contains(Actions.CedeOccupied)) yes = true;
            else if (offer.Contains(Actions.OfferWhitePeace)) yes = score <= (Cautious(t) ? 15 : 0);
            else yes = score <= (Cautious(t) ? -25 : -40);
            return new Move(yes ? Actions.AcceptPeace : Actions.RefusePeace);
        }

        private static Move Army(Temperament t, CountryRequest r)
        {
            var w = r.World;
            var army = w.FindArmy(r.ArmyId);
            if (army == null) return Move.Hold;
            string me = r.CountryId;
            var rules = w.Rules;
            var paths = WritLegality.Paths(w, me, army.Location, rules.MarchRange);

            // The value of marching on a province, or 0 when it is no target at these odds.
            double Worth(Province p)
            {
                int steps = paths.TryGetValue(p.Id, out var path) ? path.Count : 9;
                int enemies = w.ArmiesAt(p.Id).Where(a => a.Owner != me && w.AtWar(me, a.Owner)).Sum(a => a.Soldiers);
                if (p.Owner == me && p.Occupier != null)
                    return army.Soldiers >= t.AttackMargin * enemies ? 6 - steps : 0;
                if (p.Owner == null)
                {
                    double defence = p.Garrison + p.Forts * rules.FortBonus + enemies;
                    return army.Soldiers >= t.AttackMargin * defence ? 3 - steps * 0.5 : 0;
                }
                if (p.Controller != me && p.Controller != null && w.AtWar(me, p.Controller))
                {
                    double defence = enemies + rules.MilitiaBase + rules.MilitiaPerFort * p.Forts
                                     + p.Forts * rules.FortBonus + (p.IsCapital ? rules.CapitalBonus : 0);
                    return army.Soldiers >= t.AttackMargin * defence ? 5 - steps + (p.IsCapital ? 2 : 0) : 0;
                }
                return 0;
            }

            // A cautious realm at war keeps its last army home unless the target is next door.
            bool homeGuard = Cautious(t) && w.EnemiesOf(me).Any() && w.ArmiesOf(me).Count() == 1
                             && w.Province(army.Location).IsCapital && w.Province(army.Location).Owner == me;

            var best = r.Legal.Where(m => m.Action == Actions.March)
                .Select(m => (move: m, p: w.Province(m.Target), worth: Worth(w.Province(m.Target))))
                .Where(x => x.worth > 0 && (!homeGuard || paths[x.p.Id].Count == 1))
                .OrderByDescending(x => x.worth).ThenBy(x => x.p.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (best.move != null) return best.move;
            if (homeGuard) return Move.Hold;

            // Nothing in reach: walk toward the nearest province that is a target beyond it.
            var goals = new HashSet<string>(w.ProvinceList.Where(p => Worth(p) > 0 || p.Owner == null
                || (p.Controller != null && p.Controller != me && w.AtWar(me, p.Controller))).Select(p => p.Id), StringComparer.Ordinal);
            if (goals.Count == 0) return Move.Hold;
            var toward = r.Legal.Where(m => m.Action == Actions.March)
                .Select(m => (move: m, d: Distance(w, m.Target, goals)))
                .Where(x => x.d < Distance(w, army.Location, goals))
                .OrderBy(x => x.d).ThenBy(x => x.move.Target, StringComparer.Ordinal)
                .FirstOrDefault();
            return toward.move ?? Move.Hold;
        }

        // Steps on the map from a province to the nearest of a set, ignoring who may pass.
        private static int Distance(World w, string from, HashSet<string> goals)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { from };
            var frontier = new List<string> { from };
            for (int d = 0; frontier.Count > 0 && d < 50; d++)
            {
                if (frontier.Any(goals.Contains)) return d;
                var next = new List<string>();
                foreach (string at in frontier)
                    foreach (string n in w.Map.Neighbours(at))
                        if (seen.Add(n)) next.Add(n);
                frontier = next;
            }
            return int.MaxValue;
        }
    }
}
