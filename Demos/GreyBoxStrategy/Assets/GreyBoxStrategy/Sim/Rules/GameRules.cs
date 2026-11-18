using System.Collections.Generic;

namespace GreyBoxStrategy
{
    /// <summary>How a turn is played (rules v2, section 1).</summary>
    public enum TurnStyle
    {
        /// <summary>Today's game: one move a country a turn, one army number a country.</summary>
        OneMove,
        /// <summary>
        /// Rules v2's turn (slice R1): a few writs a turn, a court order, orders for the armies
        /// awaiting them; armies stand on the map, march, fight, occupy, and wars end in treaties.
        /// </summary>
        Writs,
    }

    /// <summary>How a game is won: rules v2, section 6.2.</summary>
    public enum VictoryMode
    {
        /// <summary>
        /// Today's rule: holding <see cref="GameRules.ProvincesToWin"/> provinces wins at once, and
        /// after <see cref="GameRules.MaxTurns"/> the most provinces win.
        /// </summary>
        Domination,
        /// <summary>The last country with a province wins. No turn limit; a headless run stops at its cap, unfinished.</summary>
        Conquest,
        /// <summary>The most provinces after <see cref="GameRules.MaxTurns"/> turns win; holding many ends nothing early.</summary>
        TurnLimit,
    }

    /// <summary>
    /// Every number the rules use, in one place, so the game can be tuned without touching the
    /// rules themselves. <see cref="Standard"/> is the game as designed in
    /// <c>docs/demos/grey-box-strategy.md</c>; a test may pass a changed copy.
    /// </summary>
    public sealed class GameRules
    {
        // --- The turn ---------------------------------------------------------------------
        /// <summary>One move a turn (today) or the writs turn of rules v2. Everything below "Writs" is read only by the latter.</summary>
        public TurnStyle Turn = TurnStyle.OneMove;

        // --- Length and victory ----------------------------------------------------------
        /// <summary>How the game is won (rules v2, section 6). Today's game is <see cref="VictoryMode.Domination"/>.</summary>
        public VictoryMode Victory = VictoryMode.Domination;
        /// <summary>
        /// The game ends after this turn if nobody has won before it. In <see cref="VictoryMode.Conquest"/>
        /// it is only the cap a headless run stops at, and the game is recorded as unfinished.
        /// </summary>
        public int MaxTurns = 30;
        /// <summary>Holding this many provinces at the end of a turn wins at once (<see cref="VictoryMode.Domination"/> only).</summary>
        public int ProvincesToWin = 15;

        // --- Economy ---------------------------------------------------------------------
        public int IncomePerProvince = 10;
        /// <summary>Upkeep is one gold for every this many soldiers, rounded down.</summary>
        public int SoldiersPerUpkeepGold = 2;
        /// <summary>A country whose gold falls below zero loses this share of its army.</summary>
        public double DesertionShare = 0.15;

        // --- Moves -----------------------------------------------------------------------
        /// <summary>A levy raises this many soldiers, plus <see cref="LevyPerProvince"/> for each province held.</summary>
        public int LevyBase = 5;
        public int LevyPerProvince = 5;
        /// <summary>Each soldier of a levy costs this much; a levy shrinks to what the treasury can pay.</summary>
        public int GoldPerSoldier = 2;
        /// <summary>The smallest levy worth raising: recruiting needs gold for at least this many.</summary>
        public int MinLevy = 5;
        public int FortCost = 30;
        public int MaxFortsPerProvince = 2;
        public int TaxGold = 30;
        /// <summary>Raising taxes needs at least this stability; it then costs one.</summary>
        public int MinStabilityToTax = 2;
        public int MinArmyToAttack = 10;
        public int MinArmyToDeclareWar = 20;
        public int TruceTurns = 5;

        // --- Stability -------------------------------------------------------------------
        public int MaxStability = 5;
        public int StartStability = 3;
        /// <summary>At stability 1, the chance each turn that a province breaks away.</summary>
        public double RevoltChance = 1.0 / 3.0;
        public int RevoltGarrison = 10;
        /// <summary>Stability after a revolt has spent itself.</summary>
        public int StabilityAfterRevolt = 3;

        // --- Battle ----------------------------------------------------------------------
        public double RollMin = 0.8;
        public double RollMax = 1.2;
        /// <summary>The share of the owner's whole army that defends one of its provinces.</summary>
        public double DefenceShare = 0.7;
        public int FortBonus = 15;
        public int CapitalBonus = 8;
        /// <summary>Each side loses this share of the other side's strength.</summary>
        public double CasualtyRate = 0.3;
        /// <summary>A defender that holds loses only this much of what it would have.</summary>
        public double HeldCasualtyFactor = 0.6;
        public int NeutralGarrisonMin = 8;
        public int NeutralGarrisonMax = 16;

