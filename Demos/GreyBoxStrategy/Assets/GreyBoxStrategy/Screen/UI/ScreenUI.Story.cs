using System;
using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The storyteller on screen: its banner over the map, the event card, the choice card a
    /// dilemma puts in front of a person, and the tales followed from start to end. Its voice is
    /// the chronicler's, never a ruler's: a parchment banner with its quill crest, the story in
    /// italics, no face.
    /// </summary>
    public sealed partial class ScreenUI
    {
        private VisualElement eventCard, choiceVeil, talesPanel;
        private ScrollView talesBody;

        public bool EventOpen => eventCard != null;
        public bool TalesOpen => talesPanel != null && talesPanel.ClassListContains("on");
        public bool ChoiceOpen => choiceVeil != null;
        /// <summary>Opens a tale from the tales panel or the event card.</summary>
        public Action<ReplayEvent> EventPressed;
        /// <summary>Opens a decision of the storyteller's, the scribe's or a ruler's choice in the inspector.</summary>
        public Action<RecordedDecision, ReplayEvent> StoryDecisionPressed;
        /// <summary>The tales and the events played so far, for the tales panel.</summary>
        public Func<(IReadOnlyList<Chain> chains, int turn, IReadOnlyList<ReplayEvent> played)> TalesSource;

        // ---- the banner ----

        /// <summary>The chronicler tells the event where it strikes: its title and story, in its own voice.</summary>
        public VisualElement Tell(ReplayEvent e, Action onClick)
        {
            var r = e.Story;
            var el = new VisualElement { pickingMode = PickingMode.Position, tooltip = "Click: open the event" };
            el.AddToClassList("banner"); el.AddToClassList("tale-banner");
            el.RegisterCallback<ClickEvent>(_ => onClick?.Invoke());
            el.Add(new Crest(CountryLook.Storyteller, 30));
            var body = new VisualElement(); body.AddToClassList("banner-body");
            var who = new Label($"THE CHRONICLER · {r.Title.ToUpperInvariant()}"); who.AddToClassList("banner-who"); who.AddToClassList("tale-who"); body.Add(who);
            var l = new Label(r.Story); l.AddToClassList("banner-line"); l.AddToClassList("tale-line"); body.Add(l);
            var hit = new Label(Struck(r)); hit.AddToClassList("tale-hit"); body.Add(hit);
            el.Add(body);
            fx.Add(el);
            var b = new Banner { Element = el, Province = e.ProvinceIndex >= 0 ? e.ProvinceIndex : CapitalIndex(r.Hit.FirstOrDefault()) };
            banners.Add(b);
            PlaceBanner(b);
            el.schedule.Execute(() => el.AddToClassList("on")).StartingIn(16);
            return el;
        }

        private static string Struck(EventRecord r)
        {
            if (r.Hit.Count == 0) return "It struck no one.";
            if (r.Options.Count > 0) return $"{string.Join(", ", r.Hit.Select(CountryLook.NameOf))} must choose.";
            return string.Join(" · ", r.Hit.Select(c => $"{CountryLook.NameOf(c)}: {r.Effect.GetValueOrDefault(c) ?? "struck"}"));
        }

        // ---- the event card ----

        /// <summary>The whole event: title and story, who it struck and how, what each chose and said, its tale, and the storyteller's decisions.</summary>
        public void ShowEvent(ReplayEvent e)
        {
            CloseEvent();
            var r = e.Story;
            eventCard = new VisualElement(); eventCard.AddToClassList("panel"); eventCard.AddToClassList("event-card");
            var head = new VisualElement(); head.AddToClassList("panel-head");
            var t = new Label($"THE CHRONICLE · TURN {r.Turn}"); t.AddToClassList("panel-title"); head.Add(t);
            var close = new Button(CloseEvent) { text = "×", tooltip = "Close (Esc)" }; close.AddToClassList("tbtn"); head.Add(close);
            eventCard.Add(head);
            var body = new ScrollView(); body.AddToClassList("event-body"); eventCard.Add(body);

            var top = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            top.Add(new Crest(CountryLook.Storyteller, 34));
            var tt = new VisualElement { style = { marginLeft = 10, flexShrink = 1 } };
            var title = new Label(r.Title); title.AddToClassList("event-title"); tt.Add(title);
            var kind = EventRules.Template(r.Template);
            var sub = new Label($"{(r.Options.Count > 0 ? "A dilemma" : r.Template == EventRules.Omen ? "An omen" : "An event")} · {r.Template.Replace('_', ' ')} on {Target(r)}" +
                                (r.Province != null && r.Province != r.Target ? $" · {Cap(r.Province)}" : ""));
            sub.AddToClassList("insp-sub"); tt.Add(sub);
            top.Add(tt);
            body.Add(top);
            var story = new Label(r.Story); story.AddToClassList("event-story"); body.Add(story);
            if (r.Traits != null) { var tr = new Label($"{Cap(r.Province)}: {r.Traits}"); tr.AddToClassList("src"); body.Add(tr); }

            body.Add(SectionHead("WHO IT STRUCK"));
            foreach (string c in r.Hit)
            {
                var row = Row2(c);
                string effect = r.Effect.GetValueOrDefault(c);
                if (r.Choices.TryGetValue(c, out string chose))
                {
                    var option = EventRules.Template(r.Template)?.Options?.FirstOrDefault(o => o.Id == chose);
                    string by = r.ChosenBy.GetValueOrDefault(c) ?? "?";
                    // A dilemma's effect line already says what was chosen ("chose to pay him to go: 30 gold").
                    row.Add(Text(effect != null && effect.StartsWith("chose", StringComparison.Ordinal) ? $"{effect} ({By(by)})"
                        : $"chose to {option?.Label ?? chose.Replace('_', ' ')} ({By(by)})", "event-chose"));
                    if (effect != null && effect.StartsWith("chose", StringComparison.Ordinal)) effect = null;
                }
                if (effect != null) row.Add(Text(effect, "event-effect"));
                body.Add(row);
                if (e.ChoiceDecisions.TryGetValue(c, out var d))
                {
                    if (!string.IsNullOrWhiteSpace(d.Statement)) { var q = new Label($"“{d.Statement}”"); q.AddToClassList("lg-quote"); q.style.marginLeft = 26; body.Add(q); }
                    if (d.ModelName != "you") body.Add(InspectButton($"{CountryLook.NameOf(c)}'s choice, as the model made it", d, e));
                }
            }
            if (r.Spared.Count > 0) body.Add(Text("Spared by the fairness rules: " + string.Join(", ", r.Spared.Select(CountryLook.NameOf)), "src"));

            if (r.ChainId != null)
            {
                body.Add(SectionHead("ITS TALE"));
                var chain = TalesSource?.Invoke().chains?.FirstOrDefault(c => c.Id == r.ChainId);
                var b = new Button(() => { CloseEvent(); OpenTales(r.ChainId); }) { text = $"“{chain?.Title ?? r.ChainId}” · stage {r.ChainStage + 1} · follow the tale" };
                b.AddToClassList("tbtn"); b.AddToClassList("wide"); body.Add(b);
            }

            body.Add(SectionHead("THE STORYTELLER", "the framework's decisions behind it"));
            body.Add(Text(Verdict(r), r.StoryReplaced ? "event-replaced" : "src"));
            if (r.HistoryRef != null) body.Add(Text("History check: " + r.HistoryRef, "src"));
            if (e.Decision != null) body.Add(InspectButton($"The storyteller's decision: {e.Decision.Action} on {e.Decision.Target}", e.Decision, e));
            else body.Add(Text("Set by the dev menu: no storyteller decision behind it.", "src"));
            if (e.TitleDecision != null) body.Add(InspectButton($"The title call: “{e.TitleDecision.Statement}”", e.TitleDecision, e));
            Root.Add(eventCard);
        }

        private static string By(string by) => by == "person" ? "you" : by == "model" ? "the model" : by == "default" ? "no answer: the default" : by;

        private static string Target(EventRecord r) => r.Target != null && r.Target.StartsWith("tale_", StringComparison.Ordinal) ? "a tale" : CountryLook.Of(r.Target)?.Name ?? Cap(r.Target);

        private static string Verdict(EventRecord r)
        {
            if (r.StoryVerdict == null) return "Story check: kept as written.";
            if (r.StoryReplaced) return "Story check: " + r.StoryVerdict + ". The story above is the template's own; the effect stood.";
            return "Story check: " + r.StoryVerdict + ".";
        }

        private Button InspectButton(string text, RecordedDecision d, ReplayEvent e)
        {
            var b = new Button(() => StoryDecisionPressed?.Invoke(d, e)) { text = text, tooltip = "Open it in the inspector" };
            b.AddToClassList("tbtn"); b.AddToClassList("wide");
            return b;
        }

        private static VisualElement SectionHead(string title, string note = null)
        {
            var h = new VisualElement(); h.AddToClassList("sec-head"); h.style.marginTop = 10;
            h.Add(new Label(title));
            if (note != null) { var n = new Label(note); n.AddToClassList("sec-note"); h.Add(n); }
            return h;
        }

        private static Label Text(string text, string cls) { var l = new Label(text); l.AddToClassList(cls); l.style.whiteSpace = WhiteSpace.Normal; return l; }

        private static VisualElement Row2(string country)
        {
            var row = new VisualElement(); row.AddToClassList("event-row");
            row.Add(new Crest(CountryLook.Of(country), 16));
            var n = new Label(CountryLook.NameOf(country)); n.AddToClassList("event-name"); row.Add(n);
            return row;
        }

        public void CloseEvent() { eventCard?.RemoveFromHierarchy(); eventCard = null; }

        // ---- the choice card ----

        /// <summary>
        /// The storyteller's dilemma, for a person: the story, every option with what it costs or
        /// gives, the default marked. Nothing closes it but a choice: picking an option is one click,
        /// deciding a second, so a stray click never chooses. The turn waits behind it.
        /// </summary>
        public void ShowChoice(EventChoice c, Action<string> decide)
        {
            HideChoice();
            choiceVeil = new VisualElement(); choiceVeil.AddToClassList("choice-veil");
            var card = new VisualElement(); card.AddToClassList("panel"); card.AddToClassList("choice-card");
            choiceVeil.Add(card);
            var head = new VisualElement(); head.AddToClassList("choice-head");
            head.Add(new Crest(CountryLook.Storyteller, 40));
            var ht = new VisualElement { style = { marginLeft = 12, flexShrink = 1 } };
            var kicker = new Label("THE CHRONICLER ASKS YOU TO CHOOSE"); kicker.AddToClassList("choice-kicker"); ht.Add(kicker);
            var title = new Label(c.Title); title.AddToClassList("event-title"); ht.Add(title);
            head.Add(ht);
            card.Add(head);
            var story = new Label(c.Story); story.AddToClassList("event-story"); card.Add(story);
            card.Add(Text($"The turn waits for your choice. It lands at the next upkeep, with the event.", "src"));

            string picked = null;
            var rows = new List<(VisualElement row, string id)>();
            var go = new Button { text = "Choose an option" }; go.AddToClassList("menu-button"); go.AddToClassList("choice-go");
            go.SetEnabled(false);
            foreach (var o in c.Options)
            {
                var row = new VisualElement(); row.AddToClassList("choice-option");
                var top = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                var label = new Label(Cap(o.Label)); label.AddToClassList("choice-label"); top.Add(label);
                if (o.Id == c.DefaultOption) { var tag = new Label("DEFAULT"); tag.AddToClassList("tag"); tag.tooltip = "What lands if no choice is made"; top.Add(tag); }
                row.Add(top);
                var eff = new Label(o.Effect); eff.AddToClassList("choice-effect"); eff.EnableInClassList("gain", o.Value > 0); eff.EnableInClassList("cost", o.Value < 0); row.Add(eff);
                string id = o.Id, text = o.Label;
                row.RegisterCallback<ClickEvent>(_ =>
                {
                    picked = id;
                    foreach (var (r, rid) in rows) r.EnableInClassList("pick", rid == id);
                    go.text = $"Decide: {text}";
                    go.SetEnabled(true);
                });
                rows.Add((row, id));
                card.Add(row);
            }
            go.clicked += () => { if (picked != null) decide(picked); };
            var foot = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 10 } };
            foot.Add(new VisualElement { style = { flexGrow = 1 } });
            foot.Add(go);
            card.Add(foot);
            Root.Add(choiceVeil);
        }

        public void HideChoice() { choiceVeil?.RemoveFromHierarchy(); choiceVeil = null; }

        // ---- the tales ----

        /// <summary>The tales so far, each from its first event to its end: what happened, what was chosen, where it stands.</summary>
        public void OpenTales(string focus = null)
        {
            if (talesPanel == null)
            {
                talesPanel = Panel("TALES", "tales");
                var close = new Button(CloseTales) { text = "×", tooltip = "Close (Esc)" }; close.AddToClassList("tbtn");
                talesPanel.Q(className: "panel-head").Add(close);
                talesBody = new ScrollView(); talesBody.style.flexGrow = 1; talesPanel.Add(talesBody);
                Root.Add(talesPanel);
            }
            talesPanel.AddToClassList("on");
            RenderTales(focus);
        }

        public void CloseTales() => talesPanel?.RemoveFromClassList("on");

        public void ToggleTales() { if (TalesOpen) CloseTales(); else OpenTales(); }

        public void RenderTales(string focus = null)
        {
            if (talesBody == null || TalesSource == null) return;
            var (chains, turn, played) = TalesSource();
            talesBody.Clear();
            var shown = (chains ?? Array.Empty<Chain>()).Where(c => c.Events.Any(x => x.Turn <= turn)).OrderByDescending(c => c.Events[0].Turn).ToList();
            if (shown.Count == 0)
            {
                talesBody.Add(Text("No tale has begun yet. A tale is a story over several turns: the storyteller begins one, and what the rulers choose decides how it goes on.", "src"));
                return;
            }
            VisualElement focused = null;
            foreach (var c in shown)
            {
                var events = c.Events.Where(x => x.Turn <= turn).ToList();
                bool ended = events.Count == c.Events.Count && (c.Status == ChainStatus.Completed || c.Status == ChainStatus.Abandoned);
                var box = new VisualElement(); box.AddToClassList("tale"); box.EnableInClassList("focus", c.Id == focus);
                var h = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                var name = new Label($"“{c.Title ?? events[0].Title}”"); name.AddToClassList("tale-title"); h.Add(name);
                var st = new Label(ended ? c.Status.ToString().ToUpperInvariant() : events.Count < 2 ? "BEGUN" : "GOING ON");
                st.AddToClassList("tag"); st.EnableInClassList("land", ended && c.Status == ChainStatus.Completed); st.EnableInClassList("ref", ended && c.Status == ChainStatus.Abandoned);
                h.Add(st);
                box.Add(h);
                string about = string.Join(", ", c.Countries.Select(CountryLook.NameOf));
                box.Add(Text($"{(c.Mode == ChainMode.Skeleton ? "A planned tale" : "A tale the storyteller keeps")} of {about}{(c.Province != null ? " at " + Cap(c.Province) : "")}, from turn {events[0].Turn}.", "src"));
                for (int i = 0; i < events.Count; i++)
                {
                    var x = events[i];
                    var stage = new VisualElement(); stage.AddToClassList("tale-stage");
                    stage.tooltip = "Click: open the event";
                    var ev = played?.FirstOrDefault(p => p.Story == x);
                    if (ev != null) stage.RegisterCallback<ClickEvent>(_ => EventPressed?.Invoke(ev));
                    var line = new Label($"{i + 1}. Turn {x.Turn} · {x.Title}"); line.AddToClassList("tale-stage-title"); stage.Add(line);
                    stage.Add(Text(x.Story, "tale-story"));
                    foreach (var kv in x.Choices)
                    {
                        var option = EventRules.Template(x.Template)?.Options?.FirstOrDefault(o => o.Id == kv.Value);
                        stage.Add(Text($"{CountryLook.NameOf(kv.Key)} chose to {option?.Label ?? kv.Value.Replace('_', ' ')}.", "event-chose"));
                    }
                    box.Add(stage);
                }
                if (ended) box.Add(Text(c.Status == ChainStatus.Completed ? "The tale is told." : $"The tale was left untold: {c.EndReason ?? "it was not continued"}.", "tale-end"));
                else box.Add(Text("To be continued.", "tale-end"));
                talesBody.Add(box);
                if (c.Id == focus) focused = box;
            }
            if (focused != null) talesBody.schedule.Execute(() => talesBody.ScrollTo(focused)).StartingIn(30);
        }

    }
}
