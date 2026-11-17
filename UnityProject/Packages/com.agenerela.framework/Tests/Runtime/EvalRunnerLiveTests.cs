using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Agenerela.Evaluation;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Agenerela.Tests
{
    /// <summary>
    /// The A/B runner against a live Ollama (#145): a small fixture set, guards on against guards
    /// off, interleaved on one model, written to a report under the project's <c>Logs/Evaluation</c>.
    /// </summary>
    /// <remarks>
    /// It proves the runner works on a real model: every prompt answered in both arms, the
    /// server's version and VRAM recorded, the report written. It makes no accuracy claim: eight
    /// prompts are far inside the noise floor, and the claim would need its own A/B (hard rule 2).
    /// The runner itself refuses to start while another model is resident (hard rule 3). Ignored,
    /// with the reason, when nothing answers at <see cref="LiveOllama.Endpoint"/>.
    /// </remarks>
    public sealed class EvalRunnerLiveTests
    {
        // The guard of the Phase 2 gate, with stop_following only while following. Its few-shot
        // examples are LiveGuard's, and no prompt below repeats one (hard rule 4, which the runner
        // checks through PromptSetChecks before it sends anything).
        private const string SetJson = @"{
  ""agent"": ""Village guard"",
  ""actions"": [
    { ""id"": ""follow_player"", ""needsTarget"": false },
    { ""id"": ""stop_following"", ""needsTarget"": false, ""onlyWhen"": { ""isFollowing"": true } },
    { ""id"": ""move_to"", ""needsTarget"": true },
    { ""id"": ""attack_target"", ""needsTarget"": true },
    { ""id"": ""none"", ""needsTarget"": false }
  ],
  ""targets"": [
    { ""id"": ""bridge"", ""description"": ""The wooden bridge over the stream"" },
    { ""id"": ""well"", ""description"": ""The stone well in the square"" },
    { ""id"": ""wolf"", ""description"": ""A wolf that came out of the woods"" }
  ],
  ""categories"": [
    { ""id"": ""command"", ""file"": ""prompts/commands.json"" },
    { ""id"": ""impossible"", ""file"": ""prompts/impossible.json"" }
  ]
}";

        private const string CommandsJson = @"{
  ""category"": ""command"",
  ""prompts"": [
    { ""id"": ""command_001"", ""label"": ""Player"", ""stimulus"": ""Meet me over at the bridge."", ""observations"": [],
      ""expectedAction"": ""move_to"", ""expectedTarget"": ""bridge"", ""preconditionState"": { ""isFollowing"": false }, ""labelledBy"": [""fixture""] },
    { ""id"": ""command_002"", ""label"": ""Player"", ""stimulus"": ""Go and wait by the well."", ""observations"": [],
      ""expectedAction"": ""move_to"", ""expectedTarget"": ""well"", ""preconditionState"": { ""isFollowing"": false }, ""labelledBy"": [""fixture""] },
    { ""id"": ""command_003"", ""label"": ""Player"", ""stimulus"": ""Drive off that wolf!"", ""observations"": [""A wolf is circling the sheep""],
      ""expectedAction"": ""attack_target"", ""expectedTarget"": ""wolf"", ""preconditionState"": { ""isFollowing"": false }, ""labelledBy"": [""fixture""] },
    { ""id"": ""command_004"", ""label"": ""Player"", ""stimulus"": ""Walk with me a while."", ""observations"": [],
      ""expectedAction"": ""follow_player"", ""expectedTarget"": ""no_target"", ""preconditionState"": { ""isFollowing"": false }, ""labelledBy"": [""fixture""] },
    { ""id"": ""command_005"", ""label"": ""Player"", ""stimulus"": ""Hold on, stay right there."", ""observations"": [],
      ""expectedAction"": ""stop_following"", ""expectedTarget"": ""no_target"", ""preconditionState"": { ""isFollowing"": true }, ""labelledBy"": [""fixture""] }
  ]
}";

        private const string ImpossibleJson = @"{
  ""category"": ""impossible"",
  ""prompts"": [
    { ""id"": ""impossible_001"", ""label"": ""Player"", ""stimulus"": ""Bring me the king's crown."", ""observations"": [],
      ""expectedAction"": ""none"", ""expectedTarget"": ""no_target"", ""preconditionState"": { ""isFollowing"": false }, ""labelledBy"": [""fixture""] },
    { ""id"": ""impossible_002"", ""label"": ""Player"", ""stimulus"": ""Swim across to the lighthouse."", ""observations"": [],
      ""expectedAction"": ""none"", ""expectedTarget"": ""no_target"", ""preconditionState"": { ""isFollowing"": false }, ""labelledBy"": [""fixture""] },
    { ""id"": ""impossible_003"", ""label"": ""Player"", ""stimulus"": ""Nice boots you have."", ""observations"": [],
      ""expectedAction"": ""none"", ""expectedTarget"": ""no_target"", ""preconditionState"": { ""isFollowing"": false }, ""labelledBy"": [""fixture""] }
  ]
}";

        private readonly List<Object> created = new List<Object>();
        private string folder;

        [OneTimeSetUp]
        public void ForgetTheLastRun()
        {
            LiveOllama.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created)
            {
                Object.DestroyImmediate(obj);
            }

            created.Clear();
            if (folder != null && Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        [Category(LiveOllama.Category)]
        public async Task TheRunnerComparesGuardsOnWithGuardsOffOnALiveModel()
        {
            LiveOllama.RequireReachable();
            var set = WriteSet();
            var runner = new EvalRunner(set, Profile(),
                new EvalArm("guards-on", Config("OllamaLocal"), new DecideOptions { CheckTarget = true }),
                new EvalArm("control-check-target-off", Config("OllamaLocal-control"), new DecideOptions { CheckTarget = false }))
            {
                // The Phase 3 suite's wording, so a line here reads as it did there.
                DescribeState = state => new[] { (bool)state["isFollowing"] ? "You are following the player." : "You are not following the player." },
            };

            var report = await LiveOllama.NotCancelled(runner.RunAsync());

            string saved = report.Save(Path.Combine(Application.dataPath, "..", "Logs", "Evaluation"));
            TestContext.Out.WriteLine($"EVAL-REPORT {saved}");
            TestContext.Out.WriteLine(report.ToMarkdown());

            int prompts = set.Prompts.Count();
            Assert.That(report.Answers, Has.Count.EqualTo(2 * prompts));
            Assert.That(report.Arms.Select(a => a.Overall.PipelineError), Is.All.EqualTo(0),
                "Pipeline errors: " + string.Join("; ", report.Answers.Where(a => a.Outcome == DecisionOutcome.PipelineError)
                    .Select(a => $"{a.Arm} {a.PromptId}: {a.ErrorKind}: {a.ErrorMessage}")));
            foreach (var arm in report.Conditions.Arms)
            {
                Assert.That(arm.ProviderVersion, Is.Not.Empty.And.Not.EqualTo("not checked"));
                var resident = arm.Residency.Single(r => r.When == "after warm-up").Find(LiveOllama.Model);
                Assert.That(resident, Is.Not.Null, $"{LiveOllama.Model} was not resident after the warm-up.");
                Assert.That(resident.SizeVram, Is.GreaterThan(0), "size_vram was not recorded.");
            }

            Assert.That(File.Exists(Path.Combine(saved, EvalReport.JsonFile)) && File.Exists(Path.Combine(saved, EvalReport.MarkdownFile)), Is.True);
        }

        private PromptSet WriteSet()
        {
            folder = Path.Combine(Path.GetTempPath(), "agenerela-live-eval-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(folder, "prompts"));
            File.WriteAllText(Path.Combine(folder, "agent.json"), SetJson);
            File.WriteAllText(Path.Combine(folder, "prompts", "commands.json"), CommandsJson);
            File.WriteAllText(Path.Combine(folder, "prompts", "impossible.json"), ImpossibleJson);
            return PromptSet.Load(Path.Combine(folder, "agent.json"));
        }

        private ProviderConfig Config(string name)
        {
            var config = ScriptableObject.CreateInstance<OllamaProviderConfig>();
            config.name = name;
            config.Endpoint = LiveOllama.Endpoint;
            config.Model = LiveOllama.Model;
            if (LiveOllama.ApiKey.Length > 0)
            {
                config.ApiKeyVariable = LiveOllama.ApiKeyVariable;
            }

            created.Add(config);
            return config;
        }

        // LiveGuard's identity, actions and idle example, as an asset the runner can read.
        private AgentProfile Profile()
        {
            var profile = ScriptableObject.CreateInstance<AgentProfile>();
            profile.name = "LiveVillageGuard";
            created.Add(profile);
            profile.Identity = new AgentIdentity
            {
                Name = "Village Guard",
                Role = "a guard at the town gate",
                Personality = "Loyal, brief, a little suspicious of strangers.",
                Goals = "keep the gate safe; help the player when asked.",
            };
            profile.IdleExampleStimulus = "You seem well rested today.";
            profile.Actions.Add(Action("follow_player", "Walk alongside the player.", "Accompany me on my rounds.", false));
            profile.Actions.Add(Action("stop_following", "Stop walking alongside the player.", "You can return to your post now.", false));
            profile.Actions.Add(Action("move_to", "Walk to a named place.", "Proceed to the {0}.", true, "bridge"));
            profile.Actions.Add(Action("attack_target", "Attack a named target.", "Engage the {0}.", true, "wolf"));
            return profile;
        }

        private ActionDefinitionAsset Action(string id, string description, string example, bool requiresTarget, params string[] preferred)
        {
            var asset = ScriptableObject.CreateInstance<ActionDefinitionAsset>();
            asset.name = id;
            created.Add(asset);
            asset.Action = new ActionDefinition
            {
                Id = id,
                Description = description,
                RequiresTarget = requiresTarget,
                ExampleStimulus = example,
                PreferredExampleTargets = preferred,
            };
            return asset;
        }
    }
}