        // --- Events ----------------------------------------------------------------------
        public double GoodHarvestChance = 0.07;
        public int GoodHarvestGold = 25;
        public double PoorHarvestChance = 0.07;
        public double BanditsChance = 0.05;
        public int BanditsSoldiers = 5;
        public double UnrestChance = 0.04;

        // --- The storyteller's events (the README's "Storyteller"; used only when Game.EventTable is off) ---
        /// <summary>No storyteller event before this turn, so the first expansion is the rulers' own.</summary>
        public int EventFirstTurn = 3;
        /// <summary>A country whose events over the last <see cref="EventBudgetWindow"/> turns are worth this much gold or more is offered no gift; this much or less, no blow.</summary>
        public int EventBudget = 60;
        // Five, as measured on 12 November: until Codex's review the window's lower bound was
        // exclusive, so the "6" the runs used counted five turns. The bound is inclusive now.
        public int EventBudgetWindow = 5;
        /// <summary>A template is not offered again within this many of the storyteller's events.</summary>
        public int EventTemplateCooldown = 4;
        /// <summary>
        /// At most this many blows (or gifts) in a row from the storyteller; then only the other kind
        /// is offered. Without it the smoke run's 4B cycled plague, bad harvest, unrest, volunteers.
        /// </summary>
        public int EventMaxSameKindInARow = 2;
        /// <summary>No gift for a country holding this many provinces or more: no event crowns the leader.</summary>
        public int EventNoBonusFromProvinces = 12;
        /// <summary>No blow for a country holding fewer provinces than this: no event kicks the weak out.</summary>
        public int EventMinProvincesForPenalty = 3;
        public int EventWindfallMin = 20;
        public int EventWindfallMax = 60;
        public double EventPlagueShare = 0.15;
        /// <summary>
        /// Whether the storyteller chooses how hard an event with an amount strikes (DR-023's A/B,
        /// question 5): <see cref="EventStrengthForm.None"/>, today's game, by default; with
        /// <see cref="EventStrengthForm.Bundled"/> the strength is in the action's id
        /// (<c>windfall_major</c>), with <see cref="EventStrengthForm.Parameter"/> it is a parameter
        /// of the action (<c>windfall</c>, <c>strength: major</c>). The same rule and amounts either way
        /// (<see cref="EventRules.WhyNotStrength"/>, <see cref="EventRules.StrengthFactor"/>).
        /// </summary>
        public EventStrengthForm EventStrength = EventStrengthForm.None;
        /// <summary>A sickness needs an army of this size, so it never takes the last soldiers.</summary>
        public int EventMinArmyForPlague = 20;
        public int EventVolunteersMax = 30;

        // --- Writs (rules v2, R1; every number *to tune*) ------------------------------------
        /// <summary>Writs a turn: this many, plus one per <see cref="ProvincesPerWrit"/> provinces owned, at most <see cref="MaxWrits"/>.</summary>
        // Tuned for pace on 12 November (R1b): 3 writs, not the page's 2, so a realm moves more of its armies.
        public int BaseWrits = 3;
        public int ProvincesPerWrit = 8;
        public int MaxWrits = 5;
        /// <summary>Writs for <see cref="WritsOverride"/> that set no cap: every army awaiting orders is asked, and the second court order.</summary>
        public const int UnlimitedWrits = int.MaxValue;
        /// <summary>
        /// A country's writs a turn in place of the formula, by country id: a number, or <see cref="UnlimitedWrits"/>
        /// (rules v2 1.6: the screen's "unlimited writs" for the person alone, an easy setting). Empty by default.
        /// </summary>
        public Dictionary<string, int> WritsOverride = new Dictionary<string, int>();

        // --- Free orders (the turn rework, slice 1, 17 November) ---------------------------------
        /// <summary>
        /// The writs turn without writs: a realm gives court orders one at a time until it answers none (at most
        /// <see cref="MaxCourtOrders"/>), each on the council's snapshot with the gold its earlier orders spend held back,
        /// then an order for each army with no standing order (not marching, not besieging) or woken. Human and model
        /// realms play the same rules. Off: the writs turn exactly as before.
        /// </summary>
        public bool FreeOrders;
        /// <summary>
        /// With <see cref="FreeOrders"/>: court orders a realm may give a turn. A pacing setting for the model seats, which
        /// bounds their calls a turn: the person's court follows gold alone (<see cref="CourtOrdersOverride"/>).
        /// </summary>
        public int MaxCourtOrders = 3;
        /// <summary>
        /// With <see cref="FreeOrders"/>: a holding army keeps its order, as a marching or besieging one does, and is asked
        /// again when a wake rule fires or after standing this many turns unasked. A pacing setting: each ask is a call.
        /// </summary>
        public int AskHoldingArmyEvery = 3;

