using System.Linq;
using System.Threading.Tasks;
using Agenerela;
using Agenerela.Providers;
using NUnit.Framework;
using static GreyBoxStrategy.Tests.TestGames;

namespace GreyBoxStrategy.Tests
{
    /// <summary>
    /// DR-023's storyteller A/B: an event's strength in the action's id (bundled) or as a parameter.
    /// Off by default (<see cref="GameRules.EventStrength"/>); both forms offer the same events under
    /// the same rule and apply the same amounts.
    /// </summary>
    public sealed class StorytellerStrengthTests
    {
        // A calm game with the random table off, the strength form set, played to the end of turn 2.
        private static Game AtTurnTwo(EventStrengthForm form, int seed = 1)
        {
            var g = NewGame(seed);
            g.EventTable = false;
            g.Rules.EventStrength = form;
            PlayTurn(g);
            PlayTurn(g);
            return g;
        }

        private static string Reply(string action, string target, string strength, string statement) =>
            "{\"action\": \"" + action + "\", \"target\": \"" + target + "\""
            + (strength != null ? ", \"strength\": \"" + strength + "\"" : "")
            + ", \"statement\": \"" + statement + "\"}";

        [Test]
        public void TheDefaultGame_AsksNoStrength_AndTheStorytellerIsUnchanged()
        {
            Assert.That(GameRules.Standard.EventStrength, Is.EqualTo(EventStrengthForm.None));
            Assert.That(ModelSeatOptions.Named("default+storyteller").EventStrength, Is.EqualTo(EventStrengthForm.None));
            var g = AtTurnTwo(EventStrengthForm.None);
            var teller = new Storyteller(g, new ScriptedProvider());
            Assert.That(teller.Agent.Actions.Definitions.Select(d => d.Id), Is.EquivalentTo(EventRules.Templates.Select(t => t.Id)));
            Assert.That(teller.Agent.Actions.Definitions.Any(d => d.DeclaresParameters), Is.False);
            Assert.That(teller.OpenActions(), Is.EqualTo(EventRules.Open(g.World, g.Events, g.NextTurn)));
            Assert.That(g.WhyNotEvent(EventRules.Windfall, "valen", EventRules.Major), Does.Contain("asks no strength"));
        }

        [Test]
        public void TheArms_Parse()
        {
            Assert.That(ModelSeatOptions.Named("default+storyteller+strength-bundled").EventStrength, Is.EqualTo(EventStrengthForm.Bundled));
            Assert.That(ModelSeatOptions.Named("default+storyteller+strength-param").EventStrength, Is.EqualTo(EventStrengthForm.Parameter));
            Assert.That(EventRules.TryParseBundled("bad_harvest_major", out string t, out string s), Is.True);
            Assert.That((t, s), Is.EqualTo((EventRules.BadHarvest, EventRules.Major)));
            Assert.That(EventRules.TryParseBundled("zeal_major", out _, out _), Is.False, "zeal takes no strength");
            Assert.That(EventRules.TryParseBundled(EventRules.Windfall, out _, out _), Is.False);
        }

        [Test]
        public void AStrength_ScalesTheAmount_OfTheFourEventsWithOne()
        {
            var w = AtTurnTwo(EventStrengthForm.Parameter).World;
            int Amount(string template, string strength) => EventRules.Plan(w, template, "valen", strength).amount;
            Assert.That((Amount(EventRules.Windfall, null), Amount(EventRules.Windfall, EventRules.Minor), Amount(EventRules.Windfall, EventRules.Major)),
                Is.EqualTo((30, 15, 45)));
            Assert.That((Amount(EventRules.BadHarvest, null), Amount(EventRules.BadHarvest, EventRules.Minor), Amount(EventRules.BadHarvest, EventRules.Major)),
                Is.EqualTo((15, 8, 23)));
            Assert.That((Amount(EventRules.Volunteers, null), Amount(EventRules.Volunteers, EventRules.Minor), Amount(EventRules.Volunteers, EventRules.Major)),
                Is.EqualTo((14, 7, 21)));
            int plague = Amount(EventRules.Plague, null);
            Assert.That(Amount(EventRules.Plague, EventRules.Major), Is.GreaterThan(plague));
            Assert.That(Amount(EventRules.Plague, EventRules.Minor), Is.LessThan(plague));
            Assert.That(EventRules.Plan(w, EventRules.Windfall, "valen", EventRules.Major).value, Is.EqualTo(45), "the budget counts the scaled value");
            Assert.That(Amount(EventRules.Zeal, EventRules.Major), Is.EqualTo(1), "zeal takes no strength");
        }

