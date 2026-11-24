using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Agenerela.Tests
{
    // The target summary (build plan §2.8, #151): one line per category, ids verbatim, the same lines
    // on every call, and read from the agent's own targets. Every await of DecideAsync goes through
    // Fixtures.NotCancelled.
    public sealed class TargetSummarySourceTests
    {
        [TearDown]
        public void TearDown() => Fixtures.DestroyCreated();

        private sealed class Described : IDescribedTarget
        {
            public Described(string description) { Description = description; }
            public string Description { get; }
        }

        [Test]
        public void EachCategoryIsOneLineNamingItsTargetsByIdInOrder()
        {
            var lines = new TargetSummarySource().Summarise(new[]
            {
                ("bridge", "The wooden bridge over the stream.", "place"),
                ("wolf", "A wolf that came out of the woods", "creature"),
                ("well", "The stone well", "place"),
                ("docks", "The docks", "place"),
            });

            Assert.That(lines, Is.EqualTo(new[]
            {
                "place: bridge (the wooden bridge over the stream), well (the stone well) and docks (the docks).",
                "creature: wolf (a wolf that came out of the woods).",
            }));
        }

        [Test]
        public void TargetsWithNoCategoryShareALineAndHeadingsAndDescriptionsCanBeChanged()
        {
            var summary = new TargetSummarySource { IncludeDescriptions = false, UncategorisedHeading = "Also here:" };
            summary.Headings["place"] = "Places nearby:";

            var lines = summary.Summarise(new[]
            {
                ("cart", "A cart", (string)null),
                ("bridge", "The wooden bridge", "place"),
                ("NPC_hall", "NPC hall", null),
                (" ", "blank", null),
            });

            Assert.That(lines, Is.EqualTo(new[] { "Also here: cart and NPC_hall.", "Places nearby: bridge." }));
            Assert.That(new TargetSummarySource().Summarise(new[] { ("hall", "NPC hall", (string)null) }),
                Is.EqualTo(new[] { "Here: hall (NPC hall)." }), "An acronym keeps its capital.");
        }

        [Test]
        public async Task AsAnAgentsSourceItSummarisesTheResolvedTargets()
        {
            var provider = new FakeProvider("{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"\"}");
            var agent = Fixtures.VillageGuard(provider);
            agent.Targets.Unregister("tower");
            agent.Targets.Unregister("training_dummy");
            agent.Targets.Register("tower", new Described("The north tower"), "place");
            agent.Targets.Register("training_dummy", new Described("A straw dummy"), "practice");
            agent.ObservationSources.Add(new TargetSummarySource());

            await Fixtures.NotCancelled(agent.DecideAsync("Anything to report?"));
            await Fixtures.NotCancelled(agent.DecideAsync("Anything to report?"));

            string observations = provider.Requests[0].Observations;
            Assert.That(observations, Does.Contain("place: tower (the north tower)."));
            Assert.That(observations, Does.Contain("practice: training_dummy (a straw dummy)."));
            Assert.That(provider.Requests[1].Observations, Is.EqualTo(observations), "Deterministic: the same lines on every call.");
        }
    }
}