        // --- Economy (the turn rework, slice 2, E1; every number a starting sketch) ------------------------------
        /// <summary>
        /// With <see cref="FreeOrders"/> on the writs turn: manpower limits recruiting, provinces yield by terrain, and taxes
        /// are a standing level (low, normal, high) set by a court order in place of the one-off <c>raise_taxes</c>.
        /// Off: the game as before. <see cref="EconomyOn"/> says whether it applies.
        /// </summary>
        public bool Economy;
        /// <summary>Whether <see cref="Economy"/> applies: it needs <see cref="FreeOrders"/> and the writs turn.</summary>
        public bool EconomyOn => Economy && FreeOrders && Turn == TurnStyle.Writs;
        /// <summary>Economy: manpower a realm gains each upkeep for every province it holds, and its cap per province held.</summary>
        public int ManpowerPerProvince = 2;
        public int ManpowerCapPerProvince = 10;
        /// <summary>Economy: gold a held province yields on top of <see cref="IncomePerProvince"/>: plains, hills and mountains, a coast.</summary>
        public int PlainsYield = 2;
        public int MineYield = 3;
        public int CoastYield = 3;
        /// <summary>Economy: the income factor of low and high taxes (normal is 1).</summary>
        public double LowTaxIncome = 0.8;
        public double HighTaxIncome = 1.3;
        /// <summary>With <see cref="FreeOrders"/>: a realm's court orders a turn in place of <see cref="MaxCourtOrders"/> (the person's, no limit).</summary>
        public Dictionary<string, int> CourtOrdersOverride = new Dictionary<string, int>();
        /// <summary>With <see cref="FreeOrders"/>: the court orders this country may give a turn.</summary>
        public int CourtOrdersFor(string countryId) =>
            countryId != null && CourtOrdersOverride != null && CourtOrdersOverride.TryGetValue(countryId, out int n) ? System.Math.Max(1, n) : MaxCourtOrders;
        /// <summary>An army asks for orders again after this many turns on one order (wake condition 8), and a hold lasts this long.</summary>
        public int WakeAfterTurns = 4;
        /// <summary>A march may be ordered to a province at most this many steps away.</summary>
        public int MarchRange = 4;
        /// <summary>Steps a march may take in one turn over open ground (plains, forest) where it meets nothing to fight.</summary>
        public int MarchStepsPerTurn = 2;
        /// <summary>At most this many destinations are offered an army, the nearest first (v2: about a dozen targets a decision).</summary>
        public int MarchTargets = 12;
        /// <summary>Soldiers one army may hold; a levy beyond it forms another army.</summary>
        public int ArmyMax = 100;
        /// <summary>A province with no army defends with this militia, plus <see cref="MilitiaPerFort"/> a fort.</summary>
        public int MilitiaBase = 3;
        public int MilitiaPerFort = 6;
        /// <summary>A loser beaten by this many times its strength, or with nowhere to go, is routed: destroyed.</summary>
        public double RoutRatio = 2.5;
        /// <summary>At this war score a demand lands without being asked (v2 5.1).</summary>
        public int EnforcedScore = 80;
        /// <summary>War score from battles: this much a battle won or lost, at most <see cref="BattleScoreMax"/> either way.</summary>
        public int BattleScore = 4;
        public int BattleScoreMax = 20;
        public int CapitalScore = 10;
        /// <summary>A ruler answers at most this many treaty proposals a turn.</summary>
        public int MaxTreatyAnswers = 2;
        /// <summary>
        /// A game where no province changes owner or controller for this many turns ends as a
        /// stalemate (v2 6.1 measures it; ending it is ours, so a headless run stops). 0: never.
        /// </summary>
        public int StalemateTurns = 0;
        /// <summary>
        /// A realm with no army whose capital is occupied breaks too, not only one with every province
        /// occupied (v2 5.1's rule); its land not occupied goes to its capital's occupier. Ours, for pace.
        /// </summary>
        public bool BreakWhenCapitalFalls = false;
        /// <summary>
        /// The writs turn's pace, candidate 1 of the rules v2 page's "Why Classic stalls": a province occupied
        /// this many turns in a row passes to its occupier, with no treaty. 0, the default: never; occupation
        /// changes no owner, as v2 5.1 has it.
        /// </summary>
        public int OccupationBecomesOwnership = 0;
        /// <summary>
        /// The writs turn's pace, candidate 3: a realm whose every province is occupied breaks even with armies
        /// left elsewhere, which are disbanded. Off by default: such a realm breaks only once it has no army.
        /// </summary>
        public bool BreakWhenAllOccupied = false;

