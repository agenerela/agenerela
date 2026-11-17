using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy.Replay
{
    /// <summary>The setting "Event windows": which of the storyteller's events open a window in the middle of the screen.</summary>
    public enum EventWindows { All, MineOnly, Off }

    /// <summary>How the screen presents one storyteller event to the person looking at it.</summary>
    public enum StoryShow
    {
        /// <summary>The event window in the middle of the screen: the turn waits for it when you rule.</summary>
        Window,
        /// <summary>A banner under the top bar that never stops the turn: a tale, which changes no number.</summary>
        Banner,
        /// <summary>The chronicler's small bubble on the map where it struck, as before the windows: the setting keeps it out of the middle.</summary>
        Bubble,
    }

    /// <summary>One thing an event did to a realm, in plain game terms: "+40 gold", "−12 soldiers".</summary>
    public sealed class StoryEffect
    {
        /// <summary>gold, income, soldiers, stability, fort, or other: the icon beside it.</summary>
        public string Kind;
        public string Text;
        /// <summary>+1 a gain, -1 a loss, 0 neither.</summary>
        public int Tone;
        public override string ToString() => Text;
    }

    /// <summary>A realm an event struck, as the viewer's court knows it.</summary>
    public sealed class StoryStruck
    {
        public string Country;
        /// <summary>Whether the viewer knows what it cost: always while watching and for your own realm; a rival's is its court's secret until the game is over.</summary>
        public bool Known;
        /// <summary>What every court hears of it, the rules' own news words: "suffered a flood in Ashford".</summary>
        public string News;
        /// <summary>A dilemma: what the realm chose, in words; null otherwise. What rulers do is public.</summary>
        public string Chose;
        /// <summary>What it did to the realm; empty when <see cref="Known"/> is false, or when it was a story only.</summary>
        public List<StoryEffect> Effects = new List<StoryEffect>();
    }

    /// <summary>
    /// A storyteller event as the event window and the tale banner show it: everything on it comes from the event the
    /// game recorded (<see cref="EventRecord"/>), cut down to what the viewer's court knows.
    /// </summary>
    public sealed class StoryCard
    {
        public ReplayEvent Event;
        public int Turn;
        public string Title, Story;
        /// <summary>The line over the title: "AN EVENT IN YOUR REALM", "AN EVENT IN VALEN", "A TALE".</summary>
        public string Kicker;
        /// <summary>A story with no rule effect: an omen, a tale's beginning or end.</summary>
        public bool Tale;
        public bool Dilemma;
        /// <summary>It struck the realm you rule.</summary>
        public bool Mine;
        public List<StoryStruck> Struck = new List<StoryStruck>();
    }

    /// <summary>
    /// What the storyteller's events look like to the person at the screen, worked out from the recorded event and
    /// nothing else, so tests can check it without a scene: the card, whether it opens a window, and how long a
    /// window stays when nobody has to dismiss it.
    /// </summary>
    public static class StoryCards
    {
        /// <param name="viewer">The realm you rule, or null while you watch (nothing is hidden).</param>
        /// <param name="gameOver">Once the game is over every court's books are open.</param>
        public static StoryCard For(ReplayEvent e, string viewer, Func<string, string> nameOf, Func<string, string> provinceName, bool gameOver = false)
        {
            var r = e.Story;
            var t = EventRules.Template(r.Template);
            var card = new StoryCard
            {
                Event = e, Turn = r.Turn, Title = r.Title ?? t?.FallbackTitle ?? Words(r.Template), Story = r.Story ?? "",
                Tale = IsTale(r), Dilemma = r.Options.Count > 0, Mine = viewer != null && r.Hit.Contains(viewer),
            };
            foreach (string c in r.Hit)
            {
                var s = new StoryStruck
                {
                    Country = c,
                    Known = viewer == null || gameOver || c == viewer,
                    News = (t?.NewsVerb ?? Words(r.Template)) + (r.Province != null ? " in " + provinceName(r.Province) : ""),
                };
                if (r.Choices.TryGetValue(c, out string chose))
                    s.Chose = "chose to " + (t?.Options?.FirstOrDefault(o => o.Id == chose)?.Label ?? Words(chose));
                if (s.Known) s.Effects = Effects(r, c);
                card.Struck.Add(s);
            }
            // Your own realm first, so it is in sight without scrolling on a small screen.
            card.Struck = card.Struck.OrderBy(s => s.Country == viewer ? 0 : 1).ToList();
            card.Kicker = card.Tale ? "A TALE"
                : card.Mine ? (r.Hit.Count > 1 ? "AN EVENT IN YOUR REALM AND OTHERS" : "AN EVENT IN YOUR REALM")
                : r.Hit.Count == 0 ? "AN EVENT"
                : "AN EVENT IN " + string.Join(", ", r.Hit.Select(nameOf)).ToUpperInvariant();
            return card;
        }

        /// <summary>A story with no rule effect: nobody chose anything, and nothing it did was worth anything to anyone.</summary>
        public static bool IsTale(EventRecord r) =>
            r.Options.Count == 0 && r.Value.Values.All(v => v == 0) && r.Hit.All(c => Parts(r.Effect.TryGetValue(c, out string x) ? x : null).Count == 0);

        /// <summary>What an event did to one realm, a part at a time, each with its sign: the rules' own words ("40 gold") made "+40 gold".</summary>
        public static List<StoryEffect> Effects(EventRecord r, string country)
        {
            var list = new List<StoryEffect>();
            var parts = Parts(r.Effect.TryGetValue(country, out string text) ? text : null);
            if (parts.Count == 0) return list;
            // The sign of each part is the template's (a dilemma's: the chosen option's); with none, the sign of the whole.
            var t = EventRules.Template(r.Template);
            IReadOnlyList<EffectPart> signs = r.Choices.TryGetValue(country, out string chose) ? t?.Options?.FirstOrDefault(o => o.Id == chose)?.Parts : t?.Parts;
            int whole = Math.Sign(r.Value.TryGetValue(country, out int v) ? v : 0);
            for (int i = 0; i < parts.Count; i++)
            {
                int sign = signs != null && signs.Count == parts.Count ? signs[i].Sign : whole;
                list.Add(new StoryEffect { Kind = KindOf(parts[i]), Text = (sign > 0 ? "+" : sign < 0 ? "−" : "") + parts[i], Tone = sign });
            }
            return list;
        }

        // "chose to pay him to go: 30 gold, 1 stability" → ["30 gold", "1 stability"]; "a story only" and "nothing" → none.
        private static List<string> Parts(string effect)
        {
            if (string.IsNullOrWhiteSpace(effect)) return new List<string>();
            if (effect.StartsWith("chose", StringComparison.Ordinal))
            {
                int colon = effect.IndexOf(": ", StringComparison.Ordinal);
                if (colon < 0) return new List<string>();
                effect = effect.Substring(colon + 2);
            }
            return effect.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim())
                .Where(p => p.Length > 0 && p != "nothing" && p != "a story only" && p != "struck").ToList();
        }

        /// <summary>Which icon a part of an effect takes, from the rules' words for it.</summary>
        public static string KindOf(string part)
        {
            if (part.Contains("of income")) return "income";
            if (part.Contains("gold")) return "gold";
            if (part.Contains("soldier")) return "soldiers";
            if (part.Contains("stability")) return "stability";
            if (part.Contains("fort")) return "fort";
            return "other";
        }

        /// <summary>
        /// Where an event shows. All: every event, tales too, opens the window. Mine only: what strikes your realm opens
        /// it, a tale elsewhere is the banner (while you watch, no realm is yours). Otherwise the map's bubble, as before.
        /// The chronicle has every one of them under every setting.
        /// </summary>
        public static StoryShow Show(StoryCard card, EventWindows setting)
        {
            // Every event in its window, tales too, so each one is seen and stops the turn (the lead, 17 November).
            if (setting == EventWindows.All || (setting == EventWindows.MineOnly && card.Mine)) return StoryShow.Window;
            // Mine only: a tale elsewhere still shows, as the banner that never stops the turn.
            if (setting == EventWindows.MineOnly && card.Tale) return StoryShow.Banner;
            return StoryShow.Bubble;
        }

        /// <summary>
        /// Whether the turn waits for you to dismiss the window: always, ruling or watching ("I want to check them and see
        /// each one", the lead, 17 November), except while turns are skipped or played for you (Skip, autoplay).
        /// </summary>
        public static bool Waits(string viewer, bool skipping) => !skipping;

        /// <summary>
        /// How long a window nobody has to dismiss stays, in seconds: time to read the title and the story at 1×, less at
        /// the faster speeds, never so short that it only flashes.
        /// </summary>
        public static float StaySeconds(StoryCard card, float speed)
        {
            float read = 3.5f + 0.045f * ((card.Title?.Length ?? 0) + (card.Story?.Length ?? 0));
            return Math.Max(1.2f, read / Math.Max(0.25f, speed));
        }

        private static string Words(string id) => string.IsNullOrEmpty(id) ? "" : id.Replace('_', ' ');
    }

    /// <summary>
    /// The windows of one turn, in the order the events land: when two events land in one turn the second waits behind
    /// the first, and each says where it stands ("1 of 2").
    /// </summary>
    public sealed class StoryQueue
    {
        private readonly List<StoryCard> cards = new List<StoryCard>();
        private int done;

        /// <summary>The turn whose windows are queued, or 0.</summary>
        public int Turn { get; private set; }
        /// <summary>How many windows this turn opens in all.</summary>
        public int Total => cards.Count;
        /// <summary>The window to show now, or null when none waits.</summary>
        public StoryCard Current => done < cards.Count ? cards[done] : null;
        /// <summary>How many still wait behind the one shown.</summary>
        public int Behind => Math.Max(0, cards.Count - done - 1);

        /// <summary>Queues a turn's windows, in the order its events land; a new turn forgets the last one's.</summary>
        public void Load(int turn, IEnumerable<StoryCard> windows)
        {
            Turn = turn;
            cards.Clear();
            cards.AddRange(windows);
            done = 0;
        }

        /// <summary>Whether this event's window is in the turn's queue.</summary>
        public bool Has(ReplayEvent e) => cards.Any(c => c.Event == e);

        /// <summary>"1 of 2" for an event of the turn; null when the turn has only one window, or the event has none.</summary>
        public string Place(ReplayEvent e)
        {
            int at = cards.FindIndex(c => c.Event == e);
            return at < 0 || cards.Count < 2 ? null : $"{at + 1} of {cards.Count}";
        }

        /// <summary>The window shown was dismissed (or closed itself): the next one, or null.</summary>
        public StoryCard Dismiss()
        {
            if (done < cards.Count) done++;
            return Current;
        }

        /// <summary>Marks an event's window as seen, wherever it stands: every window before it is passed too.</summary>
        public void Pass(ReplayEvent e)
        {
            int at = cards.FindIndex(c => c.Event == e);
            if (at >= 0) done = Math.Max(done, at + 1);
        }

        public void Clear() { cards.Clear(); done = 0; Turn = 0; }
    }
}