        [Test]
        public void TheStrengthRule_IsTheSameInBothForms()
        {
            foreach (var form in new[] { EventStrengthForm.Bundled, EventStrengthForm.Parameter })
            {
                var g = AtTurnTwo(form);
                Assert.That(g.WhyNotEvent(EventRules.Windfall, "valen", null), Does.Contain("needs a strength"), form.ToString());
                Assert.That(g.WhyNotEvent(EventRules.Unrest, "valen", EventRules.Major), Does.Contain("takes no strength"));
                Assert.That(g.WhyNotEvent(EventRules.Windfall, "valen", "huge"), Does.Contain("is not a strength"));
                Assert.That(g.WhyNotEvent(EventRules.Windfall, "valen", EventRules.Major), Is.Null);
                g.QueueEvent(new StoryEvent(EventRules.Windfall, "valen", "Gold Rain: Gold fell on valen.") { Strength = EventRules.Major });
                var e = PlayTurn(g).Events.Single();
                Assert.That((e.Strength, e.Value["valen"]), Is.EqualTo((EventRules.Major, 45)));
                Assert.That(g.WhyNotEvent(EventRules.Plague, "orsk", EventRules.Major), Is.EqualTo("two major events in a row"), "whoever it strikes");
                Assert.That(g.WhyNotEvent(EventRules.Plague, "orsk", EventRules.Minor), Is.Null);
            }
        }

        [Test]
        public void AStrengthGivenWhereNoneIsAsked_IsRefusedAtUpkeep()
        {
            var g = AtTurnTwo(EventStrengthForm.None);
            g.QueueEvent(new StoryEvent(EventRules.Windfall, "valen", "Gold Rain: Gold fell on valen.") { Strength = EventRules.Major });
            var report = PlayTurn(g);
            Assert.That(report.Events, Is.Empty);
            Assert.That(report.EventRefused, Does.Contain("asks no strength"));
        }

        [Test]
        public void TheBundledForm_RegistersTwoActionsAnEvent_AndClosesTheMajorOnesAfterAMajor()
        {
            var g = AtTurnTwo(EventStrengthForm.Bundled);
            var teller = new Storyteller(g, new ScriptedProvider());
            var ids = teller.Agent.Actions.Definitions.Select(d => d.Id).ToList();
            Assert.That(ids, Is.SupersetOf(new[] { "windfall_minor", "windfall_major", "plague_major", "volunteers_minor", "bad_harvest_major" }));
            Assert.That(ids, Does.Not.Contain(EventRules.Windfall));
            Assert.That(ids, Does.Contain(EventRules.Zeal));
            Assert.That(teller.Agent.Actions.Definitions.Any(d => d.DeclaresParameters), Is.False);
            Assert.That(teller.Agent.Actions.Definitions.First(d => d.Id == "windfall_major").Description, Does.EndWith("Major: about one and a half times that."));

            g.QueueEvent(new StoryEvent(EventRules.Windfall, "valen", "Gold Rain: Gold fell on valen.") { Strength = EventRules.Major });
            PlayTurn(g);
            var preview = teller.Agent.Preview(teller.Stimulus(), teller.OptionsForCall());
            var offered = preview.Available.Select(a => a.Id).ToList();
            Assert.That(offered, Does.Not.Contain("plague_major").And.Not.Contain("windfall_major"));
            Assert.That(offered, Does.Contain("plague_minor"));
            Assert.That(teller.OpenLine(), Does.Contain("plague_minor on").And.Not.Contain("plague_major"));
        }

