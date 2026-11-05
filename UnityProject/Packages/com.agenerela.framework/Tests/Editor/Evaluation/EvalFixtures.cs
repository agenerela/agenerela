using System;
using System.IO;
using Agenerela.Evaluation;

namespace Agenerela.Tests.Evaluation
{
    /// <summary>
    /// A small prompt set on disk in a folder of its own, for the tests that read and write files.
    /// Its agent is <see cref="Fixtures.VillageGuardProfile"/>'s, so the profile and the set agree.
    /// Dispose deletes the folder.
    /// </summary>
    /// <remarks>
    /// No stimulus here may be an evaluation prompt or a few-shot example (hard rule 4), except in
    /// a test about exactly that.
    /// </remarks>
    public sealed class EvalFixtures : IDisposable
    {
        public const string SetJson = @"{
  ""agent"": ""Village guard"",
  ""actions"": [
    { ""id"": ""follow_player"", ""needsTarget"": false },
    { ""id"": ""stop_following"", ""needsTarget"": false, ""onlyWhen"": { ""isFollowing"": true } },
    { ""id"": ""move_to"", ""needsTarget"": true },
    { ""id"": ""attack_target"", ""needsTarget"": true },
    { ""id"": ""none"", ""needsTarget"": false }
  ],
  ""targets"": [
    { ""id"": ""bridge"", ""description"": ""The wooden bridge"" },
    { ""id"": ""docks"", ""description"": ""The docks"" },
    { ""id"": ""wolf"", ""description"": ""A wolf"" }
  ],
  ""categories"": [
    { ""id"": ""command_with_target"", ""file"": ""prompts/commands.json"", ""idPrefix"": ""target"" },
    { ""id"": ""situation"", ""file"": ""prompts/situations.json"" }
  ]
}";

        // Three prompts Maxim wrote, one open choice, and one already settled between Maxim and Ana.
        public const string CommandsJson = @"{
  ""category"": ""command_with_target"",
  ""prompts"": [
    { ""id"": ""target_001"", ""label"": ""Player"", ""stimulus"": ""Meet me over at the bridge."", ""observations"": [],
      ""expectedAction"": ""move_to"", ""expectedTarget"": ""bridge"", ""preconditionState"": { ""isFollowing"": false }, ""labelledBy"": [""Maxim""] },
    { ""id"": ""target_002"", ""label"": ""Player"", ""stimulus"": ""Deal with that animal."", ""observations"": [""A wolf is circling the sheep""],
      ""expectedAction"": ""attack_target"", ""expectedTarget"": ""wolf"", ""preconditionState"": { ""isFollowing"": false },
      ""note"": ""SECRET_NOTE the wolf is the only animal"", ""labelledBy"": [""Maxim""] },
    { ""id"": ""target_003"", ""label"": ""Player"", ""stimulus"": ""Go wait by the water."", ""observations"": [],
      ""expectedAction"": ""move_to"", ""expectedTarget"": [""bridge"", ""docks""], ""preconditionState"": { ""isFollowing"": false }, ""labelledBy"": [""Maxim""] }
  ]
}";

        public const string SituationsJson = @"{
  ""category"": ""situation"",
  ""prompts"": [
    { ""id"": ""situation_001"", ""label"": ""Situation"", ""stimulus"": ""The square is empty."", ""observations"": [""It is raining""],
      ""expectedAction"": ""none"", ""expectedTarget"": ""no_target"", ""preconditionState"": { ""isFollowing"": false }, ""labelledBy"": [""Maxim""] },
    { ""id"": ""situation_002"", ""label"": ""Situation"", ""stimulus"": ""The player has gone quiet."", ""observations"": [],
      ""expectedAction"": ""none"", ""expectedTarget"": ""no_target"", ""preconditionState"": { ""isFollowing"": true },
      ""labelledBy"": [""Maxim"", ""Ana""], ""labels"": { ""Maxim"": ""none/no_target"", ""Ana"": ""stop_following/no_target"" } }
  ]
}";

        public EvalFixtures()
        {
            Folder = Path.Combine(Path.GetTempPath(), "agenerela-promptset-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Folder, "prompts"));
            File.WriteAllText(SetPath, SetJson);
            File.WriteAllText(Path.Combine(Folder, "prompts", "commands.json"), CommandsJson);
            File.WriteAllText(Path.Combine(Folder, "prompts", "situations.json"), SituationsJson);
        }

        public string Folder { get; }

        /// <summary>
        /// Our own set, <c>Assets/Evaluation</c> in agenerela's <c>UnityProject</c>, or an ignored
        /// test anywhere else. The project counts as ours when it sits in the agenerela repository,
        /// beside <c>tools/ci/hygiene.py</c>: any other project's set at that folder is its own,
        /// and is not held to our layout or our guard's examples.
        /// </summary>
        public static string OurSetFolder()
        {
            string folder = Path.Combine(UnityEngine.Application.dataPath, "Evaluation");
            bool ours = File.Exists(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "tools", "ci", "hygiene.py"));
            if (!ours || !File.Exists(Path.Combine(folder, "agent.json")))
            {
                NUnit.Framework.Assert.Ignore("Only agenerela's own UnityProject has our prompt set.");
            }

            return folder;
        }

        public string SetPath => Path.Combine(Folder, "agent.json");

        public PromptSet Load() => PromptSet.Load(SetPath);

        /// <summary>Writes an annotator's label file by hand, the way a teammate's would arrive through git.</summary>
        public void WriteLabels(string annotator, string category, string labelsJson)
        {
            string folder = Path.Combine(Folder, PromptSet.LabelsFolder, annotator);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, category + ".json"),
                $"{{ \"annotator\": \"{annotator}\", \"category\": \"{category}\", \"labels\": {labelsJson} }}");
        }

        public void Dispose()
        {
            if (Directory.Exists(Folder))
            {
                Directory.Delete(Folder, true);
            }
        }
    }
}
