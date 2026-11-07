using System.Collections.Generic;
using System.Linq;
using Agenerela;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>How a line above an agent's head reads: its own words, a guard's refusal, or the game saying no answer came.</summary>
    public enum SpeechKind
    {
        Statement,
        Refusal,
        Error
    }

    /// <summary>
    /// What an <see cref="AgentMind"/> drives: an agent's body in the village. Its <c>[AgentAction]</c>
    /// methods are the game code each action runs, and it tells the mind what only it knows: what it
    /// is doing, the facts it adds to every request, and the rules its game guards enforce.
    /// </summary>
    /// <remarks>
    /// The guards are the first bodies; the captain and the merchant of later slices are others. The
    /// framework sees none of this, only the agent its <see cref="AgentBehaviour"/> builds.
    /// </remarks>
    public abstract class VillageBody : MonoBehaviour
    {
        /// <summary>Set by <see cref="VillageCommands"/>.</summary>
        [System.NonSerialized] public Transform Player;
        /// <summary>The village's quests and the player's pack, set by the mind when the agent is built.</summary>
        [System.NonSerialized] public QuestBook Quests;
        [System.NonSerialized] public PlayerInventory Inventory;

        /// <summary>The game guard that contains an action aimed at the wrong kind of target, such as a quest offered "at the market".</summary>
        public const string TargetFitsAction = "target_fits_action";
        /// <summary>The game guard that contains completing a quest the player has not finished.</summary>
        public const string QuestNotReady = "quest_not_ready";

        private float speechUntil;
        private bool speaking;

        /// <summary>What the agent is doing, as a clause the observations can use.</summary>
        public abstract string Activity { get; }

        /// <summary>True while it walks, follows or chases, so its quiet timer waits.</summary>
        public abstract bool IsBusy { get; }

        /// <summary>
        /// What the agent is doing in place of its usual role, for the board and the dialogue panel,
        /// such as the night watch; null for none. The prompt keeps the profile's role.
        /// </summary>
        public virtual string Duty => null;

        /// <summary>The facts about this body sent with every request, after "You are ...". The base gives its quests.</summary>
        public virtual IEnumerable<string> Facts()
        {
            if (Quests == null) yield break;
            foreach (var q in Quests.From(name)) yield return Quests.Describe(q);
        }

        /// <summary>Target sources beyond the proximity one, added once when the agent is built: its quests.</summary>
        public virtual IEnumerable<ITargetSource> TargetSources()
        {
            if (Quests != null && Quests.From(name).Any()) yield return new QuestTargetSource(Quests, name);
        }

        /// <summary>Called after an action ran, with the statement that came with it. The captain passes his orders on here.</summary>
        public virtual void AfterAction(string actionId, string statement) { }

        /// <summary>Called before every decision, with who is asking, so the body can drop what it was doing.</summary>
        public virtual void Attend(string source) { }

        /// <summary>
        /// The game's own guards over this agent's decisions, added once when its agent is built. The
        /// base keeps every action to its kind of target, and a quest to being finished before it is
        /// completed; a body adds its own after these.
        /// </summary>
        public virtual IEnumerable<IDecisionGuard> Guards()
        {
            yield return new DelegateGuard(TargetFitsAction, input =>
            {
                var d = input.Decision;
                if (d.TargetId == TargetRegistry.NoTarget || !input.Context.Targets.TryGet(d.TargetId, out var target)) return GuardVerdict.Pass;
                return TargetFits(d.ActionId, target) ? GuardVerdict.Pass
                    : GuardVerdict.RewriteToNone(d.ActionId + " cannot be done to " + d.TargetId + ".");
            });
            yield return new DelegateGuard(QuestNotReady, input =>
            {
                var d = input.Decision;
                if (d.ActionId != "complete_quest" || Quests == null || !input.Context.Targets.TryGet(d.TargetId, out var target)) return GuardVerdict.Pass;
                return target is Quest q && Quests.IsReady(q) ? GuardVerdict.Pass
                    : GuardVerdict.RewriteToNone(d.TargetId + " is not finished: the player does not have what it needs.");
            });
        }

        /// <summary>Whether an action can be aimed at this target. Quest actions need a quest; nothing else takes one.</summary>
        protected virtual bool TargetFits(string actionId, object target) =>
            actionId == "offer_quest" || actionId == "complete_quest" ? target is Quest : !(target is Quest);

        /// <summary>What the agent says when a guard stops a decision; null says nothing (<see cref="Agent.RefusalStatement"/>).</summary>
        public virtual string RefusalLine(GuardVerdict verdict) =>
            verdict.Guard == TargetFitsAction ? "I don't follow you."
            : verdict.Guard == QuestNotReady ? "You haven't got it all yet. Come back when you do."
            : null;

        // ---- Quests, for any agent that has some ----------------------------------------------

        /// <summary>The source of the stimulus being decided, from the call's <see cref="AgentMind.Moment"/>.</summary>
        protected static string Asking(AgentContext ctx) => AgentMind.Moment.Of(ctx)?.AskedBy;

        [AgentAction("offer_quest", "Give the player one of your quests.", RequiresTarget = true)]
        [Example("Got any work for a traveller?", "quest_moonpetals", "quest_wolf", "quest_lantern")]
        private void OfferQuest(Quest quest) => Quests.Offer(quest);

        [Available("offer_quest")]
        private bool CanOffer(AgentContext ctx) =>
            Asking(ctx) == "player" && Quests != null && Quests.From(name).Any(q => q.State == QuestState.Available);

        [AgentAction("complete_quest", "Take what the player brought for a quest and pay the reward.", RequiresTarget = true)]
        [Example("Here it is, as promised.", "quest_moonpetals", "quest_wolf", "quest_lantern")]
        private void CompleteQuest(Quest quest) => Quests.Complete(quest);

        // Offered only when the player holds everything for one of this agent's quests: the rule is
        // state, so the option is absent rather than forbidden (AGENTS.md, settled rules).
        [Available("complete_quest")]
        private bool CanComplete(AgentContext ctx) =>
            Asking(ctx) == "player" && Quests != null && Quests.From(name).Any(Quests.IsReady);

        /// <summary>Forgets what the agent was doing, without moving it. Used between tests.</summary>
        public abstract void ResetState();

        /// <summary>Shows a line above the agent's head for a few seconds. A refusal reads in amber, tagged with the guard; an error in red.</summary>
        public void Say(string statement, SpeechKind kind = SpeechKind.Statement, string guard = null)
        {
            if (string.IsNullOrWhiteSpace(statement)) return;
            SpeechText = statement.Trim();
            SpeechType = kind;
            SpeechGuard = kind == SpeechKind.Refusal ? guard : null;
            speechUntil = Time.time + 6f;
            speaking = true;
            var figure = GetComponent<Demos.Shared.SegmentedFigure>();
            if (figure != null && kind != SpeechKind.Error) figure.Talk(Mathf.Clamp(SpeechText.Length * 0.06f, 1f, 4f));
        }

        /// <summary>Takes down the line above its head, so an older one never stands for a newer decision.</summary>
        public void Hush() => speaking = false;

        /// <summary>
        /// The line above its head right now, or null: the words, then for a refusal "stopped by"
        /// the guard on a second line. For tests; the HUD draws the parts.
        /// </summary>
        public string Speech => !speaking ? null
            : SpeechGuard == null ? SpeechText : SpeechText + "\nstopped by " + SpeechGuard;

        /// <summary>The words being said, while <see cref="Speech"/> is not null.</summary>
        public string SpeechText { get; private set; }
        public SpeechKind SpeechType { get; private set; }
        /// <summary>The guard that stopped the decision, for a refusal; null otherwise.</summary>
        public string SpeechGuard { get; private set; }

        /// <summary>Suggested things to say to this agent, offered as chips in the dialogue panel.</summary>
        public virtual IReadOnlyList<string> Suggestions => System.Array.Empty<string>();

        protected virtual void Update()
        {
            if (speaking && Time.time > speechUntil) speaking = false;
        }

        protected static string IdOf(Transform target)
        {
            var targetable = target.GetComponentInParent<Targetable>();
            return targetable != null && !string.IsNullOrEmpty(targetable.Id) ? targetable.Id : target.name;
        }

        protected void Report(string message) => Debug.Log("[GreyBoxVillage " + name + "] " + message, this);
    }
}