        [Test]
        public void TheParameterForm_DeclaresStrength_AndItsValueCheckClosesMajorAfterAMajor()
        {
            var g = AtTurnTwo(EventStrengthForm.Parameter);
            var teller = new Storyteller(g, new ScriptedProvider());
            var windfall = teller.Agent.Actions.Definitions.First(d => d.Id == EventRules.Windfall);
            Assert.That(windfall.Parameters.Single().Name, Is.EqualTo("strength"));
            Assert.That(windfall.Parameters.Single().Values, Is.EqualTo(new[] { EventRules.Minor, EventRules.Major }));
            Assert.That(teller.Agent.Actions.Definitions.First(d => d.Id == EventRules.Zeal).DeclaresParameters, Is.False);
            Assert.That(teller.Agent.Actions.Definitions.Any(d => d.Id == "windfall_major"), Is.False);

            var before = teller.Agent.Preview(teller.Stimulus(), teller.OptionsForCall());
            Assert.That(before.ValuesUnavailable, Is.Empty);
            g.QueueEvent(new StoryEvent(EventRules.Windfall, "valen", "Gold Rain: Gold fell on valen.") { Strength = EventRules.Major });
            PlayTurn(g);
            var after = teller.Agent.Preview(teller.Stimulus(), teller.OptionsForCall());
            Assert.That(after.ValuesUnavailable.Where(v => v.Action == EventRules.Plague).Select(v => (v.Parameter, v.Value)),
                Is.EqualTo(new[] { ("strength", EventRules.Major) }));
            Assert.That(after.Available.Select(a => a.Id), Does.Contain(EventRules.Plague), "minor is still open");
        }

        [Test]
        public async Task BothForms_LandTheSameEvent_FromTheModelsReply()
        {
            var bundled = AtTurnTwo(EventStrengthForm.Bundled);
            var b = new Storyteller(bundled, new ScriptedProvider().Otherwise(
                ScriptedReply.Decide("windfall_major", "valen", "The Golden Star: A meteor of gold fell in the hills of valen.")));
            var bd = await Await.NotCancelled(b.TellAsync());

            var param = AtTurnTwo(EventStrengthForm.Parameter);
            var p = new Storyteller(param, new ScriptedProvider().Otherwise(
                ScriptedReply.Raw(Reply(EventRules.Windfall, "valen", EventRules.Major, "The Golden Star: A meteor of gold fell in the hills of valen."))));
            var pd = await Await.NotCancelled(p.TellAsync());

            foreach (var (d, g) in new[] { (bd, bundled), (pd, param) })
            {
                Assert.That((d.ModelAnswerLegal, d.Queued, d.ModelStrength, d.Strength), Is.EqualTo((true, true, EventRules.Major, EventRules.Major)));
                var e = PlayTurn(g).Events.Single();
                Assert.That((e.Template, e.Strength, e.Value["valen"], e.Title), Is.EqualTo((EventRules.Windfall, EventRules.Major, 45, "The Golden Star")));
            }
            Assert.That(bundled.Events.Told.Single().Action, Is.EqualTo("windfall_major"));
            Assert.That(param.Events.Told.Single().Action, Is.EqualTo(EventRules.Windfall));
        }

        [Test]
        public async Task TheParameterForm_RejectsAReplyWithoutAStrength()
        {
            var g = AtTurnTwo(EventStrengthForm.Parameter);
            var teller = new Storyteller(g, new ScriptedProvider().Otherwise(
                ScriptedReply.Raw(Reply(EventRules.Windfall, "valen", null, "Gold Rain: Gold fell on valen."))));
            var d = await Await.NotCancelled(teller.TellAsync());
            Assert.That(d.ModelAnswerLegal, Is.False, "no strength, no default");
            Assert.That(d.Queued, Is.False);
            Assert.That(PlayTurn(g).Events, Is.Empty);
        }
    }
}
