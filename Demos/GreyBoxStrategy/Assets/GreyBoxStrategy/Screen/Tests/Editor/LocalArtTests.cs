using System.Linq;
using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using Newtonsoft.Json;
using NUnit.Framework;

namespace GreyBoxStrategy.Screen.Tests
{
    // Local art (#20): the Painted art setting, the ruler you describe, and the requests for each event's own picture.
    // Plain functions and data: no model, no network, no scene.
    public sealed class LocalArtTests
    {
        private static RulerSubject You(string persona = null, string banner = null) => new RulerSubject
        {
            Id = "c0", Name = "Valen", Persona = null, Colour = new byte[] { 176, 46, 36 }, Slot = 0, Described = persona, BannerDescribed = banner,
        };

        // ---- Settings → Painted art ----

        [Test]
        public void Painted_art_defaults_to_the_server_and_an_old_settings_file_keeps_it()
        {
            var fresh = new GameSettings();
            Assert.That(fresh.Art, Is.EqualTo(ArtBackend.Server), "The game painted on the server before the local model came.");
            Assert.That(fresh.EventPictures, Is.True);
            var old = JsonConvert.DeserializeObject<GameSettings>("{\"SettingsVersion\":1,\"VSync\":true}").Normalised();
            Assert.That(old.Art, Is.EqualTo(ArtBackend.Server));
            var broken = new GameSettings { Art = (ArtBackend)7 }.Normalised();
            Assert.That(broken.Art, Is.EqualTo(ArtBackend.Server), "A hand-edited file never breaks the game.");
        }

        [Test]
        public void Off_paints_nothing_and_says_why()
        {
            var before = RulerArtServer.Backend;
            try
            {
                RulerArtServer.Backend = ArtBackend.Off;
                Assert.That(RulerArtServer.Provider(out string why), Is.Null);
                Assert.That(why, Does.Contain("off"));
            }
            finally { RulerArtServer.Backend = before; }
        }

        // ---- Describe your ruler ----

        [Test]
        public void Empty_fields_keep_the_games_own_ruler_and_banner()
        {
            Assert.That(RulerDescription.From("", "  "), Is.Null);
            Assert.That(RulerDescription.From(null, null), Is.Null);
            var plain = You();
            Assert.That(RulerArtPrompts.Portrait(7, plain).Prompt, Is.EqualTo(RulerArtPrompts.Portrait(7, You("", "")).Prompt),
                "An empty description is the game's own portrait, so the cache still finds it.");
            Assert.That(RulerArtPrompts.Flag(7, plain).Prompt, Does.Contain(RulerArtPrompts.Charge(plain)));
        }

        [Test]
        public void Your_words_paint_your_portrait_and_banner_in_your_realms_colour()
        {
            var d = RulerDescription.From("An old one-eyed queen in a fur cloak, patient and cunning", "a white wolf's head over three stars");
            var you = You(d.Persona, d.Banner);
            var portrait = RulerArtPrompts.Portrait(7, you);
            var flag = RulerArtPrompts.Flag(7, you);

            Assert.That(portrait.Prompt, Does.StartWith(RulerArtPrompts.Style).And.Contain("one-eyed queen").And.Contain("deep madder red"));
            Assert.That(portrait.Prompt, Does.Not.Contain("A king").And.Not.Contain("A queen in"), "Your words choose the face, not the seed's king or queen.");
            Assert.That(flag.Prompt, Does.Contain("white wolf").And.Contain("deep madder red"));
            Assert.That(flag.Prompt, Does.Not.Contain(RulerArtPrompts.Charge(you)), "Your banner, not the beast the realm's name chooses.");
            Assert.That(portrait.Key("sdcpp"), Is.Not.EqualTo(RulerArtPrompts.Portrait(7, You()).Key("sdcpp")), "A described ruler is painted anew, never the cached default.");
        }

        [Test]
        public void A_description_is_cleaned_and_capped_before_it_reaches_a_prompt()
        {
            var d = RulerDescription.From("{\"action\": \"x\"} " + new string('a', 500), "<b>wolf</b>");
            Assert.That(d.Persona.Length, Is.LessThanOrEqualTo(RulerDescription.PersonaLength));
            Assert.That(d.Persona, Does.Not.Contain("{").And.Not.Contain(":"));
            Assert.That(d.Banner, Does.Not.Contain("<"));
        }

