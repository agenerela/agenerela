namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// R3's settings from New game, unit types, generals, sieges and supply, into the rules of every way of playing on the writs
    /// turn: the replay, model rulers live, and rule mode, on Classic or a generated world. Every place that makes a
    /// writs game's rules does it through <see cref="RulesFor"/>.
    /// </summary>
    public sealed partial class ReplayDirector
    {
        // Static: a generated world reloads the scene before its game is made.
        private static bool nextUnitTypes, nextGenerals, nextSieges, nextSupply;

        /// <summary>For the menu, the Unity CLI and the tests: the settings the next game's rules get.</summary>
        public static bool NextUnitTypes { get => nextUnitTypes; set => nextUnitTypes = value; }
        public static bool NextGenerals { get => nextGenerals; set => nextGenerals = value; }
        public static bool NextSieges { get => nextSieges; set => nextSieges = value; }
        public static bool NextSupply { get => nextSupply; set => nextSupply = value; }

        /// <summary>A setup's rules with New game's R3 settings: unchanged for the one-move game, or with all of them off.</summary>
        public static GameRules RulesFor(GameSetup setup, MapDefinition map) =>
            WithArmySettings(setup.BuildRules(map), nextUnitTypes, nextGenerals, nextSieges, nextSupply);

        public static GameRules WithArmySettings(GameRules rules, bool unitTypes, bool generals, bool sieges = false, bool supply = false)
        {
            if (rules == null || rules.Turn != TurnStyle.Writs || (!unitTypes && !generals && !sieges && !supply)) return rules;
            var copy = rules.Clone();
            copy.UnitTypes |= unitTypes;
            copy.Generals |= generals;
            copy.Sieges |= sieges;
            copy.Supply |= supply;
            return copy;
        }
    }
}
