using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Agenerela.Providers;
using NUnit.Framework;

namespace Agenerela.Tests
{
    // The legal pairs in the prompt (#151, LegalTargetListing), and the report of targets the list's
    // cap leaves out. Off, every request is what it was; on, the list names only pairs the paired
    // schema offers and the guards let through. Every await of DecideAsync goes through
    // Fixtures.NotCancelled.
    public sealed class LegalTargetListingTests
    {
        private const string Claimed = "claimed by the miller";

        [TearDown]
        public void DestroyFixtures()
        {
            Fixtures.DestroyCreated();
        }

        // ---- Off -----------------------------------------------------------------------------

        [Test]
        public void OffByDefaultAndNothingSaysThePairs()
        {
            // Byte for byte against the time before the option is PromptBuilderTests' golden files,
            // which this change leaves untouched. Here: off, no line names a pair, and the flat schema
            // still offers the action that has no legal target, as it always did.
            Assert.That(new DecideOptions().LegalTargets, Is.EqualTo(LegalTargetListing.Off));
            Assert.That(new PromptOptions().LegalTargets, Is.EqualTo(LegalTargetListing.Off));

            foreach (bool listed in new[] { false, true })
            {
                var request = Mill().Preview("What now?", new DecideOptions { ListActionsAndTargets = listed }).Request;

                string where = $"list={listed}";
                Assert.That(request.SystemPrompt, Does.Not.Contain("Target: ").And.Not.Contain(" Targets: ").And.Not.Contain("(for "), where);
                Assert.That(ActionEnum(request.Schema), Is.EqualTo(new[] { "move_to", "chop", "fetch", "wave", "none" }), where);
            }
        }

        [Test]
        public void OffTheFlatListStillNamesEveryTargetAndEveryOfferedAction()
        {
            // What #190's list has always said, pairs or no pairs: the fixture is here to show what
            // the option changes, not to pin the list (its golden file is PromptBuilderTests').
            var request = Mill().Preview("What now?", new DecideOptions { ListActionsAndTargets = true }).Request;

            Assert.That(ListLines(request), Is.EqualTo(new[]
            {
                "Allowed actions:",
                "- move_to: Walk somewhere.",
                "- chop: Chop a tree.",
                "- fetch: Fetch a bucket.",
                "- wave: Wave at someone.",
                "- none: Take no action.",
                "Targets:",
                "- tree_a",
                "- tree_b",
                "- well: The village well.",
            }));
        }

        // ---- On --------------------------------------------------------------------------------

        [Test]
        public void PerTargetNamesOnlyTheLegalPairs()
        {
            var request = Mill().Preview("What now?", new DecideOptions { LegalTargets = LegalTargetListing.PerTarget }).Request;

            Assert.That(ListLines(request), Is.EqualTo(new[]
            {
                "Allowed actions:",
                "- move_to: Walk somewhere.",
                "- chop: Chop a tree.",
                "- wave: Wave at someone. Target: no_target.",
                "- none: Take no action. Target: no_target.",
                "Targets:",
                "- tree_a (for move_to, chop)",
                "- tree_b (for move_to)",
                "- well (for move_to): The village well.",
            }));
        }

        [Test]
        public void AnActionThatTakesSomeTargetsIsNamedOnThoseAlone()
        {
            var agent = Mill();
            agent.Actions.Register(
                new ActionDefinition { Id = "sing", Description = "Sing.", RequiresTarget = false, TargetCategories = new[] { "place" } },
                new FakeHandler());

            var request = agent.Preview("What now?", new DecideOptions { LegalTargets = LegalTargetListing.PerTarget }).Request;

            var lines = ListLines(request);
            Assert.That(lines, Does.Contain("- sing: Sing."), "It takes a listed target, so its own line carries no note.");
            Assert.That(lines, Does.Contain("- well (for move_to, sing): The village well."));
            Assert.That(lines, Does.Contain("- tree_a (for move_to, chop)"), "And not on a tree, which it may not take.");
        }

