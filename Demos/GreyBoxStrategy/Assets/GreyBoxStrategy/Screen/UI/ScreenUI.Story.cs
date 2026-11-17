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

        /// <summary>
        /// The one place a storyteller event's picture comes from: null leaves the window's picture frame empty. The art
        /// session's catalogue plugs in here (an event's template, title and province in, a texture out).
        /// </summary>
        public static Func<EventRecord, Texture2D> EventPicture = _ => null;

        /// <summary>The setting "Event windows", from the game's settings.</summary>
        public EventWindows EventWindowsSetting = EventWindows.All;

        private Action eventDismissed;

        /// <summary>
        /// The event window, in the style of a grand strategy game's: a picture, the title large, the story, each realm it
        /// struck with its crest and what it did in game terms with icons, and one button that dismisses it. Under it, the
        /// storyteller's decisions behind it. <paramref name="place"/> is "1 of 2" when a turn brings two;
        /// <paramref name="dismissed"/> runs when it closes, by the button, Enter, Esc or by itself.
        /// </summary>
        public void ShowEvent(ReplayEvent e, string place = null, Action dismissed = null)
        {
            // Another window in its place counts as dismissing it, so a turn waiting on it never waits for nothing.
            DismissEvent();
            var r = e.Story;
            var card = StoryCards.For(e, Fog.Viewer, CountryLook.NameOf, Cap, GameOver);
            eventDismissed = dismissed;
            eventCard = new VisualElement(); eventCard.AddToClassList("panel"); eventCard.AddToClassList("event-card"); eventCard.AddToClassList("story-window");
            eventCard.EnableInClassList("mine", card.Mine);
            if (card.Mine)
            {
                // Yours: the frame in your realm's colour. (A stronger sound plays here once the game has audio.)
                var c = (Color)CountryLook.Of(Fog.Viewer).Colour;
                eventCard.style.borderTopColor = eventCard.style.borderBottomColor = eventCard.style.borderLeftColor = eventCard.style.borderRightColor = c;
            }
            var head = new VisualElement(); head.AddToClassList("panel-head");
            var t = new Label($"THE CHRONICLE · TURN {r.Turn}" + (place != null ? $" · {place}" : "")); t.AddToClassList("panel-title"); head.Add(t);
            var close = new Button(DismissEvent) { text = "×", tooltip = "Close (Esc)" }; close.AddToClassList("tbtn"); head.Add(close);
            eventCard.Add(head);
            var body = new ScrollView(); body.AddToClassList("event-body"); eventCard.Add(body);

            // The picture: a framed slot until generated event pictures arrive (EventPicture).
            var picture = new VisualElement(); picture.AddToClassList("story-picture");
            var tex = EventPicture?.Invoke(r);
            if (tex != null) picture.style.backgroundImage = tex;
            else { var hint = new Label("A picture of the event will hang here"); hint.AddToClassList("story-picture-hint"); picture.Add(hint); }
            body.Add(picture);
            var kicker = new Label(card.Kicker); kicker.AddToClassList("story-kicker"); body.Add(kicker);
            var big = new Label(card.Title); big.AddToClassList("story-title"); body.Add(big);
            var tale = new Label(card.Story); tale.AddToClassList("story-text"); body.Add(tale);
            foreach (var s in card.Struck)
            {
                var row = new VisualElement(); row.AddToClassList("story-struck"); row.EnableInClassList("mine", s.Country == Fog.Viewer);
                row.Add(new Crest(CountryLook.Of(s.Country), 26));
                var col = new VisualElement(); col.AddToClassList("story-struck-col");
                var who = new Label(s.Country == Fog.Viewer ? $"{CountryLook.NameOf(s.Country)} (you)" : CountryLook.NameOf(s.Country)); who.AddToClassList("story-who"); col.Add(who);
                if (s.Chose != null) col.Add(Text(s.Chose, "event-chose"));
                var fx = new VisualElement(); fx.AddToClassList("story-effects");
                foreach (var x in s.Effects)
                {
                    var chip = new VisualElement(); chip.AddToClassList("story-effect"); chip.EnableInClassList("gain", x.Tone > 0); chip.EnableInClassList("loss", x.Tone < 0);
                    chip.Add(new Icon(EffectIcon(x.Kind), 14) { Tint = x.Tone > 0 ? Good : x.Tone < 0 ? Bad : RowInk });
                    chip.Add(new Label(x.Text));
                    fx.Add(chip);
                }
                if (s.Effects.Count > 0) col.Add(fx);
                else if (!s.Known) col.Add(Text($"News: {CountryLook.NameOf(s.Country)} {s.News}. What it cost them is their court's secret.", "src"));
                else if (s.Chose == null) col.Add(Text("A story only: nothing changed.", "src"));
                row.Add(col);
                body.Add(row);
            }
            if (card.Struck.Count == 0) body.Add(Text("It struck no realm.", "src"));
            var ok = new Button(DismissEvent) { text = card.Tale ? "So the tale goes" : "So be it", tooltip = "Close (Enter or Esc)" };
            ok.AddToClassList("menu-button"); ok.AddToClassList("story-ok");
            // The button stays in sight at the window's foot, outside the scroll, as End turn does in the council.
            var foot = new VisualElement(); foot.AddToClassList("story-foot"); foot.Add(ok);
            // Behind it: what the window above leaves out, never repeated, and never more than the fog allows.
            body.Add(SectionHead("BEHIND IT", "the storyteller's call and the framework's decisions"));
            var sub = new Label($"{(r.Options.Count > 0 ? "A dilemma" : r.Template == EventRules.Omen ? "An omen" : "An event")} · {r.Template.Replace('_', ' ')} on {Target(r)}" +
                                (r.Province != null && r.Province != r.Target ? $" · {Cap(r.Province)}" : ""));
            sub.AddToClassList("insp-sub"); body.Add(sub);
            if (r.Traits != null) { var tr = new Label($"{Cap(r.Province)}: {r.Traits}"); tr.AddToClassList("src"); body.Add(tr); }
            // A dilemma: who chose for each realm, and what its ruler said; what a ruler does and says is public.
            foreach (string c in r.Hit)
            {
                if (!r.Choices.ContainsKey(c)) continue;
                var row = Row2(c);
                row.Add(Text($"chosen by {By(r.ChosenBy.GetValueOrDefault(c) ?? "?")}", "event-chose"));
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
                var b = new Button(() => { DismissEvent(); OpenTales(r.ChainId); }) { text = $"“{chain?.Title ?? r.ChainId}” · stage {r.ChainStage + 1} · follow the tale" };
                b.AddToClassList("tbtn"); b.AddToClassList("wide"); body.Add(b);
            }

            body.Add(SectionHead("THE STORYTELLER", "the framework's decisions behind it"));
            body.Add(Text(Verdict(r), r.StoryReplaced ? "event-replaced" : "src"));
            if (r.HistoryRef != null) body.Add(Text("History check: " + r.HistoryRef, "src"));
            if (e.Decision != null) body.Add(InspectButton($"The storyteller's decision: {e.Decision.Action} on {e.Decision.Target}", e.Decision, e));
            else body.Add(Text("Set by the dev menu: no storyteller decision behind it.", "src"));
            if (e.TitleDecision != null) body.Add(InspectButton($"The title call: “{e.TitleDecision.Statement}”", e.TitleDecision, e));
            eventCard.Add(foot);
            Root.Add(eventCard);
            KeepLayout();
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

        /// <summary>Closes the event window without telling anyone: a new one replaces it, or the screen resets.</summary>
        public void CloseEvent() { eventCard?.RemoveFromHierarchy(); eventCard = null; eventDismissed = null; }

        /// <summary>The window's button, Enter, Esc, or its own timer: closes it and lets the turn go on.</summary>
        public void DismissEvent()
        {
            var then = eventDismissed;
            CloseEvent();
            then?.Invoke();
        }

        private static IconKind EffectIcon(string kind) => kind switch
        {
            "gold" => IconKind.Gold, "income" => IconKind.Gold, "soldiers" => IconKind.Army,
            "stability" => IconKind.Scales, "fort" => IconKind.Fort, _ => IconKind.Seal,
        };

        // ---- the tale banner: a story with no rule effect, under the top bar; it never stops the turn ----

        private VisualElement taleBanner;
        public bool TaleBannerShown => taleBanner != null;

        /// <summary>A tale under the top bar: the chronicler's crest, the title, the story's first lines. A click opens its window; it goes by itself.</summary>
        public void ShowTaleBanner(StoryCard card, float seconds)
        {
            taleBanner?.RemoveFromHierarchy();
            var b = new VisualElement { tooltip = "Click: the whole tale" }; b.AddToClassList("tale-strip");
            b.Add(new Crest(CountryLook.Storyteller, 30));
            var col = new VisualElement(); col.AddToClassList("tale-strip-col");
            var k = new Label(card.Kicker + " · TURN " + card.Turn); k.AddToClassList("tale-who"); col.Add(k);
            var t = new Label(card.Title); t.AddToClassList("tale-strip-title"); col.Add(t);
            var s = new Label(card.Story); s.AddToClassList("tale-line"); col.Add(Fold(s, 2));
            b.Add(col);
            var e = card.Event;
            b.RegisterCallback<ClickEvent>(_ => { HideTaleBanner(); EventPressed?.Invoke(e); });
            Root.Add(b);
            taleBanner = b;
            b.schedule.Execute(() => { if (taleBanner == b) HideTaleBanner(); }).StartingIn((long)(seconds * 1000));
        }

        public void HideTaleBanner() { taleBanner?.RemoveFromHierarchy(); taleBanner = null; }

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
            Root.AddToClassList("tales-open"); // the council panel moves left of it
            RenderTales(focus);
        }

        public void CloseTales() { talesPanel?.RemoveFromClassList("on"); Root.RemoveFromClassList("tales-open"); }

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
