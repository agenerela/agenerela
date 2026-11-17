using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Replay;
using Newtonsoft.Json;
using NUnit.Framework;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// The storyteller made impossible to miss (#20): which events open a window for which viewer under fog, what a
    /// rival's event shows you, the queue when two land in one turn, how long a window stays, and the setting.
    /// </summary>
    public class StoryCardsTests
    {
        private static string Name(string id) => char.ToUpperInvariant(id[0]) + id.Substring(1);

        private static ReplayEvent Event(string template, string hit, int value, string effect, string title = "The Event", string province = null)
        {
            var r = new EventRecord { Turn = 7, Source = EventRecord.FromStoryteller, Template = template, Target = hit, Province = province, Title = title, Story = "Something happened at the ford." };
            if (hit != null) { r.Hit.Add(hit); r.Value[hit] = value; r.Effect[hit] = effect; }
            return new ReplayEvent { Turn = 7, Kind = ReplayEventKind.Story, Story = r };
        }

        private static ReplayEvent Windfall(string hit) => Event(EventRules.Windfall, hit, 40, "40 gold", "A Windfall");
        private static ReplayEvent Plague(string hit) => Event(EventRules.Plague, hit, -24, "12 soldiers", "The Plague");
        private static ReplayEvent Omen(string hit) => Event(EventRules.Omen, hit, 0, "a story only", "An Omen");

        [Test]
        public void An_event_on_your_realm_is_yours_with_its_effect_signed_and_iconed()
        {
            var c = StoryCards.For(Windfall("kesh"), "kesh", Name, Name);
            Assert.That(c.Mine, Is.True);
            Assert.That(c.Kicker, Is.EqualTo("AN EVENT IN YOUR REALM"));
            Assert.That(c.Struck.Single().Known, Is.True);
            Assert.That(c.Struck.Single().Effects.Select(x => x.Text), Is.EqualTo(new[] { "+40 gold" }));
            Assert.That(c.Struck.Single().Effects.Single().Kind, Is.EqualTo("gold"));
            var p = StoryCards.For(Plague("kesh"), "kesh", Name, Name).Struck.Single().Effects.Single();
            Assert.That((p.Text, p.Kind, p.Tone), Is.EqualTo(("−12 soldiers", "soldiers", -1)), "a loss is signed as one");
        }

        [Test]
        public void A_rivals_event_under_fog_shows_the_news_and_not_what_it_cost()
        {
            var c = StoryCards.For(Event(EventRules.BadHarvest, "valen", -30, "30 gold of income", "The Blight", "ashford"), "kesh", Name, Name);
            var s = c.Struck.Single();
            Assert.That(c.Mine, Is.False);
            Assert.That(c.Kicker, Is.EqualTo("AN EVENT IN VALEN"));
            Assert.That(s.Known, Is.False, "a rival's treasury is its court's secret");
            Assert.That(s.Effects, Is.Empty);
            Assert.That(s.News, Does.EndWith("in Ashford"), "where it struck is news to every court");
            Assert.That(StoryCards.For(Event(EventRules.BadHarvest, "valen", -30, "30 gold of income"), null, Name, Name).Struck.Single().Effects, Is.Not.Empty, "watching, nothing is hidden");
            Assert.That(StoryCards.For(Event(EventRules.BadHarvest, "valen", -30, "30 gold of income"), "kesh", Name, Name, gameOver: true).Struck.Single().Known, Is.True, "nor once the game is over");
            var all = Event(EventRules.BadHarvest, "valen", -30, "30 gold of income");
            all.Story.Hit.Add("orsk"); all.Story.Hit.Add("kesh"); all.Story.Effect["kesh"] = "15 gold of income"; all.Story.Value["kesh"] = -15;
            var many = StoryCards.For(all, "kesh", Name, Name);
            Assert.That(many.Struck.Select(x => x.Country), Is.EqualTo(new[] { "kesh", "valen", "orsk" }), "yours first, the rest as they were struck");
            Assert.That(many.Kicker, Is.EqualTo("AN EVENT IN YOUR REALM AND OTHERS"));
        }

        [Test]
        public void A_story_with_no_effect_is_a_tale_and_takes_the_banner()
        {
            var tale = StoryCards.For(Omen("kesh"), "kesh", Name, Name);
            Assert.That(tale.Tale, Is.True);
            Assert.That(tale.Kicker, Is.EqualTo("A TALE"));
            Assert.That(tale.Struck.Single().Effects, Is.Empty);
            Assert.That(StoryCards.Show(tale, EventWindows.All), Is.EqualTo(StoryShow.Banner));
            Assert.That(StoryCards.For(Windfall("kesh"), "kesh", Name, Name).Tale, Is.False);
        }

        [Test]
        public void The_setting_decides_which_events_open_a_window_for_whom()
        {
            var mine = StoryCards.For(Windfall("kesh"), "kesh", Name, Name);
            var rival = StoryCards.For(Windfall("valen"), "kesh", Name, Name);
            var watched = StoryCards.For(Windfall("valen"), null, Name, Name);
            Assert.That(StoryCards.Show(mine, EventWindows.All), Is.EqualTo(StoryShow.Window));
            Assert.That(StoryCards.Show(rival, EventWindows.All), Is.EqualTo(StoryShow.Window), "all: a rival's too, cut to what fog allows");
            Assert.That(StoryCards.Show(mine, EventWindows.MineOnly), Is.EqualTo(StoryShow.Window));
            Assert.That(StoryCards.Show(rival, EventWindows.MineOnly), Is.EqualTo(StoryShow.Bubble));
            Assert.That(StoryCards.Show(watched, EventWindows.MineOnly), Is.EqualTo(StoryShow.Bubble), "watching, no realm is yours");
            Assert.That(StoryCards.Show(mine, EventWindows.Off), Is.EqualTo(StoryShow.Bubble));
        }

        [Test]
        public void Two_events_in_one_turn_wait_one_behind_the_other()
        {
            var a = Windfall("kesh"); var b = Plague("valen"); var other = Windfall("orsk");
            var q = new StoryQueue();
            q.Load(7, new[] { a, b }.Select(e => StoryCards.For(e, "kesh", Name, Name)));
            Assert.That(q.Total, Is.EqualTo(2));
            Assert.That(q.Current.Event, Is.SameAs(a), "in the order they land");
            Assert.That(q.Place(a), Is.EqualTo("1 of 2"));
            Assert.That(q.Place(b), Is.EqualTo("2 of 2"));
            Assert.That(q.Place(other), Is.Null);
            Assert.That(q.Behind, Is.EqualTo(1));
            Assert.That(q.Dismiss().Event, Is.SameAs(b));
            Assert.That(q.Dismiss(), Is.Null);
            q.Load(7, new[] { StoryCards.For(a, "kesh", Name, Name) });
            Assert.That(q.Place(a), Is.Null, "one window says no count");
            q.Load(8, new[] { a, b }.Select(e => StoryCards.For(e, "kesh", Name, Name)));
            q.Pass(b);
            Assert.That(q.Current, Is.Null, "passing the second passes the first");
        }

        [Test]
        public void The_window_waits_only_when_you_rule_and_are_not_skipping()
        {
            Assert.That(StoryCards.Waits("kesh", skipping: false), Is.True);
            Assert.That(StoryCards.Waits("kesh", skipping: true), Is.False, "never blocks Skip or autoplay");
            Assert.That(StoryCards.Waits(null, skipping: false), Is.False, "watching, it closes by itself");
        }

        [Test]
        public void A_window_nobody_dismisses_stays_long_enough_to_read_and_shorter_when_faster()
        {
            var c = StoryCards.For(Windfall("kesh"), null, Name, Name);
            float one = StoryCards.StaySeconds(c, 1), four = StoryCards.StaySeconds(c, 4);
            Assert.That(one, Is.GreaterThan(4f));
            Assert.That(four, Is.EqualTo(one / 4).Within(0.01f).Or.EqualTo(1.2f));
            Assert.That(StoryCards.StaySeconds(c, 32), Is.EqualTo(1.2f), "never a flash");
        }

        [Test]
        public void Event_windows_default_to_all_and_survive_a_bad_file()
        {
            Assert.That(new GameSettings().EventWindows, Is.EqualTo(EventWindows.All));
            var s = new GameSettings { EventWindows = EventWindows.MineOnly };
            Assert.That(JsonConvert.DeserializeObject<GameSettings>(JsonConvert.SerializeObject(s)).Normalised().EventWindows, Is.EqualTo(EventWindows.MineOnly));
            Assert.That(new GameSettings { EventWindows = (EventWindows)9 }.Normalised().EventWindows, Is.EqualTo(EventWindows.All));
        }

        [Test]
        public void The_window_for_a_rivals_event_under_fog_shows_its_cost_nowhere()
        {
            var g = ReplayBuilder.Build(new Recording { Seed = 11, Title = "empty" }, partial: true);
            var ui = new GreyBoxStrategy.Realm.ScreenUI(new UnityEngine.UIElements.VisualElement(), UnityEngine.ScriptableObject.CreateInstance<UnityEngine.UIElements.StyleSheet>(), null,
                g.CountryIds, g.ProvinceIds, new Dictionary<string, RecordedPlayer>(), "test");
            ui.Fog = new Fog("kesh", new RealmQueries(g));
            string Shown() => string.Join(" | ", UnityEngine.UIElements.UQueryExtensions.Query<UnityEngine.UIElements.Label>(ui.Root).ToList().Select(l => l.text));
            ui.ShowEvent(Event(EventRules.Plague, "valen", -24, "17 soldiers", "The Plague"));
            Assert.That(Shown(), Does.Not.Contain("17 soldiers"), "a rival's losses are its court's secret");
            Assert.That(Shown(), Does.Contain("The Plague").And.Contain("Valen"));
            ui.ShowEvent(Event(EventRules.Plague, "kesh", -24, "17 soldiers", "The Plague"));
            Assert.That(Shown(), Does.Contain("−17 soldiers"), "your own, in full");
        }

        [Test]
        public void A_dilemmas_choice_is_said_and_its_cost_signed_from_the_option()
        {
            var t = EventRules.AllTemplates.First(x => x.Options != null && x.Options.Any(o => o.Parts.Count > 0 && o.Parts.All(p => p.Sign < 0)));
            var o = t.Options.First(x => x.Parts.Count > 0 && x.Parts.All(p => p.Sign < 0));
            var e = Event(t.Id, "kesh", -30, $"chose to {o.Label}: " + string.Join(", ", o.Parts.Select(_ => "30 gold")));
            e.Story.Options.AddRange(t.Options.Select(x => x.Id));
            e.Story.Choices["kesh"] = o.Id;
            var s = StoryCards.For(e, "kesh", Name, Name).Struck.Single();
            Assert.That(s.Chose, Is.EqualTo("chose to " + o.Label));
            Assert.That(s.Effects.All(x => x.Tone < 0 && x.Text.StartsWith("−")), Is.True);
        }
    }
}