        [Test]
        public void AnActionWithNoLegalTargetIsNotOfferedAnywhere()
        {
            var flat = Mill().Preview("Fetch a bucket.", new DecideOptions { ListActionsAndTargets = true });
            var legal = Mill().Preview("Fetch a bucket.", new DecideOptions { LegalTargets = LegalTargetListing.PerTarget });

            Assert.That(ActionEnum(flat.Request.Schema), Does.Contain("fetch"), "Off, the flat schema offers it, as before.");
            Assert.That(ActionEnum(legal.Request.Schema), Is.EqualTo(new[] { "move_to", "chop", "wave", "none" }),
                "On, it is left out of the schema too, as the paired schema leaves it out.");
            Assert.That(legal.Request.FewShotBlock, Does.Not.Contain("bucket"), "And its example with it.");
            Assert.That(legal.LeftOut.Select(l => l.Action.Id), Does.Contain("fetch"));
            Assert.That(legal.Request.Schema.Pairs, Is.Null, "The schema stays flat unless pairing is asked for.");
        }

        [Test]
        public void ThePromptThePairedSchemaAndTheGuardsAgree()
        {
            foreach (var layout in new[] { LegalTargetListing.PerTarget })
            {
                var agent = Mill();
                var request = agent.Preview("What now?", new DecideOptions { LegalTargets = layout }).Request;
                var said = SaidPairs(request, layout);

                var ctx = Context(agent);
                var paired = DecisionSchema.Build(ctx, pairTargets: true);
                var schemaPairs = new HashSet<string>(paired.Pairs.SelectMany(p => p.Actions.SelectMany(a => p.Targets.Select(t => a + "/" + t))));

                var targets = new[] { TargetRegistry.NoTarget }.Concat(ctx.Targets.Ids).ToList();
                foreach (string action in ActionEnum(request.Schema))
                {
                    foreach (string target in targets)
                    {
                        string pair = action + "/" + target;
                        bool inPrompt = said.Contains(pair);
                        bool inSchema = schemaPairs.Contains(pair);
                        Assert.That(inPrompt, Is.EqualTo(inSchema), $"{layout}: prompt and paired schema on {pair}");

                        // none runs nothing, so no guard holds its target; the schema and the list
                        // give it no_target alone.
                        if (action == ActionRegistry.None) continue;

                        Assert.That(GuardsPass(ctx, action, target), Is.EqualTo(inSchema), $"{layout}: guards and paired schema on {pair}");
                    }
                }
            }
        }

        [Test]
        public void PairedAndListedTheTwoReadOneAnswer()
        {
            // With pairing on too, the list is the schema's own pairs.
            var request = Mill().Preview("What now?", new DecideOptions { LegalTargets = LegalTargetListing.PerTarget, PairTargetsWithActions = true }).Request;

            Assert.That(request.Schema.Pairs, Is.Not.Null);
            Assert.That(ListLines(request), Does.Contain("- tree_a (for move_to, chop)"));
            Assert.That(ListLines(request), Does.Contain("- tree_b (for move_to)"));
        }

        [Test]
        public async Task ACheckIsAskedOnceForTheListTheSchemaAndTheGuard()
        {
            var asked = new List<string>();
            var provider = new FakeProvider(Reply("chop", "tree_a"));
            var agent = Mill(provider, asked);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Chop.", new DecideOptions { LegalTargets = LegalTargetListing.PerTarget }));

            Assert.That(result.Decision.ActionId, Is.EqualTo("chop"));
            Assert.That(asked, Is.EqualTo(new[] { "tree_a", "tree_b" }), "One answer for the whole decision.");
        }

        [Test]
        public void AnUndefinedListingThrowsAtTheCall()
        {
            var agent = Mill();
            var options = new DecideOptions { LegalTargets = (LegalTargetListing)7 };

            Assert.Throws<ArgumentOutOfRangeException>(() => agent.Preview("Hello.", options));
            Assert.Throws<ArgumentOutOfRangeException>(() => PromptBuilder.Build(Context(agent), DecisionSchema.Build(Context(agent)), null, null,
                new PromptOptions { LegalTargets = (LegalTargetListing)7 }));
        }

        // ---- The cap's cut (#151) ------------------------------------------------------------

