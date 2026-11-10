namespace GreyBoxStrategy
{
    /// <summary>
    /// Every number the rules use, in one place, so the game can be tuned without touching the
    /// rules themselves. <see cref="Standard"/> is the game as designed in
    /// <c>docs/demos/grey-box-strategy.md</c>; a test may pass a changed copy.
    /// </summary>
    public sealed class GameRules
    {
        // --- Length and victory ----------------------------------------------------------
        /// <summary>The game ends after this turn if nobody has won before it.</summary>
        public int MaxTurns = 30;
        /// <summary>Holding this many provinces at the end of a turn wins at once.</summary>
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

        /// <summary>The rules as designed.</summary>
        public static GameRules Standard => new GameRules();

        /// <summary>A copy, for a test that changes one number.</summary>
        public GameRules Clone() => (GameRules)MemberwiseClone();
    }
}
