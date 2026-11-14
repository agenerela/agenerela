using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>
    /// An army standing on the map (rules v2, section 3.1, as slice R1 builds it): one kind of soldier
    /// until R3, an order it carries out on its own, and the reasons it is waiting for a new one.
    /// </summary>
    public sealed class Army
    {
        public Army(string id, string owner, string location, int soldiers, int raisedOnTurn)
        {
            Id = id;
            Owner = owner;
            Location = location;
            Soldiers = soldiers;
            OrderedOnTurn = raisedOnTurn;
            Wake.Add("raised");
        }

        /// <summary>The general's name as an id plus <c>_host</c> (<c>maren_host</c>); it stays with the army.</summary>
        public string Id { get; }
        public string Owner { get; }
        public string Location { get; set; }
        /// <summary>
        /// Every soldier in it, of whatever type. Setting it keeps the mix (losses come from each type in
        /// proportion); with no riders or bows every soldier is a pike, which is all the rules know while
        /// <see cref="GameRules.UnitTypes"/> is off.
        /// </summary>
        public int Soldiers
        {
            get => Pikes + Riders + Bows;
            set => SetTotal(value);
        }
        /// <summary>Soldiers by type (rules v2 3.3); riders and bows only while <see cref="GameRules.UnitTypes"/> is on.</summary>
        public int Pikes { get; private set; }
        public int Riders { get; private set; }
        public int Bows { get; private set; }

        public int Of(UnitType t) => t == UnitType.Riders ? Riders : t == UnitType.Bows ? Bows : Pikes;

        /// <summary>Adds soldiers of one type (a levy joining).</summary>
        public void Add(UnitType t, int n)
        {
            if (n <= 0) return;
            if (t == UnitType.Riders) Riders += n;
            else if (t == UnitType.Bows) Bows += n;
            else Pikes += n;
        }

        // The new total split over the types in proportion, the remainder to the largest fractions (pikes first on ties).
        private void SetTotal(int value)
        {
            if (value <= 0) { Pikes = Riders = Bows = 0; return; }
            int total = Pikes + Riders + Bows;
            if (total == 0 || (Riders == 0 && Bows == 0)) { Pikes = value; Riders = Bows = 0; return; }
            if (value == total) return;
            var have = new[] { Pikes, Riders, Bows };
            var now = new int[3];
            var frac = new double[3];
            for (int i = 0; i < 3; i++)
            {
                double exact = (double)value * have[i] / total;
                now[i] = (int)Math.Floor(exact);
                frac[i] = exact - now[i];
            }
            int left = value - now.Sum();
            foreach (int i in Enumerable.Range(0, 3).OrderByDescending(i => frac[i]).ThenBy(i => i).Take(left)) now[i]++;
            Pikes = now[0]; Riders = now[1]; Bows = now[2];
        }

        /// <summary>Where it is marching, or null while it holds.</summary>
        public string Destination { get; set; }
        /// <summary>The provinces still to enter on the way, the next first.</summary>
        public List<string> Path { get; } = new List<string>();
        /// <summary>Turns still to spend before it enters <see cref="Path"/>'s first province (rough ground takes two).</summary>
        public int StepTurnsLeft { get; set; }
        /// <summary>The turn its current order was given (or it was raised).</summary>
        public int OrderedOnTurn { get; set; }
        /// <summary>
        /// Why it is waiting for orders, newest last; empty while it carries out an order. An army with
        /// none waits too (v2 1.1's wake conditions).
        /// </summary>
        public List<string> Wake { get; } = new List<string>();
        /// <summary>Where it stood when this turn's movement began: where a beaten attacker falls back to.</summary>
        public string CameFrom { get; set; }

        public bool Marching => Destination != null;
        public bool Awaiting => Wake.Count > 0;

        /// <summary>Stops it where it stands and marks it waiting, with the reason.</summary>
        public void Stop(string why)
        {
            Destination = null;
            Path.Clear();
            StepTurnsLeft = 0;
            if (!Wake.Contains(why)) Wake.Add(why);
        }

        public override string ToString() => $"{Id} ({Owner}, {Soldiers} at {Location}{(Marching ? ", marching to " + Destination : "")})";
    }

    /// <summary>
    /// One battle of the writs turn, every term of it: what the screen's battle report shows and the
    /// chronicle tells (v2 3.4's report, with today's formula's terms until R3 adds unit types and generals).
    /// </summary>
    public sealed class BattleRecord
    {
        public int Turn;
        /// <summary>The province fought in, or null for a border crossing (<see cref="Border"/>).</summary>
        public string Province;
        /// <summary>For a battle on a border: the two provinces, the attacker's side first.</summary>
        public string[] Border;
        public string Attacker;
        /// <summary>The defending country, or null for a neutral garrison.</summary>
        public string Defender;
        /// <summary><c>army</c>, <c>militia</c> (a country's land with no army) or <c>garrison</c> (neutral land).</summary>
        public string DefenderKind;
        public List<string> AttackerArmies = new List<string>();
        public List<string> DefenderArmies = new List<string>();
        public int AttackerSoldiers;
        /// <summary>The defending armies' soldiers plus <see cref="Militia"/>.</summary>
        public int DefenderSoldiers;
        /// <summary>The militia or garrison that fought, included in <see cref="DefenderSoldiers"/>.</summary>
        public int Militia;
        /// <summary>The province's terrain; null on a border. It has no effect on a battle until R3.</summary>
        public string Terrain;
        public int Forts;
        /// <summary>The defence added by forts and by a capital, when the defender holds the province.</summary>
        public int FortTerm;
        public int CapitalTerm;
        public double AttackRoll;
        public double DefenceRoll;
        public double AttackStrength;
        public double DefenceStrength;
        /// <summary>With unit types (R3): each side's strength from the counter rule over its plain strength; 1 without them.</summary>
        public double AttackCounter = 1, DefenceCounter = 1;
        /// <summary>The battle's largest counter effect, "valen's pikes broke orsk's riders (+18%)", or null.</summary>
        public string CounterNote;
        public int AttackerLost;
        public int DefenderLost;
        public bool AttackerWon;
        /// <summary>The loser was beaten by more than <see cref="GameRules.RoutRatio"/> to 1, or had nowhere to go, and was destroyed.</summary>
        public bool Routed;
        /// <summary>Where each beaten army fell back to, by army id.</summary>
        public Dictionary<string, string> Retreats = new Dictionary<string, string>();
        /// <summary>One sentence: who won, and the three terms that weighed most (numbers, forts, the capital, the dice).</summary>
        public string Why;
    }

    /// <summary>One war's record of battles, for its war score (v2 5.1).</summary>
    public sealed class WarRecord
    {
        public WarRecord(string key)
        {
            Key = key;
        }

        /// <summary>The two country ids in order, joined by '|'.</summary>
        public string Key { get; }
        /// <summary>Battles won by each side in this war, by country id; reset when a war starts.</summary>
        public Dictionary<string, int> BattlesWon { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
        /// <summary>The turn this war began.</summary>
        public int StartedOnTurn { get; set; }

        public int Won(string id) => BattlesWon.TryGetValue(id, out int n) ? n : 0;

        public void Reset(int turn)
        {
            BattlesWon.Clear();
            StartedOnTurn = turn;
        }
    }

    /// <summary>The war score and the values it is made of (v2 5.1), read by the rules, the terms' costs and the players.</summary>
    public static class WarScore
    {
        /// <summary>A province's value: 1 + its population ÷ 3, +2 for a capital.</summary>
        public static int Value(Province p) => 1 + p.Info.Population / 3 + (p.IsCapital ? 2 : 0);

        /// <summary>The value of every province a country owns.</summary>
        public static int RealmValue(World w, string countryId) => w.ProvincesOf(countryId).Sum(Value);

        /// <summary>The occupation points of the provinces of <paramref name="them"/> that <paramref name="us"/> occupies: what <c>demand_occupied</c> costs.</summary>
        public static int OccupationPoints(World w, string us, string them)
        {
            int realm = Math.Max(1, RealmValue(w, them));
            int ours = w.ProvinceList.Where(p => p.Owner == them && p.Occupier == us).Sum(Value);
            return 100 * ours / realm;
        }

        /// <summary>The war score from <paramref name="us"/>'s side, −100 to +100; 0 when not at war.</summary>
        public static int Of(World w, string us, string them)
        {
            if (!w.AtWar(us, them)) return 0;
            var r = w.Rules;
            int occupation = OccupationPoints(w, us, them) - OccupationPoints(w, them, us);
            var war = w.War(us, them);
            int battles = Math.Max(-r.BattleScoreMax, Math.Min(r.BattleScoreMax, r.BattleScore * (war.Won(us) - war.Won(them))));
            int capital = 0;
            if (w.ProvinceList.Any(p => p.Owner == them && p.IsCapital && p.Occupier == us)) capital += r.CapitalScore;
            if (w.ProvinceList.Any(p => p.Owner == us && p.IsCapital && p.Occupier == them)) capital -= r.CapitalScore;
            return Math.Max(-100, Math.Min(100, occupation + battles + capital));
        }
    }

    /// <summary>Generals' names for armies' ids: plain, short, and not real people.</summary>
    /// <summary>The three unit types of rules v2 3.3: pikes counter riders, riders counter bows, bows counter pikes.</summary>
    public enum UnitType { Pikes, Riders, Bows }

    /// <summary>A side's soldiers by type, for the counter rule.</summary>
    public struct UnitMix
    {
        public int Pikes, Riders, Bows;
        public int Total => Pikes + Riders + Bows;
        public int Of(UnitType t) => t == UnitType.Riders ? Riders : t == UnitType.Bows ? Bows : Pikes;
        public double Share(UnitType t) => Total == 0 ? 0 : (double)Of(t) / Total;

        /// <summary>The armies' soldiers, plus a militia or garrison, which fights as pikes.</summary>
        public static UnitMix Of(IEnumerable<Army> armies, int militia = 0)
        {
            var m = new UnitMix { Pikes = militia };
            foreach (var a in armies) { m.Pikes += a.Pikes; m.Riders += a.Riders; m.Bows += a.Bows; }
            return m;
        }
    }

    /// <summary>The counter rule (rules v2 3.3), and what each type costs.</summary>
    public static class Units
    {
        public static readonly UnitType[] All = { UnitType.Pikes, UnitType.Riders, UnitType.Bows };

        /// <summary>The type <paramref name="t"/> counters.</summary>
        public static UnitType Beats(UnitType t) => t == UnitType.Pikes ? UnitType.Riders : t == UnitType.Riders ? UnitType.Bows : UnitType.Pikes;
        /// <summary>The type that counters <paramref name="t"/>.</summary>
        public static UnitType BeatenBy(UnitType t) => t == UnitType.Pikes ? UnitType.Bows : t == UnitType.Riders ? UnitType.Pikes : UnitType.Riders;

        public static string Name(UnitType t) => t == UnitType.Riders ? "riders" : t == UnitType.Bows ? "bows" : "pikes";

        public static double Base(GameRules r, UnitType t) => t == UnitType.Riders ? r.RiderStrength : t == UnitType.Bows ? r.BowStrength : r.PikeStrength;

        /// <summary>m(t) = 1 + c x (enemy's share of what t counters) - c x (enemy's share of what counters t).</summary>
        public static double Multiplier(GameRules r, UnitType t, UnitMix enemy) =>
            1 + r.CounterStrength * enemy.Share(Beats(t)) - r.CounterStrength * enemy.Share(BeatenBy(t));

        /// <summary>Soldiers x base, summed, without counters.</summary>
        public static double Plain(GameRules r, UnitMix side) => All.Sum(t => side.Of(t) * Base(r, t));

        /// <summary>Soldiers x base x m(t) against the enemy's mix, summed.</summary>
        public static double Strength(GameRules r, UnitMix side, UnitMix enemy) => All.Sum(t => side.Of(t) * Base(r, t) * Multiplier(r, t, enemy));

        /// <summary>What a soldier of the type costs to recruit and to keep, as a multiple of a pike.</summary>
        public static int CostFactor(GameRules r, UnitType t) => t == UnitType.Riders ? r.RiderCostFactor : 1;

        /// <summary>
        /// The battle's largest counter effect as the report says it, "valen's pikes broke orsk's riders (+18%)", or
        /// null when neither side gained 1% or more from counters.
        /// </summary>
        public static string Describe(GameRules r, string aName, UnitMix a, string dName, UnitMix d)
        {
            string best = null;
            double most = 0.01;
            foreach (var (name, side, otherName, other) in new[] { (aName, a, dName, d), (dName, d, aName, a) })
            {
                double plain = Plain(r, side);
                if (plain <= 0) continue;
                double effect = Strength(r, side, other) / plain - 1;
                if (effect < most) continue;
                var t = All.OrderByDescending(x => side.Of(x) * Base(r, x) * (Multiplier(r, x, other) - 1)).ThenBy(x => x).First();
                most = effect;
                best = $"{name}'s {Name(t)} broke {otherName}'s {Name(Beats(t))} (+{Math.Round(effect * 100)}%)";
            }
            return best;
        }
    }

    public static class ArmyNames
    {
        public static readonly string[] Generals =
        {
            "maren", "tolly", "brask", "edda", "corin", "halvar", "isolde", "jory", "kestrel", "lunet",
            "mabon", "nessa", "orrin", "perrin", "quill", "rowan", "sabine", "tamsin", "ulric", "vesna",
            "wendel", "yorick", "aldo", "bree", "caspar", "dora", "elric", "fenna", "gideon", "hester",
            "ingram", "juno", "kael", "liesel", "morrow", "nell", "osric", "petra", "roswitha", "silas",
        };

        /// <summary>
        /// A new army's id: a general's name drawn from the seed, then the next free one, with a number
        /// only when all forty are taken.
        /// </summary>
        public static string Next(World w, string countryId)
        {
            int start = Dice.Int(w.Seed, w.Turn, countryId, "general:" + w.Armies.Count, 0, Generals.Length);
            var taken = new HashSet<string>(w.Armies.Select(a => a.Id), StringComparer.Ordinal);
            foreach (var used in w.RetiredArmyIds) taken.Add(used);
            for (int k = 0; k < Generals.Length; k++)
            {
                string id = Generals[(start + k) % Generals.Length] + "_host";
                if (!taken.Contains(id)) return id;
            }
            for (int n = 2; ; n++)
            {
                string id = Generals[start] + n + "_host";
                if (!taken.Contains(id)) return id;
            }
        }
    }
}
