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
        /// <summary>A sickness needs an army of this size, so it never takes the last soldiers.</summary>
        public int EventMinArmyForPlague = 20;
        public int EventVolunteersMax = 30;

        // --- Writs (rules v2, R1; every number *to tune*) ------------------------------------
        /// <summary>Writs a turn: this many, plus one per <see cref="ProvincesPerWrit"/> provinces owned, at most <see cref="MaxWrits"/>.</summary>
        // Tuned for pace on 12 November (R1b): 3 writs, not the page's 2, so a realm moves more of its armies.
        public int BaseWrits = 3;
        public int ProvincesPerWrit = 8;
        public int MaxWrits = 5;
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

        /// <summary>Turns to enter a province of this terrain (v2 3.2): 1 on plains and forest, 2 elsewhere.</summary>
        public int TurnsToEnter(Terrain t) => t == Terrain.Plains || t == Terrain.Forest ? 1 : 2;

        /// <summary>The rules as designed.</summary>
        public static GameRules Standard => new GameRules();

        /// <summary>A copy, for a test that changes one number.</summary>
        public GameRules Clone() => (GameRules)MemberwiseClone();
    }
}
