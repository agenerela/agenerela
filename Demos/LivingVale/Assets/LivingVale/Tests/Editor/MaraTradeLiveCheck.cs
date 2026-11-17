using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Agenerela;
using LivingVale.Agenerela;
using LivingVale.Game;
using LivingVale.Interaction;
using NUnit.Framework;
using UnityEngine;

namespace LivingVale.Tests
{
    /// <summary>
    /// The G5b live check: buy and sell lines to Mara through the real driver and a local Ollama,
    /// one JSON line per case with the model's choice and what the guards left. Explicit and
    /// ignored without <c>LV_MODEL</c> and <c>LV_TRADE_OUT</c>; take the Ollama lock first.
    /// </summary>
    /// <remarks>
    /// Each case is staged on its own: a fresh driver, game state and conversation, with the player
    /// carrying one wolf pelt and one iron scrap, so both actions are offered (measuring.md, rule 4).
    /// None of the lines repeats the few-shot examples' wording ("I'll take …, please.",
    /// "Would you buy my …?"), or a prompt of the evaluation set (hard rule 4).
    /// </remarks>
    [Explicit("Needs a local Ollama with the model pulled; run by name.")]
    [Category("RequiresOllama")]
    public sealed class MaraTradeLiveCheck
    {
        private static readonly (string line, string action, string target)[] Cases =
        {
            ("Could I get a healing potion from you?", GameActions.QuotePurchase, "stock_mara_potion_one"),
            ("I'm starving. Got any bread?", GameActions.QuotePurchase, "stock_mara_bread_one"),
            ("Iven asked me to bring him herbs. Do you have some?", GameActions.QuotePurchase, "stock_mara_herb_one"),
            ("I skinned a wolf on the road. Want the pelt?", GameActions.QuoteSale, "sale_mara_wolf_pelt_one"),
            ("What would you give me for this lump of iron scrap?", GameActions.QuoteSale, "sale_mara_iron_scrap_one"),
            ("Just looking around. How is business today?", ActionRegistry.None, TargetRegistry.NoTarget),
        };

        // G5f: lines that ask for nothing, so any offer is unasked. New, and none repeats a line
        // above, a few-shot example (including the negative one below) or the evaluation set.
        private static readonly string[] NonTradeLines =
        {
            "Evening, Mara. Heard anything new from the travelers lately?",
            "How long have you kept this stall?",
            "Which way is the inn from here?",
            "You look tired today. Is everything all right?",
            "Do you ever miss the days when the road was busy?",
            "Who is that healer on the porch over there?",
            "Your stall smells of herbs. Do you dry them yourself?",
            "Is it safe to walk the Old Road at night?",
            "What do people in Briar Glen do for fun?",
            "Did you know the wagon's driver?",
            "I'm only passing through on my way to the castle.",
            "Thanks for the chat, Mara. Take care.",
        };

        /// <summary>G5f's negative example: a line the few-shot block shows answered with none.</summary>
        public const string NegativeExample = AgenerelaNpcDecisionDriver.DefaultIdleExample;

        // The arms of G5f's A/B, by name: control (G5b's prompt), example (a negative few-shot
        // example), rule (a stricter rule line). LV_ARMS picks them, comma-separated.
        private static void Configure(AgenerelaNpcDecisionDriver driver, string arm)
        {
            switch (arm)
            {
                case "control":
                    driver.TradeRule = TradeActionBinding.DefaultRule;
                    driver.IdleExampleStimulus = null;
                    break;
                case "example":
                    driver.TradeRule = TradeActionBinding.DefaultRule;
                    driver.IdleExampleStimulus = NegativeExample;
                    break;
                case "rule":
                    driver.TradeRule = TradeActionBinding.OnlyWhenAskedRule;
                    driver.IdleExampleStimulus = null;
                    break;
                case "default":
                    break;
                default:
                    throw new ArgumentException("Unknown arm " + arm);
            }
        }

        [Serializable]
        private sealed class Row
        {
            public string model;
            public string arm;
            public int index;
            public string line;
            public string expectedAction;
            public string expectedTarget;
            public string failure;
            public string modelAction;
            public string modelTarget;
            public string action;
            public string target;
            public string statement;
            public string guards;
            public string verdict;
            public bool executed;
            public bool quoted;
            public float latencySeconds;
        }

        private sealed class Session : ITradeSession
        {
            public long Generation => 1;
            public string ActiveNpcId => VendorStock.MaraId;
        }

        [Test, Timeout(900000)]
        public async Task RunTheTradeLines()
        {
            string model = Environment.GetEnvironmentVariable("LV_MODEL");
            string output = Environment.GetEnvironmentVariable("LV_TRADE_OUT");
            if (string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(output))
            {
                Assert.Ignore("Set LV_MODEL and LV_TRADE_OUT to run the live check.");
            }

            var mara = new NpcDescriptor(VendorStock.MaraId, "Mara", "General trader");
            string[] arms = (Environment.GetEnvironmentVariable("LV_ARMS") ?? "default").Split(',');
            var cases = new System.Collections.Generic.List<(string line, string action, string target)>(Cases);
            if (arms.Length > 1 || arms[0] != "default")
            {
                foreach (var line in NonTradeLines)
                {
                    cases.Add((line, ActionRegistry.None, TargetRegistry.NoTarget));
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(output));
            using (var writer = new StreamWriter(output, false))
            {
                // Two arms on one model interleave, line by line (measuring.md, item 4).
                for (int c = 0; c < cases.Count * arms.Length; c++)
                {
                    int i = c / arms.Length;
                    string arm = arms[c % arms.Length].Trim();
                    var (line, action, target) = cases[i];
                    var catalog = ItemCatalog.CreateDefault();
                    var inventory = new PlayerInventory(catalog);
                    inventory.TryApply(0, new[] { new ItemChange(ItemCatalog.WolfPelt, 1), new ItemChange(ItemCatalog.IronScrap, 1) }, out _);
                    var trade = new TradeService(inventory, new[] { VendorStock.CreateMara(catalog) }, new Session());
                    var driver = AgenerelaNpcDecisionDriver.ForOllama(model, new NpcActionExecutor(trade));
                    Configure(driver, arm);

                    var row = new Row { model = model, arm = arm, index = i + 1, line = line, expectedAction = action, expectedTarget = target };
                    var turn = new NpcTurn(i + 1, mara, line, Array.Empty<ChatLine>(), 1);
                    var decide = driver.DecideAsync(turn, CancellationToken.None);
                    await Run.NotCancelled(decide);
                    var reply = decide.Result;
                    row.failure = reply.Failure.ToString();

                    if (reply.DriverState is DecisionResult result)
                    {
                        row.modelAction = result.ModelDecision?.ActionId;
                        row.modelTarget = result.ModelDecision?.TargetId;
                        row.action = result.Decision?.ActionId;
                        row.target = result.Decision?.TargetId;
                        row.statement = result.Decision?.Statement;
                        row.guards = string.Join(",", result.Telemetry.GuardsFired);
                        row.verdict = result.Telemetry.Verdict?.ToString();
                        row.latencySeconds = result.Telemetry.LatencySeconds;
                        row.executed = driver.Execute(turn, reply);
                        row.quoted = trade.ActiveQuote != null;
                    }

                    writer.WriteLine(JsonUtility.ToJson(row));
                    writer.Flush();
                }
            }
        }
    }
}
