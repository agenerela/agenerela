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
    /// The G4c live check: one line per new route action through the real driver and a local Ollama,
    /// plus the "I have proof" claim with no proof, one JSON line per case. Explicit and ignored
    /// without <c>LV_MODEL</c> and <c>LV_ROUTES_OUT</c>; take the Ollama lock first.
    /// </summary>
    /// <remarks>
    /// Each case is staged on its own: fresh game state at the quest stage the line needs. None of
    /// the lines repeats a few-shot example's wording (<see cref="RouteActionBinding"/>) or an
    /// evaluation prompt (hard rule 4). A smoke check, not an accuracy claim (hard rule 2).
    /// </remarks>
    [Explicit("Needs a local Ollama with the model pulled; run by name.")]
    [Category("RequiresOllama")]
    public sealed class QuestRouteLiveCheck
    {
        private enum Stage
        {
            Active,
            ActiveWithFenClue,
            Recovered,
            RecoveredWithPermit
        }

        private static readonly (string npc, Stage stage, string line, string action, string target)[] Cases =
        {
            (QuestRoutes.FenId, Stage.Active, "The medicine wagon vanished on the Old Road. Did you notice anything out there?",
                GameActions.ShareInformation, QuestRoutes.KnowledgeFenRaid),
            (QuestRoutes.KestId, Stage.Active, "I have proof. A witness watched your people rob that wagon, so hand the parcel over.",
                ActionRegistry.None, TargetRegistry.NoTarget),
            (QuestRoutes.KestId, Stage.ActiveWithFenClue, "Fen from Reedbank watched your raiders take the wagon. The castle will hear it.",
                GameActions.OfferExchange, QuestRoutes.ExchangeKestTestimony),
            (QuestRoutes.KestId, Stage.Active, "What would it cost me to get that medicine back from you?",
                GameActions.OfferExchange, QuestRoutes.ExchangeKestPayment),
            (QuestRoutes.BrannId, Stage.Recovered, "I've got the castle's medicine with me. Can I take it in to the steward?",
                GameActions.GrantAccess, QuestLog.AccessDeliveryPermit),
            (QuestRoutes.ElraId, Stage.RecoveredWithPermit, "This is the medical parcel from the wagon, for your garrison.",
                GameActions.ResolveDelivery, QuestRoutes.DeliveryParcelElra),
            (QuestLog.IvenId, Stage.Recovered, "I got the parcel back from the camp. It's for your patients.",
                GameActions.ResolveDelivery, QuestRoutes.DeliveryParcelIven),
        };

        [Serializable]
        private sealed class Row
        {
            public string model;
            public string arm;
            public int index;
            public string npc;
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
            public bool executed;
            public bool card;
            public bool clueRecorded;
            public float latencySeconds;
        }

        private sealed class Session : ITradeSession
        {
            public long Generation => 1;
            public string ActiveNpcId { get; set; }
        }

        [Test, Timeout(900000)]
        public async Task RunTheRouteLines()
        {
            string model = Environment.GetEnvironmentVariable("LV_MODEL");
            string output = Environment.GetEnvironmentVariable("LV_ROUTES_OUT");
            if (string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(output))
            {
                Assert.Ignore("Set LV_MODEL and LV_ROUTES_OUT to run the live check.");
            }

            // LV_ARMS interleaves arms line by line (measuring.md, item 4); only "default" is left.
            string[] arms = (Environment.GetEnvironmentVariable("LV_ARMS") ?? "default").Split(',');
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            using (var writer = new StreamWriter(output, false))
            {
                for (int c = 0; c < Cases.Length * arms.Length; c++)
                {
                    int i = c / arms.Length;
                    string arm = arms[c % arms.Length].Trim();
                    var (npcId, stage, line, action, target) = Cases[i];
                    var session = new Session { ActiveNpcId = QuestLog.IvenId };
                    var catalog = ItemCatalog.CreateDefault();
                    var inventory = PlayerInventory.CreateStarting(catalog);
                    var trade = new TradeService(inventory, new[] { VendorStock.CreateMara(catalog) }, session);
                    var quests = new QuestLog(inventory, new Progression(), session);
                    var routes = new QuestRoutes(quests, session);
                    quests.TryOffer(QuestLog.IvenId, QuestLog.IvenOfferId, 1, out _);
                    quests.AcceptOffer(QuestLog.IvenOfferId);
                    if (stage == Stage.ActiveWithFenClue)
                    {
                        quests.RecordClue(QuestLog.ClueFenTestimony);
                    }

                    if (stage == Stage.Recovered || stage == Stage.RecoveredWithPermit)
                    {
                        quests.TryRecoverParcel(RecoveryRoute.Combat);
                    }

                    if (stage == Stage.RecoveredWithPermit)
                    {
                        quests.RecordClue(QuestLog.AccessDeliveryPermit);
                    }

                    session.ActiveNpcId = npcId;
                    var driver = AgenerelaNpcDecisionDriver.ForOllama(model, new NpcActionExecutor(trade, quests, routes));
                    if (arm != "default")
                    {
                        // G5g's shipped/framework arms were measured with a switch the game no longer has.
                        throw new ArgumentException("Unknown arm " + arm);
                    }
                    var npc = new NpcDescriptor(npcId, npcId.Substring(4), "Villager");
                    var row = new Row { model = model, arm = arm, index = i + 1, npc = npcId, line = line, expectedAction = action, expectedTarget = target };
                    var turn = new NpcTurn(i + 1, npc, line, Array.Empty<ChatLine>(), 1);
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
                        row.latencySeconds = result.Telemetry.LatencySeconds;
                        bool hadClue = quests.HasClue(QuestLog.ClueFenTestimony);
                        row.executed = driver.Execute(turn, reply);
                        row.card = driver.PendingProposal != null;
                        row.clueRecorded = !hadClue && quests.HasClue(QuestLog.ClueFenTestimony);
                    }

                    writer.WriteLine(JsonUtility.ToJson(row));
                    writer.Flush();
                }
            }
        }
    }
}
