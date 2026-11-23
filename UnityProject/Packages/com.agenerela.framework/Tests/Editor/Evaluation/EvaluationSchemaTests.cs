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

        [TestCase("")]
        [TestCase("Interim")]
        public void OurSetsAgreeWithOurProfileTheirCategoriesIncluded(string subfolder)
        {
            // The categories (#190 B, #210) and the example statements (#190 G14, #218) are written
            // on our set and profile; no label may become illegal, and no prompt may be an example.
            string folder = Path.Combine(EvalFixtures.OurSetFolder(), subfolder);
            var set = PromptSet.Load(Path.Combine(folder, "agent.json"));
            var profile = AssetDatabase.LoadAssetAtPath<AgentProfile>(OurProfilePath);

            Assert.That(PromptSetChecks.CompareWithProfile(set, profile), Is.Empty);
            Assert.That(PromptSetChecks.Check(set, profile).Select(p => p.ToString()), Is.Empty);
            Assert.That(set.Targets.Select(t => t.Category).Distinct(), Is.EquivalentTo(new[] { "place", "creature" }), "Every target has one.");
            Assert.That(profile.Actions.Select(a => a.Action).Where(a => a.RequiresTarget).Select(a => string.Join(",", a.TargetCategories)),
                Is.EquivalentTo(new[] { "place", "creature" }));
            Assert.That(profile.Actions.Select(a => a.Action.ExampleStatement), Has.All.Not.Empty);
            Assert.That(profile.IdleExampleStatement, Is.Not.Empty);
        }

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

        // SHA-256 of every distinct system prompt and few-shot block our set's default arm sends,
        // sorted, each as "system prompt, a line of three dashes, few-shot block". Stored, so a
        // change to what the default request says fails here even when both arms of a run change
        // together. Taken on a clean origin/test at 5f07e03 (16 November 2026), before typed parameters.
        // A change that has to move it updates the hashes, and owes an A/B (#151). These are now what
        // an arm with ExampleStatements off sends: the statements written on the profile (#151) are
        // the only change since, and the run comparing the two is #151's.
        private static readonly string[] OurPromptHashesWithoutStatements =
        {
            "06057a4179d077bb750563be8dded4ee0846c45518f1ccffe0cb30e19cf5316a",
            "2be670ed354f62d21738e3233b7f3e43ef24bf7af94a1c2a6ead308a2b79eb2a",
            "5b0d5e0ccd749f4c1cdd03cfd6aae4eec99d804933cdf31e0fbbc25fc7f8feed",
            "9b87e99a7df09618148737d6662d6964630cd76326952914eb27be139ca50a4f",
            "c4e96134a332c70991dd7fc1fe46da24dfd3e82b03fac66751223de4ac3e0c34",
            "d6cfb5249a2f8dde10e3d86c074e2409677ddf5fc02cfc84afd4f8d95b1e9dad",
        };

        // The same, with the example statements written on VillageGuard.asset (#151, #190 G14), taken
        // on 23 November 2026: what the default arm sends since.
        private static readonly string[] OurPromptHashes =
        {
            "59f6c4b14f8c546f91964f8936ee12b25a84ba886380dd4b728bd4d4e66bc537",
            "6da47a725081bcb3bdc638e0796bcce2a24f8beb7ac3a2a4d29506cf32ae9607",
            "75406297b51605b87ab32ad0106891c1a96c042e9a66f2606af95b205aa687cb",
            "7a5a9009ab85840f930f3d6bfa3ac702181f04594e4c418add83aca6a8e148da",
            "c49a3aa2674ebc2e6a33560d0576e73b73757fd44bd11e43af06451543cfe8e6",
            "d2a8ee93c84273fd4f5f9e3ae4f6d7f0930a2632f741af44de822c525fd88fdb",
        };

        [Test]
        public Task OurSetsDefaultPromptIsWhatItWas() => AssertPromptHashes(null, OurPromptHashes);

        [Test]
        public Task WithoutExampleStatementsOurSetsPromptIsTheOneFromBeforeThem() =>
            AssertPromptHashes(new DecideOptions { ExampleStatements = false }, OurPromptHashesWithoutStatements);

        private static async Task AssertPromptHashes(DecideOptions options, string[] expected)
        {
            string folder = EvalFixtures.OurSetFolder();
            var set = PromptSet.Load(Path.Combine(folder, "agent.json"));
            var profile = AssetDatabase.LoadAssetAtPath<AgentProfile>(OurProfilePath);
            var provider = new FakeProvider("{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"\"}");
            using (var fixtures = new RunnerFixtures())
            {
                var config = fixtures.Config(provider, "Fake");
                await Fixtures.NotCancelled(new EvalRunner(set, profile, new EvalArm("A", config, options), new EvalArm("B", config, options)).RunAsync());
            }

            var hashes = new SortedSet<string>(System.StringComparer.Ordinal);
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                foreach (var request in provider.Requests)
                {
                    string text = request.SystemPrompt + "\n---\n" + request.FewShotBlock;
                    hashes.Add(System.BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant());
                }
            }

            Assert.That(provider.Requests, Is.Not.Empty);
            Assert.That(hashes, Is.EqualTo(expected), "ACTUAL: " + string.Join(" ", hashes));
        }

        [Test]
        public async Task AnArmWithADeclaredParameterLeavesTheOtherArmByteForByteWhatItWas()
        {
            // DR-023's first A/B: the same set, arm B with one harmless parameter on move_to. Arm A,
            // which declares none, must still send exactly the schema every number was scored
            // against; arm B's is paired, asks for the parameter between target and statement, and
            // its prompt lists the actions.
            string folder = EvalFixtures.OurSetFolder();
            var set = PromptSet.Load(Path.Combine(folder, "agent.json"));
            var profile = AssetDatabase.LoadAssetAtPath<AgentProfile>(OurProfilePath);
            const string Idle = "{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"\"}";
            var a = new FakeProvider(Idle);
            var b = new FakeProvider(Idle);
            var plain = new FakeProvider(Idle);

            using (var fixtures = new RunnerFixtures())
            {
                var withPace = new EvalArm("B", fixtures.Config(b, "FakeB"))
                {
                    Legality = new EvalLegality
                    {
                        Name = "pace",
                        ActionParameters =
                        {
                            ["move_to"] = new[] { new ActionParameter { Name = "pace", Description = "how fast to go", Values = new[] { "walk", "run" } } },
                        },
                    },
                };
                await Fixtures.NotCancelled(new EvalRunner(set, profile, new EvalArm("A", fixtures.Config(a, "FakeA")), withPace).RunAsync());

                // The same arm A beside an arm with no parameter, to compare whole requests with.
                await Fixtures.NotCancelled(new EvalRunner(set, profile,
                    new EvalArm("A", fixtures.Config(plain, "FakePlain")), new EvalArm("B", fixtures.Config(new FakeProvider(Idle), "FakeOther"))).RunAsync());
            }

            var json = new JsonSchemaSerializer();
            Assert.That(a.Requests, Is.Not.Empty);
            Assert.That(a.Requests.Select(r => json.Serialize(r.Schema)), Has.All.EqualTo(NotFollowing).Or.EqualTo(Following));
            Assert.That(a.Requests, Has.Count.EqualTo(plain.Requests.Count));
            for (int i = 0; i < a.Requests.Count; i++)
            {
                Assert.That(a.Requests[i].SystemPrompt, Is.EqualTo(plain.Requests[i].SystemPrompt));
                Assert.That(a.Requests[i].FewShotBlock, Is.EqualTo(plain.Requests[i].FewShotBlock));
                Assert.That(a.Requests[i].Observations, Is.EqualTo(plain.Requests[i].Observations));
                Assert.That(a.Requests[i].Stimulus, Is.EqualTo(plain.Requests[i].Stimulus));
            }

            Assert.That(b.Requests, Has.Count.EqualTo(a.Requests.Count));
            foreach (var request in b.Requests)
            {
                var pairs = request.Schema.Pairs.Select(p => string.Join(", ", p.Actions) + " [" + string.Join(", ", p.Parameters.Select(f => f.Name)) + "]").ToList();
                Assert.That(pairs, Is.EqualTo(new[] { "follow_player []", "move_to [pace]", "attack_target []", "none []" })
                    .Or.EqualTo(new[] { "follow_player, stop_following []", "move_to [pace]", "attack_target []", "none []" }));
                Assert.That(json.Serialize(request.Schema), Does.Contain("\"required\": [\"action\", \"target\", \"pace\", \"statement\"]"));
                Assert.That(request.SystemPrompt, Does.Contain("pace (how fast to go): walk or run."));
                Assert.That(request.FewShotBlock, Does.Contain(", pace: walk"));
            }
        }

        [Test]
        public async Task AnArmThatPairsTargetsChangesNothingButTheSchema()
        {
            // #151's A/B for DR-020: the same set, arm B with the paired schema. Our set's targets are
            // places and a creature, and the profile's move_to takes places and attack_target creatures
            // (#190 B, #210), so pairing gives each its own targets.
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
                Assert.That(pairs, Is.EqualTo(new[] { "follow_player -> 12", "move_to -> 10", "attack_target -> 1", "none -> 1" })
                    .Or.EqualTo(new[] { "follow_player, stop_following -> 12", "move_to -> 10", "attack_target -> 1", "none -> 1" }));
                Assert.That(b.Requests[i].Schema.Pairs.Single(p => p.Actions.Contains("attack_target")).Targets, Is.EqualTo(new[] { "wolf" }));
            }
        }
    }
}
