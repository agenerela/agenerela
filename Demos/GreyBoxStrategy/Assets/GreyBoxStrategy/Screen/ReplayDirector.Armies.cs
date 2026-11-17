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
        // On by default, as GameRules has them since 15 November; New game sets each.
        private static bool nextUnitTypes = true, nextGenerals = true, nextSieges = true, nextSupply = true;

        /// <summary>For the menu, the Unity CLI and the tests: the settings the next game's rules get.</summary>
        public static bool NextUnitTypes { get => nextUnitTypes; set => nextUnitTypes = value; }
        public static bool NextGenerals { get => nextGenerals; set => nextGenerals = value; }
        public static bool NextSieges { get => nextSieges; set => nextSieges = value; }
        public static bool NextSupply { get => nextSupply; set => nextSupply = value; }
        // The new turn in testing, for rule games from New game (and the CLI); off by default.
        private static bool nextFreeOrders;
        public static bool NextFreeOrders { get => nextFreeOrders; set => nextFreeOrders = value; }

        /// <summary>A setup's rules with New game's R3 settings: unchanged for the one-move game.</summary>
        public static GameRules RulesFor(GameSetup setup, MapDefinition map)
        {
            var rules = WithArmySettings(setup.BuildRules(map), nextUnitTypes, nextGenerals, nextSieges, nextSupply);
            if (nextFreeOrders && rules != null && rules.Turn == TurnStyle.Writs) rules.FreeOrders = true;
            return rules;
        }

        /// <summary>
        /// The writs turn's rules with each of the four set as given, on or off: since they are on by default, New game's
        /// Off must be able to turn one off, not only add one. The one-move game's rules come back unchanged.
        /// </summary>
        public static GameRules WithArmySettings(GameRules rules, bool unitTypes, bool generals, bool sieges, bool supply)
        {
            if (rules == null || rules.Turn != TurnStyle.Writs) return rules;
            var copy = rules.Clone();
            copy.UnitTypes = unitTypes;
            copy.Generals = generals;
            copy.Sieges = sieges;
            copy.Supply = supply;
            return copy;
        }
    }
}
