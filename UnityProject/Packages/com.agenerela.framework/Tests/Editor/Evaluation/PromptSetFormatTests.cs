using System.IO;
using System.Linq;
using Agenerela.Evaluation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Agenerela.Tests.Evaluation
{
    /// <summary>
    /// The format reads and writes our own set without changing a word of it (#134): every file
    /// in <c>Assets/Evaluation/prompts/</c> is loaded, saved to a copy, and compared as JSON.
    /// </summary>
    public sealed class PromptSetFormatTests
    {
        private string copy;

        private static string Ours => Path.Combine(Application.dataPath, "Evaluation");

        [SetUp]
        public void CopyOurSet()
        {
            Assert.That(Directory.Exists(Ours), Is.True, $"Our prompt set is not at {Ours}.");
            copy = Path.Combine(Path.GetTempPath(), "agenerela-ourset-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(copy, "prompts"));
            File.Copy(Path.Combine(Ours, "agent.json"), Path.Combine(copy, "agent.json"));
            foreach (string file in Directory.GetFiles(Path.Combine(Ours, "prompts"), "*.json"))
            {
                File.Copy(file, Path.Combine(copy, "prompts", Path.GetFileName(file)));
            }
        }

        [TearDown]
        public void DeleteCopy()
        {
            if (copy != null && Directory.Exists(copy))
            {
                Directory.Delete(copy, true);
            }
        }

        [Test]
        public void EveryPromptFileIsACategoryOfTheSet()
        {
            var set = PromptSet.Load(Path.Combine(copy, "agent.json"));
            var listed = set.Categories.Select(c => Path.GetFullPath(set.CategoryPath(c))).ToList();

            foreach (string file in Directory.GetFiles(Path.Combine(copy, "prompts"), "*.json"))
            {
                Assert.That(listed, Does.Contain(Path.GetFullPath(file)), $"{Path.GetFileName(file)} is not listed in agent.json, so the window never shows it.");
            }
        }

        [Test]
        public void EveryPromptFileRoundTripsWithNoChangeToItsContent()
        {
            var set = PromptSet.Load(Path.Combine(copy, "agent.json"));
            int prompts = 0;

            foreach (var category in set.Categories)
            {
                string path = set.CategoryPath(category);
                var before = Parse(File.ReadAllText(path));
                prompts += ((JArray)before["prompts"]).Count;

                set.SaveCategory(category);

                var after = Parse(File.ReadAllText(path));
                Assert.That(JToken.DeepEquals(before, after), Is.True,
                    $"{Path.GetFileName(path)} changed on a round trip:\n{after}");
            }

            Assert.That(set.Prompts.Count(), Is.EqualTo(prompts), "A prompt was dropped on loading.");
        }

        [Test]
        public void TheSetFileRoundTripsWithNoChangeToItsContent()
        {
            string path = Path.Combine(copy, "agent.json");
            var before = Parse(File.ReadAllText(path));

            PromptSet.Load(path).Save();

            Assert.That(JToken.DeepEquals(before, Parse(File.ReadAllText(path))), Is.True);
        }

        [Test]
        public void ASecondSaveWritesTheSameBytes()
        {
            var set = PromptSet.Load(Path.Combine(copy, "agent.json"));
            var category = set.Categories[0];
            set.SaveCategory(category);
            string first = File.ReadAllText(set.CategoryPath(category));

            set = PromptSet.Load(Path.Combine(copy, "agent.json"));
            set.SaveCategory(set.Categories[0]);

            Assert.That(File.ReadAllText(set.CategoryPath(category)), Is.EqualTo(first));
            Assert.That(first, Does.Not.Contain("\r"), "Saved files use LF on every machine.");
            Assert.That(first, Does.EndWith("}\n"));
        }

        [Test]
        public void AFieldTheFormatDoesNotKnowIsKept()
        {
            using (var fixture = new EvalFixtures())
            {
                string path = Path.Combine(fixture.Folder, "prompts", "situations.json");
                File.WriteAllText(path, EvalFixtures.SituationsJson.Replace("\"label\": \"Situation\", \"stimulus\": \"The square",
                    "\"label\": \"Situation\", \"turn\": 3, \"stimulus\": \"The square"));
                var set = fixture.Load();

                set.SaveCategory(set.FindCategory("situation"));

                var saved = Parse(File.ReadAllText(path));
                Assert.That((int)saved["prompts"][0]["turn"], Is.EqualTo(3));
            }
        }

        [Test]
        public void AnExpectedTargetKeepsTheShapeItWasWrittenIn()
        {
            Assert.That(JsonConvert.SerializeObject(new ExpectedTarget("bridge")), Is.EqualTo("\"bridge\""));
            Assert.That(JsonConvert.SerializeObject(new ExpectedTarget("bridge", "docks")), Is.EqualTo("[\"bridge\",\"docks\"]"));

            var listOfOne = JsonConvert.DeserializeObject<ExpectedTarget>("[\"bridge\"]");
            Assert.That(listOfOne.IsOpenChoice, Is.False);
            Assert.That(JsonConvert.SerializeObject(listOfOne), Is.EqualTo("[\"bridge\"]"));
        }

        [Test]
        public void ACategoryFileThatNamesAnotherCategoryIsRefused()
        {
            using (var fixture = new EvalFixtures())
            {
                File.WriteAllText(Path.Combine(fixture.Folder, "prompts", "situations.json"),
                    EvalFixtures.SituationsJson.Replace("\"category\": \"situation\"", "\"category\": \"event\""));

                Assert.That(() => fixture.Load(), Throws.TypeOf<InvalidDataException>().With.Message.Contains("event"));
            }
        }

        [Test]
        public void NextIdFollowsTheHighestInTheCategory()
        {
            using (var fixture = new EvalFixtures())
            {
                var set = fixture.Load();

                Assert.That(set.NextId(set.FindCategory("command_with_target")), Is.EqualTo("target_004"));
                Assert.That(set.NextId(set.FindCategory("situation")), Is.EqualTo("situation_003"));
            }
        }

        [TestCase("move_to/village_gate", true, "move_to", "village_gate")]
        [TestCase("none/no_target", true, "none", "no_target")]
        [TestCase("move_to", false, null, null)]
        [TestCase("/bridge", false, null, null)]
        [TestCase("move_to/", false, null, null)]
        [TestCase("a/b/c", false, null, null)]
        public void ALabelIsActionSlashTarget(string text, bool valid, string action, string target)
        {
            Assert.That(LabelAnswer.TryParse(text, out var answer), Is.EqualTo(valid));
            if (valid)
            {
                Assert.That(answer.Action, Is.EqualTo(action));
                Assert.That(answer.Target, Is.EqualTo(target));
                Assert.That(answer.ToString(), Is.EqualTo(text));
            }
        }

        private static JToken Parse(string json)
        {
            using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
            {
                return JToken.ReadFrom(reader);
            }
        }
    }
}
