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
