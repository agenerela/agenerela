using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GreyBoxStrategy.Replay
{
    /// <summary>
    /// The writs turn's events (rules v2, R1), in the order the rules resolve them: treaties, wars
    /// declared, home orders, army orders, every army's steps at once, then battles, occupation and
    /// land taken, broken realms and the end. Battles come from <see cref="TurnReport.BattleRecords"/>;
    /// the steps from each army's path when the council decided.
    /// </summary>
    public static partial class ReplayBuilder
    {
        private static readonly Regex BattleLine = new Regex(@"^Battle at ");
        private static readonly Regex TookNeutral = new Regex(@"^(\w+) took (\w+), neutral land\.$");
        private static readonly Regex FreedLine = new Regex(@"^(\w+) freed (\w+) from (\w+)\.$");
        private static readonly Regex OccupiesLine = new Regex(@"^(\w+) occupies (\w+) \((\w+)'s\)\.$");
        private static readonly Regex LostLine = new Regex(@"^(\w+_host\w*) (was routed at [\w -]+|was destroyed|had nowhere to go and disbanded)\.$");
        private static readonly Regex BrokenLine = new Regex(@"^(\w+)'s realm is broken");
        private static readonly Regex StalemateLine = new Regex(@"^No province changed hands for \d+ turns");
        private static readonly Regex Joined = new Regex(@"(new )?(\w+_host\w*) \+?(\d+)");

        // One writs turn: rounds of recorded orders, then the turn lands; returns its report and each army's path as ordered.
        private static TurnReport PlayWritsTurn(Game game, RecordedPlayers players, out Dictionary<string, List<string>> paths)
        {
            var requests = game.BeginTurn();
            var answers = new List<CountryAnswer>();
            while (requests.Count > 0)
            {
                var round = requests.Select(r => new CountryAnswer(r, players.Decide(r))).ToList();
                answers.AddRange(round);
                requests = game.Submit(round);
            }
            // Where each army means to go, on the world the council saw: a new march order's path, else the one it carries.
            var w = game.World;
            paths = w.Armies.ToDictionary(a => a.Id, a => a.Path.ToList(), StringComparer.Ordinal);
            foreach (var a in answers.Where(a => a.Request.Moment == Moment.Army))
            {
                var army = w.FindArmy(a.Request.ArmyId);
                if (army == null) continue;
                if (a.Choice.Action == Actions.March && a.Choice.Target != null
                    && WritLegality.Paths(w, army.Owner, army.Location, w.Rules.MarchRange).TryGetValue(a.Choice.Target, out var p)) paths[army.Id] = p.ToList();
                else paths[army.Id] = new List<string>();
            }
            return game.Resolve();
        }

        private static IEnumerable<ReplayEvent> WritsEvents(TurnReport tr, ReplayState s, ReplayState end, ReplayGame replay, RecordedPlayers players,
            int turn, Dictionary<string, List<string>> paths)
        {
            var seen = new HashSet<CountryRequest>();
            var outcomes = tr.Outcomes.Where(o => o.Request != null).ToList();

            // a. Treaties: each term offered, its answer, and the treaty if it landed.
            foreach (var o in outcomes.Where(o => o.Request.Moment == Moment.Council && Actions.IsTerm(o.Choice.Action)))
            {
                if (!seen.Add(o.Request)) continue;
                string me = o.Request.CountryId, them = o.Choice.Target, term = o.Choice.Action;
                var d = players.DecisionFor(o.Request);
                var mine = outcomes.Where(x => x.Request == o.Request).ToList();
                if (mine.Any(x => x.Refused))
                {
                    var r = mine.First(x => x.Refused);
                    yield return Snap(new ReplayEvent { Turn = turn, Kind = ReplayEventKind.Refused, Country = me, Other = them, Text = r.Text, Decision = d, Statement = d?.Statement ?? "" }, s);
                    continue;
                }
                bool enforced = mine.Any(x => x.Text.StartsWith("enforced ", StringComparison.Ordinal));
                if (!enforced)
                    yield return Snap(new ReplayEvent { Turn = turn, Kind = ReplayEventKind.TermOffered, Country = me, Other = them, Term = term, Text = o.Text, Decision = d, Statement = d?.Statement ?? "" }, s);
                var answer = outcomes.FirstOrDefault(x => x.Request.Moment == Moment.Envoy && x.Request.OfferedBy == me && x.Request.CountryId == them
                                                          && x.Request.OfferId != null && x.Request.OfferId.StartsWith($"t{turn}-{me}-{term}", StringComparison.Ordinal));
                if (answer != null)
                {
                    var ad = players.DecisionFor(answer.Request);
                    bool yes = answer.Applied.Action == Actions.AcceptPeace;
                    yield return Snap(new ReplayEvent { Turn = turn, Kind = yes ? ReplayEventKind.TermAccepted : ReplayEventKind.TermRefused, Country = them, Other = me, Term = term, Text = answer.Text, Decision = ad, Statement = ad?.Statement ?? "" }, s);
                }
                if (!mine.Any(x => x.Text == $"{(enforced ? "enforced " : "")}{term} on {them}")) continue;
                var e = new ReplayEvent { Turn = turn, Kind = ReplayEventKind.Treaty, Country = me, Other = them, Term = term, Enforced = enforced, Text = $"{term} on {them}" };
                for (int i = 0; i < s.Owners.Length; i++)
                {
                    string owner = s.Owners[i], occ = s.Occupiers[i];
                    bool fromHoldsTheirs = owner == them && occ == me, toHoldsOurs = owner == me && occ == them;
                    if (!fromHoldsTheirs && !toHoldsOurs) continue;
                    if (term == Actions.DemandOccupied && fromHoldsTheirs) { s.Owners[i] = me; e.Ceded.Add(replay.ProvinceIds[i]); }
                    else if (term == Actions.CedeOccupied && toHoldsOurs) { s.Owners[i] = them; e.Ceded.Add(replay.ProvinceIds[i]); }
                    else e.Returned.Add(replay.ProvinceIds[i]);
                    s.Occupiers[i] = null;
                }
                string first = e.Ceded.Count > 0 ? e.Ceded[0] : e.Returned.FirstOrDefault();
                if (first != null) { e.Province = first; e.ProvinceIndex = replay.IndexOf(first); }
                SetTruce(s, me, them);
                s.WarScores.Remove(me + "|" + them); s.WarScores.Remove(them + "|" + me);
                yield return Snap(e, s);
            }

            // b-c. Wars declared, then home orders, then each army's order.
            foreach (string kind in new[] { Actions.DeclareWar, Actions.Recruit, Actions.RaiseTaxes, Actions.Fortify })
                foreach (var o in outcomes.Where(o => o.Request.Moment == Moment.Council && o.Choice.Action == kind))
                    if (seen.Add(o.Request)) yield return CourtEvent(o, s, end, replay, players, turn);
            foreach (var o in outcomes.Where(o => o.Request.Moment == Moment.Council))
                if (seen.Add(o.Request)) yield return CourtEvent(o, s, end, replay, players, turn);
            foreach (var o in outcomes.Where(o => o.Request.Moment == Moment.Army))
            {
                if (!seen.Add(o.Request)) continue;
                var d = players.DecisionFor(o.Request);
                string id = o.Request.ArmyId;
                var e = new ReplayEvent { Turn = turn, Country = o.Request.CountryId, Army = id, Text = o.Text, Decision = d, Statement = d?.Statement ?? "" };
                var army = s.Army(id);
                if (o.Refused) e.Kind = ReplayEventKind.Refused;
                else
                {
                    e.Kind = ReplayEventKind.ArmyOrder;
                    if (army != null)
                    {
                        army.Wake.Clear();
                        army.Destination = o.Choice.Action == Actions.March ? o.Choice.Target : null;
                        army.Path = paths.TryGetValue(id, out var p) ? p.ToList() : new List<string>();
                    }
                }
                if (o.Choice.Action == Actions.March) { e.Province = o.Choice.Target; e.ProvinceIndex = replay.IndexOf(e.Province); }
                yield return Snap(e, s);
            }

            // d. Every army steps at once. Where it got to: its battle, or where the turn left it.
            var battles = tr.BattleRecords;
            var march = new ReplayEvent { Turn = turn, Kind = ReplayEventKind.March, Text = "The armies march" };
            foreach (var a in s.Armies)
            {
                var b = battles.FirstOrDefault(x => x.AttackerArmies.Contains(a.Id) || x.DefenderArmies.Contains(a.Id));
                var route = new ArmyRoute { Army = a.Id, Owner = a.Owner, From = a.Location };
                var plan = paths.TryGetValue(a.Id, out var pl) ? pl : a.Path;
                string reached;
                if (b != null && b.Province == null)
                {
                    // Met on a border, perhaps after a first step: it got to its own side, and went towards the other.
                    bool attacker = b.AttackerArmies.Contains(a.Id);
                    route.Towards = attacker ? b.Border?[1] : b.Border?[0];
                    reached = (attacker ? b.Border?[0] : b.Border?[1]) ?? a.Location;
                }
                else reached = b != null ? b.Province : end.Army(a.Id)?.Location;
                if (reached != null && reached != a.Location)
                {
                    int k = plan.IndexOf(reached);
                    // Not on its path: the rules sent it home after a peace, or it fell back; it is set down, not marched.
                    if (k >= 0)
                    {
                        route.Steps = plan.Take(k + 1).ToList();
                        a.Path = plan.Skip(k + 1).ToList();
                    }
                    a.Location = reached;
                    if (a.Path.Count == 0) a.Destination = null;
                }
                else if (route.Towards == null && a.Marching && reached == a.Location && plan.Count > 0) route.Towards = plan[0];
                if (route.Steps.Count > 0 || route.Towards != null) march.Routes.Add(route);
            }
            if (march.Routes.Count > 0) yield return Snap(march, s);

            // e. Battles, occupation, land taken and freed, armies lost, broken realms, the end: as the rules wrote them.
            int battle = 0;
            foreach (string line in tr.Resolution)
            {
                Match m;
                if (BattleLine.IsMatch(line) && battle < battles.Count)
                {
                    var b = battles[battle++];
                    var e = new ReplayEvent
                    {
                        Turn = turn, Kind = ReplayEventKind.Battle, Battle = b, Country = b.Attacker, Other = b.Defender, Text = b.Why ?? line,
                        Province = b.Province ?? b.Border?[1], Attack = b.AttackerSoldiers, Defence = b.DefenderSoldiers,
                    };
                    e.ProvinceIndex = replay.IndexOf(e.Province);
                    foreach (string id in b.AttackerArmies.Concat(b.DefenderArmies))
                    {
                        var a = s.Army(id);
                        if (a == null) continue;
                        var after = end.Army(id);
                        bool attacker = b.AttackerArmies.Contains(id);
                        if (b.Province == null && attacker == b.AttackerWon) a.Location = (attacker ? b.Border?[1] : b.Border?[0]) ?? a.Location;
                        if (b.Retreats.TryGetValue(id, out string back)) a.Location = back;
                        if (after != null) a.Soldiers = after.Soldiers;
                        a.Destination = null;
                        a.Path.Clear();
                    }
                    yield return Snap(e, s);
                }
                else if ((m = LostLine.Match(line)).Success)
                {
                    string id = m.Groups[1].Value;
                    var a = s.Army(id);
                    s.Armies.RemoveAll(x => x.Id == id);
                    yield return Snap(new ReplayEvent
                    {
                        Turn = turn, Kind = ReplayEventKind.ArmyLost, Army = id, Country = a?.Owner, Text = line,
                        Province = a?.Location, ProvinceIndex = a == null ? -1 : replay.IndexOf(a.Location),
                    }, s);
                }
                else if ((m = TookNeutral.Match(line)).Success)
                {
                    int i = replay.IndexOf(m.Groups[2].Value);
                    var e = new ReplayEvent { Turn = turn, Kind = ReplayEventKind.TakeNeutral, Country = m.Groups[1].Value, Province = m.Groups[2].Value, ProvinceIndex = i, Text = line };
                    var b = battles.LastOrDefault(x => x.Province == e.Province && x.Attacker == e.Country && x.DefenderKind == "garrison");
                    if (b != null) { e.Attack = b.AttackerSoldiers; e.Defence = b.DefenderSoldiers; }
                    s.Owners[i] = e.Country; s.Occupiers[i] = null; s.Garrisons[i] = 0;
                    yield return Snap(e, s);
                }
                else if ((m = FreedLine.Match(line)).Success)
                {
                    int i = replay.IndexOf(m.Groups[2].Value);
                    s.Occupiers[i] = null;
                    yield return Snap(new ReplayEvent { Turn = turn, Kind = ReplayEventKind.Free, Country = m.Groups[1].Value, Other = m.Groups[3].Value, Province = m.Groups[2].Value, ProvinceIndex = i, Text = line }, s);
                }
                else if ((m = OccupiesLine.Match(line)).Success)
                {
                    int i = replay.IndexOf(m.Groups[2].Value);
                    s.Occupiers[i] = m.Groups[1].Value;
                    yield return Snap(new ReplayEvent { Turn = turn, Kind = ReplayEventKind.Occupy, Country = m.Groups[1].Value, Other = m.Groups[3].Value, Province = m.Groups[2].Value, ProvinceIndex = i, Text = line }, s);
                }
                else if ((m = BrokenLine.Match(line)).Success)
                {
                    string c = m.Groups[1].Value;
                    var e = new ReplayEvent { Turn = turn, Kind = ReplayEventKind.Broken, Country = c, Text = line };
                    for (int i = 0; i < s.Owners.Length; i++)
                        if (s.Owners[i] == c) { s.Owners[i] = end.Owners[i]; s.Occupiers[i] = null; e.Ceded.Add(replay.ProvinceIds[i]); }
                    s.Armies.RemoveAll(a => a.Owner == c);
                    for (int i = 0; i < s.Occupiers.Length; i++) if (s.Occupiers[i] == c) s.Occupiers[i] = null;
                    OutOfTheGame(s, c);
                    if (e.Ceded.Count > 0) { e.Province = e.Ceded[0]; e.ProvinceIndex = replay.IndexOf(e.Province); }
                    yield return Snap(e, s);
                }
                else if ((m = OutOfGame.Match(line)).Success)
                {
                    // Out of the game: its armies go, and the land it occupied returns to its owners.
                    string c = m.Groups[1].Value;
                    s.Armies.RemoveAll(a => a.Owner == c);
                    for (int i = 0; i < s.Occupiers.Length; i++) if (s.Occupiers[i] == c) s.Occupiers[i] = null;
                    OutOfTheGame(s, c);
                    foreach (var k in s.WarScores.Keys.Where(k => k.StartsWith(c + "|", StringComparison.Ordinal) || k.EndsWith("|" + c, StringComparison.Ordinal)).ToList()) s.WarScores.Remove(k);
                    yield return Snap(new ReplayEvent { Turn = turn, Kind = ReplayEventKind.Eliminated, Country = c, Text = line }, s);
                }
                else if (StalemateLine.IsMatch(line))
                    yield return Snap(new ReplayEvent { Turn = turn, Kind = ReplayEventKind.Stalemate, Text = line }, s);
            }
        }

        // Out of the game: at war with nobody, and no longer alive.
        private static void OutOfTheGame(ReplayState s, string c)
        {
            s.Relations.RemoveAll(r => r.A == c || r.B == c);
            foreach (var k in s.WarScores.Keys.Where(k => k.StartsWith(c + "|", StringComparison.Ordinal) || k.EndsWith("|" + c, StringComparison.Ordinal)).ToList()) s.WarScores.Remove(k);
            if (s.Countries.TryGetValue(c, out var cs)) cs.Alive = false;
        }

        private static ReplayEvent CourtEvent(MoveOutcome o, ReplayState s, ReplayState end, ReplayGame replay, RecordedPlayers players, int turn)
        {
            var d = players.DecisionFor(o.Request);
            string me = o.Request.CountryId;
            var choice = o.Choice;
            var e = new ReplayEvent { Turn = turn, Country = me, Text = o.Text, Decision = d, Statement = d?.Statement ?? "" };
            if (o.Refused)
            {
                e.Kind = ReplayEventKind.Refused;
                e.Province = choice.Target != null && Actions.TargetsProvince(choice.Action) ? choice.Target : null;
                e.Other = choice.Target != null && Actions.TargetsCountry(choice.Action) ? choice.Target : null;
                e.ProvinceIndex = e.Province == null ? -1 : replay.IndexOf(e.Province);
                return Snap(e, s);
            }
            switch (choice.Action)
            {
                case Actions.DeclareWar:
                    e.Kind = ReplayEventKind.WarDeclared;
                    e.Other = choice.Target;
                    s.Relations.RemoveAll(r => Pair(r, me, choice.Target));
                    s.Relations.Add(new RelationState { A = me, B = choice.Target, Kind = "war", Since = turn });
                    break;
                case Actions.Recruit:
                {
                    e.Kind = ReplayEventKind.Recruit;
                    // "raised 15 soldiers at highmoor (maren_host +5, new tolly_host 10)": the armies it joined or formed.
                    int at = o.Text.IndexOf(" at ", StringComparison.Ordinal), open = o.Text.IndexOf(" (", StringComparison.Ordinal);
                    string site = at > 0 && open > at ? o.Text.Substring(at + 4, open - at - 4) : null;
                    e.Province = site;
                    e.ProvinceIndex = site == null ? -1 : replay.IndexOf(site);
                    if (open > 0)
                        foreach (Match m in Joined.Matches(o.Text.Substring(open)))
                        {
                            string id = m.Groups[2].Value;
                            int n = int.Parse(m.Groups[3].Value);
                            var a = s.Army(id);
                            if (m.Groups[1].Success || a == null)
                                s.Armies.Add(new ArmyState { Id = id, Owner = me, Location = site ?? end.Army(id)?.Location, Soldiers = n, Wake = new List<string> { "raised" } });
                            else a.Soldiers += n;
                        }
                    break;
                }
                case Actions.RaiseTaxes: e.Kind = ReplayEventKind.Taxes; break;
                case Actions.Fortify:
                    e.Kind = ReplayEventKind.Fort;
                    e.Province = choice.Target;
                    e.ProvinceIndex = replay.IndexOf(choice.Target);
                    if (e.ProvinceIndex >= 0) s.Forts[e.ProvinceIndex]++;
                    break;
                default: e.Kind = ReplayEventKind.Hold; break;
            }
            return Snap(e, s);
        }
    }
}
