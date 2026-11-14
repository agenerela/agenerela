using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy.Replay
{
    /// <summary>A change made through the dev menu, applied at the start of <see cref="Turn"/>, before its upkeep.</summary>
    public sealed class DevEdit
    {
        public int Turn;
        public string Command;
        public override string ToString() => $"turn {Turn}: {Command}";
    }

    /// <summary>
    /// The dev menu's changes to a game's state: gold, soldiers, stability, forts, owners, wars,
    /// eliminations and events. Each is a one-line command applied to a <see cref="Game"/> between
    /// turns, and recorded, so <see cref="ReplayBuilder"/> applies the same command at the same turn
    /// and the replay never drifts from the game it shows. A game with any is a dev game, never a
    /// measured one.
    /// </summary>
    /// <remarks>
    /// The commands: <c>gold kesh +50</c>, <c>army kesh -10</c>, <c>stability kesh =5</c>,
    /// <c>forts harrow +1</c>, <c>owner harrow kesh</c> (or <c>neutral</c>), <c>war kesh valen</c>,
    /// <c>peace kesh valen</c>, <c>eliminate valen</c>, <c>event windfall kesh</c>. An amount is
    /// <c>+n</c>, <c>-n</c> or <c>=n</c>. The writs turn's: <c>spawn kesh keshtar 30</c> (a new army),
    /// <c>march maren_host corwick</c> (an order), <c>battle maren_host tolly_host</c> (the first army
    /// steps into the second's province, so they fight when the turn resolves), <c>occupy corwick kesh</c>,
    /// <c>free corwick</c>, <c>warscore kesh valen =12</c> (sets the battles part of the score: 4 a battle,
    /// at most 20), <c>treaty kesh demand_occupied valen</c> (lands a term at once, unasked).
    /// </remarks>
    public static class DevEdits
    {
        public static readonly string[] Names = { "gold", "army", "stability", "forts", "owner", "war", "peace", "eliminate", "event",
            "spawn", "march", "battle", "occupy", "free", "warscore", "treaty" };

        /// <summary>The writs turn's commands (rules v2, R1): armies, occupation, war score and treaties.</summary>
        public static readonly string[] WritsNames = { "spawn", "march", "battle", "occupy", "free", "warscore", "treaty" };

        /// <summary>Whether a line is one of these commands at all.</summary>
        public static bool IsEdit(string command) => Names.Contains(Split(command).FirstOrDefault() ?? "");

        /// <summary>Why the command cannot apply to this world now, or null.</summary>
        public static string WhyNot(World w, string command) => Run(w, null, command, dryRun: true);

        /// <summary>Why the command cannot apply to this game at the next turn, or null; an event is checked against the game's ledger too.</summary>
        public static string WhyNot(Game game, string command) => Run(game.World, game, command, dryRun: true);

        /// <summary>
        /// Applies the command to the game, which must be between turns. Null on success, else why
        /// not. An event turns the random event table off for the one upkeep it fires in; call
        /// <see cref="AfterBeginTurn"/> once that turn has begun.
        /// </summary>
        public static string Apply(Game game, string command) => Run(game.World, game, command, dryRun: false);

        /// <summary>Turns the random event table back on after an upkeep a dev event fired in. Not in a storyteller's game, which never has one.</summary>
        public static void AfterBeginTurn(Game game) { if (!game.EventTable) game.EventTable = true; }

        /// <summary>Whether the command queues an event, so the storyteller is not asked that turn.</summary>
        public static bool QueuesEvent(string command) => Split(command).FirstOrDefault() == "event";

        /// <summary>Applies every edit for <paramref name="turn"/>; for ReplayBuilder and the live session alike.</summary>
        public static void ApplyFor(Game game, IEnumerable<DevEdit> edits, int turn)
        {
            foreach (var e in edits.Where(e => e.Turn == turn))
            {
                string why = Apply(game, e.Command);
                if (why != null) throw new InvalidOperationException($"Dev edit '{e}' cannot apply: {why}");
            }
        }

        private static string[] Split(string command) =>
            (command ?? "").Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        private static string Run(World w, Game game, string command, bool dryRun)
        {
            var a = Split(command);
            if (a.Length == 0) return "empty command";
            string verb = a[0].ToLowerInvariant();
            Country C(int i) => a.Length > i ? w.Country(a[i]) : null;
            Province P(int i) => a.Length > i ? w.Province(a[i]) : null;
            switch (verb)
            {
                case "gold":
                case "army":
                case "stability":
                {
                    var c = C(1);
                    if (c == null) return $"usage: {verb} <country> <+n|-n|=n>";
                    if (!c.Alive) return $"{c.Id} is out of the game";
                    int now = verb == "gold" ? c.Gold : verb == "army" ? c.Army : c.Stability;
                    if (!Amount(a, 2, now, out int value)) return $"usage: {verb} {c.Id} <+n|-n|=n>";
                    int min = verb == "stability" ? 1 : 0, max = verb == "stability" ? w.Rules.MaxStability : int.MaxValue;
                    value = Math.Max(min, Math.Min(max, value));
                    if (dryRun) return null;
                    if (verb == "gold") c.Gold = value; else if (verb == "army") c.Army = value; else c.Stability = value;
                    return null;
                }
                case "forts":
                {
                    var p = P(1);
                    if (p == null) return "usage: forts <province> <+n|-n|=n>";
                    if (!Amount(a, 2, p.Forts, out int value)) return $"usage: forts {p.Id} <+n|-n|=n>";
                    value = Math.Max(0, Math.Min(w.Rules.MaxFortsPerProvince, value));
                    if (!dryRun) p.Forts = value;
                    return null;
                }
                case "owner":
                {
                    var p = P(1);
                    if (p == null || a.Length < 3) return "usage: owner <province> <country|neutral>";
                    string owner = a[2] == "neutral" ? null : a[2];
                    if (owner != null && C(2) == null) return $"no country '{a[2]}'";
                    if (owner != null && !C(2).Alive) return $"{owner} is out of the game";
                    if (p.IsCapital && owner != p.Owner && p.Owner != null && w.ProvinceCount(p.Owner) == 1)
                        return $"{p.Id} is {p.Owner}'s last province: use eliminate";
                    if (!dryRun) { p.Owner = owner; if (owner == null) p.Garrison = Math.Max(p.Garrison, w.Rules.RevoltGarrison); }
                    return null;
                }
                case "war":
                case "peace":
                {
                    var x = C(1); var y = C(2);
                    if (x == null || y == null || x == y) return $"usage: {verb} <country> <other country>";
                    if (!x.Alive || !y.Alive) return "both must be in the game";
                    if (!dryRun) w.SetRelation(x.Id, y.Id, verb == "war" ? Relation.War : Relation.Peace);
                    return null;
                }
                case "eliminate":
                {
                    var c = C(1);
                    if (c == null) return "usage: eliminate <country>";
                    if (!c.Alive) return $"{c.Id} is already out";
                    if (w.Alive.Count() <= 2) return "the game needs two countries left";
                    if (dryRun) return null;
                    foreach (var p in w.ProvincesOf(c.Id).ToList()) { p.Owner = null; p.Forts = 0; p.Garrison = Math.Max(p.Garrison, w.Rules.RevoltGarrison); }
                    foreach (var o in w.CountryList.Where(o => o != c)) w.SetRelation(c.Id, o.Id, Relation.Peace);
                    c.Alive = false;
                    c.EliminatedOnTurn = w.Turn;
                    return null;
                }
                case "event":
                {
                    if (a.Length < 3) return "usage: event <template> <target> [Title: story]";
                    var t = EventRules.Template(a[1]);
                    if (t == null) return $"no event '{a[1]}': one of {string.Join(", ", EventRules.AllTemplates.Select(x => x.Id))}";
                    if (game == null) return null; // a dry run needs the game's ledger: checked when applied
                    // Stage S2 deals each turn one kind of event (a tale's, a dilemma's, an ordinary one) and opens
                    // only that kind: the dev menu deals the turn the kind its event needs, in the game and its replay alike.
                    if (game.Story.IsS2)
                        game.Events.TurnKinds[game.NextTurn] = t.Skeleton != null || a[2].StartsWith("tale_", StringComparison.Ordinal) ? TurnKind.Tale
                            : t.IsChoice ? TurnKind.Dilemma : TurnKind.Ordinary;
                    string why = game.WhyNotEvent(t.Id, a[2]);
                    if (why != null) return why;
                    if (dryRun) return null;
                    // The words after the target are the event's "Title: story"; without them, the template's own.
                    string told = a.Length > 3 ? string.Join(" ", a.Skip(3)) : null;
                    string title = t.FallbackTitle, statement;
                    if (told == null)
                    {
                        // Who it reaches as the game will reach it: a tale's subject, a group's members, a province's owner.
                        var hit = EventRules.ReachFor(game.World, game.Events, game.NextTurn, t, a[2], game.Story).hit;
                        var chain = game.Events.Chain(a[2]);
                        string at = game.World.IsProvince(a[2]) ? a[2] : chain?.Province;
                        statement = EventRules.FallbackStory(t, hit, at);
                    }
                    else
                    {
                        var (head, body) = EventRules.SplitTitle(told);
                        if (head != null) title = head;
                        statement = body.Length > 0 ? body : told;
                    }
                    // A storyteller game takes the title from the title call; one without takes it from the statement.
                    if (!game.Story.TitleCall) statement = $"{title}: {statement}";
                    try
                    {
                        game.EventTable = false;
                        game.QueueEvent(new StoryEvent(t.Id, a[2], statement, game.Story.TitleCall ? title : null));
                    }
                    catch (InvalidOperationException e) { return e.Message; }
                    return null;
                }
                case "spawn": case "march": case "battle": case "occupy": case "free": case "warscore": case "treaty":
                    if (w.Rules.Turn != TurnStyle.Writs) return $"{verb} is the writs turn's: armies, occupation and treaties";
                    return Writs(w, verb, a, dryRun);
                default:
                    return $"unknown edit '{verb}': {string.Join(", ", Names)}";
            }
        }

        private static string Writs(World w, string verb, string[] a, bool dryRun)
        {
            Country C(int i) => a.Length > i ? w.Country(a[i]) : null;
            Province P(int i) => a.Length > i ? w.Province(a[i]) : null;
            Army A(int i) => a.Length > i ? w.FindArmy(a[i]) : null;
            switch (verb)
            {
                case "spawn":
                {
                    var c = C(1); var p = P(2);
                    if (c == null || p == null || a.Length < 4 || !int.TryParse(a[3], out int n) || n <= 0) return "usage: spawn <country> <province> <soldiers>";
                    if (!c.Alive) return $"{c.Id} is out of the game";
                    if (p.Controller != c.Id) return $"{p.Id} is not held by {c.Id}";
                    n = Math.Min(n, w.Rules.ArmyMax);
                    if (dryRun) return null;
                    w.Armies.Add(new Army(ArmyNames.Next(w, c.Id), c.Id, p.Id, n, w.Turn));
                    c.Army = w.ArmiesOf(c.Id).Sum(x => x.Soldiers);
                    return null;
                }
                case "march":
                {
                    var army = A(1); var p = P(2);
                    if (army == null || p == null) return "usage: march <army> <province>";
                    var paths = WritLegality.Paths(w, army.Owner, army.Location, 99);
                    if (!paths.TryGetValue(p.Id, out var path)) return $"{army.Id} cannot reach {p.Id}";
                    if (dryRun) return null;
                    army.Wake.Clear();
                    army.OrderedOnTurn = w.Turn;
                    army.Destination = p.Id;
                    army.Path.Clear();
                    army.Path.AddRange(path);
                    army.StepTurnsLeft = w.Rules.TurnsToEnter(w.Province(path[0]).Info.Terrain);
                    return null;
                }
                case "battle":
                {
                    var x = A(1); var y = A(2);
                    if (x == null || y == null || x.Owner == y.Owner) return "usage: battle <army> <enemy army>";
                    if (!w.AtWar(x.Owner, y.Owner)) return $"{x.Owner} and {y.Owner} are not at war: war {x.Owner} {y.Owner} first";
                    if (dryRun) return null;
                    x.Location = y.Location;
                    x.Destination = null;
                    x.Path.Clear();
                    return null;
                }
                case "occupy":
                {
                    var p = P(1); var c = C(2);
                    if (p == null || c == null) return "usage: occupy <province> <country>";
                    if (p.Owner == null || p.Owner == c.Id) return $"{p.Id} must be another country's land";
                    if (!w.AtWar(c.Id, p.Owner)) return $"{c.Id} is not at war with {p.Owner}";
                    if (!dryRun) p.Occupier = c.Id;
                    return null;
                }
                case "free":
                {
                    var p = P(1);
                    if (p == null) return "usage: free <province>";
                    if (p.Occupier == null) return $"{p.Id} is not occupied";
                    if (!dryRun) p.Occupier = null;
                    return null;
                }
                case "warscore":
                {
                    var x = C(1); var y = C(2);
                    if (x == null || y == null || a.Length < 4 || a[3].Length < 2 || a[3][0] != '=' || !int.TryParse(a[3].Substring(1), out int v))
                        return "usage: warscore <country> <country> =<battles part, -20 to 20>";
                    if (!w.AtWar(x.Id, y.Id)) return $"{x.Id} and {y.Id} are not at war";
                    if (dryRun) return null;
                    v = Math.Max(-w.Rules.BattleScoreMax, Math.Min(w.Rules.BattleScoreMax, v));
                    int battles = (int)Math.Round(Math.Abs(v) / (double)Math.Max(1, w.Rules.BattleScore));
                    var war = w.War(x.Id, y.Id);
                    war.BattlesWon[x.Id] = v >= 0 ? battles : 0;
                    war.BattlesWon[y.Id] = v < 0 ? battles : 0;
                    return null;
                }
                case "treaty":
                {
                    var x = C(1); var y = C(3);
                    string term = a.Length > 2 ? a[2] : null;
                    if (x == null || y == null || !Actions.IsTerm(term)) return "usage: treaty <country> <offer_white_peace|demand_occupied|cede_occupied> <country>";
                    if (!w.AtWar(x.Id, y.Id)) return $"{x.Id} and {y.Id} are not at war";
                    if (dryRun) return null;
                    // As the rules land a term: ceded land changes owner, the rest returns; a truce begins.
                    foreach (var p in w.ProvinceList)
                    {
                        bool xHoldsTheirs = p.Owner == y.Id && p.Occupier == x.Id, yHoldsOurs = p.Owner == x.Id && p.Occupier == y.Id;
                        if (!xHoldsTheirs && !yHoldsOurs) continue;
                        if (term == Actions.DemandOccupied && xHoldsTheirs) p.Owner = x.Id;
                        else if (term == Actions.CedeOccupied && yHoldsOurs) p.Owner = y.Id;
                        p.Occupier = null;
                    }
                    w.SetRelation(x.Id, y.Id, Relation.Truce, w.Rules.TruceTurns);
                    return null;
                }
            }
            return $"unknown edit '{verb}'";
        }

        private static bool Amount(string[] a, int i, int now, out int value)
        {
            value = now;
            if (a.Length <= i || a[i].Length < 2) return false;
            char op = a[i][0];
            if (!int.TryParse(a[i].Substring(1), out int n)) return false;
            if (op == '+') value = now + n;
            else if (op == '-') value = now - n;
            else if (op == '=') value = n;
            else return false;
            return true;
        }
    }
}
