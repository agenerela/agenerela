using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Agenerela.Evaluation;
using NUnit.Framework;
using UnityEditor;

namespace Agenerela.Tests.Evaluation
{
    /// <summary>
    /// The schema our evaluation set is scored against, byte for byte, with every option off (#190
    /// workstream B). Bram, GreyBoxVillage's evaluation agent, has this agent's actions and targets,
    /// and every accuracy number in findings was measured against this shape, so a framework change
    /// that moves it, such as a new default, would make old and new runs incomparable without anyone
    /// noticing. A change that has to move it updates the goldens here, and owes an A/B (#151).
    /// </summary>
    /// <remarks>
    /// Run through <see cref="EvalRunner"/> itself, over our real set and profile, so the test sees
    /// exactly what a run sends: every request of both arms, warm-ups included. Only agenerela's own
    /// project has the set (<see cref="EvalFixtures.OurSetFolder"/>).
    /// </remarks>
    public sealed class EvaluationSchemaTests
    {
        private const string OurProfilePath = "Assets/Evaluation/VillageGuard.asset";

        // The two shapes the set's states give: follow_player is always offered, stop_following only
        // while isFollowing. Written out in full, not built, so the serializer cannot move them both.
        private const string NotFollowing =
            "{\n" +
            "  \"type\": \"object\",\n" +
            "  \"properties\": {\n" +
            "    \"action\": {\n" +
            "      \"type\": \"string\",\n" +
            "      \"description\": \"The one action to take now, from the allowed list.\",\n" +
            "      \"enum\": [\"follow_player\", \"move_to\", \"attack_target\", \"none\"]\n" +
            "    },\n" +
            "    \"target\": {\n" +
            "      \"type\": \"string\",\n" +
            "      \"description\": \"What the action applies to. Use no_target when none of the listed targets applies.\",\n" +
            "      \"enum\": [\"no_target\", \"bridge\", \"village_gate\", \"campfire\", \"well\", \"north_tower\", \"market\", \"docks\", \"shelter\", \"eastern_road\", \"wolf\", \"locked_cave\"]\n" +
            "    },\n" +
            "    \"statement\": {\n" +
            "      \"type\": \"string\",\n" +
            "      \"description\": \"What the agent says or announces. One or two short sentences, or empty.\"\n" +
            "    }\n" +
            "  },\n" +
            "  \"required\": [\"action\", \"target\", \"statement\"],\n" +
            "  \"propertyOrdering\": [\"action\", \"target\", \"statement\"],\n" +
            "  \"additionalProperties\": false\n" +
            "}";

        private static readonly string Following = NotFollowing.Replace(
            "[\"follow_player\", \"move_to\"", "[\"follow_player\", \"stop_following\", \"move_to\"");

        [Test]
        public async Task OurSetIsScoredAgainstTheSameSchemaWithEveryOptionOff()
        {
            string folder = EvalFixtures.OurSetFolder();
            var set = PromptSet.Load(Path.Combine(folder, "agent.json"));
            var profile = AssetDatabase.LoadAssetAtPath<AgentProfile>(OurProfilePath);
            Assert.That(profile, Is.Not.Null, OurProfilePath);

            var provider = new FakeProvider("{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"\"}");
            using (var fixtures = new RunnerFixtures())
            {
                var config = fixtures.Config(provider, "Fake");

                // Two arms with no options: every switch at its default.
                var runner = new EvalRunner(set, profile, new EvalArm("A", config), new EvalArm("B", config));
                await Fixtures.NotCancelled(runner.RunAsync());
            }

            var json = new JsonSchemaSerializer();
            var sent = provider.Requests.Select(request => json.Serialize(request.Schema)).ToList();

            Assert.That(sent, Is.Not.Empty);
            Assert.That(sent, Has.All.EqualTo(NotFollowing).Or.EqualTo(Following));
            Assert.That(new HashSet<string>(sent), Is.EquivalentTo(new[] { NotFollowing, Following }),
                "The set has prompts in both states.");
        }

        [Test]
        public async Task AnArmThatPairsTargetsChangesNothingButTheSchema()
        {
            // #151's A/B for DR-020: the same set, arm B with the paired schema. Our set declares no
            // categories, so pairing narrows only what an action that needs a target and none take.
            string folder = EvalFixtures.OurSetFolder();
            var set = PromptSet.Load(Path.Combine(folder, "agent.json"));
            var profile = AssetDatabase.LoadAssetAtPath<AgentProfile>(OurProfilePath);
            const string Idle = "{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"\"}";
            var a = new FakeProvider(Idle);
            var b = new FakeProvider(Idle);

            EvalReport report;
            using (var fixtures = new RunnerFixtures())
            {
                var runner = new EvalRunner(set, profile,
                    new EvalArm("A", fixtures.Config(a, "FakeA")),
                    new EvalArm("B", fixtures.Config(b, "FakeB"), new DecideOptions { PairTargetsWithActions = true }));
                report = await Fixtures.NotCancelled(runner.RunAsync());
            }

            Assert.That((report.Conditions.Arms[0].PairTargetsWithActions, report.Conditions.Arms[1].PairTargetsWithActions), Is.EqualTo((false, true)));
            Assert.That(b.Requests, Has.Count.EqualTo(a.Requests.Count));
            for (int i = 0; i < a.Requests.Count; i++)
            {
                Assert.That(b.Requests[i].SystemPrompt, Is.EqualTo(a.Requests[i].SystemPrompt));
                Assert.That(b.Requests[i].FewShotBlock, Is.EqualTo(a.Requests[i].FewShotBlock));
                Assert.That(b.Requests[i].Observations, Is.EqualTo(a.Requests[i].Observations));
                Assert.That(b.Requests[i].Stimulus, Is.EqualTo(a.Requests[i].Stimulus));
                Assert.That(a.Requests[i].Schema.Pairs, Is.Null);

                var pairs = b.Requests[i].Schema.Pairs.Select(p => string.Join(", ", p.Actions) + " -> " + p.Targets.Count).ToList();
                Assert.That(pairs, Is.EqualTo(new[] { "follow_player -> 12", "move_to, attack_target -> 11", "none -> 1" })
                    .Or.EqualTo(new[] { "follow_player, stop_following -> 12", "move_to, attack_target -> 11", "none -> 1" }));
            }
        }
    }
}
