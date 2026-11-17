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
    /// </summary>
    public sealed class FreeCourtPromptTests
    {
        // A fake model that raises taxes whenever the call lists it, and otherwise answers the first action listed.
        private static async Task<(WritModelRecord record, List<ScriptedRequest> seen)> Play()
        {
            var seen = new List<ScriptedRequest>();
            var provider = new ScriptedProvider
            {
                Callback = req =>
                {
                    seen.Add(req);
                    if (req.OfferedActions.Contains(Actions.RaiseTaxes)) return ScriptedReply.Decide(Actions.RaiseTaxes, TargetRegistry.NoTarget, "More gold.");
                    var first = req.OfferedActions.FirstOrDefault(a => a != ActionRegistry.None && a != Actions.AcceptPeace);
                    if (first == null || first == Actions.RefusePeace) return ScriptedReply.Decide(req.OfferedActions.Contains(Actions.RefusePeace) ? Actions.RefusePeace : ActionRegistry.None);
                    return ScriptedReply.Decide(first, req.OfferedTargets.FirstOrDefault(t => t != TargetRegistry.NoTarget) ?? TargetRegistry.NoTarget, "So be it.");
                },
            };
            var rules = GameRules.Standard;
            rules.FreeOrders = true;
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
    }
}
