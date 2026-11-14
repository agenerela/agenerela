using System;
using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The end of a game: who won and how, provinces held by each realm over the turns, the wars and treaties as a
    /// short timeline, each ruler with its persona and a line or two, and the way on: watch the replay, a new game,
    /// or back to the map. Built from <see cref="EndSummary"/>.
    /// </summary>
    public sealed partial class ScreenUI
    {
        /// <param name="you">The country you ruled, or null when you watched.</param>
        public void ShowEnd(EndSummary s, string you, Action replay, Action newGame)
        {
            var card = Page("end", HideMenu);
            menu.Q(className: "menu").AddToClassList("end-card");

            string title = s.Winner == null ? "THE GAME STOPPED"
                : !s.Won ? $"NO WINNER · {CountryLook.NameOf(s.Winner).ToUpperInvariant()} LEADS"
                : you != null && s.Winner == you ? "YOU WON"
                : $"{CountryLook.NameOf(s.Winner).ToUpperInvariant()} WINS";
            var head = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            if (s.Winner != null) head.Add(new Crest(CountryLook.Of(s.Winner), 34) { style = { marginRight = 10 } });
            var ht = new VisualElement();
            ht.Add(Heading(title));
            var how = new Label($"{(s.Winner == null ? "" : Capitalised(s.How))}, after {s.Turns} turns." + (you != null && s.Winner != null && s.Winner != you ? $" You ruled {CountryLook.NameOf(you)}." : ""));
            how.AddToClassList("menu-sub"); how.style.unityTextAlign = TextAnchor.MiddleLeft; how.style.whiteSpace = WhiteSpace.Normal;
            ht.Add(how);
            head.Add(ht);
            card.Add(head);

            // The way on, first, so it is never below the fold.
            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 10, marginBottom = 4 } };
            foreach (var (text, act, note) in new (string, Action, string)[]
                     { ("Watch the replay", replay, "The whole game again from turn 1, every council open"), ("New game", newGame, null), ("Back to the map", HideMenu, "Esc") })
            {
                var b = BigButton(text, note, act); b.style.flexGrow = 1; b.style.flexBasis = 0; b.style.marginRight = 6; buttons.Add(b);
            }
            card.Add(buttons);

            card.Add(Section2("THE LAND", "provinces each realm held, turn by turn"));
            card.Add(new LandChart(s));

            if (s.Timeline.Count > 0)
            {
                card.Add(Section2("WARS AND TREATIES", s.Timeline.Count >= EndSummary.TimelineMax ? "the realms' falls, then the first wars and treaties" : ""));
                foreach (var m in s.Timeline)
                {
                    var row = new VisualElement(); row.AddToClassList("end-moment"); row.AddToClassList(m.Kind);
                    var t = new Label("Turn " + m.Turn); t.AddToClassList("end-when"); row.Add(t);
                    if (m.Country != null) row.Add(new Crest(CountryLook.Of(m.Country), 14) { style = { marginRight = 5 } });
                    var l = new Label(m.Text); l.AddToClassList("end-text"); row.Add(l);
                    card.Add(row);
                }
            }

            card.Add(Section2("THE RULERS", "each a model with a persona, in its own words"));
            foreach (var r in s.Rulers)
            {
                var look = CountryLook.Of(r.Country);
                var row = new VisualElement(); row.AddToClassList("end-ruler");
                var face = new Image { image = portraits, uv = Faces.Uv(r.Persona, look.Slot % 2), scaleMode = ScaleMode.ScaleAndCrop };
                face.AddToClassList("end-face");
                face.style.borderTopColor = face.style.borderBottomColor = face.style.borderLeftColor = face.style.borderRightColor = (Color)look.Colour;
                row.Add(face);
                var body = new VisualElement { style = { flexGrow = 1, flexShrink = 1 } };
                var name = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                name.Add(new Crest(look, 15) { style = { marginRight = 5 } });
                var n = new Label(look.Name + (r.Country == you ? " (you)" : "")); n.AddToClassList("end-name"); name.Add(n);
                string fate = r.Fell > 0 ? $"fell on turn {r.Fell}" : $"{r.Provinces} provinces at the end";
                // A model ruler: its persona and model; a scripted one names only its style.
                string kind = r.Persona != null ? $"{Capitalised(r.Persona)} · {r.Model}" : Capitalised(r.Model ?? "ruler");
                var who = new Label(r.Country == you ? $"you · {fate}" : $"{kind} · {fate}"); who.AddToClassList("end-who"); name.Add(who);
                body.Add(name);
                foreach (var line in r.Lines) { var q = new Label($"“{line}”"); q.AddToClassList("end-line"); body.Add(q); }
                row.Add(body);
                card.Add(row);
            }
        }

        /// <summary>Whether the end screen is open.</summary>
        public bool EndOpen => menu != null && menu.Q(className: "end-card") != null;

        private static string Capitalised(string t) => string.IsNullOrEmpty(t) ? t : char.ToUpperInvariant(t[0]) + t.Substring(1);

        /// <summary>Provinces held per realm over the turns: a line in each realm's colour, the winner's drawn last and thicker.</summary>
        private sealed class LandChart : VisualElement
        {
            private readonly EndSummary s;
            private readonly List<Label> ticks = new List<Label>();

            public LandChart(EndSummary s)
            {
                this.s = s;
                AddToClassList("end-chart");
                generateVisualContent += Draw;
                int max = Math.Max(1, s.Held.Values.Max(v => v.Count == 0 ? 0 : v.Max()));
                foreach (var (text, left, top) in new[] { ($"{max}", true, true), ("0", true, false), ($"turn {Math.Max(0, s.Held.Values.First().Count - 1)}", false, false) })
                {
                    var l = new Label(text); l.AddToClassList("end-tick");
                    l.style.position = Position.Absolute;
                    if (left) { l.style.left = 2; if (top) l.style.top = 0; else l.style.bottom = 12; }
                    else { l.style.right = 2; l.style.bottom = -2; }
                    Add(l);
                }
            }

            private void Draw(MeshGenerationContext ctx)
            {
                var p = ctx.painter2D;
                var r = contentRect;
                float x0 = 26, y0 = 6, x1 = r.width - 6, y1 = r.height - 18;
                int turns = Math.Max(1, s.Held.Values.First().Count - 1);
                int max = Math.Max(1, s.Held.Values.Max(v => v.Count == 0 ? 0 : v.Max()));
                // The frame: the baseline, and a faint line at each quarter of the most land held.
                p.lineWidth = 1;
                for (int q = 0; q <= 4; q++)
                {
                    float y = y1 - (y1 - y0) * q / 4f;
                    p.strokeColor = new Color(0.79f, 0.64f, 0.35f, q == 0 ? 0.6f : 0.14f);
                    p.BeginPath(); p.MoveTo(new Vector2(x0, y)); p.LineTo(new Vector2(x1, y)); p.Stroke();
                }
                foreach (var c in s.Held.Keys.OrderBy(c => c == s.Winner ? 1 : 0))
                {
                    var v = s.Held[c];
                    if (v.Count == 0) continue;
                    p.strokeColor = (Color)CountryLook.Of(c).Colour;
                    p.lineWidth = c == s.Winner ? 2.6f : 1.5f;
                    p.lineJoin = LineJoin.Round;
                    p.BeginPath();
                    for (int i = 0; i < v.Count; i++)
                    {
                        var pt = new Vector2(x0 + (x1 - x0) * i / turns, y1 - (y1 - y0) * v[i] / max);
                        if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
                    }
                    p.Stroke();
                }
            }
        }
    }
}