        [Test]
        public async Task TheTargetsTheListsCapLeavesOutAreReported()
        {
            var provider = new FakeProvider(Reply("none", "no_target"));
            var agent = Mill(provider);
            var options = new DecideOptions { ListActionsAndTargets = true, MaxListedTargets = 1 };

            var result = await Fixtures.NotCancelled(agent.DecideAsync("What now?", options));
            var preview = Mill().Preview("What now?", options);

            Assert.That(result.Telemetry.TargetsUnlisted, Is.EqualTo(new[] { "tree_b", "well" }), "In schema order.");
            Assert.That(preview.TargetsUnlisted, Is.EqualTo(new[] { "tree_b", "well" }));
            Assert.That(ListLines(provider.LastRequest).Where(l => l.StartsWith("- tree") || l.StartsWith("- well")),
                Is.EqualTo(new[] { "- tree_a" }), "What the model was sent is unchanged: the report is beside it.");

            var line = DecisionLogLine.From(new DecisionFeedEntry(agent, result, DecisionStage.Decided, null, DateTime.UtcNow, 1));
            Assert.That(line.TargetsUnlisted, Is.EqualTo(new[] { "tree_b", "well" }));
        }

        [Test]
        public async Task NothingIsReportedWhenNothingIsCutOrNothingIsListed()
        {
            var listedAll = await Fixtures.NotCancelled(Mill(new FakeProvider(Reply("none", "no_target")))
                .DecideAsync("What now?", new DecideOptions { ListActionsAndTargets = true }));
            var notListed = await Fixtures.NotCancelled(Mill(new FakeProvider(Reply("none", "no_target")))
                .DecideAsync("What now?", new DecideOptions { MaxListedTargets = 1 }));

            Assert.That(listedAll.Telemetry.TargetsUnlisted, Is.Empty);
            Assert.That(notListed.Telemetry.TargetsUnlisted, Is.Empty, "With no list, the cap cuts nothing from it.");
            var line = DecisionLogLine.From(new DecisionFeedEntry(Mill(), notListed, DecisionStage.Decided, null, DateTime.UtcNow, 1));
            Assert.That(line.TargetsUnlisted, Is.Null, "Left out of the log line.");
        }

        [Test]
        public async Task WithTheLegalPairsTheCapCountsOnlyTargetsSomeActionMayTake()
        {
            // Only chop, which takes trees and whose check rejects tree_b.
            var actions = new[] { new ActionDefinition { Id = "chop", Description = "Chop a tree.", RequiresTarget = true, TargetCategories = new[] { "tree" } } };
            var agent = new Agent(new AgentIdentity { Name = "Bram" }, actions, new FakeProvider(Reply("none", "no_target")));
            agent.Bind("chop", new DelegateActionHandler(_ => true, (_, __) => { })
            {
                IsAvailableFor = (_, id) => id == "tree_b" ? Availability.No(Claimed) : Availability.Yes,
            });
            foreach (string id in new[] { "tree_a", "tree_b", "well", "tree_c" })
            {
                agent.Targets.Register(id, new object(), id == "well" ? "place" : "tree");
            }

            var result = await Fixtures.NotCancelled(agent.DecideAsync("What now?",
                new DecideOptions { LegalTargets = LegalTargetListing.PerTarget, MaxListedTargets = 1 }));

            Assert.That(result.Telemetry.TargetsUnlisted, Is.EqualTo(new[] { "tree_c" }),
                "tree_b and the well are not listed because chop may not take them, not because of the cap.");
        }

        // ---- The fixture ---------------------------------------------------------------------

