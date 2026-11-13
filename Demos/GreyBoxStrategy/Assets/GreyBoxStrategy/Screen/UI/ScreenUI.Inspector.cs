using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The decision inspector, the tooltips and the thinking indicator. The inspector is the
    /// framework's window on this screen (design page, "The decision inspector, the showcase"):
    /// six numbered sections, each naming the fields it shows.
    /// </summary>
    public sealed partial class ScreenUI
    {
        private VisualElement inspector;
        private ScrollView inspectorBody;
        private Label inspectorTitle;
        private VisualElement thinking;
        private Label tip;
        private IVisualElementScheduledItem thinkingBeat;

        public bool InspectorOpen => inspector != null && inspector.ClassListContains("on");

        // ---- thinking: the council deciding (decision 24) ----

        private VisualElement BuildThinking()
        {
            thinking = new VisualElement { tooltip = "The council is deciding: every ruler is asked at once, on one snapshot" };
            thinking.AddToClassList("thinking");
            for (int i = 0; i < 3; i++) { var d = new VisualElement(); d.AddToClassList("thinking-dot"); thinking.Add(d); }
            var l = new Label("council deciding"); l.AddToClassList("thinking-label"); thinking.Add(l);
            int beat = 0;
            thinkingBeat = thinking.schedule.Execute(() =>
            {
                beat = (beat + 1) % 3;
                for (int i = 0; i < 3; i++) thinking[i].EnableInClassList("lit", i == beat);
            }).Every(260);
            thinkingBeat.Pause();
            return thinking;
        }

        public void SetThinking(bool on)
        {
            thinking.EnableInClassList("on", on);
            if (on) thinkingBeat.Resume(); else thinkingBeat.Pause();
        }

        // ---- tooltips: runtime UI Toolkit shows none of its own ----

        private void BuildTooltips()
        {
            tip = new Label { pickingMode = PickingMode.Ignore };
            tip.AddToClassList("tip");
            tip.style.display = DisplayStyle.None;
            Root.Add(tip);
            Root.RegisterCallback<PointerMoveEvent>(e =>
            {
                var t = e.target as VisualElement;
                while (t != null && string.IsNullOrEmpty(t.tooltip)) t = t.parent;
                if (t == null) { tip.style.display = DisplayStyle.None; return; }
                tip.text = t.tooltip;
                tip.style.display = DisplayStyle.Flex;
                var size = Root.layout.size;
                float x = e.position.x + 14, y = e.position.y + 16;
                if (x + 320 > size.x) x = e.position.x - 330;
                if (y + 80 > size.y) y = e.position.y - 80;
                tip.style.left = x; tip.style.top = y;
                tip.BringToFront();
            }, TrickleDown.TrickleDown);
            Root.RegisterCallback<PointerLeaveEvent>(_ => tip.style.display = DisplayStyle.None);
        }

        // ---- the inspector ----

        private VisualElement BuildInspector()
        {
            inspector = new VisualElement(); inspector.AddToClassList("panel"); inspector.AddToClassList("inspector");
            var head = new VisualElement(); head.AddToClassList("panel-head");
            head.Add(new Icon(IconKind.Eye, 13) { style = { marginRight = 7 } });
            inspectorTitle = new Label("DECISION"); inspectorTitle.AddToClassList("panel-title"); head.Add(inspectorTitle);
            var close = new Button(CloseInspector) { text = "×", tooltip = "Close (Esc)" }; close.AddToClassList("tbtn"); head.Add(close);
            inspector.Add(head);
            inspectorBody = new ScrollView(); inspectorBody.style.flexGrow = 1; inspector.Add(inspectorBody);
            return inspector;
        }

        public void CloseInspector() => inspector.RemoveFromClassList("on");

        public void ShowCard(DecisionCard c)
        {
            var look = CountryLook.Of(c.Country);
            var player = Players?.GetValueOrDefault(c.Country);
            inspectorTitle.text = $"DECISION · {look.Name.ToUpperInvariant()} · TURN {c.Turn}";
            inspectorBody.Clear();

            // header: face, crest, outcome
            var head = new VisualElement(); head.AddToClassList("insp-head");
            bool teller = CountryLook.IsStoryteller(c.Country);
            if (teller) head.Add(new Crest(look, 44)); // the chronicler has no face
            else
            {
                var face = new Image { image = portraits, uv = Faces.Uv(player?.Persona, look.Slot % 2), scaleMode = ScaleMode.ScaleAndCrop };
                face.AddToClassList("insp-face");
                face.style.borderTopColor = face.style.borderBottomColor = face.style.borderLeftColor = face.style.borderRightColor = (Color)look.Colour;
                head.Add(face);
            }
            var hb = new VisualElement { style = { marginLeft = 10, flexShrink = 1 } };
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            row.Add(new Crest(look, 18));
            var name = new Label(look.Name.ToUpperInvariant()); name.AddToClassList("insp-title"); row.Add(name);
            string cls = c.Outcome == CardOutcome.Done || c.Outcome == CardOutcome.Held ? "ok" : c.Outcome == CardOutcome.Contained ? "guard" : "ref";
            var badge = new Label(c.OutcomeText.ToUpperInvariant()); badge.AddToClassList("badge-outcome"); badge.AddToClassList(cls); row.Add(badge);
            hb.Add(row);
            var sub = new Label(teller ? $"The game's storyteller · {c.Moment} · before turn {c.Turn}'s upkeep"
                : $"The {player?.Persona ?? "ruler"} · the {c.Moment} · {c.ChainId}{(c.ChainHop > 0 ? " · hop " + c.ChainHop : "")}");
            sub.AddToClassList("insp-sub"); hb.Add(sub);
            head.Add(hb);
            inspectorBody.Add(head);

            // 1 the answer
            var s1 = Section("1 · WHAT THE MODEL ANSWERED", "action, then target, then statement");
            var box = new VisualElement(); box.AddToClassList("code-box");
            box.Add(Mono(c.AnswerJson));
            s1.Add(box);
            if (c.ModelWrote != null) s1.Add(Note($"Before the guard, the model wrote {c.ModelWrote}."));
            if (c.Outcome == CardOutcome.Contained) s1.Add(Note("A guard contained it: the agent ran none instead, and no statement was shown."));

            // 2 the menu
            var s2 = Section("2 · THE MOVES IT WAS OFFERED", $"{c.Offered.Count}, from its state");
            var chips = new VisualElement(); chips.AddToClassList("chips");
            for (int i = 0; i < c.Offered.Count; i++)
            {
                var chip = new Label(c.Offered[i]); chip.AddToClassList("mchip"); chip.AddToClassList("mono");
                if (i == c.Picked) { chip.AddToClassList("pick"); chip.tooltip = "Its choice"; }
                chips.Add(chip);
            }
            s2.Add(chips);
            bool report = Fog.KnowsReport(c.Country, GameOver);
            if (c.NotOffered.Count > 0 && report)
            {
                s2.Add(Note("Not offered, so absent from its schema:"));
                foreach (var n in c.NotOffered.Take(14)) { var l = new Label($"{n.move}  —  {n.why}"); l.AddToClassList("why-no"); s2.Add(l); }
                if (c.NotOffered.Count > 14) s2.Add(Note($"and {c.NotOffered.Count - 14} more"));
            }
            s2.Add(Src("TargetsUnavailable · ITargetAvailability, each with its reason"));

            // 3 what it was told
            var s3 = Section("3 · WHAT IT WAS TOLD", report ? $"{c.Standing.Count} standing + {c.ThisTurn.Count} this turn" : "private");
            if (!report)
            {
                s2.Add(Note("Why other moves were not offered is its court's business: it shows the treasury."));
                s3.Add(Note($"What {look.Name}'s court told it holds its treasury, stability and plans, so it stays private while the game runs. It opens when the game is over. What it did and said is above."));
            }
            else
            {
            s3.Add(Note("Standing facts (IObservationSource)"));
            var f1 = new VisualElement(); f1.AddToClassList("facts-box"); f1.Add(Mono(string.Join("\n", c.Standing))); s3.Add(f1);
            s3.Add(Note("The stimulus, then this turn's facts (ObservationPlacement.WithStimulus)"));
            var f2 = new VisualElement(); f2.AddToClassList("facts-box"); f2.Add(Mono(string.Join("\n", c.ThisTurn))); s3.Add(f2);
            s3.Add(Src($"ObservationsDropped: {c.ObservationsDropped}"));
            }

            // 4 guards
            var s4 = Section("4 · GUARDS, AND THE RE-CHECK WHEN IT LANDED", "");
            foreach (var g in c.Guards)
            {
                var gl = new VisualElement(); gl.AddToClassList("gline");
                var ic = new Icon(IconKind.Fort, 11); ic.AddToClassList(g.Good ? "good" : "bad"); gl.Add(ic);
                gl.Add(new Label(g.Name) { style = { marginLeft = 6 } });
                var st = new Label(g.State); st.AddToClassList("gline-state"); st.AddToClassList(g.Good ? "good" : "bad"); gl.Add(st);
                s4.Add(gl);
                var dl = new Label(g.Detail); dl.AddToClassList("gline-detail"); s4.Add(dl);
                if (g.Name == "TargetGroundingGuard" && c.Grounding.HasValue)
                {
                    var meter = new VisualElement(); meter.AddToClassList("meter"); meter.style.marginLeft = 16;
                    var fill = new VisualElement(); fill.AddToClassList("meter-fill");
                    fill.style.width = Length.Percent(Mathf.Clamp01(c.Grounding.Value) * 100);
                    fill.style.backgroundColor = g.Good ? (Color)new Color32(143, 191, 122, 255) : (Color)new Color32(216, 162, 74, 255);
                    meter.Add(fill);
                    var mark = new VisualElement(); mark.AddToClassList("meter-mark"); mark.style.left = Length.Percent(DecisionCard.GroundingThreshold * 100); meter.Add(mark);
                    s4.Add(meter);
                }
            }
            s4.Add(Src("GuardsFired · Verdict · TargetGrounding · Execution · ExecutionReason"));

            // 5 memory
            var s5 = Section("5 · MEMORY", "");
            s5.Add(KeyValue("Exchanges sent with this call", c.MemorySent.ToString()));
            s5.Add(KeyValue("Held but left out", c.MemoryDropped.ToString()));
            s5.Add(Src("HistoryTurnsSent · HistoryTurnsDropped. Only moves that landed are remembered."));

            // 6 model and time
            var s6 = Section("6 · MODEL AND TIME", "");
            s6.Add(KeyValue("Model", c.Model ?? "?"));
            if (!teller) s6.Add(KeyValue("Persona", (player?.Persona ?? "?") + $" · face {look.Slot % 2 + 1} of 2"));
            s6.Add(KeyValue("Tokens", $"{c.PromptTokens:N0} in · {c.CompletionTokens} out"));
            float total = Mathf.Max(0.01f, c.QueueWaitSeconds + c.LatencySeconds);
            var tb = new VisualElement(); tb.AddToClassList("timebar");
            if (c.QueueWaitSeconds > 0.005f)
            {
                var q = new Label(string.Format(CultureInfo.InvariantCulture, "queue {0:0.00} s", c.QueueWaitSeconds)) { tooltip = $"Waiting behind {c.QueuedAhead} other call(s): one model, one queue" };
                q.AddToClassList("timebar-part"); q.style.width = Length.Percent(c.QueueWaitSeconds / total * 100); q.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.25f);
                tb.Add(q);
            }
            var m = new Label(string.Format(CultureInfo.InvariantCulture, "model {0:0.00} s", c.LatencySeconds)) { tooltip = "The provider's request alone" };
            m.AddToClassList("timebar-part"); m.style.flexGrow = 1; m.style.backgroundColor = (Color)look.Colour; m.style.color = Color.white;
            tb.Add(m);
            s6.Add(tb);
            s6.Add(Src("QueueWaitSeconds · LatencySeconds · PromptTokens · CompletionTokens · Model"));

            var foot = new Label("Everything on this card comes from DecisionFeed.Recorded: the DecisionResult and its DecisionTelemetry, recorded when the table played. The game adds only the layout.");
            foot.AddToClassList("src"); foot.style.paddingLeft = 10; foot.style.paddingRight = 10; foot.style.paddingBottom = 10;
            inspectorBody.Add(foot);

            inspector.AddToClassList("on");
            inspectorBody.scrollOffset = Vector2.zero;
        }

        private VisualElement Section(string title, string note)
        {
            var s = new VisualElement(); s.AddToClassList("sec");
            var h = new VisualElement(); h.AddToClassList("sec-head");
            h.Add(new Label(title));
            if (!string.IsNullOrEmpty(note)) { var n = new Label(note); n.AddToClassList("sec-note"); h.Add(n); }
            s.Add(h);
            inspectorBody.Add(s);
            return s;
        }

        private Label Mono(string text) { var l = new Label(text); l.AddToClassList("mono"); if (monoFont != null) l.style.unityFontDefinition = FontDefinition.FromSDFFont(monoFont); return l; }
        private static Label Note(string text) { var l = new Label(text); l.AddToClassList("why-no"); l.AddToClassList("muted"); return l; }
        private static Label Src(string text) { var l = new Label(text); l.AddToClassList("src"); return l; }
        private static VisualElement KeyValue(string k, string v)
        {
            var r = new VisualElement(); r.AddToClassList("kv");
            var a = new Label(k); a.AddToClassList("kv-key"); r.Add(a);
            var b = new Label(v); b.AddToClassList("kv-val"); r.Add(b);
            return r;
        }

        /// <summary>A monospace OS font for answers and facts; null falls back to the screen's serif.</summary>
        public UnityEngine.TextCore.Text.FontAsset monoFont;
    }
}
