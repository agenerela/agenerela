using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Agenerela;
using Agenerela.Providers;
using NUnit.Framework;

namespace GreyBoxStrategy.Tests
{
    /// <summary>
    /// What a court reads on its later requests of a turn with free orders (17 November): only the actions this request
    /// offers. The live check found <c>raise_taxes</c> listed again after it was given: 72 answers held as not offered.
    /// Then the two A/B arms, <see cref="GameRules.CourtTurnFacts"/> and <see cref="GameRules.CourtNoneWording"/>.
    /// </summary>
    public sealed class FreeCourtPromptTests
    {
        // A fake model that raises taxes whenever the call lists it, and otherwise answers the first action listed.
        private static async Task<(WritModelRecord record, List<ScriptedRequest> seen)> Play(bool facts = false, bool wording = false)
        {
            var seen = new List<ScriptedRequest>();
            var provider = new ScriptedProvider
            {
                Callback = req =>
                {
                    seen.Add(req);
                    if (req.OfferedActions.Contains(Actions.RaiseTaxes)) return ScriptedReply.Decide(Actions.RaiseTaxes, TargetRegistry.NoTarget, "More gold.");
                    // Then a recruit, so a later request has gold held back to read about.
                    if (req.OfferedActions.FirstOrDefault(a => a == Actions.Recruit || Actions.RecruitTypes.Contains(a)) is string recruit) return ScriptedReply.Decide(recruit, TargetRegistry.NoTarget, "More soldiers.");
                    var first = req.OfferedActions.FirstOrDefault(a => a != ActionRegistry.None && a != Actions.AcceptPeace);
                    if (first == null || first == Actions.RefusePeace) return ScriptedReply.Decide(req.OfferedActions.Contains(Actions.RefusePeace) ? Actions.RefusePeace : ActionRegistry.None);
                    return ScriptedReply.Decide(first, req.OfferedTargets.FirstOrDefault(t => t != TargetRegistry.NoTarget) ?? TargetRegistry.NoTarget, "So be it.");
                },
            };
            var rules = GameRules.Standard;
            rules.FreeOrders = true;
            rules.CourtTurnFacts = facts;
            rules.CourtNoneWording = wording;
            var setup = GameSetup.Writs(3);
            setup.TurnCap = 6;
            setup.Numbers = rules;
            var record = await Await.NotCancelled(WritModelRunner.PlayAsync(setup, Enumerable.Repeat(ModelGameRunner.Model, 5).ToList(),
                (w, id) => new WritRuler(w, id, provider), "fake"));
            return (record, seen);
        }

        [Test]
        public async Task ACourt_IsNeverListedAnOrderItAlreadyGaveThisTurn()
        {
            var (record, seen) = await Play();
            var courts = record.Decisions.Where(d => d.Moment == Moment.Council).ToList();
            Assert.That(courts.Count(d => d.Move.Action == Actions.RaiseTaxes), Is.GreaterThan(0), "the fake raised taxes");
            var held = record.Decisions.Where(d => !d.Offered && d.Move.Action == Actions.RaiseTaxes).Select(d => $"{d.Turn} {d.Country} {d.Move}").ToList();
            Assert.That(held, Is.Empty, "taxes held as not offered: " + string.Join("; ", held.Take(5)));
            Assert.That(courts.Where(d => d.ModelAction == Actions.RaiseTaxes).All(d => d.ModelAnswerLegal), Is.True, "taxes are asked for only where they are offered");
            // Taxes once a realm a turn, though the fake asks for them at every chance it is given.
            var twice = courts.GroupBy(d => (d.Turn, d.Country)).Where(g => g.Count(d => d.Move.Action == Actions.RaiseTaxes) > 1).ToList();
            Assert.That(twice, Is.Empty);
        }

        [Test]
        public async Task CourtTurnFacts_TellsALaterRequestThatTaxesAreRaised_AndTheGoldHeld()
        {
            var (_, off) = await Play();
            var (_, on) = await Play(facts: true);
            string Read(ScriptedRequest r) => r.Request.Observations + " " + r.Request.Stimulus;
            Assert.That(off.Any(r => Read(r).Contains("Taxes are raised this turn")), Is.False, "off: no new fact");
            Assert.That(on.Any(r => Read(r).Contains("Taxes are raised this turn")), Is.True);
            Assert.That(on.Any(r => Read(r).Contains("Gold held for this turn's orders")), Is.True, "a recruit's gold, held, is told");
        }

        [Test]
        public async Task CourtNoneWording_AsksALaterCourtRequestForAnotherOrderOrNone()
        {
            var (_, off) = await Play();
            var (_, on) = await Play(wording: true);
            Assert.That(off.Any(r => (r.Request.Stimulus ?? "").Contains("none when the court is done")), Is.False);
            var later = on.Where(r => (r.Request.Stimulus ?? "").Contains("The court's next order: give another, or none when the court is done.")).ToList();
            Assert.That(later, Is.Not.Empty);
        }
    }
}