        // A mill hand: move_to (anything), chop (trees, its check rejecting the claimed tree_b),
        // fetch (buckets, of which there are none), wave (people, none here, no target needed). Two
        // trees and a described well.
        private static Agent Mill(FakeProvider provider = null, List<string> asked = null)
        {
            var actions = new[]
            {
                new ActionDefinition { Id = "move_to", Description = "Walk somewhere.", RequiresTarget = true },
                new ActionDefinition { Id = "chop", Description = "Chop a tree.", RequiresTarget = true, TargetCategories = new[] { "tree" } },
                new ActionDefinition
                {
                    Id = "fetch", Description = "Fetch a bucket.", RequiresTarget = true, TargetCategories = new[] { "bucket" },
                    ExampleStimulus = "Bring me the {0} bucket.",
                },
                new ActionDefinition { Id = "wave", Description = "Wave at someone.", RequiresTarget = false, TargetCategories = new[] { "person" } },
            };

            var agent = new Agent(new AgentIdentity { Name = "Bram", Role = "a mill hand" }, actions, provider ?? new FakeProvider());
            agent.Bind("move_to", new FakeHandler());
            agent.Bind("chop", new DelegateActionHandler(_ => true, (_, __) => { })
            {
                IsAvailableFor = (_, id) =>
                {
                    asked?.Add(id);
                    return id == "tree_b" ? Availability.No(Claimed) : Availability.Yes;
                },
            });
            agent.Bind("fetch", new FakeHandler());
            agent.Bind("wave", new FakeHandler());
            agent.Targets.Register("tree_a", new object(), "tree");
            agent.Targets.Register("tree_b", new object(), "tree");
            agent.Targets.Register("well", new Described("The village well."), "place");
            return agent;
        }

        // The pairs a rendered list says, read back from its text in the layout's own terms.
        private static HashSet<string> SaidPairs(DecisionRequest request, LegalTargetListing layout)
        {
            var lines = ListLines(request);
            int targetsAt = lines.IndexOf("Targets:");
            var actionLines = lines.Skip(1).Take(targetsAt - 1).ToList();
            var targetLines = lines.Skip(targetsAt + 1).ToList();
            var said = new HashSet<string>();

            foreach (string line in actionLines)
            {
                string action = line.Substring(2).Split(':')[0];
                int at = line.IndexOf("Target", StringComparison.Ordinal);
                string note = at < 0 ? "" : line.Substring(at);
                if (note.StartsWith("Target: ")) said.Add(action + "/" + TargetRegistry.NoTarget);
            }

            if (layout == LegalTargetListing.PerTarget)
            {
                foreach (string line in targetLines)
                {
                    string id = line.Substring(2).Split(' ')[0];
                    int open = line.IndexOf("(for ", StringComparison.Ordinal);
                    string inside = line.Substring(open + 5, line.IndexOf(')') - open - 5);
                    foreach (string action in inside.Split(new[] { ", " }, StringSplitOptions.None)) said.Add(action + "/" + id);
                }
            }

            return said;
        }

        // Whether the decision would pass the guards that hold a pair to its legality: the
        // category guard and the per-target one, with the answer the schema was built from.
        private static bool GuardsPass(AgentContext ctx, string action, string target)
        {
            var input = new GuardInput(ctx, null, null, new AgentDecision(action, target, ""), null, null);
            var legality = new SchemaLegalityGuard().Inspect(input);
            var category = new TargetCategoryGuard().Inspect(input);
            var perTarget = new TargetAvailabilityGuard().Inspect(input);
            return legality.Kind == GuardVerdictKind.Pass && category.Kind == GuardVerdictKind.Pass && perTarget.Kind == GuardVerdictKind.Pass;
        }

        private static AgentContext Context(Agent agent)
        {
            return new AgentContext(agent.Identity, "", null, null, agent.Targets, agent.Actions);
        }

        private static string Reply(string action, string target) =>
            $"{{\"action\":\"{action}\",\"target\":\"{target}\",\"statement\":\"\"}}";

        private static IReadOnlyList<string> ActionEnum(DecisionSchema schema)
        {
            return schema.Fields.First(f => f.Name == DecisionSchema.ActionFieldName).AllowedValues;
        }

        private static string Json(DecisionSchema schema)
        {
            return new JsonSchemaSerializer().Serialize(schema);
        }

        // The system prompt's lines from the action list's heading on.
        private static List<string> ListLines(DecisionRequest request)
        {
            var lines = request.SystemPrompt.Split('\n').ToList();
            int at = lines.IndexOf("Allowed actions:");
            return at < 0 ? new List<string>() : lines.Skip(at).ToList();
        }

        private sealed class Described : IDescribedTarget
        {
            public Described(string description)
            {
                Description = description;
            }

            public string Description { get; }
        }
    }
}
