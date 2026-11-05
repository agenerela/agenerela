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
    /// <remarks>
    /// Our set lives in this repository's <c>UnityProject/Assets</c>, not in the package, so the
    /// tests that read it run only there (<see cref="EvalFixtures.OurSetFolder"/>). The rest use fixtures.
    /// </remarks>
    public sealed class PromptSetFormatTests
    {
        private string copy;

        private static string Ours => EvalFixtures.OurSetFolder();

        private void CopyOurSet()
        {
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
            CopyOurSet();
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
            CopyOurSet();
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
            CopyOurSet();
            string path = Path.Combine(copy, "agent.json");
            var before = Parse(File.ReadAllText(path));

            PromptSet.Load(path).Save();

            Assert.That(JToken.DeepEquals(before, Parse(File.ReadAllText(path))), Is.True);
        }

        [Test]
        public void ASecondSaveWritesTheSameBytes()
        {
            CopyOurSet();
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

        /// <summary>
        /// Our files are kept in the layout the window writes, so a save in the window shows only
        /// what changed rather than reflowing the whole file.
        /// </summary>
        [Test]
        public void OurFilesAreInTheLayoutTheWindowWrites()
        {
            CopyOurSet();
            string setPath = Path.Combine(copy, "agent.json");
            var set = PromptSet.Load(setPath);
            var before = set.Categories.ToDictionary(c => c.Id, c => Lf(File.ReadAllText(set.CategoryPath(c))));
            string setBefore = Lf(File.ReadAllText(setPath));

            set.Save();
            foreach (var category in set.Categories)
            {
                set.SaveCategory(category);
            }

            Assert.That(File.ReadAllText(setPath), Is.EqualTo(setBefore), "agent.json is not in the layout PromptSet.Save writes.");
            foreach (var category in set.Categories)
            {
                Assert.That(File.ReadAllText(set.CategoryPath(category)), Is.EqualTo(before[category.Id]),
                    $"{category.File} is not in the layout PromptSet.SaveCategory writes.");
            }
        }

        [TestCase("../agent")]
        [TestCase("a/b")]
        [TestCase("..")]
        [TestCase("Situation")]
        [TestCase("")]
        public void ACategoryIdThatIsNotAFileNameIsRefused(string id)
        {
            Assert.That(PromptSet.ValidateCategoryId(id, out _), Is.False);
            using (var fixture = new EvalFixtures())
            {
                File.WriteAllText(fixture.SetPath, EvalFixtures.SetJson.Replace("\"id\": \"situation\"", $"\"id\": \"{id}\""));

                Assert.That(() => fixture.Load(), Throws.TypeOf<InvalidDataException>());
            }
        }

        [TestCase("prompts/../agent.json")]
        [TestCase("agent.json")]
        [TestCase("../outside.json")]
        [TestCase("labels/ana/situation.json")]
        [TestCase("prompts/situations.txt")]
        public void ACategoryFileOutsideItsPlaceIsRefused(string file)
        {
            using (var fixture = new EvalFixtures())
            {
                File.WriteAllText(fixture.SetPath, EvalFixtures.SetJson.Replace("prompts/situations.json", file));

                Assert.That(() => fixture.Load(), Throws.TypeOf<InvalidDataException>());
            }
        }

        [Test]
        public void TwoCategoriesCannotShareAFile()
        {
            using (var fixture = new EvalFixtures())
            {
                var set = fixture.Load();
                var thief = new PromptCategory { Id = "situations", File = "prompts/situations.json" };
                string before = File.ReadAllText(Path.Combine(fixture.Folder, "prompts", "situations.json"));

                Assert.That(set.ValidateCategory(thief, out _), Is.False, "situation already owns that file.");
                set.Categories.Add(thief);
                Assert.That(() => set.Save(), Throws.InvalidOperationException, "The set file is not written with two owners for one file.");
                Assert.That(() => set.SaveCategory(thief), Throws.InvalidOperationException);
                Assert.That(File.ReadAllText(Path.Combine(fixture.Folder, "prompts", "situations.json")), Is.EqualTo(before));
            }
        }

        [Test]
        public void ACategoryAddedInCodeWithABadFileIsNotWritten()
        {
            using (var fixture = new EvalFixtures())
            {
                var set = fixture.Load();
                var category = new PromptCategory { Id = "overwrite", File = "prompts/../agent.json" };
                set.Categories.Add(category);
                string before = File.ReadAllText(fixture.SetPath);

                Assert.That(() => set.SaveCategory(category), Throws.InvalidOperationException);
                Assert.That(File.ReadAllText(fixture.SetPath), Is.EqualTo(before));
            }
        }

        [TestCase("\"categories\": [", "\"categories\": null, \"x\": [")]
        [TestCase("\"actions\": [", "\"actions\": null, \"x\": [")]
        public void ANullListInTheSetFileIsInvalidData(string from, string to)
        {
            using (var fixture = new EvalFixtures())
            {
                File.WriteAllText(fixture.SetPath, EvalFixtures.SetJson.Replace(from, to));

                Assert.That(() => fixture.Load(), Throws.TypeOf<InvalidDataException>());
            }
        }

        [Test]
        public void NullPromptsInACategoryFileAreInvalidData()
        {
            using (var fixture = new EvalFixtures())
            {
                File.WriteAllText(Path.Combine(fixture.Folder, "prompts", "situations.json"),
                    "{ \"category\": \"situation\", \"prompts\": null }");

                Assert.That(() => fixture.Load(), Throws.TypeOf<InvalidDataException>());
            }
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

        // Git may check our files out with CRLF (core.autocrlf); the layout is what is compared.
        private static string Lf(string text) => text.Replace("\r\n", "\n");

        private static JToken Parse(string json)
        {
            using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
            {
                return JToken.ReadFrom(reader);
            }
        }
    }
}