        /// <summary>
        /// Pacing candidate (the tech lead's pick after the live look of 14 November): the stalemate counter reads
        /// war score and land held by treaty, not occupation. Land taken or freed by force of arms (occupation, a
        /// siege, holding it <see cref="OccupationBecomesOwnership"/> turns) no longer restarts it; a treaty, free
        /// land taken, a realm broken, a new war, or a war's score moving <see cref="StalemateWarScoreStep"/> points
        /// does. Off by default.
        /// </summary>
        public bool StalemateByWarScore = false;
        public int StalemateWarScoreStep = 10;

        // --- R3, armies in depth: unit types and the counter rule (rules v2 3.3) ---

        /// <summary>
        /// Pikes, riders and bows with the counter rule, recruited by <c>recruit_pikes</c>, <c>recruit_riders</c> and
        /// <c>recruit_bows</c> in place of <c>recruit</c>. Off, every soldier is a pike and battles are fought as
        /// before. The writs turn only. On by default since 15 November (the lead's call); <c>-strategyRules "UnitTypes=false"</c> turns it off for an arm.
        /// </summary>
        public bool UnitTypes = true;

        /// <summary>
        /// With <see cref="UnitTypes"/> on, the counter rule in the rulers' own facts: the court reads one line saying
        /// what beats what and what each rival it names is mostly, and a march order names the largest type among the
        /// enemy armies it can see there. The recruit orders' descriptions say the same, but a model served by Ollama
        /// never reads the schema. On by default (15 November), so it is there whenever unit types are; it does
        /// nothing without them. Twelve pairs on qwen3.5:4b: the courts counter the type they see 67% of the time
        /// against 20% without it, at the cost of personas that no longer differ in which types they buy
        /// (findings, 15 November). <c>-strategyRules "CountersLine=false"</c> runs the old prompt as an arm.
        /// </summary>
        public bool CountersLine = true;

        /// <summary>
        /// R3, rules v2 3.5: every army has a general with a trait, which counts in its battles and marches.
        /// Seven of the page's ten traits; siegewright, forager and marshal wait for sieges, supply and army sizes.
        /// On by default since 15 November (the lead's call); <c>-strategyRules "Generals=false"</c> turns it off for an arm.
        /// </summary>
        public bool Generals = true;
        /// <summary>A trait's strength or casualty effect, as a fraction (bold, wary +10%; horse-born, bowmaster +15% of that type; inspiring -20% casualties).</summary>
        public double BoldStrength = 0.10, BoldCasualties = 0.10, WaryStrength = 0.10, TypeTraitStrength = 0.15, InspiringCasualties = 0.20;
        /// <summary>Battles a general must win to gain a second trait.</summary>
        public int WinsForSecondTrait = 3;
        /// <summary>The chance a general falls in any lost battle (routed, always); the army then gets a new one.</summary>
        public double GeneralFallsChance = 0.05;

        /// <summary>
        /// R3, rules v2 3.7: an enemy province with forts (walls) is besieged over several turns instead of
        /// stormed: no militia battle, the province occupied when the siege ends, its walls one level lower.
        /// The fort that holds the road and income lost under siege are their own settings, off.
        /// On by default since 15 November (the lead's call); <c>-strategyRules "Sieges=false"</c> turns it off for an arm.
        /// </summary>
        public bool Sieges = true;
        /// <summary>Turns a siege takes per fort (walls level).</summary>
        public int SiegeTurnsPerFort = 2;
        /// <summary>The garrison a fort holds, for the three-to-one rule: besiegers that many times the garrison take a turn off.</summary>
        public int GarrisonPerFort = 10;
        public int SiegeOutnumber = 3;
        /// <summary>Each turn of a siege, the seeded chance that the walls are breached early.</summary>
        public double SiegeBreachChance = 0.10;

