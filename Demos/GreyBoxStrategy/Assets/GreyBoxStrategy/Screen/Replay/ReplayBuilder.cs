using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GreyBoxStrategy.Replay
{
    /// <summary>
    /// Plays a recorded game again through <see cref="Game"/>, feeding each country the choice its
    /// model made, and turns every resolved turn into the ordered events the screen plays back.
    /// </summary>
    /// <remarks>
    /// The rules are deterministic per seed: every die is a hash of the seed, the turn, who rolls
    /// and why. So the recorded choices rebuild the very game the table played, no model needed. A
    /// move <c>Agent.Execute</c> refused in the recorded game is refused here too, through a gate.
    /// If the rebuilt game ever drifts from the record (the rules changed since it was made),
    /// <see cref="Build"/> throws rather than show a different game with the same statements.
    /// </remarks>
    public static partial class ReplayBuilder
    {
        private static readonly Regex Took = new Regex(@"^took (\w+) from (\w+|the neutrals) \((\d+) against (\d+)\)");
        private static readonly Regex Failed = new Regex(@"^attack on (\w+) failed \((\d+) against (\d+)\)");
        private static readonly Regex Built = new Regex(@"^built a fort in (\w+)");
        private static readonly Regex Revolted = new Regex(@"^(\w+): (\w+) revolted");
        private static readonly Regex TruceEnded = new Regex(@"^The truce between (\w+) and (\w+) ended");
        private static readonly Regex OutOfGame = new Regex(@"^(\w+) has lost its last province");

        /// <param name="partial">For a game still being played: stop at the first turn the recording
        /// cannot finish, and return the turns before it, rather than throw.</param>
        public static ReplayGame Build(Recording recording, MapDefinition map = null, GameRules rules = null, bool partial = false)
        {
            map = map ?? MapDefinition.Standard();
            var game = new Game(recording.Seed, map, rules);
            // A storyteller game: no random table, the storyteller's rules, its recorded events.
            var story = StoryArms.Rules(recording.StoryArm);
            if (story != null) { game.EventTable = false; game.Story = story; }
            var players = new RecordedPlayers(recording);
            foreach (var c in game.World.CountryList) game.Gates[c.Id] = players;

            int cols = map.Columns;
            var provinceIds = ProvinceOrder(map);

            var replay = new ReplayGame
            {
                Seed = recording.Seed,
                Title = recording.Title,
                CountryIds = map.Countries.Select(c => c.Id).ToList(),
                ProvinceIds = provinceIds,
                Columns = cols,
                Players = recording.Players,
                Map = map,
                Rules = game.Rules,
                Dev = recording.Dev || recording.DevEdits.Count > 0,
                Story = story,
                Writs = game.Rules.Turn == TurnStyle.Writs,
            };
            var since = new Dictionary<string, int>(StringComparer.Ordinal);
            replay.Initial = Capture(game.World, provinceIds, since);

            int guard = game.Rules.MaxTurns + 1;
            var state = replay.Initial;
            while (!game.IsOver)
            {
                if (guard-- <= 0) throw new InvalidOperationException("The replay did not end within the turn limit.");
                var turn = new ReplayTurn { Turn = game.World.Turn + 1, Start = state };
                TurnReport tr;
                Dictionary<string, List<string>> paths = null;
                try
                {
                    // The dev menu's changes for this turn land first, as they did in the game.
                    int next = game.World.Turn + 1;
                    if (recording.DevEdits.Any(d => d.Turn == next))
                    {
                        DevEdits.ApplyFor(game, recording.DevEdits, next);
                        // The turn starts from the edited state, so upkeep and the end check build on it.
                        state = Capture(game.World, provinceIds, since);
                        turn.Start = state;
                    }
                    // The storyteller's event for this upkeep, unless the dev menu set one, and the rulers' choices.
                    if (story != null) QueueStory(game, players, next, recording.DevEdits.Any(d => d.Turn == next && DevEdits.QueuesEvent(d.Command)));
                    if (replay.Writs)
                    {
                        tr = PlayWritsTurn(game, players, out paths);
                        if (story == null) DevEdits.AfterBeginTurn(game);
                    }
                    else
                    {
                        var council = game.BeginTurn();
                        if (story == null) DevEdits.AfterBeginTurn(game);
                        var envoys = game.SubmitCouncil(council.Select(r => new CountryAnswer(r, players.Decide(r))).ToList());
                        tr = game.Resolve(envoys.Select(r => new CountryAnswer(r, players.Decide(r))).ToList());
                    }
                }
                catch (InvalidOperationException e) when (partial)
                {
                    replay.StoppedBecause = e.Message;
                    break; // the game has not got this far yet, or the recording cannot rebuild it
                }

                // Upkeep happened before the council; its state is the council's snapshot plus what we read off the world now.
                foreach (var d in recording.DevEdits.Where(d => d.Turn == turn.Turn))
                    turn.Events.Add(new ReplayEvent { Turn = turn.Turn, Kind = ReplayEventKind.Dev, Text = d.Command, After = state.Clone() });
                var councilState = state.Clone();
                var snap = game.Snapshots.Last();
                for (int i = 0; i < provinceIds.Length; i++) councilState.Owners[i] = snap.OwnerOf(provinceIds[i]);
                if (replay.Writs)
                {
                    for (int i = 0; i < provinceIds.Length; i++) councilState.Occupiers[i] = snap.Occupiers.TryGetValue(provinceIds[i], out var occ) ? occ : null;
                    councilState.Armies = snap.Armies.Select(ArmyOf).ToList();
                }
                var upkeepEvents = UpkeepEvents(tr, state, councilState, replay, turn.Turn);
                if (story != null) upkeepEvents.InsertRange(0, StoryEvents(tr, state, replay, players));
                turn.Events.AddRange(upkeepEvents);
                turn.Council = councilState;
                foreach (var kv in snap.Army) if (councilState.Countries.TryGetValue(kv.Key, out var cs)) cs.Army = kv.Value;

                var running = councilState.Clone();
                var end = Capture(game.World, provinceIds, since);
                foreach (var e in replay.Writs ? WritsEvents(tr, running, end, replay, players, turn.Turn, paths) : ResolutionEvents(tr, running, replay, players, turn.Turn))
                    turn.Events.Add(e);

                // The resolution events moved owners and forts one by one; they must land where the game did.
                for (int i = 0; i < provinceIds.Length; i++)
                {
                    if (running.Owners[i] != end.Owners[i])
                        throw new InvalidOperationException($"Turn {turn.Turn}: the replay has {provinceIds[i]} held by {running.Owners[i] ?? "nobody"}, the game by {end.Owners[i] ?? "nobody"}.");
                    if (running.Occupiers[i] != end.Occupiers[i])
                        throw new InvalidOperationException($"Turn {turn.Turn}: the replay has {provinceIds[i]} occupied by {running.Occupiers[i] ?? "nobody"}, the game by {end.Occupiers[i] ?? "nobody"}.");
                }
                // The pieces: armies the events left somewhere other than the rules did (the last event then moves them there).
                if (replay.Writs)
                    turn.ArmyDrift = end.Armies.Count(a => running.Army(a.Id) is ArmyState r ? r.Location != a.Location : true)
                                     + running.Armies.Count(a => end.Army(a.Id) == null);
                // The last event carries the game's own numbers for the turn's end.
                if (turn.Events.Count > 0) turn.Events[turn.Events.Count - 1].After = end;
                else turn.Council = end;
                turn.TerritoryChanges = tr.TerritoryChanges;
                replay.Turns.Add(turn);
                state = end;
            }
            replay.Result = game.Result;
            replay.Chains = game.Chains.ToList();
            if (!partial) players.CheckAllUsed();
            return replay;
        }

        /// <summary>
        /// The screen's order of provinces. The Classic board's grid is dense (5 × 5), and its
        /// order is the grid's, which its baked province map uses. A generated world's grid has
        /// gaps, so its order is the map's own, which its drawn province map uses.
        /// </summary>
        public static string[] ProvinceOrder(MapDefinition map)
        {
            int cols = map.Columns, n = map.Provinces.Count;
            var ids = new string[n];
            bool dense = true;
            foreach (var p in map.Provinces)
            {
                int k = p.Row * cols + p.Column;
                if (k < 0 || k >= n || ids[k] != null) { dense = false; break; }
                ids[k] = p.Id;
            }
            if (!dense) for (int i = 0; i < n; i++) ids[i] = map.Provinces[i].Id;
            return ids;
        }

        // The storyteller's recorded event, queued as the game queued it, then each ruler's recorded choice.
        private static void QueueStory(Game game, RecordedPlayers players, int turn, bool devEvent)
        {
            var told = players.Story(turn);
            if (!devEvent && told != null && told.ExecutionStatus == RecordedDecision.Done && told.HasTarget)
                game.QueueEvent(new StoryEvent(told.Action, told.Target, told.Statement, players.Title(turn)?.Statement));
            foreach (var c in game.PendingChoices.ToList())
            {
                var d = players.Choice(turn, c.Country);
                if (d == null || d.ExecutionStatus == "Refused") continue; // the default lands, as it did
                game.Choose(c.Country, d.Action, d.ModelName == "you" ? "person" : "model");
            }
        }

        // One event a storyteller event, in place of the upkeep lines that tell it country by country.
        private static IEnumerable<ReplayEvent> StoryEvents(TurnReport tr, ReplayState state, ReplayGame replay, RecordedPlayers players)
        {
            foreach (var r in tr.Events.Where(x => x.Source == EventRecord.FromStoryteller))
            {
                string at = r.Province ?? (r.Hit.Count > 0 ? CapitalOf(replay.Map, r.Hit[0]) : null);
                var e = new ReplayEvent
                {
                    Turn = r.Turn, Kind = ReplayEventKind.Story, Story = r,
                    Country = r.Hit.Count == 1 ? r.Hit[0] : null,
                    Province = at, ProvinceIndex = replay.IndexOf(at),
                    Text = r.Title, Statement = r.Story ?? "",
                    Decision = players.Story(r.Turn), TitleDecision = players.Title(r.Turn),
                    After = state.Clone(),
                };
                foreach (string c in r.Hit) { var d = players.Choice(r.Turn, c); if (d != null) e.ChoiceDecisions[c] = d; }
                yield return e;
            }
        }

        private static readonly Regex StoryLine = new Regex(@"^\w+: event "".*"" \(");

        private static string CapitalOf(MapDefinition map, string country) => map.Provinces.FirstOrDefault(p => p.IsCapital && p.StartOwner == country)?.Id;

        private static List<ReplayEvent> UpkeepEvents(TurnReport tr, ReplayState before, ReplayState council, ReplayGame replay, int turn)
        {
            var events = new List<ReplayEvent>();
            var running = before.Clone();
            bool told = tr.Events.Any(x => x.Source == EventRecord.FromStoryteller);
            foreach (string line in tr.Upkeep)
            {
                if (told && StoryLine.IsMatch(line)) continue; // the storyteller's event is its own event
                var e = new ReplayEvent { Turn = turn, Text = line, Kind = ReplayEventKind.Upkeep };
                var m = Revolted.Match(line);
                var t = TruceEnded.Match(line);
                if (m.Success)
                {
                    e.Kind = ReplayEventKind.Revolt;
                    e.Country = m.Groups[1].Value;
                    e.Province = m.Groups[2].Value;
                    e.ProvinceIndex = replay.IndexOf(e.Province);
                    running.Owners[e.ProvinceIndex] = null;
                    running.Forts[e.ProvinceIndex] = 0;
                }
                else if (t.Success)
                {
                    e.Kind = ReplayEventKind.TruceEnded;
                    e.Country = t.Groups[1].Value;
                    e.Other = t.Groups[2].Value;
                    running.Relations.RemoveAll(r => r.Kind == "truce" && ((r.A == e.Country && r.B == e.Other) || (r.A == e.Other && r.B == e.Country)));
                }
                else
                {
                    int colon = line.IndexOf(':');
                    e.Country = colon > 0 ? line.Substring(0, colon) : null;
                }
                e.After = running.Clone();
                events.Add(e);
            }
            // Upkeep's numbers (income, events, desertion) are the council's.
            foreach (var kv in council.Countries) running.Countries[kv.Key] = kv.Value.Clone();
            if (events.Count > 0) events[events.Count - 1].After = running.Clone();
            council.Owners = (string[])running.Owners.Clone();
            council.Forts = (int[])running.Forts.Clone();
            council.Relations = running.Relations.Select(r => new RelationState { A = r.A, B = r.B, Kind = r.Kind, TruceTurnsLeft = r.TruceTurnsLeft, Since = r.Since }).ToList();
            return events;
        }

        private static IEnumerable<ReplayEvent> ResolutionEvents(TurnReport tr, ReplayState s, ReplayGame replay, RecordedPlayers players, int turn)
        {
            foreach (var o in tr.Outcomes)
            {
                var d = players.DecisionFor(o.Request);
                string me = o.Request.CountryId;
                var e = new ReplayEvent { Turn = turn, Country = me, Text = o.Text, Decision = d, Statement = d?.Statement ?? "" };

                if (o.Request.Moment == Moment.Envoy)
                {
                    // The envoy's answer comes first in the report; show the offer, then the answer.
                    string from = o.Request.OfferedBy;
                    var offer = players.CouncilDecision(turn, from);
                    yield return Snap(new ReplayEvent
                    {
                        Turn = turn, Kind = ReplayEventKind.PeaceOffered, Country = from, Other = me,
                        Text = $"{from} offered {me} peace", Decision = offer, Statement = offer?.Statement ?? "",
                    }, s);
                    bool accepted = o.Applied.Action == Actions.AcceptPeace;
                    e.Kind = o.Refused ? ReplayEventKind.Refused : accepted ? ReplayEventKind.PeaceAccepted : ReplayEventKind.PeaceRefused;
                    e.Other = from;
                    if (accepted) SetTruce(s, me, from);
                    yield return Snap(e, s);
                    continue;
                }

                var choice = o.Choice;
                if (choice.Action == Actions.OfferPeace && !o.Refused)
                {
                    if (o.Text.Contains("offered peace too"))
                    {
                        e.Kind = ReplayEventKind.PeaceMutual;
                        e.Other = choice.Target;
                        SetTruce(s, me, choice.Target);
                        yield return Snap(e, s);
                    }
                    continue; // otherwise the envoy's events above already showed it
                }

                if (o.Refused)
                {
                    e.Kind = ReplayEventKind.Refused;
                    e.Province = choice.Target != null && Actions.TargetsProvince(choice.Action) ? choice.Target : null;
                    e.Other = choice.Target != null && Actions.TargetsCountry(choice.Action) ? choice.Target : null;
                    e.ProvinceIndex = e.Province == null ? -1 : replay.IndexOf(e.Province);
                    yield return Snap(e, s);
                    continue;
                }

                switch (choice.Action)
                {
                    case Actions.Attack:
                    {
                        var took = Took.Match(o.Text);
                        var failed = Failed.Match(o.Text);
                        e.Province = choice.Target;
                        e.ProvinceIndex = replay.IndexOf(choice.Target);
                        if (took.Success)
                        {
                            e.Kind = ReplayEventKind.Capture;
                            e.Other = took.Groups[2].Value == "the neutrals" ? null : took.Groups[2].Value;
                            e.Attack = int.Parse(took.Groups[3].Value);
                            e.Defence = int.Parse(took.Groups[4].Value);
                            s.Owners[e.ProvinceIndex] = me;
                            s.Forts[e.ProvinceIndex] = Math.Max(0, s.Forts[e.ProvinceIndex] - 1);
                            s.Garrisons[e.ProvinceIndex] = 0;
                        }
                        else if (failed.Success)
                        {
                            e.Kind = ReplayEventKind.AttackFailed;
                            e.Other = s.Owners[e.ProvinceIndex];
                            e.Attack = int.Parse(failed.Groups[2].Value);
                            e.Defence = int.Parse(failed.Groups[3].Value);
                        }
                        else throw new InvalidOperationException($"Turn {turn}: cannot read the attack outcome \"{o.Text}\".");
                        break;
                    }
                    case Actions.Fortify:
                        e.Kind = ReplayEventKind.Fort;
                        e.Province = choice.Target;
                        e.ProvinceIndex = replay.IndexOf(choice.Target);
                        if (Built.IsMatch(o.Text)) s.Forts[e.ProvinceIndex]++;
                        break;
                    case Actions.DeclareWar:
                        e.Kind = ReplayEventKind.WarDeclared;
                        e.Other = choice.Target;
                        s.Relations.RemoveAll(r => Pair(r, me, choice.Target));
                        s.Relations.Add(new RelationState { A = me, B = choice.Target, Kind = "war", Since = turn });
                        break;
                    case Actions.Recruit: e.Kind = ReplayEventKind.Recruit; break;
                    case Actions.RaiseTaxes: e.Kind = ReplayEventKind.Taxes; break;
                    default: e.Kind = ReplayEventKind.Hold; break;
                }
                yield return Snap(e, s);
            }

            foreach (string line in tr.Resolution)
            {
                var m = OutOfGame.Match(line);
                if (!m.Success) continue;
                yield return Snap(new ReplayEvent { Turn = turn, Kind = ReplayEventKind.Eliminated, Country = m.Groups[1].Value, Text = line }, s);
            }
        }

        private static ArmyState ArmyOf(ArmySnapshot a) => new ArmyState
        {
            Id = a.Id, Owner = a.Owner, Location = a.Location, Soldiers = a.Soldiers, Destination = a.Destination,
            Path = a.Path.ToList(), StepTurnsLeft = a.StepTurnsLeft, Wake = a.Wake.ToList(),
        };

        private static bool Pair(RelationState r, string a, string b) => (r.A == a && r.B == b) || (r.A == b && r.B == a);

        private static void SetTruce(ReplayState s, string a, string b)
        {
            s.Relations.RemoveAll(r => Pair(r, a, b));
            s.Relations.Add(new RelationState { A = a, B = b, Kind = "truce", TruceTurnsLeft = 5 });
        }

        private static ReplayEvent Snap(ReplayEvent e, ReplayState s)
        {
            foreach (var c in s.Countries.Values) c.Provinces = s.ProvinceCount(c.Id);
            e.After = s.Clone();
            return e;
        }

        /// <summary>
        /// The state of a game being played, as the screen shows it: rule mode's council, after
        /// upkeep and before any move lands. A war keeps the turn it began from <paramref name="before"/>.
        /// </summary>
        public static ReplayState StateOf(World w, IReadOnlyList<string> provinceIds, ReplayState before = null)
        {
            var since = new Dictionary<string, int>(StringComparer.Ordinal);
            if (before != null)
                foreach (var r in before.Relations.Where(r => r.Kind == "war"))
                {
                    since[r.A + "|" + r.B] = r.Since; since[r.B + "|" + r.A] = r.Since;
                }
            return Capture(w, provinceIds.ToArray(), since);
        }

        private static ReplayState Capture(World w, string[] provinceIds, Dictionary<string, int> since)
        {
            var s = new ReplayState
            {
                Owners = provinceIds.Select(id => w.Province(id).Owner).ToArray(),
                Forts = provinceIds.Select(id => w.Province(id).Forts).ToArray(),
                Garrisons = provinceIds.Select(id => w.Province(id).Garrison).ToArray(),
                Occupiers = provinceIds.Select(id => w.Province(id).Occupier).ToArray(),
                Armies = w.Armies.Select(a => new ArmyState
                {
                    Id = a.Id, Owner = a.Owner, Location = a.Location, Soldiers = a.Soldiers, Destination = a.Destination,
                    Path = a.Path.ToList(), StepTurnsLeft = a.StepTurnsLeft, Wake = a.Wake.ToList(),
                }).ToList(),
            };
            foreach (var c in w.CountryList)
                s.Countries[c.Id] = new CountryState
                {
                    Id = c.Id, Gold = c.Gold, Army = c.Army, Stability = c.Stability, Alive = c.Alive,
                    Provinces = w.ProvinceCount(c.Id), Income = w.Income(c), Upkeep = w.Upkeep(c),
                };
            var ids = w.CountryList.Select(c => c.Id).ToList();
            for (int i = 0; i < ids.Count; i++)
                for (int j = i + 1; j < ids.Count; j++)
                {
                    // A country out of the game is at war with nobody, whatever its last relations were.
                    if (!w.Country(ids[i]).Alive || !w.Country(ids[j]).Alive) { since.Remove(ids[i] + "|" + ids[j]); continue; }
                    var rel = w.RelationBetween(ids[i], ids[j]);
                    string key = ids[i] + "|" + ids[j];
                    if (rel == Relation.War)
                    {
                        if (w.Rules.Turn == TurnStyle.Writs) s.WarScores[key] = GreyBoxStrategy.WarScore.Of(w, ids[i], ids[j]);
                        if (!since.ContainsKey(key)) since[key] = Math.Max(1, w.Turn);
                        s.Relations.Add(new RelationState { A = ids[i], B = ids[j], Kind = "war", Since = since[key] });
                    }
                    else
                    {
                        since.Remove(key);
                        if (rel == Relation.Truce)
                            s.Relations.Add(new RelationState { A = ids[i], B = ids[j], Kind = "truce", TruceTurnsLeft = w.TruceTurnsLeft(ids[i], ids[j]) });
                    }
                }
            return s;
        }
    }

    /// <summary>
    /// Every country's recorded choices, handed out in the order the game asks, and the gate that
    /// refuses what <c>Agent.Execute</c> refused in the recorded game.
    /// </summary>
    internal sealed class RecordedPlayers : IMoveGate
    {
        private readonly Dictionary<string, Queue<RecordedDecision>> queues = new Dictionary<string, Queue<RecordedDecision>>(StringComparer.Ordinal);
        private readonly Dictionary<CountryRequest, RecordedDecision> answered = new Dictionary<CountryRequest, RecordedDecision>();
        private readonly Dictionary<string, RecordedDecision> council = new Dictionary<string, RecordedDecision>(StringComparer.Ordinal);

        private readonly Dictionary<string, RecordedDecision> told = new Dictionary<string, RecordedDecision>(StringComparer.Ordinal);

        public RecordedPlayers(Recording recording)
        {
            foreach (var d in recording.Decisions)
            {
                if (!d.IsMove)
                {
                    // The storyteller's, the scribe's and the rulers' choices are not moves; the replay looks them up.
                    told[$"{d.Moment}|{d.Turn}|{(d.IsChoice ? d.AgentId : "")}"] = d;
                    continue;
                }
                string k = Key(d.Turn, d.Country, d.IsEnvoy);
                if (!queues.TryGetValue(k, out var q)) queues[k] = q = new Queue<RecordedDecision>();
                q.Enqueue(d);
            }
        }

        private static string Key(int turn, string country, bool envoy) => $"{turn}|{country}|{(envoy ? "envoy" : "council")}";

        public Move Decide(CountryRequest r)
        {
            bool envoy = r.Moment == Moment.Envoy;
            if (!queues.TryGetValue(Key(r.Turn, r.CountryId, envoy), out var q) || q.Count == 0)
                throw new InvalidOperationException($"The recording has no {(envoy ? "envoy" : "council")} decision for {r.CountryId} on turn {r.Turn}: it is from a different game or different rules.");
            var d = q.Dequeue();
            if (r.Moment == Moment.Army && d.ArmyId != null && d.ArmyId != r.ArmyId)
                throw new InvalidOperationException($"Turn {r.Turn}: the game asks {r.CountryId} for {r.ArmyId}'s orders, the recording has {d.ArmyId}'s: it is from a different game or different rules.");
            answered[r] = d;
            d.OfferedMoves = r.Legal.Select(m => m.ToString()).ToList();
            if (!envoy) council[r.Turn + "|" + r.CountryId] = d;
            if (envoy) return d.Action == Actions.AcceptPeace ? new Move(Actions.AcceptPeace) : new Move(Actions.RefusePeace);
            return new Move(d.Action ?? Actions.None, d.HasTarget ? d.Target : null);
        }

        public RecordedDecision Story(int turn) => told.TryGetValue($"{RecordedDecision.StoryMoment}|{turn}|", out var d) ? d : null;
        public RecordedDecision Title(int turn) => told.TryGetValue($"{RecordedDecision.TitleMoment}|{turn}|", out var d) ? d : null;
        public RecordedDecision Choice(int turn, string country) => told.TryGetValue($"{RecordedDecision.ChoiceMoment}|{turn}|{country}", out var d) ? d : null;

        public RecordedDecision DecisionFor(CountryRequest r) => r != null && answered.TryGetValue(r, out var d) ? d : null;

        public RecordedDecision CouncilDecision(int turn, string country) => council.TryGetValue(turn + "|" + country, out var d) ? d : null;

        public string Admit(CountryRequest request, Move move)
        {
            var d = DecisionFor(request);
            return d != null && d.ExecutionStatus == "Refused" ? (d.ExecutionReason ?? "refused by the agent") : null;
        }

        public void CheckAllUsed()
        {
            var left = queues.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key).ToList();
            if (left.Count > 0)
                throw new InvalidOperationException("The recording has decisions the replay never used: " + string.Join(", ", left));
        }
    }
}
