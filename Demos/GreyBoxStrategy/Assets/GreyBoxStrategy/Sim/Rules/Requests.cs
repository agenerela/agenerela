using System.Collections.Generic;

namespace GreyBoxStrategy
{
    /// <summary>The two moments the game asks a country to decide.</summary>
    public enum Moment
    {
        /// <summary>Once per living country per turn: its one move.</summary>
        Council,
        /// <summary>When another country offers it peace: accept or refuse.</summary>
        Envoy,
    }

    /// <summary>
    /// Everything a player is given for one decision. A scripted player reads <see cref="World"/>;
    /// a model, in stage 2, reads only the text: <see cref="StandingFacts"/> through an observation
    /// source and <see cref="TurnFacts"/> with the call. Both are offered exactly <see cref="Legal"/>.
    /// </summary>
    public sealed class CountryRequest
    {
        public CountryRequest(Moment moment, int turn, string countryId, World world,
            IReadOnlyList<string> standingFacts, IReadOnlyList<string> turnFacts, IReadOnlyList<Move> legal,
            string offerId = null, string offeredBy = null)
        {
            Moment = moment;
            Turn = turn;
            CountryId = countryId;
            World = world;
            StandingFacts = standingFacts;
            TurnFacts = turnFacts;
            Legal = legal;
            OfferId = offerId;
            OfferedBy = offeredBy;
        }

        public Moment Moment { get; }
        public int Turn { get; }
        public string CountryId { get; }
        /// <summary>The state as it was when every country decided: nothing resolves until all have.</summary>
        public World World { get; }
        /// <summary>What holds on every call: treasury, army, relations. At most eight lines.</summary>
        public IReadOnlyList<string> StandingFacts { get; }
        /// <summary>The one to three facts about this moment: what just happened to this country.</summary>
        public IReadOnlyList<string> TurnFacts { get; }
        public IReadOnlyList<Move> Legal { get; }
        /// <summary>For an envoy call: the offer being answered, which stage 2 sends as the correlation.</summary>
        public string OfferId { get; }
        /// <summary>For an envoy call: the country offering peace.</summary>
        public string OfferedBy { get; }
    }

    /// <summary>A player's answer, paired with the request it answers.</summary>
    public sealed class CountryAnswer
    {
        public CountryAnswer(CountryRequest request, Move choice, string note = null)
        {
            Request = request;
            Choice = choice;
            Note = note;
        }

        public CountryRequest Request { get; }
        public Move Choice { get; }
        /// <summary>
        /// Anything the player wants in the log beside its choice, copied to
        /// <see cref="MoveOutcome.Note"/>: a model seat's statement, latency and guard verdicts.
        /// </summary>
        public string Note { get; }
    }

    /// <summary>
    /// A last word on a move at the moment it lands, after the game's own re-check has passed: a
    /// seat driven by a framework agent answers through <c>Agent.Execute</c>, whose re-check is the
    /// framework's. Null admits the move; anything else refuses it, with that reason.
    /// </summary>
    /// <remarks>
    /// <see cref="Game"/> asks the gate of the country whose move it is, for every move it is about
    /// to carry out, holds and envoy answers included, and never for one it has already refused.
    /// </remarks>
    public interface IMoveGate
    {
        string Admit(CountryRequest request, Move move);
    }

    /// <summary>What happened to one decision once the turn resolved.</summary>
    public sealed class MoveOutcome
    {
        public MoveOutcome(CountryRequest request, Move choice, Move applied, bool refused, string text)
        {
            Request = request;
            Choice = choice;
            Applied = applied;
            Refused = refused;
            Text = text;
        }

        public CountryRequest Request { get; }
        /// <summary>What the player chose.</summary>
        public Move Choice { get; }
        /// <summary>What the game carried out: the choice, or hold when it was illegal or refused.</summary>
        public Move Applied { get; }
        /// <summary>
        /// True when the choice was legal on the snapshot but an earlier move in the same turn made
        /// it stale, so the re-check refused it. Also true for a choice that was never legal.
        /// </summary>
        public bool Refused { get; }
        /// <summary>One line for the log: "took fenmark from orsk (34 against 21)".</summary>
        public string Text { get; }
        /// <summary>The player's <see cref="CountryAnswer.Note"/>, or null.</summary>
        public string Note { get; set; }
    }

    /// <summary>How a game ended.</summary>
    public enum Ending
    {
        /// <summary>A country held <see cref="GameRules.ProvincesToWin"/> provinces.</summary>
        Domination,
        /// <summary>Every other country was out.</summary>
        LastStanding,
        /// <summary>The last turn ended: most provinces won, then the larger army, then more gold.</summary>
        TurnLimit,
        /// <summary>
        /// A conquest game reached its headless cap with more than one country left. The leader is
        /// named as by the turn limit, but nobody won (rules v2, section 6.1).
        /// </summary>
        Unfinished,
    }

    /// <summary>A finished game's result.</summary>
    public sealed class GameResult
    {
        public GameResult(string winner, Ending ending, int turns, IReadOnlyDictionary<string, int> provinces)
        {
            Winner = winner;
            Ending = ending;
            Turns = turns;
            Provinces = provinces;
        }

        public string Winner { get; }
        public Ending Ending { get; }
        /// <summary>The number of turns played.</summary>
        public int Turns { get; }
        /// <summary>Provinces held at the end, per country id.</summary>
        public IReadOnlyDictionary<string, int> Provinces { get; }
    }

    /// <summary>One resolved turn: what every decision came to and what changed on the map.</summary>
    public sealed class TurnReport
    {
        public int Turn;
        /// <summary>Upkeep's lines: income, events, revolts, truces ending.</summary>
        public List<string> Upkeep = new List<string>();
        /// <summary>Resolution's lines, in the order things happened.</summary>
        public List<string> Resolution = new List<string>();
        public List<MoveOutcome> Outcomes = new List<MoveOutcome>();
        /// <summary>Provinces whose owner changed this turn, revolts included.</summary>
        public int TerritoryChanges;
        public int Battles;
        /// <summary>Moves the re-check refused because an earlier move this turn made them stale.</summary>
        public int StaleRefusals;
        /// <summary>The events that happened at this turn's upkeep: the random table's, or the storyteller's one.</summary>
        public List<EventRecord> Events = new List<EventRecord>();
        /// <summary>A storyteller event refused when it came to land, with the reason; null when none was.</summary>
        public string EventRefused;
        public int PeaceOffers;
        public int PeaceAccepted;
        public string OwnerGrid;
        public Dictionary<string, int> ProvinceCounts = new Dictionary<string, int>();
    }
}
