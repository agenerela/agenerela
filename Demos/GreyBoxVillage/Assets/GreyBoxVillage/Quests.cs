using System;
using System.Collections.Generic;
using System.Linq;
using Agenerela;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    public enum QuestState
    {
        Available,
        Active,
        Done
    }

    /// <summary>A task an agent can give the player: bring a number of one item, for a reward.</summary>
    public sealed class Quest
    {
        /// <summary>The target id the model names it by, such as "quest_moonpetals".</summary>
        public string Id;
        /// <summary>The agent who gives it and takes it back, by name.</summary>
        public string Giver;
        public string Title;
        /// <summary>What the giver asks for, in the giver's own words; also the journal's text.</summary>
        public string Ask;
        public string ItemId;
        public int Count = 1;
        public int RewardCoins;
        public string RewardItem;
        public QuestState State = QuestState.Available;

        public string Reward => RewardCoins + " coins" + (RewardItem == null ? "" : " and a " + ItemCatalog.Get(RewardItem)?.Name.ToLowerInvariant());
    }

    /// <summary>
    /// Every quest in the village and the player's progress on it. Agents change a quest's state only
    /// through their actions (offer_quest, complete_quest); this book applies the rules.
    /// </summary>
    public sealed class QuestBook : MonoBehaviour
    {
        public PlayerInventory Inventory;
        public readonly List<Quest> Quests = new List<Quest>();

        /// <summary>A line for the player, such as "Quest accepted: ...". The HUD shows it as a toast.</summary>
        public event Action<string> Message;

        private void Awake() => Define();

        /// <summary>Puts every quest back to the start. Used between tests.</summary>
        public void Define()
        {
            Quests.Clear();
            Quests.Add(new Quest
            {
                Id = "quest_moonpetals", Giver = "Odile", Title = "Moonpetals for Odile",
                Ask = "Bring Odile three moonpetals from the woods north of the river. They glow after dark.",
                ItemId = "moonpetal", Count = 3, RewardCoins = 15, RewardItem = "rope"
            });
            Quests.Add(new Quest
            {
                Id = "quest_wolf", Giver = "Hale", Title = "The wolf at the gate",
                Ask = "Get the wolf driven off by the guards, and bring Captain Hale its pelt as proof.",
                ItemId = "pelt", Count = 1, RewardCoins = 25
            });
            Quests.Add(new Quest
            {
                Id = "quest_lantern", Giver = "Wren", Title = "Wren's lost lantern",
                Ask = "Find the lantern Wren lost down at the docks, and give it back to her.",
                ItemId = "old_lantern", Count = 1, RewardCoins = 10
            });
        }

        public Quest Get(string id) => Quests.FirstOrDefault(q => q.Id == id);
        public IEnumerable<Quest> From(string giver) => Quests.Where(q => q.Giver == giver);
        public bool IsReady(Quest q) => q.State == QuestState.Active && Inventory != null && Inventory.Has(q.ItemId, q.Count);

        public void Offer(Quest q)
        {
            if (q.State != QuestState.Available) return;
            q.State = QuestState.Active;
            Message?.Invoke("Quest accepted: " + q.Title);
        }

        /// <summary>Takes the items, pays the reward and closes the quest. False when it is not ready.</summary>
        public bool Complete(Quest q)
        {
            if (!IsReady(q)) return false;
            Inventory.Remove(q.ItemId, q.Count);
            Inventory.AddCoins(q.RewardCoins);
            if (q.RewardItem != null) Inventory.Add(q.RewardItem);
            q.State = QuestState.Done;
            Message?.Invoke("Quest complete: " + q.Title + "   +" + q.Reward);
            return true;
        }

        /// <summary>Where the player stands on a quest, for the giver's observations.</summary>
        public string Describe(Quest q)
        {
            switch (q.State)
            {
                case QuestState.Available:
                    return "You can offer the player the quest " + q.Id + ": " + q.Ask + " Reward: " + q.Reward + ".";
                case QuestState.Active when IsReady(q):
                    return "The player has everything for the quest " + q.Id + "; you can complete it.";
                case QuestState.Active:
                    return "The player is on the quest " + q.Id + " and has " + Inventory.Count(q.ItemId) + " of " + q.Count + " " + q.ItemId + ".";
                default:
                    return "The player finished the quest " + q.Id + ".";
            }
        }

        /// <summary>The step the journal and the tracker show for an active quest.</summary>
        public string Step(Quest q) => IsReady(q) ? "Return to " + q.Giver
            : q.Count > 1 ? Inventory.Count(q.ItemId) + " / " + q.Count + " " + ItemCatalog.Get(q.ItemId)?.Name.ToLowerInvariant()
            : "Find the " + ItemCatalog.Get(q.ItemId)?.Name.ToLowerInvariant();
    }

    /// <summary>
    /// Offers a giver's open quests as targets: the ones it can still give, and the ones the player is
    /// on. A finished quest drops out of the schema, so the model can no longer name it.
    /// </summary>
    public sealed class QuestTargetSource : ITargetSource
    {
        private readonly QuestBook book;
        private readonly string giver;

        public QuestTargetSource(QuestBook book, string giver)
        {
            this.book = book;
            this.giver = giver;
        }

        public void Collect(AgentContext ctx, TargetRegistry into)
        {
            if (book == null) return;
            foreach (var q in book.From(giver))
                if (q.State != QuestState.Done) into.Register(q.Id, q);
        }
    }
}
