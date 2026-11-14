using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy.Replay
{
    /// <summary>
    /// The end of a game in a page's worth (the end screen): who won and how, provinces held by each realm over the
    /// turns, the wars and treaties as a short timeline, and each ruler with its persona and a line or two of its own.
    /// Plain C#, from the replay alone, so the same game always ends the same way on screen.
    /// </summary>
    public sealed class EndSummary
    {
        public string Winner;
        public Ending? Ending;
        public int Turns;
        /// <summary>The ending in words: "by conquest, the last realm standing".</summary>
        public string How;
        /// <summary>True when the game had a winner; false for a stalemate, an unfinished game or one cut short.</summary>
        public bool Won;
        /// <summary>Per realm, provinces held at the start of the game and at the end of each turn (index 0 is the start).</summary>
        public readonly Dictionary<string, List<int>> Held = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        public readonly List<Entry> Timeline = new List<Entry>();
        public readonly List<Ruler> Rulers = new List<Ruler>();

        /// <summary>The timeline keeps at most this many moments: every realm's fall, then the wars and treaties, earliest first.</summary>
        public const int TimelineMax = 12;

        public sealed class Entry
        {
            public int Turn;
            public string Kind;   // "war", "peace", "fall"
            public string Text;
            public string Country, Other;
        }

        public sealed class Ruler
        {
            public string Country;
            public string Persona, Model;
            public int Provinces;
            /// <summary>The turn it fell, or 0 if it lasted.</summary>
            public int Fell;
            public readonly List<string> Lines = new List<string>();
        }

        public static string HowWords(Ending? e) => e switch
        {
            GreyBoxStrategy.Ending.LastStanding => "by conquest, the last realm standing",
            GreyBoxStrategy.Ending.Domination => "by holding the land the rules ask",
            GreyBoxStrategy.Ending.TurnLimit => "with the most land when the last turn ended",
            GreyBoxStrategy.Ending.Unfinished => "unfinished: the turns ran out with more than one realm standing; the leader is named, nobody won",
            GreyBoxStrategy.Ending.Stalemate => "in a stalemate: no land changed hands for many turns; the leader is named, nobody won",
            _ => "the game stopped before an ending",
        };

        public static EndSummary From(ReplayGame g, Func<string, string> nameOf)
        {
            nameOf = nameOf ?? (id => id);
            var s = new EndSummary
            {
                Winner = g.Result?.Winner,
                Ending = g.Result?.Ending,
                Turns = g.Result?.Turns ?? g.Turns.Count,
            };
            s.How = HowWords(s.Ending);
            s.Won = s.Ending == GreyBoxStrategy.Ending.LastStanding || s.Ending == GreyBoxStrategy.Ending.Domination || s.Ending == GreyBoxStrategy.Ending.TurnLimit;

            // Provinces held: owners at the start, then at each turn's end.
            foreach (var c in g.CountryIds) s.Held[c] = new List<int>();
            void Count(ReplayState st)
            {
                foreach (var c in g.CountryIds) s.Held[c].Add(st?.Owners?.Count(o => o == c) ?? 0);
            }
            Count(g.Initial);
            foreach (var t in g.Turns) Count(t.End);

            // The timeline: falls always; then wars and treaties, earliest first, to the cap.
            var falls = new List<Entry>(); var rest = new List<Entry>();
            foreach (var e in g.AllEvents)
            {
                string who = nameOf(e.Country), other = e.Other == null ? null : nameOf(e.Other);
                switch (e.Kind)
                {
                    case ReplayEventKind.Eliminated:
                        falls.Add(new Entry { Turn = e.Turn, Kind = "fall", Country = e.Country, Text = $"{who} falls" });
                        break;
                    case ReplayEventKind.WarDeclared:
                        rest.Add(new Entry { Turn = e.Turn, Kind = "war", Country = e.Country, Other = e.Other, Text = other == null ? $"{who} goes to war" : $"{who} declares war on {other}" });
                        break;
                    case ReplayEventKind.PeaceAccepted:
                    case ReplayEventKind.PeaceMutual:
                    case ReplayEventKind.TermAccepted:
                        rest.Add(new Entry { Turn = e.Turn, Kind = "peace", Country = e.Country, Other = e.Other, Text = other == null ? $"{who} makes peace" : $"{who} and {other} make peace" });
                        break;
                }
            }
            // A realm that lost its last province fell then, whether or not the rules called it out (a broken realm's
            // occupiers keep what they hold): the turn after which it never held land again.
            foreach (var c in g.CountryIds)
            {
                var held = s.Held[c];
                if (held.Count < 2 || held[0] == 0 || held[held.Count - 1] > 0 || c == s.Winner || falls.Any(f => f.Country == c)) continue;
                int last = held.FindLastIndex(n => n > 0);
                falls.Add(new Entry { Turn = last + 1, Kind = "fall", Country = c, Text = $"{nameOf(c)} loses its last province" });
            }
            s.Timeline.AddRange(falls);
            foreach (var m in rest)
            {
                if (s.Timeline.Count >= TimelineMax) break;
                // One line for a pair's peace on a turn, however many terms it took.
                if (s.Timeline.Any(x => x.Turn == m.Turn && x.Kind == m.Kind && Pair(x) == Pair(m))) continue;
                s.Timeline.Add(m);
            }
            s.Timeline.Sort((a, b) => a.Turn != b.Turn ? a.Turn.CompareTo(b.Turn) : string.CompareOrdinal(a.Kind, b.Kind));

            // The rulers, the winner first, then by land; each with up to two of its own distinct lines, its last ones.
            var lines = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var e in g.AllEvents)
            {
                var d = e.Decision;
                string st = d?.Statement?.Trim();
                if (d == null || string.IsNullOrEmpty(st) || e.Country == null || d.ModelName == "you") continue;
                if (!lines.TryGetValue(e.Country, out var l)) lines[e.Country] = l = new List<string>();
                l.Remove(st);
                l.Add(st);
            }
            foreach (var c in g.CountryIds)
            {
                var p = g.Players != null && g.Players.TryGetValue(c, out var rp) ? rp : null;
                var r = new Ruler
                {
                    Country = c, Persona = p?.Persona, Model = p?.Model,
                    Provinces = s.Held[c].Count > 0 ? s.Held[c][s.Held[c].Count - 1] : 0,
                    Fell = falls.FirstOrDefault(f => f.Country == c)?.Turn ?? 0,
                };
                if (lines.TryGetValue(c, out var mine)) r.Lines.AddRange(mine.Skip(Math.Max(0, mine.Count - 2)));
                s.Rulers.Add(r);
            }
            s.Rulers.Sort((a, b) => a.Country == s.Winner ? -1 : b.Country == s.Winner ? 1 : b.Provinces != a.Provinces ? b.Provinces.CompareTo(a.Provinces) : a.Fell == 0 ? -1 : b.Fell == 0 ? 1 : b.Fell.CompareTo(a.Fell));
            return s;
        }

        private static string Pair(Entry m) => string.CompareOrdinal(m.Country ?? "", m.Other ?? "") < 0 ? m.Country + "|" + m.Other : m.Other + "|" + m.Country;
    }
}
