using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Agenerela;
using Agenerela.Providers;
using GreyBoxStrategy.Replay;
using UnityEngine;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// Rule mode on the screen: you rule one country, four model rulers the others. The game waits
    /// for your order in the orders panel while the rulers decide; a peace offer to you opens the
    /// envoy card; the turn then plays back as in watch mode. Fog of war: <see cref="Fog"/>.
    /// </summary>
    public sealed partial class ReplayDirector
    {
        private RuleSession rule;

        private readonly List<ScreenUI.EdgeMark> edgeItems = new List<ScreenUI.EdgeMark>();

        /// <summary>
        /// Rule mode: markers at the map's edge for the last turn's battles and your capital, when they are off screen.
        /// Not when watching: there the camera follows the story.
        /// </summary>
        private void PlaceEdgeMarks()
        {
            if (ui == null) return;
            if (rule == null || shown == null) { if (ui.EdgeMarksShown > 0) ui.PlaceEdgeMarks(null); return; }
            edgeItems.Clear();
            int cap = ui.CapitalIndex(rule.You);
            if (cap >= 0 && shown.Owners[cap] == rule.You)
                edgeItems.Add(new ScreenUI.EdgeMark { Kind = "capital", Province = cap, Colour = CountryLook.Of(rule.You).Colour, Text = "Your capital, " + Wording.Province(game.ProvinceIds[cap]) });
            int turn = played.Count > 0 ? played[played.Count - 1].Turn : -1;
            foreach (var e in played)
                if (e.Kind == ReplayEventKind.Battle && e.Turn == turn && e.ProvinceIndex >= 0)
                    edgeItems.Add(new ScreenUI.EdgeMark { Kind = "battle", Province = e.ProvinceIndex, Event = e, Colour = new Color(0.86f, 0.3f, 0.22f), Text = "Battle at " + Wording.Province(game.ProvinceIds[e.ProvinceIndex]) });
            ui.PlaceEdgeMarks(edgeItems);
        }
        private CardLock ruleLock;
        private int builtResolved = -1;
        private string lastOrdersStatus;
        private CountryRequest lastOffer;

        /// <summary>For the Unity CLI: the rule game, or null.</summary>
        public RuleSession Rule => rule;
        public string RuleStatus => rule == null ? "no rule game" : $"{rule.Status}, {rule.Resolved} turns resolved, playing turn {turnIndex + 1}, order {ScreenUI.Describe(ui.Chosen)}, log {rule.LogPath}";

        /// <summary>The menu's third choice, and the Unity CLI's: rule <paramref name="country"/> against four model rulers on the server.</summary>
        public void TryRule(string country, string model, int gameSeed, string arm = null) => StartCoroutine(StartRule(country, model, gameSeed, arm));

        /// <summary>
        /// Rule mode on the writs turn (rules v2, R1) on the Classic board: you rule <paramref name="country"/>,
        /// four model rulers with a court and a marshal each the rest, on the server behind the card's lock.
        /// </summary>
        public void TryRuleWrits(string country, string model, int gameSeed, string victory = GameSetup.ConquestPreset, string arm = null)
        {
            var setup = GameSetup.Writs(gameSeed, GameSetup.ClassicMapId, victory);
            var map = setup.BuildMap();
            StartCoroutine(StartRule(country, model, gameSeed, arm, map, setup.BuildRules(map)));
        }

        /// <summary>For the Unity CLI, the dev menu and frames without a server: the writs turn with four stand-in rulers through the same agents.</summary>
        public void StartRuleScriptedWrits(string country, int gameSeed, string victory = GameSetup.ConquestPreset, string arm = null)
        {
            var setup = GameSetup.Writs(gameSeed, GameSetup.ClassicMapId, victory);
            var map = setup.BuildMap();
            BeginRuleOn(country, "scripted", true, arm, gameSeed, map, setup.BuildRules(map));
        }

        /// <summary>
        /// A rule game on a world already made (Classic or generated): stand-in rulers through the agents when
        /// <paramref name="scripted"/>, else model rulers behind the card's lock (the server checked before).
        /// </summary>
        private void BeginRuleOn(string country, string model, bool scripted, string arm, int gameSeed, MapDefinition map, GameRules rules)
        {
            if (!scripted)
            {
                ruleLock = new CardLock(model, $"rule game on the writs turn, {model} with personas, seed {gameSeed}, {map.Provinces.Count} provinces");
                BeginRule(country, model, gameSeed, (m, w) => Gateway.Provider(m), ruleLock, arm, map, rules);
                return;
            }
            var rng = new System.Random(gameSeed);
            World world = null;
            var provider = new ScriptedProvider { Callback = r => ScriptedRuler(r, rng, world) };
            provider.Otherwise(ScriptedReply.Decide("none", statement: "We wait."));
            var wrapped = DevProvider.Wrap(provider);
            BeginRule(country, "scripted", gameSeed, (m, w) => { world = w; return wrapped; }, null, arm, map, rules);
            if (rule != null) rule.Dev = true;
        }

        private IEnumerator StartRule(string country, string model, int gameSeed, string arm, MapDefinition map = null, GameRules rules = null)
        {
            if (started) yield break;
            string why = Gateway.WhyNotSet();
            if (why != null) { ui.ShowMenuStatus(why); yield break; }
            ui.ShowMenuStatus("Asking the server...", true);
            string serverWhy = null;
            yield return Gateway.CheckServer(w => serverWhy = w);
            if (serverWhy != null) { ui.ShowMenuStatus(serverWhy); yield break; }
            model = string.IsNullOrWhiteSpace(model) ? "qwen3.5:4b" : model.Trim();
            if (Gateway.Paused) { ui.ShowMenuStatus("The RTX 5080 is paused for a measurement (its lock's pause file is set). Try again when it is lifted."); yield break; }
            ruleLock = new CardLock(model, $"rule game{(rules?.Turn == TurnStyle.Writs ? " on the writs turn" : "")}, {model} x4 with personas, seed {gameSeed}");
            BeginRule(country, model, gameSeed, (m, w) => Gateway.Provider(m), ruleLock, arm, map, rules);
        }

        /// <summary>
        /// For the Unity CLI and for frames without a server: rule with the four seats played by a
        /// <c>ScriptedProvider</c> that picks a move the game offers, at random, with a stock line.
        /// </summary>
        /// <param name="busyCardSeconds">For frames of the card's states: a stand-in lock "held by another session" for this long, then held by this game. No real lock is touched.</param>
        public void StartRuleScripted(string country, int gameSeed, float busyCardSeconds = 0, string arm = null)
        {
            var rng = new System.Random(gameSeed);
            World world = null;
            var provider = new ScriptedProvider { Callback = r => ScriptedRuler(r, rng, world) };
            provider.Otherwise(ScriptedReply.Decide("none", statement: "We wait."));
            var wrapped = DevProvider.Wrap(provider);
            BeginRule(country, "scripted", gameSeed, (m, w) => { world = w; return wrapped; }, busyCardSeconds > 0 ? new StandInLock(busyCardSeconds) : null, arm);
            if (rule != null) rule.Dev = true;
        }

        private sealed class StandInLock : RuleSession.ICardLock
        {
            private readonly float seconds;
            public StandInLock(float seconds) { this.seconds = seconds; }
            public string WaitingFor { get; private set; }
            public async Awaitable Acquire(System.Threading.CancellationToken ct)
            {
                WaitingFor = $"another session (since {DateTime.Now.AddMinutes(-4):HH:mm}, expects 25 minutes)";
                await Awaitable.WaitForSecondsAsync(seconds, ct);
                WaitingFor = null;
            }
            public async Awaitable Release() { await Awaitable.NextFrameAsync(); }
        }

        private static readonly string[] StockLines =
        {
            "Our banners move at first light.", "Let the treasury grow; patience wins realms.", "They will learn what our steel costs.",
            "A wall today saves a city tomorrow.", "We have bled enough. Let there be terms.", "Not one field more for them.",
        };

        // A stand-in ruler: one of the moves the rules offer its country, at random, with a stock line.
        internal static ScriptedReply ScriptedRuler(ScriptedRequest r, System.Random rng, World world)
        {
            if (r.IsRefusalLine) return ScriptedReply.Statement("We will not.");
            // A storyteller game: the stand-in storyteller and scribe, and a ruler's answer to a dilemma.
            if (r.AgentId == Recording.StorytellerId) return ScriptedStory.Tell(r, rng);
            if (r.AgentId == Recording.ScribeId) return ScriptedStory.Title(rng);
            if (ScriptedStory.IsChoiceCall(r)) return ScriptedStory.Choose(r, rng);
            if (r.OfferedActions.Contains(Actions.AcceptPeace))
                return ScriptedReply.Decide(rng.NextDouble() < 0.6 ? Actions.AcceptPeace : Actions.RefusePeace, statement: "So be it.");
            if (world != null && world.Rules.Turn == TurnStyle.Writs && r.AgentId != null) return ScriptedWrits(r, rng, world);
            var legal = world == null || r.AgentId == null ? null : Legality.CouncilMoves(world, r.AgentId).Where(m => m.Action != Actions.None).ToList();
            if (legal == null || legal.Count == 0 || rng.NextDouble() < 0.25) return ScriptedReply.Decide("none", statement: "We wait and watch.");
            // Attacks and offers of peace a little more often than the rest, so a short game has some.
            var pick = legal.FirstOrDefault(m => m.Action == Actions.OfferPeace && rng.NextDouble() < 0.5)
                       ?? legal.FirstOrDefault(m => m.Action == Actions.Attack && rng.NextDouble() < 0.5)
                       ?? legal[rng.Next(legal.Count)];
            return ScriptedReply.Decide(pick.Action, pick.Target ?? TargetRegistry.NoTarget, StockLines[rng.Next(StockLines.Length)]);
        }

        // The writs turn's stand-in: the court picks one of its court orders, the marshal a march for the army
        // its stimulus names ("Orders for maren_host."), both from what the rules offer now.
        // The stand-in advisor's counsel, for frames without a server.
        private static readonly string[] AdvisorLines =
        {
            "My lord, strike while their armies are away.", "Hold what we have, my lord, and let them bleed each other.",
            "Our treasury can bear more soldiers, my lord.", "A fort on the border would sleep more soundly than a promise.",
        };

        private static ScriptedReply ScriptedWrits(ScriptedRequest r, System.Random rng, World world)
        {
            bool marshal = r.AgentId.EndsWith(RecordedDecision.MarshalSuffix, StringComparison.Ordinal);
            string country = marshal ? r.AgentId.Substring(0, r.AgentId.Length - RecordedDecision.MarshalSuffix.Length) : r.AgentId;
            // The advisor's agents answer for your own seat, so frames without a server show advice too.
            bool advisor = country.EndsWith(Advisor.AgentSuffix, StringComparison.Ordinal);
            if (advisor) country = country.Substring(0, country.Length - Advisor.AgentSuffix.Length);
            List<Move> moves;
            if (marshal)
            {
                var m = System.Text.RegularExpressions.Regex.Match(r.Stimulus ?? "", @"^Orders for (\w+)\.");
                moves = m.Success && world.FindArmy(m.Groups[1].Value) != null ? WritLegality.ArmyMoves(world, country, m.Groups[1].Value) : new List<Move>();
            }
            else moves = WritLegality.CourtMoves(world, country);
            moves = moves.Where(x => x.Action != Actions.None).ToList();
            if (moves.Count == 0 || rng.NextDouble() < (marshal ? 0.15 : 0.3)) return ScriptedReply.Decide("none", statement: marshal ? "Hold the line." : "We wait and watch.");
            // Wars and treaty terms a little more often than the rest, so a short game has some.
            var pick = moves.FirstOrDefault(x => Actions.IsTerm(x.Action) && rng.NextDouble() < 0.5)
                       ?? moves.FirstOrDefault(x => x.Action == Actions.DeclareWar && rng.NextDouble() < 0.3)
                       ?? moves[rng.Next(moves.Count)];
            return ScriptedReply.Decide(pick.Action, pick.Target ?? TargetRegistry.NoTarget, advisor ? AdvisorLines[rng.Next(AdvisorLines.Length)] : StockLines[rng.Next(StockLines.Length)]);
        }

        /// <summary>
        /// New game's "Your writs: unlimited", for the next rule game on the writs turn. Static, as a generated world
        /// reloads the scene before its rule game begins; used once.
        /// </summary>
        private static bool yourWritsUnlimited;

        /// <summary>
        /// New game's "Advisor: on", for the next rule game with model rulers (the lead's answer 29: on by default).
        /// Static for the same reason as <see cref="yourWritsUnlimited"/>; used once.
        /// </summary>
        private static bool advisorNext;

        /// <summary>For the menu and the Unity CLI: the next rule game starts with the advisor on.</summary>
        public static bool NextRuleAdvisor { get => advisorNext; set => advisorNext = value; }

        /// <summary>The in-game menu's switch: the advisor on or off from the next turn.</summary>
        public void SetAdvisor(bool on)
        {
            if (rule == null || !rule.Writs) return;
            rule.AdvisorOn = on;
            lastOrdersStatus = null;
        }

        // "Why?" on a suggestion: the advice's decision in the inspector, as any ruler's, marked as advice.
        private void ShowAdvice(AdviceItem item)
        {
            var d = rule?.AdviceDecision(item);
            if (d == null) return;
            var e = new ReplayEvent { Turn = d.Turn, Country = rule.You, Decision = d, Kind = d.ArmyId != null ? ReplayEventKind.ArmyOrder : ReplayEventKind.Hold, Text = "Your advisor's suggestion" };
            var card = DecisionCard.From(e);
            // Your realm's card, its agent the advisor's: the inspector looks the country up by its id.
            card.Country = rule.You;
            card.Moment = "your advisor, as " + card.Moment + ": advice, not a move";
            ui.ShowCard(card);
        }

        /// <summary>For the menu and the Unity CLI: the next rule game gives you unlimited writs.</summary>
        public static bool NextRuleUnlimitedWrits { get => yourWritsUnlimited; set => yourWritsUnlimited = value; }

        /// <summary>
        /// The rules a rule game is played by: <paramref name="rules"/> as given, or a copy where <paramref name="you"/>
        /// alone has <see cref="GameRules.UnlimitedWrits"/> (the writs turn only; the one-move game has no writs).
        /// </summary>
        public static GameRules WithYourWrits(GameRules rules, string you, bool unlimited)
        {
            if (!unlimited || rules == null || rules.Turn != TurnStyle.Writs) return rules;
            var copy = rules.Clone();
            copy.WritsOverride[you] = GameRules.UnlimitedWrits;
            return copy;
        }

        private void BeginRule(string country, string model, int gameSeed, Func<string, World, ILLMProvider> providerFor, RuleSession.ICardLock cardLock, string arm = null,
            MapDefinition map = null, GameRules rules = null)
        {
            if (started) return;
            started = true;
            rules = WithYourWrits(rules, country, yourWritsUnlimited);
            yourWritsUnlimited = false;
            bool advise = advisorNext && model != "scripted";
            advisorNext = false;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Rule", $"seed-{gameSeed:D4}-{country}-{DateTime.Now:yyyyMMdd-HHmmss}.jsonl"));
            rule = new RuleSession(country, model, arm ?? "default", gameSeed, path, providerFor, cardLock, map, rules) { AdvisorOn = advise };
            ui.AdviceWhy = ShowAdvice;
            Debug.Log($"Painted Realm: rule game, you rule {country}, seed {gameSeed}, {model} x4 with personas; decisions into {path}");
            RunRule();

            ui.Players = rule.Players;
            game = rule.BuildReplay();
            UseGame();
            ui.Fog = new Fog(country, queries);
            ui.NeighboursOf = id => game.Map.Neighbours(id);
            ui.ShowAwaiting = rule.Writs;
            ui.PlanChanged = () => { ApplyMode(); RefreshSide(); };
            ui.SetRuleMode(country);
            followed = country;
            ui.OrderPressed = m => { ui.Chosen = m; lastOrdersStatus = null; RuleTick(); RefreshSide(); };
            ui.EndTurnPressed = EndTurn;
            ui.HintMode = SetMode;
            ui.FollowPressed = _ => { };
            ui.HideMenu();
            ui.SetPlaying(true, speed);
            playing = true;
            Show(game.Initial, null);
            StartCoroutine(Play());
        }

        private async void RunRule()
        {
            await rule.RunAsync();
            if (rule.Error != null) Debug.LogWarning("Painted Realm: rule game stopped: " + rule.Error);
            else if (rule.Result != null) Debug.Log($"Painted Realm: rule game over: {rule.Result.Winner} won on turn {rule.Result.Turns}");
        }

        /// <summary>For the Unity CLI: choose an order, as a click on it would ("attack harrow", "recruit", "none").</summary>
        public string Order(string move)
        {
            if (rule?.Orders == null || ui.OrdersRequest == null) return "not waiting for an order";
            var parts = (move ?? "none").Trim().Split(' ');
            var m = rule.Orders.Legal.FirstOrDefault(x => x.Action == parts[0] && x.Target == (parts.Length > 1 ? parts[1] : null));
            if (m == null) return $"{move} is not offered: {Legality.WhyNot(rule.Orders.World, rule.You, new Move(parts[0], parts.Length > 1 ? parts[1] : null)) ?? "not on the list"}";
            ui.OrderPressed(m);
            return "order: " + ScreenUI.Describe(m);
        }

        /// <summary>End turn: sends the chosen order, if the game is waiting for one.</summary>
        public void EndTurn()
        {
            if (rule == null || rule.WaitingFor != RuleSession.Waiting.Orders || ui.OrdersRequest == null) return;
            if (rule.Writs)
            {
                // Your plan for the turn; armies with a writ and no order hold, as the rules hold them.
                try { rule.Plan(ui.Plan); }
                catch (ArgumentException e) { ui.ShowNotice(e.Message); return; }
                ui.OrdersRequest = null;
                ui.SelectedArmy = null;
                ui.Plan = new RuleSession.WritsPlan();
                lastOrdersStatus = null;
                ApplyMode();
                RuleTick();
                RefreshSide();
                return;
            }
            var m = ui.Chosen ?? Move.Hold;
            rule.Order(m);
            ui.OrdersRequest = null;
            ui.Chosen = Move.Hold;
            lastOrdersStatus = null;
            ApplyMode();
            RuleTick();
            RefreshSide();
        }

        /// <summary>For the Unity CLI: answer the peace offer on the envoy card.</summary>
        public void AnswerOffer(bool accept)
        {
            if (rule?.WaitingFor != RuleSession.Waiting.Envoy) return;
            ui.HideOffer();
            rule.Answer(accept);
        }

        private int RuleReadyTurns()
        {
            if (rule.Resolved != builtResolved)
            {
                builtResolved = rule.Resolved;
                game = rule.BuildReplay();
                // Never silent: a turn that resolved but cannot be rebuilt from the recording is a bug to see.
                if (game.Turns.Count < rule.Resolved)
                    ui.ShowNotice($"The replay cannot rebuild turn {game.Turns.Count + 1}: {game.StoppedBecause}");
            }
            return game.Turns.Count;
        }

        /// <summary>"0:42", "12:05": how long the game has waited.</summary>
        private static string Clock(DateTime sinceUtc)
        {
            var t = DateTime.UtcNow - sinceUtc;
            return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
        }

        /// <summary>After this long with no answer, the status line says the server may be busy.</summary>
        private const double SlowSeconds = 20;

        private string RulersLine(string prefix)
        {
            double waited = (DateTime.UtcNow - rule.WaitingSince).TotalSeconds;
            string line = $"{prefix} ({Clock(rule.WaitingSince)}).";
            if (waited > SlowSeconds)
                line += $" The server is slow to answer: another client may be using it. The game gives up after {rule.RoundTimeout.TotalSeconds:0} s; Esc, then Main menu, leaves now.";
            return line;
        }

        /// <summary>While the screen waits on the game: the orders panel, the envoy card, or what it waits for and for how long.</summary>
        private void RuleTick()
        {
            ui.SetCard(rule.HoldsCard ? "held" : rule.WaitingFor == RuleSession.Waiting.Card ? "waiting" : null);
            if (rule.Error != null) { ui.ShowNotice("The rule game stopped: " + rule.Error); return; }
            var w = rule.WaitingFor;
            string status;
            if (StoryTick(w, out string told)) status = told;
            else if (w == RuleSession.Waiting.Card)
            {
                ui.OrdersRequest = null;
                status = $"Waiting for the RTX 5080, held by {rule.WaitingForCard ?? "someone"}. Waited {Clock(rule.WaitingSince)}. " +
                         "The game takes the card when it is free and holds it until you leave. Esc, then Main menu, leaves now.";
                ui.SetThinking(false);
            }
            else if (w == RuleSession.Waiting.Orders && rule.Orders != null && rule.Writs)
            {
                if (ui.OrdersRequest != rule.Orders)
                {
                    // Your court meets: the map shows the state it meets on, your plan starts empty.
                    ui.OrdersRequest = rule.Orders;
                    ui.Plan = new RuleSession.WritsPlan();
                    ui.SelectedArmy = null;
                    lastOrdersStatus = null;
                    Show(ReplayBuilder.StateOf(rule.Orders.World, game.ProvinceIds, shown), null);
                    ui.SetPlaying(false, speed);
                    RefreshSide();
                }
                int used = ui.WritsUsed(rule.Orders), waiting = WritLegality.Awaiting(rule.Orders.World, rule.You).Count(), withWrit = ui.ArmiesWithWrits(rule.Orders).Count;
                ui.SetTurn(rule.Orders.Turn, rule.Orders.Writs == GameRules.UnlimitedWrits ? $"Orders · writs {used}, no limit" : $"Orders · writs {used} of {rule.Orders.Writs}");
                string writsLine = waiting > withWrit ? $" {waiting - withWrit} of your armies wait for want of a writ: they hold." : "";
                status = (rule.ModelsThinking ? RulersLine($"The {OtherRulers} are deciding while you choose") : $"The {OtherRulers} have decided. Your orders?") + writsLine;
                ui.SetThinking(rule.ModelsThinking);
            }
            else if (w == RuleSession.Waiting.Orders && rule.Orders != null)
            {
                if (ui.OrdersRequest != rule.Orders)
                {
                    // Your council meets: the map shows the state it meets on, after upkeep.
                    ui.OrdersRequest = rule.Orders;
                    ui.Chosen = Move.Hold;
                    lastOrdersStatus = null;
                    Show(ReplayBuilder.StateOf(rule.Orders.World, game.ProvinceIds, shown), null);
                    ui.SetPlaying(false, speed);
                    RefreshSide();
                }
                status = rule.ModelsThinking ? RulersLine($"The {OtherRulers} are deciding while you choose")
                    : $"The {OtherRulers} have decided. Your order?";
                ui.SetThinking(rule.ModelsThinking);
            }
            else if (w == RuleSession.Waiting.Envoy && rule.Offer != null)
            {
                if (lastOffer != rule.Offer)
                {
                    lastOffer = rule.Offer;
                    if (rule.Writs) ui.ShowTreaty(rule.Offer, rule.OfferStatement, () => rule.Answer(true), () => rule.Answer(false));
                    else ui.ShowOffer(rule.Offer.OfferedBy, rule.OfferStatement, queries.Rules.TruceTurns, () => rule.Answer(true), () => rule.Answer(false));
                }
                status = $"{CountryLook.NameOf(rule.Offer.OfferedBy)}'s envoy waits for your answer.";
            }
            else if (!rule.Game.World.Country(rule.You).Alive)
            {
                ui.OrdersRequest = null;
                status = $"{CountryLook.NameOf(rule.You)} has lost its last province. The {OtherRulers} play on; watch how it ends.";
                ui.SetThinking(rule.ModelsThinking);
            }
            else
            {
                ui.OrdersRequest = null;
                status = rule.ModelsThinking ? RulersLine("Your order is in. The rulers are deciding") : "Your order is in.";
                ui.SetThinking(true);
            }
            // Rebuild the panel only when what it offers changes; the clock updates its line alone,
            // so a button is never rebuilt under the pointer.
            if (rule.Writs)
            {
                ui.AdvisorShown = rule.AdvisorOn;
                ui.Advice = rule.Advice;
                ui.AdvisorThinking = rule.AdvisorThinking;
                ui.AdvisorError = rule.AdvisorError;
            }
            var key = (ui.OrdersRequest, ui.Chosen, w, ui.SelectedArmy, ui.WritsKey() + "|" + ui.AdviceKey());
            if (!Equals(key, lastOrdersKey) || lastOrdersStatus == null) { lastOrdersKey = key; lastOrdersStatus = status; ui.ShowOrders(status); }
            else if (status != lastOrdersStatus) { lastOrdersStatus = status; ui.SetOrdersStatus(status); }
        }

        private object lastOrdersKey;

        /// <summary>
        /// Back to the start menu: stops the game, lets the card go (unloading the model), and loads
        /// the scene afresh. The Menu button in the top bar, and the Unity CLI.
        /// </summary>
        /// <summary>"four rulers" on the Classic board, "seven rulers" on a world of eight realms: the others, in words.</summary>
        private string OtherRulers
        {
            get
            {
                int n = Math.Max(1, (game?.CountryIds.Count ?? 5) - 1);
                string[] words = { "", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };
                return (n < words.Length ? words[n] : n.ToString()) + (n == 1 ? " ruler" : " rulers");
            }
        }

        public void LeaveToMenu()
        {
            rule?.Stop();
            ruleLock?.ReleaseNow();
            if (live != null) live.Stop();
            Gateway.ReleaseHeldNow();
            var scene = gameObject.scene;
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#else
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.name);
#endif
        }

        /// <summary>A turn about to play back: the orders panel says so and offers nothing.</summary>
        private void RuleResolving(int turn)
        {
            ui.SetThinking(false);
            ui.HideOffer();
            ui.OrdersRequest = null;
            lastOrdersStatus = $"Turn {turn} resolves: your order and the {OtherRulers}' moves land in the rules' order.";
            lastOrdersKey = null;
            ui.ShowOrders(lastOrdersStatus);
            ui.SetCard(rule.HoldsCard ? "held" : null);
            RefreshSide();
            ui.SetPlaying(true, speed);
            playing = true;
        }

        private void RuleOver()
        {
            ui.OrdersRequest = null;
            string end = rule.Result != null
                ? (rule.Result.Winner == rule.You ? $"You won, by {rule.Result.Ending}, on turn {rule.Result.Turns}." : $"{CountryLook.NameOf(rule.Result.Winner)} won, by {rule.Result.Ending}, on turn {rule.Result.Turns}.")
                  + " Every ruler's council is open in the inspector now."
                : rule.Error != null ? "The game stopped: " + rule.Error : "The game stopped.";
            lastOrdersKey = null;
            ui.ShowOrders(end);
            ui.SetCard(null);
        }

        private void OnDestroy()
        {
            rule?.Stop();
            ruleLock?.ReleaseNow();
        }
    }
}
