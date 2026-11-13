using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy
{
    /// <summary>
    /// The writs turn of rules v2 (slice R1), when <see cref="GameRules.Turn"/> is
    /// <see cref="TurnStyle.Writs"/>. Upkeep, events, the storyteller's choices and victory are the
    /// one-move game's (<c>Game.cs</c>); what differs is how rulers order and how the turn lands.
    /// </summary>
    /// <remarks>
    /// <code>
    /// var requests = game.BeginTurn();                     // upkeep, then one court request a ruler
    /// while (requests.Count > 0)
    ///     requests = game.Submit(answers to requests);     // armies awaiting orders, a second court
    ///                                                      // order, then treaty answers
    /// var report = game.Resolve();                         // treaties, war, home, movement, battles
    /// </code>
    /// Every request of a round is built on the same snapshot: nothing lands until
    /// <see cref="Resolve"/>, and every order is re-checked when it lands, with the same function that
    /// offered it.
    /// </remarks>
    public sealed partial class Game
    {
        /// <summary>One ruler's turn of orders while it is being given.</summary>
        private sealed class Docket
        {
            public string Country;
            public int Writs;
            public int Used;
            public Move FirstCourt;
            public bool SecondAsked;
            public Queue<Army> Queue;
            public int ArmiesAsked;
            public readonly List<string> Ordered = new List<string>();
            public readonly List<CountryAnswer> Answers = new List<CountryAnswer>();
        }

        private sealed class Proposal
        {
            public string Id;
            public CountryAnswer Order;
            public string From;
            public string To;
            public bool Enforced;
            public CountryRequest Request;
            public CountryAnswer Answer;
            public bool Waits;
        }

        private Dictionary<string, Docket> dockets;
        private List<CountryRequest> openRequests;
        private List<Proposal> proposals;
        private int lastChangeTurn;

        /// <summary>The proposals now waiting for an answer (the screen and a log show them).</summary>
        public IReadOnlyList<CountryRequest> TreatyRequests => proposals?.Where(p => p.Request != null).Select(p => p.Request).ToList() ?? new List<CountryRequest>();

        // ------------------------------------------------------------------------------------
        // 1-2. Orders, a round at a time
        // ------------------------------------------------------------------------------------

        // Called from BeginTurn after upkeep and events, on the writs turn.
        private IReadOnlyList<CountryRequest> BeginWrits()
        {
            SyncArmies();
            foreach (var a in World.Armies)
                if (!a.Awaiting && World.Turn - a.OrderedOnTurn >= Rules.WakeAfterTurns)
                    a.Wake.Add(a.Marching ? "four turns on one march" : "four turns holding");
            dockets = World.Alive.ToDictionary(c => c.Id, c => new Docket { Country = c.Id, Writs = World.Writs(c.Id) }, StringComparer.Ordinal);
            proposals = null;
            openRequests = World.Alive.Select(c => CourtRequest(dockets[c.Id])).ToList();
            phase = Phase.AwaitingCouncil;
            return openRequests;
        }

        /// <summary>
        /// The writs turn: takes the answers to the last round's requests and returns the next round:
        /// each ruler's next army awaiting orders or its second court order, then the treaty answers.
        /// An empty list means every order is in; then call <see cref="Resolve"/>. A request with no
        /// answer holds.
        /// </summary>
        public IReadOnlyList<CountryRequest> Submit(IEnumerable<CountryAnswer> answers)
        {
            if (Rules.Turn != TurnStyle.Writs) throw new InvalidOperationException("Submit is the writs turn's; the one-move turn uses SubmitCouncil.");
            var given = (answers ?? Enumerable.Empty<CountryAnswer>()).Where(a => a?.Request != null)
                .GroupBy(a => a.Request).ToDictionary(g => g.Key, g => g.First());

            if (phase == Phase.AwaitingEnvoys)
            {
                foreach (var p in proposals.Where(p => p.Request != null))
                    if (given.TryGetValue(p.Request, out var a)) p.Answer = a;
                return new List<CountryRequest>();
            }
            Expect(Phase.AwaitingCouncil);

            var next = new List<CountryRequest>();
            foreach (var request in openRequests)
            {
                var d = dockets[request.CountryId];
                var answer = given.TryGetValue(request, out var a) ? new CountryAnswer(request, a.Choice ?? Move.Hold, a.Note) : new CountryAnswer(request, Move.Hold);
                d.Answers.Add(answer);
                bool court = request.Moment == Moment.Council;
                if (court && d.FirstCourt == null)
                {
                    d.FirstCourt = answer.Choice;
                    d.Queue = new Queue<Army>(WritLegality.Awaiting(World, d.Country));
                }
                if (!(court && answer.Choice.Action == Actions.None)) d.Used++;
                d.Ordered.Add(request.ArmyId == null ? answer.Choice.ToString()
                    : answer.Choice.Action == Actions.March ? $"{request.ArmyId} to march to {answer.Choice.Target}" : $"{request.ArmyId} to hold");

                var following = NextRequest(d);
                if (following != null) next.Add(following);
            }
            openRequests = next;
            if (next.Count > 0) return next;

            CollectProposals();
            phase = Phase.AwaitingEnvoys;
            return proposals.Where(p => p.Request != null).Select(p => p.Request).ToList();
        }

        private CountryRequest NextRequest(Docket d)
        {
            // Armies awaiting orders, while writs are left beyond one kept for the court.
            while (d.Queue.Count > 0 && d.ArmiesAsked < d.Writs - 1 && d.Used < d.Writs)
            {
                var army = d.Queue.Dequeue();
                if (World.FindArmy(army.Id) == null) continue;
                d.ArmiesAsked++;
                return ArmyRequest(d, army);
            }
            if (!d.SecondAsked && d.FirstCourt != null && d.FirstCourt.Action != Actions.None && d.Used < d.Writs)
            {
                d.SecondAsked = true;
                return CourtRequest(d);
            }
            return null;
        }

        private CountryRequest CourtRequest(Docket d) =>
            new CountryRequest(Moment.Council, World.Turn, d.Country, World,
                Facts.Standing(World, d.Country), Facts.Select(pending[d.Country]), WritLegality.CourtMoves(World, d.Country))
            {
                Ordered = d.Ordered.ToList(),
                Writ = d.Used + 1,
                Writs = d.Writs,
            };

        private CountryRequest ArmyRequest(Docket d, Army army)
        {
            var facts = new List<string> { $"{army.Id}: {army.Soldiers} soldiers at {army.Location}; " + (army.Wake.Count > 0 ? "awaiting orders: " + string.Join(", ", army.Wake) : "") + "." };
            facts.AddRange(Facts.Select(pending[d.Country], Facts.MaxTurnFacts - 1));
            return new CountryRequest(Moment.Army, World.Turn, d.Country, World,
                Facts.Standing(World, d.Country), facts, WritLegality.ArmyMoves(World, d.Country, army.Id))
            {
                ArmyId = army.Id,
                Ordered = d.Ordered.ToList(),
                Writ = d.Used + 1,
                Writs = d.Writs,
            };
        }

        // Every treaty term ordered this turn becomes a proposal: enforced at a war score of 90, else
        // put to the other side, at most two a ruler; more wait.
        private void CollectProposals()
        {
            proposals = new List<Proposal>();
            var answering = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string id in RotatedOrder())
            {
                if (!dockets.TryGetValue(id, out var d)) continue;
                foreach (var a in d.Answers.Where(a => a.Request.Moment == Moment.Council && Actions.IsTerm(a.Choice.Action) && IsOffered(a)))
                {
                    var p = new Proposal
                    {
                        Id = $"t{World.Turn}-{id}-{a.Choice.Action}-{a.Choice.Target}",
                        Order = a,
                        From = id,
                        To = a.Choice.Target,
                    };
                    p.Enforced = a.Choice.Action == Actions.DemandOccupied && WarScore.Of(World, id, p.To) >= Rules.EnforcedScore;
                    if (!p.Enforced)
                    {
                        answering.TryGetValue(p.To, out int n);
                        if (n >= Rules.MaxTreatyAnswers) p.Waits = true;
                        else
                        {
                            answering[p.To] = n + 1;
                            var turnFacts = new List<string> { TermLine(p.From, a.Choice) };
                            turnFacts.AddRange(Facts.Select(pending[p.To], Facts.MaxTurnFacts - 1));
                            p.Request = new CountryRequest(Moment.Envoy, World.Turn, p.To, World,
                                Facts.Standing(World, p.To), turnFacts, Legality.EnvoyMoves(), p.Id, p.From);
                        }
                    }
                    proposals.Add(p);
                }
            }
        }

        /// <summary>A term in words, as the side asked reads it: "orsk offers white peace: all occupied land returns."</summary>
        public string TermLine(string from, Move term)
        {
            int score = WarScore.Of(World, from, term.Target);
            switch (term.Action)
            {
                case Actions.OfferWhitePeace:
                    return $"{from} offers white peace: all occupied land returns, and a truce for {Rules.TruceTurns} turns. War score {-score:+0;-0;0} for us.";
                case Actions.DemandOccupied:
                    var taken = World.ProvinceList.Where(p => p.Owner == term.Target && p.Occupier == from).Select(p => p.Id).ToList();
                    return $"{from} demands what it occupies of ours ({string.Join(", ", taken)}); ours of theirs returns. War score {-score:+0;-0;0} for us.";
                default:
                    var ceded = World.ProvinceList.Where(p => p.Owner == from && p.Occupier == term.Target).Select(p => p.Id).ToList();
                    return $"{from} offers us what we occupy of theirs ({string.Join(", ", ceded)}) for peace. War score {-score:+0;-0;0} for us.";
            }
        }

        // ------------------------------------------------------------------------------------
        // 4. Resolution
        // ------------------------------------------------------------------------------------

        private TurnReport ResolveWrits(IEnumerable<CountryAnswer> treatyAnswers)
        {
            Expect(Phase.AwaitingEnvoys);
            if (treatyAnswers != null) Submit(treatyAnswers);
            foreach (var c in World.CountryList) pending[c.Id].Clear();
            var order = RotatedOrder();
            var all = order.Where(dockets.ContainsKey).SelectMany(id => dockets[id].Answers).ToList();
            var landed = new HashSet<CountryAnswer>();

            // Illegal on the snapshot itself: a player chose something it was not offered.
            foreach (var a in all.Where(a => !IsOffered(a)))
            {
                landed.Add(a);
                report.Outcomes.Add(new MoveOutcome(a.Request, a.Choice, Move.Hold, true, $"{a.Choice} was not offered; held instead"));
                report.Resolution.Add($"{a.Request.CountryId} chose {a.Choice}, which it was not offered, and held instead.");
            }

            // a. Diplomacy: treaties, then declarations of war.
            ResolveTreaties(landed);
            foreach (var a in all.Where(a => !landed.Contains(a) && a.Choice.Action == Actions.DeclareWar))
            {
                landed.Add(a);
                if (Recheck(a) is string why) { Refuse(a, why); continue; }
                string me = a.Request.CountryId, them = a.Choice.Target;
                World.SetRelation(me, them, Relation.War);
                World.War(me, them).Reset(World.Turn);
                foreach (var army in World.Armies.Where(x => x.Owner == me || x.Owner == them)) army.Wake.Add("war declared");
                Done(a, $"declared war on {them}");
                Tell(them, 0, $"{me} declared war on us.");
            }
            SendHomeArmiesInPeacefulLand();

            // b. Home: recruits appear, taxes, forts.
            foreach (string action in new[] { Actions.Recruit, Actions.RaiseTaxes, Actions.Fortify })
                foreach (var a in all.Where(a => !landed.Contains(a) && a.Choice.Action == action && a.Request.Moment == Moment.Council))
                {
                    landed.Add(a);
                    if (Recheck(a) is string why) { Refuse(a, why); continue; }
                    LandHome(a);
                }

            // c. Army orders take effect, then every marching army steps at the same moment.
            foreach (var a in all.Where(a => !landed.Contains(a) && a.Request.Moment == Moment.Army))
            {
                landed.Add(a);
                if (Recheck(a) is string why) { Refuse(a, why); continue; }
                var army = World.FindArmy(a.Request.ArmyId);
                army.Wake.Clear();
                army.OrderedOnTurn = World.Turn;
                army.Destination = null;
                army.Path.Clear();
                if (a.Choice.Action == Actions.March)
                {
                    army.Destination = a.Choice.Target;
                    army.Path.AddRange(WritLegality.Paths(World, army.Owner, army.Location, Rules.MarchRange)[a.Choice.Target]);
                    army.StepTurnsLeft = Rules.TurnsToEnter(World.Province(army.Path[0]).Info.Terrain);
                    Done(a, $"{army.Id} marches to {army.Destination}");
                }
                else Done(a, $"{army.Id} holds at {army.Location}");
            }
            foreach (var a in all.Where(a => !landed.Contains(a)))
            {
                Gate(a.Request, a.Choice);
                report.Outcomes.Add(new MoveOutcome(a.Request, a.Choice, a.Choice, false, a.Choice.Action == Actions.None ? "held" : "done"));
            }

            MoveArmies();
            // d-e. Battles wherever enemies now share a province, then occupation and taking land.
            foreach (var p in World.ProvinceList.OrderBy(p => p.Id, StringComparer.Ordinal)) FightIn(p, order);
            foreach (var p in World.ProvinceList.OrderBy(p => p.Id, StringComparer.Ordinal)) Settle(p, order);
            SendHomeArmiesInPeacefulLand();

            // 5. Broken realms, eliminations, victory.
            BreakRealms();
            foreach (var c in World.Alive.ToList())
            {
                if (World.ProvinceCount(c.Id) > 0) continue;
                Eliminate(c, $"{c.Id} has lost its last province and is out of the game.");
            }
            SyncArmies();

            var notes = all.Concat(proposals.Where(p => p.Answer != null).Select(p => p.Answer)).Where(a => a.Note != null)
                .GroupBy(a => a.Request).ToDictionary(g => g.Key, g => g.First().Note);
            foreach (var o in report.Outcomes)
                if (o.Request != null && notes.TryGetValue(o.Request, out string note)) o.Note = note;

            report.OwnerGrid = World.OwnerGrid();
            foreach (var c in World.CountryList) report.ProvinceCounts[c.Id] = World.ProvinceCount(c.Id);
            Reports.Add(report);
            CheckForWinner();
            if (Result == null && Rules.StalemateTurns > 0 && World.Turn - lastChangeTurn >= Rules.StalemateTurns)
            {
                var counts = World.CountryList.ToDictionary(c => c.Id, c => World.ProvinceCount(c.Id), StringComparer.Ordinal);
                var leader = World.Alive.OrderByDescending(c => counts[c.Id]).ThenByDescending(c => c.Army).ThenByDescending(c => c.Gold).First();
                Result = new GameResult(leader.Id, Ending.Stalemate, World.Turn, counts);
                report.Resolution.Add($"No province changed hands for {Rules.StalemateTurns} turns: a stalemate.");
            }
            phase = IsOver ? Phase.Over : Phase.AwaitingTurn;
            return report;
        }

        // The same check that offered the order, on the world as it now stands, then the seat's gate.
        private string Recheck(CountryAnswer a)
        {
            string id = a.Request.CountryId;
            string why = a.Request.Moment == Moment.Army
                ? WritLegality.WhyNotArmy(World, id, a.Request.ArmyId, a.Choice)
                : WritLegality.WhyNotCourt(World, id, a.Choice);
            return why ?? Gate(a.Request, a.Choice);
        }

        private void ResolveTreaties(HashSet<CountryAnswer> landed)
        {
            // A pair whose two sides both proposed and both had theirs accepted: the higher war score's lands.
            var accepted = new List<Proposal>();
            foreach (var p in proposals)
            {
                landed.Add(p.Order);
                if (p.Waits)
                {
                    report.Outcomes.Add(new MoveOutcome(p.Order.Request, p.Order.Choice, p.Order.Choice, false, $"{p.To} has two proposals to answer already: it waits"));
                    Tell(p.From, 2, $"{p.To} could not answer our {p.Order.Choice.Action} this turn.");
                    continue;
                }
                if (p.Enforced) { accepted.Add(p); continue; }
                var reply = p.Answer?.Choice ?? new Move(Actions.RefusePeace);
                if (p.Answer != null && Gate(p.Request, reply) != null) reply = new Move(Actions.RefusePeace);
                bool yes = reply.Action == Actions.AcceptPeace;
                report.Outcomes.Add(new MoveOutcome(p.Request, reply, yes ? reply : new Move(Actions.RefusePeace), false,
                    yes ? $"accepted {p.From}'s {p.Order.Choice.Action}" : $"refused {p.From}'s {p.Order.Choice.Action}"));
                if (yes) accepted.Add(p);
                else
                {
                    report.Outcomes.Add(new MoveOutcome(p.Order.Request, p.Order.Choice, p.Order.Choice, false, $"{p.To} refused"));
                    report.Resolution.Add($"{p.To} refused {p.From}'s {p.Order.Choice.Action}.");
                    Tell(p.From, 1, $"{p.To} refused our {Phrase(p.Order.Choice)}.");
                }
            }
            var done = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in accepted.OrderByDescending(p => WarScore.Of(World, p.From, p.To)).ThenBy(p => proposals.IndexOf(p)))
            {
                string pair = World.PairKey(p.From, p.To);
                if (done.Contains(pair))
                {
                    report.Outcomes.Add(new MoveOutcome(p.Order.Request, p.Order.Choice, Move.Hold, false, "the other side's treaty landed first"));
                    continue;
                }
                string why = WritLegality.WhyNotCourt(World, p.From, p.Order.Choice) ?? Gate(p.Order.Request, p.Order.Choice);
                if (why != null) { Refuse(p.Order, why); continue; }
                done.Add(pair);
                ApplyTreaty(p.From, p.Order.Choice, p.Enforced);
                Done(p.Order, (p.Enforced ? "enforced " : "") + $"{p.Order.Choice.Action} on {p.To}");
            }
        }

        private void ApplyTreaty(string from, Move term, bool enforced)
        {
            string to = term.Target;
            int moved = 0;
            foreach (var p in World.ProvinceList)
            {
                bool fromHoldsTheirs = p.Owner == to && p.Occupier == from;
                bool toHoldsOurs = p.Owner == from && p.Occupier == to;
                if (!fromHoldsTheirs && !toHoldsOurs) continue;
                if (term.Action == Actions.DemandOccupied && fromHoldsTheirs) { p.Owner = from; moved++; }
                else if (term.Action == Actions.CedeOccupied && toHoldsOurs) { p.Owner = to; moved++; }
                p.Occupier = null;
                lastChangeTurn = World.Turn;
            }
            report.TerritoryChanges += moved;
            World.SetRelation(from, to, Relation.Truce, Rules.TruceTurns);
            report.PeaceAccepted++;
            foreach (var army in World.Armies.Where(x => x.Owner == from || x.Owner == to)) army.Wake.Add("peace made");
            string what = term.Action == Actions.OfferWhitePeace ? "white peace: all occupied land returned"
                : term.Action == Actions.DemandOccupied ? $"{to} ceded {moved} provinces {from} occupied"
                : $"{from} ceded {moved} provinces {to} occupied";
            report.Resolution.Add($"{(enforced ? "Enforced treaty" : "Treaty")} between {from} and {to}: {what}; a truce for {Rules.TruceTurns} turns.");
            Tell(from, 0, $"Peace with {to}: {what}.");
            Tell(to, 0, $"Peace with {from}: {what}.");
        }

        private void LandHome(CountryAnswer a)
        {
            var me = World.Country(a.Request.CountryId);
            var r = Rules;
            switch (a.Choice.Action)
            {
                case Actions.Recruit:
                {
                    int levy = World.Levy(me);
                    me.Gold -= levy * r.GoldPerSoldier;
                    me.Army += levy;
                    var site = WritLegality.RecruitSite(World, me.Id);
                    var joined = Reinforce(me.Id, site.Id, levy);
                    Done(a, $"raised {levy} soldiers at {site.Id} ({joined})");
                    break;
                }
                case Actions.RaiseTaxes:
                    me.Gold += r.TaxGold;
                    me.Stability--;
                    me.RaisedTaxes = true;
                    Done(a, $"raised taxes, +{r.TaxGold} gold, stability {me.Stability}");
                    break;
                case Actions.Fortify:
                {
                    var p = World.Province(a.Choice.Target);
                    me.Gold -= r.FortCost;
                    p.Forts++;
                    Done(a, $"built a fort at {p.Id} ({p.Forts})");
                    break;
                }
            }
        }

        // Soldiers join the armies standing at a province, up to the army cap; the rest form new armies.
        private string Reinforce(string countryId, string provinceId, int soldiers)
        {
            var names = new List<string>();
            foreach (var army in World.ArmiesAt(provinceId).Where(x => x.Owner == countryId && !x.Marching).OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                if (soldiers <= 0) break;
                int add = Math.Min(soldiers, Rules.ArmyMax - army.Soldiers);
                if (add <= 0) continue;
                army.Soldiers += add;
                soldiers -= add;
                if (!army.Wake.Contains("reinforced")) army.Wake.Add("reinforced");
                names.Add($"{army.Id} +{add}");
            }
            while (soldiers > 0)
            {
                int size = Math.Min(soldiers, Rules.ArmyMax);
                var army = new Army(ArmyNames.Next(World, countryId), countryId, provinceId, size, World.Turn);
                World.Armies.Add(army);
                soldiers -= size;
                names.Add($"new {army.Id} {size}");
            }
            return string.Join(", ", names);
        }

        // ------------------------------------------------------------------------------------
        // Movement: every marching army steps at once
        // ------------------------------------------------------------------------------------

        private void MoveArmies()
        {
            foreach (var a in World.Armies) a.CameFrom = a.Location;
            var stepping = new List<(Army army, string to)>();
            foreach (var a in World.Armies.Where(x => x.Marching).OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                if (a.StepTurnsLeft > 1) { a.StepTurnsLeft--; continue; }
                string to = a.Path[0];
                if (!WritLegality.Passable(World, a.Owner, World.Province(to)))
                {
                    a.Stop("its path is blocked");
                    report.Resolution.Add($"{a.Id}'s march stopped: {to} is closed to {a.Owner}.");
                    continue;
                }
                stepping.Add((a, to));
            }

            // Two enemy armies crossing one border in opposite directions meet on it; the winner goes on.
            var stopped = new HashSet<Army>();
            foreach (var (a, to) in stepping)
            {
                if (stopped.Contains(a)) continue;
                var against = stepping.Where(s => s.army.Location == to && s.to == a.Location && World.AtWar(a.Owner, s.army.Owner) && !stopped.Contains(s.army)).ToList();
                if (against.Count == 0) continue;
                var mine = stepping.Where(s => s.army.Owner == a.Owner && s.army.Location == a.Location && s.to == to).Select(s => s.army).ToList();
                var theirs = against.Select(s => s.army).ToList();
                bool weWon = Battle($"the {a.Location}-{to} border", a.Owner, mine, theirs[0].Owner, theirs, 0, null, onBorder: true);
                foreach (var loser in weWon ? theirs : mine) stopped.Add(loser);
            }

            foreach (var (a, to) in stepping)
            {
                if (stopped.Contains(a) || World.FindArmy(a.Id) == null) continue;
                a.Location = to;
                if (a.Path.Count == 0) continue; // won a battle on the border: it completes its step, then waits for orders
                a.Path.RemoveAt(0);
                if (a.Path.Count == 0)
                {
                    a.Destination = null;
                    a.Wake.Add("arrived");
                }
                else a.StepTurnsLeft = Rules.TurnsToEnter(World.Province(a.Path[0]).Info.Terrain);
            }

            // An enemy army now stands beside one of ours: ours asks for orders.
            foreach (var a in World.Armies)
                if (World.Armies.Any(o => o.Location != o.CameFrom && World.AtWar(a.Owner, o.Owner)
                                          && (o.Location == a.Location || World.Map.AreNeighbours(o.Location, a.Location)))
                    && !a.Wake.Contains("an enemy army came near"))
                    a.Wake.Add("an enemy army came near");
        }

        // ------------------------------------------------------------------------------------
        // Battles
        // ------------------------------------------------------------------------------------

        // Enemies sharing a province fight until no two hostile sides remain: the side that holds the
        // province (or was there first) defends, the others attack in turn.
        private void FightIn(Province p, List<string> order)
        {
            for (int guard = 0; guard < 16; guard++)
            {
                var here = World.ArmiesAt(p.Id).ToList();
                var sides = order.Where(s => here.Any(a => a.Owner == s)).ToList();
                var fighting = sides.Where(s => sides.Any(o => o != s && World.AtWar(s, o))).ToList();
                if (fighting.Count < 2) return;
                string defender = fighting.FirstOrDefault(s => s == p.Controller)
                                  ?? fighting.FirstOrDefault(s => here.Any(a => a.Owner == s && a.CameFrom == p.Id))
                                  ?? fighting[0];
                string attacker = fighting.First(s => s != defender && World.AtWar(s, defender));
                bool holds = defender == p.Controller;
                Battle(p.Id, attacker, here.Where(a => a.Owner == attacker).ToList(), defender, here.Where(a => a.Owner == defender).ToList(), 0, holds ? p : null);
            }
        }

        // After the battles, land with one side's army alone in it: neutral land is fought for and
        // taken; an enemy's undefended land is occupied after its militia; our own occupied land is freed.
        private void Settle(Province p, List<string> order)
        {
            var here = World.ArmiesAt(p.Id).ToList();
            if (here.Count == 0) return;
            bool Acts(string s) =>
                p.Owner == null                                                                  // neutral land to take
                || (p.Owner == s && p.Occupier != null && !here.Any(a => a.Owner == p.Occupier)) // our land to free
                || (p.Controller != s && World.AtWar(s, p.Controller));                         // enemy land to occupy
            string who = order.FirstOrDefault(s => here.Any(a => a.Owner == s) && Acts(s));
            if (who == null) return;
            var mine = here.Where(a => a.Owner == who).ToList();

            if (p.Owner == null)
            {
                if (p.Garrison > 0 && !Battle(p.Id, who, mine, null, new List<Army>(), p.Garrison, p)) return;
                p.Owner = who;
                p.Garrison = 0;
                p.Occupier = null;
                report.TerritoryChanges++;
                lastChangeTurn = World.Turn;
                report.Resolution.Add($"{who} took {p.Id}, neutral land.");
                Tell(who, 1, $"We took {p.Id}.");
                return;
            }
            if (p.Owner == who)
            {
                report.Resolution.Add($"{who} freed {p.Id} from {p.Occupier}.");
                Tell(p.Occupier, 1, $"{who} freed {p.Id} from our occupation.");
                Tell(who, 1, $"We freed {p.Id}.");
                p.Occupier = null;
                lastChangeTurn = World.Turn;
                return;
            }
            int militia = Rules.MilitiaBase + Rules.MilitiaPerFort * p.Forts;
            string holder = p.Controller;
            if (!Battle(p.Id, who, mine, holder, new List<Army>(), militia, p)) return;
            p.Occupier = who;
            lastChangeTurn = World.Turn;
            report.Resolution.Add($"{who} occupies {p.Id} ({p.Owner}'s).");
            Tell(who, 1, $"We occupy {p.Id}, {p.Owner}'s.");
            Tell(p.Owner, 0, $"{who} occupies our {p.Id}.");
        }

        /// <summary>
        /// One battle, today's formula: each side's soldiers (and a garrison or militia) times a roll,
        /// the holder of a province adding its forts and capital; each side loses 30% of the other's
        /// strength, the winner 60% of that. The loser falls back, or is routed. Returns whether the
        /// attacker won.
        /// </summary>
        private bool Battle(string where, string attackerId, List<Army> attackers, string defenderId, List<Army> defenders, int fixedDefence, Province held, bool onBorder = false)
        {
            var r = Rules;
            string key = where + ":" + World.Turn;
            int att = attackers.Sum(a => a.Soldiers);
            int def = defenders.Sum(a => a.Soldiers) + fixedDefence;
            double attack = att * Dice.Range(Seed, World.Turn, attackerId, "battle-attack:" + key, r.RollMin, r.RollMax);
            double defence = def * Dice.Range(Seed, World.Turn, defenderId ?? "neutral", "battle-defence:" + key, r.RollMin, r.RollMax)
                             + (held != null ? held.Forts * r.FortBonus + (held.IsCapital ? r.CapitalBonus : 0) : 0);
            bool attackerWon = attack > defence;
            int attLoss = Math.Min(att, (int)Math.Round(defence * r.CasualtyRate * (attackerWon ? r.HeldCasualtyFactor : 1)));
            int defLoss = Math.Min(def, (int)Math.Round(attack * r.CasualtyRate * (attackerWon ? 1 : r.HeldCasualtyFactor)));
            Lose(attackers, attLoss);
            int armyLoss = Math.Min(defenders.Sum(a => a.Soldiers), defLoss);
            Lose(defenders, armyLoss);
            if (held != null && held.Owner == null) held.Garrison = Math.Max(0, held.Garrison - (defLoss - armyLoss));
            report.Battles++;

            string defName = defenderId ?? "the garrison";
            if (defenders.Count == 0 && defenderId != null) defName = $"{defenderId}'s militia";
            report.Resolution.Add($"Battle at {where}: {attackerId} {att} against {defName} {def}; " +
                                  (attackerWon ? $"{attackerId} won" : $"{defName} held") + $" ({attLoss} and {defLoss} lost).");
            if (defenderId != null)
            {
                var war = World.War(attackerId, defenderId);
                string winner = attackerWon ? attackerId : defenderId;
                war.BattlesWon[winner] = war.Won(winner) + 1;
                Tell(defenderId, 1, attackerWon ? $"We lost a battle at {where} to {attackerId}." : $"We held {where} against {attackerId}.");
            }
            Tell(attackerId, 1, attackerWon ? $"We won a battle at {where}." : $"We were beaten at {where}.");

            foreach (var a in attackers.Concat(defenders).Where(a => World.FindArmy(a.Id) != null)) a.Stop("fought a battle");
            var losers = attackerWon ? defenders : attackers;
            double loserStrength = attackerWon ? defence : attack, winnerStrength = attackerWon ? attack : defence;
            bool rout = loserStrength * r.RoutRatio < winnerStrength;
            foreach (var a in losers.Where(a => World.FindArmy(a.Id) != null).ToList())
            {
                // On a border the loser never crosses; the winner steps into the province it was leaving,
                // so it is beaten off that too and falls back, without a second battle (our reading of v2 1.1).
                string back = rout ? null : RetreatTo(a, attackerWon || onBorder ? null : a.CameFrom);
                if (back == null)
                {
                    Disband(a, $"{a.Id} was routed at {where}");
                    continue;
                }
                a.Location = back;
            }
            return attackerWon;
        }

        // Where a beaten army falls back: where it came from this turn, or else a bordering province it controls.
        private string RetreatTo(Army a, string cameFrom)
        {
            if (cameFrom != null && cameFrom != a.Location && WritLegality.Passable(World, a.Owner, World.Province(cameFrom))) return cameFrom;
            return World.Map.Neighbours(a.Location).Select(World.Province)
                .Where(p => p.Controller == a.Owner && !World.ArmiesAt(p.Id).Any(o => World.AtWar(o.Owner, a.Owner)))
                .Select(p => p.Id).OrderBy(id => id, StringComparer.Ordinal).FirstOrDefault();
        }

        // Losses spread over the armies in proportion to their size, the remainder from the largest.
        private void Lose(List<Army> armies, int loss)
        {
            int total = armies.Sum(a => a.Soldiers);
            if (total == 0 || loss <= 0) return;
            int left = loss;
            foreach (var a in armies.OrderByDescending(a => a.Soldiers).ThenBy(a => a.Id, StringComparer.Ordinal))
            {
                int share = Math.Min(a.Soldiers, loss * a.Soldiers / total);
                a.Soldiers -= share;
                left -= share;
            }
            foreach (var a in armies.OrderByDescending(a => a.Soldiers).ThenBy(a => a.Id, StringComparer.Ordinal))
            {
                if (left <= 0) break;
                int more = Math.Min(a.Soldiers, left);
                a.Soldiers -= more;
                left -= more;
            }
            foreach (var a in armies.Where(a => a.Soldiers <= 0).ToList()) Disband(a, $"{a.Id} was destroyed");
            foreach (string owner in armies.Select(a => a.Owner).Distinct())
                World.Country(owner).Army = World.ArmiesOf(owner).Sum(a => a.Soldiers);
        }

        private void Disband(Army a, string why)
        {
            World.Armies.Remove(a);
            World.RetiredArmyIds.Add(a.Id);
            var c = World.Country(a.Owner);
            c.Army = World.ArmiesOf(a.Owner).Sum(x => x.Soldiers);
            report.Resolution.Add(why + ".");
            Tell(a.Owner, 0, why + ".");
        }

        // ------------------------------------------------------------------------------------
        // Peace, broken realms, eliminations, and keeping armies and totals in step
        // ------------------------------------------------------------------------------------

        // Armies left in land they may no longer stand in (after a treaty) go to the nearest land of theirs.
        private void SendHomeArmiesInPeacefulLand()
        {
            foreach (var a in World.Armies.ToList())
            {
                if (WritLegality.Passable(World, a.Owner, World.Province(a.Location))) continue;
                var home = NearestControlled(a.Owner, a.Location);
                if (home == null) { Disband(a, $"{a.Id} had nowhere to go and disbanded"); continue; }
                a.Location = home;
                a.Stop("sent home after peace");
            }
        }

        private string NearestControlled(string countryId, string from)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { from };
            var queue = new Queue<string>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                string at = queue.Dequeue();
                if (at != from && World.Province(at).Controller == countryId) return at;
                foreach (string n in World.Map.Neighbours(at))
                    if (seen.Add(n)) queue.Enqueue(n);
            }
            return null;
        }

        // A realm whose every province is occupied, with no army left, is broken: its occupiers take what they hold.
        private void BreakRealms()
        {
            foreach (var c in World.Alive.ToList())
            {
                var owned = World.ProvincesOf(c.Id).ToList();
                if (owned.Count == 0 || owned.Any(p => p.Occupier == null) || World.ArmiesOf(c.Id).Any()) continue;
                foreach (var p in owned)
                {
                    p.Owner = p.Occupier;
                    p.Occupier = null;
                    report.TerritoryChanges++;
                }
                lastChangeTurn = World.Turn;
                Eliminate(c, $"{c.Id}'s realm is broken: every province occupied, no army left. Its occupiers keep what they hold.");
            }
        }

        private void Eliminate(Country c, string line)
        {
            c.Alive = false;
            c.EliminatedOnTurn = World.Turn;
            foreach (var a in World.ArmiesOf(c.Id).ToList()) { World.Armies.Remove(a); World.RetiredArmyIds.Add(a.Id); }
            c.Army = 0;
            foreach (var p in World.ProvinceList.Where(p => p.Occupier == c.Id)) p.Occupier = null;
            report.Resolution.Add(line);
        }

        /// <summary>
        /// Keeps the armies and the country's soldier count in step. Upkeep, desertion and the
        /// storyteller's events change <see cref="Country.Army"/>; the change lands on the armies: a gain
        /// at the recruiting site, a loss spread over every army.
        /// </summary>
        private void SyncArmies()
        {
            foreach (var c in World.CountryList)
            {
                if (!c.Alive) { foreach (var a in World.ArmiesOf(c.Id).ToList()) { World.Armies.Remove(a); World.RetiredArmyIds.Add(a.Id); } continue; }
                var armies = World.ArmiesOf(c.Id).ToList();
                int total = armies.Sum(a => a.Soldiers);
                int delta = c.Army - total;
                if (delta > 0)
                {
                    var site = WritLegality.RecruitSite(World, c.Id);
                    string at = site?.Id ?? armies.OrderByDescending(a => a.Soldiers).Select(a => a.Location).FirstOrDefault();
                    if (at != null) Reinforce(c.Id, at, delta);
                }
                else if (delta < 0)
                {
                    int loss = -delta;
                    foreach (var a in armies.OrderByDescending(a => a.Soldiers).ThenBy(a => a.Id, StringComparer.Ordinal))
                    {
                        int share = Math.Min(a.Soldiers, (int)Math.Ceiling(loss * (double)a.Soldiers / Math.Max(1, total)));
                        share = Math.Min(share, -delta - (total - armies.Sum(x => x.Soldiers)));
                        a.Soldiers -= share;
                    }
                    foreach (var a in armies.Where(a => a.Soldiers <= 0)) { World.Armies.Remove(a); World.RetiredArmyIds.Add(a.Id); }
                }
                c.Army = World.ArmiesOf(c.Id).Sum(a => a.Soldiers);
            }
        }
    }
}