        /// <summary>
        /// R3, rules v2 3.7, a fort holds the road: a march goes no further than a province bordering an enemy's
        /// walled province that no one besieges, and an army standing in one may step only into that walled province
        /// (to besiege or attack it) or into land we hold. Our own land is never held up. Off by default.
        /// </summary>
        public bool FortsHoldTheRoad = false;

        /// <summary>R3, rules v2 3.7: a province under siege gives its owner no income. Off by default.</summary>
        public bool SiegeStopsIncome = false;

        /// <summary>
        /// R3, rules v2 3.6, model B: every army carries stores, full and refilled in our own land, eaten
        /// elsewhere, faster in poor land; an army with none loses soldiers and fights weaker.
        /// On by default since 15 November (the lead's call); <c>-strategyRules "Supply=false"</c> turns it off for an arm.
        /// </summary>
        public bool Supply = true;
        /// <summary>The most stores an army carries, in turns of food.</summary>
        public int MaxStores = 4;
        /// <summary>Stores refilled a turn in our own land.</summary>
        public int StoresRefill = 2;
        /// <summary>Stores eaten a turn elsewhere: plains and forest, then hills, marsh and mountains.</summary>
        public int StoresRich = 1, StoresPoor = 2;
        /// <summary>An army with no stores loses this share of its soldiers a turn, at least <see cref="HungerMinLoss"/>, and fights this much weaker.</summary>
        public double HungerLoss = 0.10, HungerStrength = 0.15;
        public int HungerMinLoss = 2;

        /// <summary>
        /// With <see cref="Supply"/> on, hunger first (the tech lead's call, 15 November): a starving army, then one whose
        /// stores run out next turn, is asked before the others within the writs, and its own facts say what hunger costs
        /// and the nearest land that would feed it. Six pairs on qwen3.5:4b: soldiers lost to hunger 580 against 125, with
        /// fewer battles and sieges (findings, 15 November). It does nothing without supply.
        /// On by default since 15 November (the lead's call); <c>-strategyRules "HungerFirst=false"</c> turns it off for an arm.
        /// </summary>
        public bool HungerFirst = true;

        /// <summary>
        /// R3, rules v2 3.8: the rulers read rival armies only where they see (<see cref="ArmySight"/>): position, general
        /// and size rounded to 10 near their land and their armies, the mix only when scouted by riders; each
        /// rival's total, rounded, always. Scripted players still read everything.
        /// On by default since 15 November (the lead's call); <c>-strategyRules "Fog=false"</c> turns it off for an arm.
        /// </summary>
        public bool Fog = true;
        /// <summary>
        /// R3, land traditions (the lead's answer to the counters line's cost, 15 November: the land, not the persona,
        /// should make realms differ). With <see cref="UnitTypes"/> on, each type's soldiers fight stronger for the share
        /// of the realm's held land that leans to it: hills and mountains to pikes, plains to riders, forest to bows, marsh
        /// to none; the specials <c>iron_deep</c>, <c>horse_plains</c> and <c>great_wood</c> a step each. Battle strength
        /// only, capped so the counter rule still decides between equal realms; the court reads it in one line
        /// (<see cref="LandTraditions"/>). Off by default until an A/B run with the models.
        /// </summary>
        public bool LandTraditions = false;
        /// <summary>The strength one step gives, each full <see cref="LandShareStep"/> of held land, and the cap.</summary>
        public double LandStep = 0.05, LandShareStep = 0.20, LandCap = 0.15;

        /// <summary>The most rival armies a ruler's "Rival armies we see" line names.</summary>
        public int MaxSightings = 5;
        public double PikeStrength = 1.0;
        public double RiderStrength = 1.4;
        public double BowStrength = 0.9;
        /// <summary>The counter rule's c: pikes facing all riders fight at 1 + c, riders facing all pikes at 1 - c.</summary>
        public double CounterStrength = 0.5;
        /// <summary>Riders cost this many times a pike to recruit and to keep.</summary>
        public int RiderCostFactor = 2;

        /// <summary>Turns to enter a province of this terrain (v2 3.2): 1 on plains and forest, 2 elsewhere.</summary>
        public int TurnsToEnter(Terrain t) => t == Terrain.Plains || t == Terrain.Forest ? 1 : 2;

        /// <summary>The rules as designed.</summary>
        public static GameRules Standard => new GameRules();

        /// <summary>A copy, for a test that changes one number.</summary>
        public GameRules Clone()
        {
            var copy = (GameRules)MemberwiseClone();
            copy.WritsOverride = new Dictionary<string, int>(WritsOverride);
            copy.CourtOrdersOverride = new Dictionary<string, int>(CourtOrdersOverride);
            return copy;
        }
    }
}