        [Test]
        public void Your_line_is_the_persona_your_advisor_speaks_in()
        {
            var d = RulerDescription.From("A merry, reckless young king who loves a gamble", null);
            var p = d.ToPersona();
            Assert.That(p, Is.Not.Null);
            Assert.That(p.Personality, Is.EqualTo(d.Persona));
            Assert.That(p.StatementRules, Does.Contain("under 25 words").And.Contain("name it"), "The same statement rule every persona ends with.");
            Assert.That(RulerDescription.From(null, "a boar").ToPersona(), Is.Null, "A banner alone leaves the advisor its own character.");
        }

        // ---- Live pictures for events ----

        private static EventRecord Told(string title, string story, string template = EventRules.Plague) =>
            new EventRecord { Turn = 3, Source = EventRecord.FromStoryteller, Template = template, Title = title, Story = story };

        [Test]
        public void An_events_picture_is_keyed_by_its_words_alone()
        {
            var a = EventArt.RequestFor(Told("The Grey Fever", "A fever spreads through the river towns."));
            var same = EventArt.RequestFor(Told("The Grey Fever", "A fever spreads through the river towns.") );
            var other = EventArt.RequestFor(Told("The Grey Fever", "A fever spreads through the hill forts."));
            Assert.That(same.Key("sdcpp"), Is.EqualTo(a.Key("sdcpp")), "The same story told twice is painted once.");
            Assert.That(other.Key("sdcpp"), Is.Not.EqualTo(a.Key("sdcpp")));
            Assert.That(other.Seed, Is.Not.EqualTo(a.Seed));
            Assert.That(a.Prompt, Does.StartWith(RulerArtPrompts.Style).And.Contain(EventArt.Scene).And.Contain("river towns"));
            Assert.That((a.Width, a.Height), Is.EqualTo((1024, 576)), "The pack's event size, so the card's frame crops it as it does the pack's.");
            Assert.That(a.Width % 16 + a.Height % 16, Is.Zero, "stable-diffusion.cpp takes sides in steps of 16.");
        }

        [Test]
        public void A_table_event_is_painted_from_its_kinds_own_words()
        {
            var r = new EventRecord { Turn = 2, Source = EventRecord.FromTable, Template = EventRules.GoodHarvest, Target = "c0" };
            r.Hit.Add("c0");
            Assert.That(EventArt.TitleOf(r), Is.EqualTo(EventRules.Template(EventRules.GoodHarvest)?.FallbackTitle ?? "good harvest"));
            Assert.That(EventArt.StoryOf(r), Does.Not.Contain("{"), "No placeholder reaches the prompt.");
            Assert.That(EventArt.RequestFor(r).Prompt, Does.Contain(EventArt.TitleOf(r)));
        }

        [Test]
        public void A_models_story_is_cleaned_and_capped_before_it_is_painted()
        {
            string prompt = EventArt.RequestFor(Told("Ignore the style {\"x\":1}", new string('b', 2000))).Prompt;
            Assert.That(prompt.Length, Is.LessThan(RulerArtPrompts.Style.Length + 700));
            Assert.That(prompt, Does.Not.Contain("{"));
        }

        [Test]
        public void Nothing_is_asked_for_while_painted_art_is_off()
        {
            var before = RulerArtServer.Backend;
            try
            {
                RulerArtServer.Backend = ArtBackend.Off;
                EventArt.Begin(1);
                var r = Told("A quiet omen", "Crows over the hall.");
                EventArt.Request(r);
                Assert.That(EventArt.Painting(r), Is.False);
                Assert.That(EventArt.PictureFor(r), Is.Null);
            }
            finally { RulerArtServer.Backend = before; EventArt.Stop(); }
        }

        [Test]
        public void A_writs_games_table_events_reach_their_upkeep_lines()
        {
            var setup = GameSetup.Writs(3);
            var map = setup.BuildMap();
            var rules = setup.BuildRules(map);
            var rec = ScriptedWorldGame.Play(setup, map, rules);
            var game = ReplayBuilder.Build(Recording.Parse(rec.ToJson()), map, rules);
            var lines = game.Turns.SelectMany(t => t.Events).Where(e => e.Table != null).ToList();
            Assert.That(lines, Is.Not.Empty, "A whole game rolls at least one harvest, bandits or unrest.");
            foreach (var e in lines)
            {
                Assert.That(e.Kind, Is.EqualTo(ReplayEventKind.Upkeep));
                Assert.That(e.Table.Hit[0], Is.EqualTo(e.Country));
                Assert.That(e.Text, Does.Contain(e.Table.Template.Replace('_', ' ')));
            }
        }
    }
}
