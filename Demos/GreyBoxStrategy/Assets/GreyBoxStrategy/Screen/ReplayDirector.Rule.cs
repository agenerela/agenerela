using System;
using System.Collections;
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
        private CardLock ruleLock;
        private int builtResolved = -1;
        private string lastOrdersStatus;
        private CountryRequest lastOffer;

        /// <summary>For the Unity CLI: the rule game, or null.</summary>
        public RuleSession Rule => rule;
        public string RuleStatus => rule == null ? "no rule game" : $"{rule.Status}, {rule.Resolved} turns resolved, playing turn {turnIndex + 1}, order {ScreenUI.Describe(ui.Chosen)}, log {rule.LogPath}";

        /// <summary>The menu's third choice, and the Unity CLI's: rule <paramref name="country"/> against four model rulers on the server.</summary>
        public void TryRule(string country, string model, int gameSeed) => StartCoroutine(StartRule(country, model, gameSeed));

        private IEnumerator StartRule(string country, string model, int gameSeed)
        {
            if (started) yield break;
            string why = Gateway.WhyNotSet();
            if (why != null) { ui.ShowRuleStatus(why); yield break; }
            ui.ShowRuleStatus("Asking the server...", true);
            string serverWhy = null;
            yield return Gateway.CheckServer(w => serverWhy = w);
            if (serverWhy != null) { ui.ShowRuleStatus(serverWhy); yield break; }
            model = string.IsNullOrWhiteSpace(model) ? "qwen3.5:4b" : model.Trim();
            ruleLock = new CardLock(model, $"rule game, {model} x4 with personas, seed {gameSeed}");
            BeginRule(country, model, gameSeed, (m, w) => Gateway.Provider(m), ruleLock);
        }

        /// <summary>
        /// For the Unity CLI and for frames without a server: rule with the four seats played by a
        /// <c>ScriptedProvider</c> that picks a move the game offers, at random, with a stock line.
        /// </summary>
        public void StartRuleScripted(string country, int gameSeed)
        {
            var rng = new System.Random(gameSeed);
            World world = null;
            var provider = new ScriptedProvider { Callback = r => ScriptedRuler(r, rng, world) };
            provider.Otherwise(ScriptedReply.Decide("none", statement: "We wait."));
            BeginRule(country, "scripted", gameSeed, (m, w) => { world = w; return provider; }, null);
        }

        private static readonly string[] StockLines =
        {
            "Our banners move at first light.", "Let the treasury grow; patience wins realms.", "They will learn what our steel costs.",
            "A wall today saves a city tomorrow.", "We have bled enough. Let there be terms.", "Not one field more for them.",
        };

        // A stand-in ruler: one of the moves the rules offer its country, at random, with a stock line.
        private static ScriptedReply ScriptedRuler(ScriptedRequest r, System.Random rng, World world)
        {
            if (r.IsRefusalLine) return ScriptedReply.Statement("We will not.");
            if (r.OfferedActions.Contains(Actions.AcceptPeace))
                return ScriptedReply.Decide(rng.NextDouble() < 0.6 ? Actions.AcceptPeace : Actions.RefusePeace, statement: "So be it.");
            var legal = world == null || r.AgentId == null ? null : Legality.CouncilMoves(world, r.AgentId).Where(m => m.Action != Actions.None).ToList();
            if (legal == null || legal.Count == 0 || rng.NextDouble() < 0.25) return ScriptedReply.Decide("none", statement: "We wait and watch.");
            // Attacks and offers of peace a little more often than the rest, so a short game has some.
            var pick = legal.FirstOrDefault(m => m.Action == Actions.OfferPeace && rng.NextDouble() < 0.5)
                       ?? legal.FirstOrDefault(m => m.Action == Actions.Attack && rng.NextDouble() < 0.5)
                       ?? legal[rng.Next(legal.Count)];
            return ScriptedReply.Decide(pick.Action, pick.Target ?? TargetRegistry.NoTarget, StockLines[rng.Next(StockLines.Length)]);
        }

        private void BeginRule(string country, string model, int gameSeed, Func<string, World, ILLMProvider> providerFor, RuleSession.ICardLock cardLock)
        {
            if (started) return;
            started = true;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Rule", $"seed-{gameSeed:D4}-{country}-{DateTime.Now:yyyyMMdd-HHmmss}.jsonl"));
            rule = new RuleSession(country, model, "default", gameSeed, path, providerFor, cardLock);
            Debug.Log($"Painted Realm: rule game, you rule {country}, seed {gameSeed}, {model} x4 with personas; decisions into {path}");
            RunRule();

            ui.Players = rule.Players;
            game = rule.BuildReplay();
            queries = new RealmQueries(game);
            ui.Queries = queries;
            ui.Fog = new Fog(country, queries);
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
            }
            return game.Turns.Count;
        }

        /// <summary>While the screen waits on the game: the orders panel, the envoy card, or who it waits for.</summary>
        private void RuleTick()
        {
            if (rule.Error != null) { ui.ShowNotice("The rule game stopped: " + rule.Error); return; }
            var w = rule.WaitingFor;
            string status;
            if (w == RuleSession.Waiting.Orders && rule.Orders != null)
            {
                if (ui.OrdersRequest != rule.Orders)
                {
                    // Your council meets: the map shows the state it meets on, after upkeep.
                    ui.OrdersRequest = rule.Orders;
                    ui.Chosen = Move.Hold;
                    Show(ReplayBuilder.StateOf(rule.Orders.World, game.ProvinceIds, shown), null);
                    ui.SetPlaying(false, speed);
                    RefreshSide();
                }
                status = rule.WaitingForCard != null ? $"The rulers wait for the RTX 5080 ({rule.WaitingForCard})."
                    : rule.ModelsThinking ? "The four rulers are deciding while you choose."
                    : "The four rulers have decided. Your order?";
                ui.SetThinking(rule.ModelsThinking);
            }
            else if (w == RuleSession.Waiting.Envoy && rule.Offer != null)
            {
                if (lastOffer != rule.Offer)
                {
                    lastOffer = rule.Offer;
                    ui.ShowOffer(rule.Offer.OfferedBy, rule.OfferStatement, queries.Rules.TruceTurns, () => rule.Answer(true), () => rule.Answer(false));
                }
                status = $"{CountryLook.NameOf(rule.Offer.OfferedBy)}'s envoy waits for your answer.";
            }
            else if (!rule.Game.World.Country(rule.You).Alive)
            {
                ui.OrdersRequest = null;
                status = $"{CountryLook.NameOf(rule.You)} has lost its last province. The four rulers play on; watch how it ends.";
                ui.SetThinking(rule.ModelsThinking);
            }
            else
            {
                ui.OrdersRequest = null;
                status = rule.WaitingForCard != null ? $"Your order is in. The rulers wait for the RTX 5080 ({rule.WaitingForCard})."
                    : "Your order is in. The rulers are deciding.";
                ui.SetThinking(true);
            }
            if (status != lastOrdersStatus) { lastOrdersStatus = status; ui.ShowOrders(status); }
        }

        /// <summary>A turn about to play back: the orders panel says so and offers nothing.</summary>
        private void RuleResolving(int turn)
        {
            ui.SetThinking(false);
            ui.HideOffer();
            ui.OrdersRequest = null;
            lastOrdersStatus = $"Turn {turn} resolves: your order and the four rulers' moves land in the rules' order.";
            ui.ShowOrders(lastOrdersStatus);
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
            ui.ShowOrders(end);
        }

        private void OnDestroy()
        {
            rule?.Stop();
            ruleLock?.ReleaseNow();
        }
    }
}
