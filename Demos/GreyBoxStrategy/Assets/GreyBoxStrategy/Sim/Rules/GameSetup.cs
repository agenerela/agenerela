using System;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>
    /// Everything that decides a new game before its first turn, as one plain data object the menu,
    /// the headless runners and the tests share (rules v2, slice R0, and section 7): the seed, the
    /// map, how many countries, how the game is won, and the storyteller and advisor switches.
    /// Saved as JSON (<see cref="ToJson"/>, <see cref="FromJson"/>), so it can go into a recording and
    /// rebuild the same game.
    /// </summary>
    /// <remarks>
    /// <para>Named <c>GameSetup</c> rather than the page's <c>GameSettings</c>: the screen already has a
    /// <c>GameSettings</c> for the display (window, UI scale, volume), and the two are different
    /// things, one kept between runs and one chosen per game.</para>
    /// <para>The defaults are today's game exactly: the Classic board, domination at 15 of 25
    /// provinces with a 30-turn limit, the random event table. The page makes conquest the default;
    /// that waits for R1, because conquest on today's one-move-a-turn rules is the same game without
    /// its end. Conquest is here as an option.</para>
    /// <para>The storyteller preset and the advisor switch are data only in R0: nothing reads them yet.</para>
    /// </remarks>
    public sealed class GameSetup
    {
        public const int CurrentVersion = 1;

        /// <summary>Today's 25-province board (<see cref="ClassicMap"/>).</summary>
        public const string ClassicMapId = "classic";

        /// <summary>A world made by the generator from the seed and <see cref="Generator"/> (rules v2, R2).</summary>
        public const string GeneratedMapId = "generated";

        /// <summary>The maps a setup may name.</summary>
        public static readonly string[] Maps = { ClassicMapId, GeneratedMapId };

        /// <summary>The storyteller presets of rules v2 section 7; <c>off</c> is today's random event table.</summary>
        public static readonly string[] StorytellerPresets = { "off", "chronicler", "kingmaker", "saga", "chaos" };

        public const int MinCountries = 3;
        public const int MaxCountries = 10;

        public int Version = CurrentVersion;
        /// <summary>
        /// One move a turn (today's game, the default, so every pinned test and recording holds) or rules
        /// v2's writs turn with armies on the map (<see cref="Writs"/> makes that setup).
        /// </summary>
        public TurnStyle Turn = TurnStyle.OneMove;
        /// <summary>The game's seed: every die and, for a generated map, the world.</summary>
        public int Seed = 1;
        /// <summary>One of <see cref="Maps"/>.</summary>
        public string Map = ClassicMapId;
        /// <summary>How many countries play. The Classic board has exactly five.</summary>
        public int Countries = 5;
        /// <summary>The generator's settings, for a <see cref="GeneratedMapId"/> map; null takes the defaults.</summary>
        public MapSettings Generator;
        /// <summary>
        /// The seed a generated world is made from, when it should differ from <see cref="Seed"/>: one
        /// world shared by many games, as a balance run plays it. Null uses <see cref="Seed"/>.
        /// </summary>
        public int? MapSeed;
        public VictoryMode Victory = VictoryMode.Domination;
        /// <summary>Domination: the share of all provinces that wins, in percent (60 is today's 15 of 25).</summary>
        public int DominationPercent = 60;
        /// <summary>Domination and turn limit: the last turn.</summary>
        public int TurnLimit = 30;
        /// <summary>Conquest: the turn a headless run stops at, recording the game as unfinished (v2 section 6.1).</summary>
        public int TurnCap = 150;
        /// <summary>One of <see cref="StorytellerPresets"/>. Data only until the storyteller reads it.</summary>
        public string Storyteller = "off";
        /// <summary>The advisor (v2 slice A). Data only until it exists.</summary>
        public bool Advisor = true;

        /// <summary>Today's game: the defaults, with a seed.</summary>
        public static GameSetup Classic(int seed = 1) => new GameSetup { Seed = seed };

        /// <summary>
        /// Rules v2's game as R1 builds it: the writs turn, armies on the map, conquest with no turn
        /// limit (a headless run stops at the cap or a 20-turn stalemate).
        /// </summary>
        public static GameSetup Writs(int seed = 1, string map = ClassicMapId) =>
            new GameSetup { Seed = seed, Map = map, Turn = TurnStyle.Writs, Victory = VictoryMode.Conquest, TurnCap = 150 };

        public GameSetup Clone() => (GameSetup)MemberwiseClone();

        /// <summary>Why this setup cannot start a game, in words a menu can show, or null.</summary>
        public string WhyInvalid()
        {
            if (Version != CurrentVersion) return $"setup version {Version} is not {CurrentVersion}";
            if (string.IsNullOrEmpty(Map) || !Maps.Contains(Map, StringComparer.Ordinal))
                return $"unknown map '{Map}'; expected one of {string.Join(", ", Maps)}";
            if (Countries < MinCountries || Countries > MaxCountries)
                return $"{Countries} countries is outside {MinCountries} to {MaxCountries}";
            if (Map == ClassicMapId && Countries != 5)
                return $"the Classic board has five countries, not {Countries}";
            if (Map == ClassicMapId && (Generator != null || MapSeed != null))
                return "map settings apply only to a generated map";
            if (Map == GeneratedMapId)
            {
                string why = (Generator ?? new MapSettings()).WhyInvalid(Countries);
                if (why != null) return why;
            }
            if (!Enum.IsDefined(typeof(VictoryMode), Victory)) return $"unknown victory mode {(int)Victory}";
            if (!Enum.IsDefined(typeof(TurnStyle), Turn)) return $"unknown turn style {(int)Turn}";
            if (Victory == VictoryMode.Domination && (DominationPercent < 51 || DominationPercent > 100))
                return $"domination at {DominationPercent}% is outside 51% to 100%";
            if (Victory != VictoryMode.Conquest && (TurnLimit < 1 || TurnLimit > 1000))
                return $"a turn limit of {TurnLimit} is outside 1 to 1000";
            if (Victory == VictoryMode.Conquest && (TurnCap < 1 || TurnCap > 10000))
                return $"a turn cap of {TurnCap} is outside 1 to 10000";
            if (string.IsNullOrEmpty(Storyteller) || !StorytellerPresets.Contains(Storyteller, StringComparer.Ordinal))
                return $"unknown storyteller preset '{Storyteller}'; expected one of {string.Join(", ", StorytellerPresets)}";
            return null;
        }

        /// <summary>Throws <see cref="ArgumentException"/> with <see cref="WhyInvalid"/>'s message.</summary>
        public void Validate()
        {
            string why = WhyInvalid();
            if (why != null) throw new ArgumentException($"These settings cannot start a game: {why}.");
        }

        /// <summary>The map this setup plays on. A generated one is made from <see cref="Seed"/>.</summary>
        /// <exception cref="InvalidOperationException">The generator made no playable world.</exception>
        public MapDefinition BuildMap() => BuildWorld().ToDefinition();

        /// <summary>The map as data: Classic, or the generated world with its geometry.</summary>
        public WorldMap BuildWorld()
        {
            Validate();
            if (Map != GeneratedMapId) return ClassicMap.Load();
            int mapSeed = MapSeed ?? Seed;
            var made = MapGenerator.Generate(mapSeed, Countries, Generator ?? new MapSettings());
            if (!made.Playable)
                throw new InvalidOperationException(
                    $"No playable world from seed {mapSeed} after {made.Attempts} tries: {string.Join("; ", made.Problems.Take(3))}.");
            return made.Map;
        }

        /// <summary>
        /// The rules for this setup on <paramref name="map"/>: today's numbers, with the victory mode
        /// and its values. Domination's share becomes a province count, rounded up.
        /// </summary>
        public GameRules BuildRules(MapDefinition map, GameRules numbers = null)
        {
            Validate();
            if (map == null) throw new ArgumentNullException(nameof(map));
            var rules = (numbers ?? GameRules.Standard).Clone();
            rules.Victory = Victory;
            rules.Turn = Turn;
            if (Turn == TurnStyle.Writs) rules.StalemateTurns = 20;
            rules.MaxTurns = Victory == VictoryMode.Conquest ? TurnCap : TurnLimit;
            if (Victory == VictoryMode.Domination)
                rules.ProvincesToWin = (map.Provinces.Count * DominationPercent + 99) / 100;
            return rules;
        }

        /// <summary>A new game from this setup.</summary>
        public Game NewGame(GameRules numbers = null)
        {
            var map = BuildMap();
            return new Game(Seed, map, BuildRules(map, numbers));
        }

        public string ToJson(bool indented = true) => MapJson.Write(this, indented);

        /// <summary>
        /// Reads a saved setup. Unknown names, bad values and invalid settings are refused with a
        /// <see cref="FormatException"/> that says what was wrong. Names left out keep their defaults.
        /// </summary>
        public static GameSetup FromJson(string json)
        {
            var setup = MapJson.Read<GameSetup>(json, "settings");
            string why = setup.WhyInvalid();
            if (why != null) throw new FormatException($"These settings cannot start a game: {why}.");
            return setup;
        }
    }
}
