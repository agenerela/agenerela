using System;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The advisor in the council (rule mode on the writs turn): its counsel in a sentence and its orders as
    /// suggestions, each the legal move it maps to. "Take the advice" fills your plan with them; one can be taken
    /// alone, any changed after; nothing is sent until End turn.
    /// </summary>
    public sealed partial class ScreenUI
    {
        /// <summary>Whether the advisor is on in this game: its section shows.</summary>
        public bool AdvisorShown;
        /// <summary>This turn's advice, or null while it thinks, when it failed, or when it is off.</summary>
        public Advice Advice;
        public bool AdvisorThinking;
        public string AdvisorError;
        /// <summary>"Why?" on a suggestion: the director opens the inspector on that decision, marked as advice.</summary>
        public Action<AdviceItem> AdviceWhy;

        /// <summary>For the director: what the advice section shows, so the council is rebuilt when it changes.</summary>
        public string AdviceKey() => AdvisorShown + "|" + AdvisorThinking + "|" + AdvisorError + "|" + (Advice == null ? "" : Advice.Turn + ":" + Advice.Items.Count + ":" + Advice.GetHashCode());

        private void AdvisorSection(VisualElement body, CountryRequest req)
        {
            if (!AdvisorShown) return;
            body.Add(SectionHead("YOUR ADVISOR", "a model on your seat; nothing is sent until End turn"));
            if (AdvisorThinking) { body.Add(Text("Your advisor is weighing the turn, beside the rulers…", "src")); return; }
            if (AdvisorError != null) { body.Add(Text("Your advisor could not answer this turn: " + AdvisorError, "why-no")); return; }
            var advice = Advice;
            if (advice == null || advice.Turn != req.Turn) { body.Add(Text("No advice this turn.", "src")); return; }

            if (!string.IsNullOrWhiteSpace(advice.Counsel))
            {
                var counsel = new Label($"“{advice.Counsel.Trim()}”"); counsel.AddToClassList("advice-counsel");
                body.Add(counsel);
            }
            foreach (var item in advice.Items)
            {
                bool army = item.Request.Moment == Moment.Army;
                string who = army ? Chronicle.Host(item.Request.ArmyId) : "The court";
                string what = army
                    ? (item.Move.Action == Actions.March ? "march to " + Cap(item.Move.Target) : "hold")
                    : Describe(item.Move);
                bool taken = army ? Plan.Armies.TryGetValue(item.Request.ArmyId, out var given) && given.Equals(item.Move)
                                  : Plan.Court != null && Plan.Court.Equals(item.Move);
                var row = new VisualElement(); row.AddToClassList("advice-row");
                var l = new Label($"{who}: {Lowered(what)}"); l.AddToClassList("advice-text"); row.Add(l);
                if (!item.AsAsked) l.tooltip = "Your advisor named a move the rules do not offer here; the advice holds instead.";
                var take = new Button(() => { TakeAdvice(item); }) { text = taken ? "Taken" : "Take", tooltip = "Put this order in your plan; you can still change it" };
                take.AddToClassList("order-move"); take.AddToClassList("advice-take"); if (taken) take.AddToClassList("pick");
                row.Add(take);
                var why = new Button(() => AdviceWhy?.Invoke(item)) { text = "Why?", tooltip = "What your advisor read and answered, in the inspector" };
                why.AddToClassList("tbtn"); why.AddToClassList("advice-why");
                row.Add(why);
                body.Add(row);
            }
            bool all = advice.Items.All(i => i.Request.Moment == Moment.Army
                ? Plan.Armies.TryGetValue(i.Request.ArmyId, out var g) && g.Equals(i.Move)
                : Plan.Court != null && Plan.Court.Equals(i.Move));
            var takeAll = new Button(() =>
            {
                var plan = RuleSession.WritsPlan.From(advice);
                Plan.Court = plan.Court;
                foreach (var kv in plan.Armies) Plan.Armies[kv.Key] = kv.Value;
                PlanChanged?.Invoke();
                ShowOrders(ordersStatus?.text);
            }) { text = all ? "The advice is your plan" : "Take the advice", tooltip = "Fill your plan with every order your advisor proposes. Change any of them after; nothing is sent until End turn." };
            takeAll.AddToClassList("order-move"); takeAll.AddToClassList("wide"); takeAll.AddToClassList("advice-all");
            if (all) takeAll.AddToClassList("pick");
            body.Add(takeAll);
        }

        private void TakeAdvice(AdviceItem item)
        {
            if (item.Request.Moment == Moment.Army) Plan.Armies[item.Request.ArmyId] = item.Move;
            else Plan.Court = item.Move;
            PlanChanged?.Invoke();
            ShowOrders(ordersStatus?.text);
        }
    }
}
