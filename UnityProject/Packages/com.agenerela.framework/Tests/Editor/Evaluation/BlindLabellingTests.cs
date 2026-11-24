using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Agenerela.Editor.Evaluation;
using Agenerela.Evaluation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Agenerela.Tests.Evaluation
{
    /// <summary>
    /// Label mode is blind (#134, findings.md): nothing the view is given holds an expected answer,
    /// a note or another person's label, and each answer goes to the annotator's own file.
    /// </summary>
    public sealed class BlindLabellingTests
    {
        private EvalFixtures fixture;

        [SetUp]
        public void MakeTheFixture() => fixture = new EvalFixtures();

        [TearDown]
        public void DeleteTheFixture() => fixture.Dispose();

        [Test]
        public void ABlindPromptHoldsOnlyTheLabelStimulusObservationsStateAndHistory()
        {
            var properties = typeof(BlindPrompt).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name);

            // History (#147) is the earlier turns, which the agent has already answered: context,
            // not the answer asked for. The next test checks it carries nothing else of the prompt.
            Assert.That(properties, Is.EquivalentTo(new[] { "Id", "CategoryId", "Label", "Stimulus", "Observations", "PreconditionState", "History" }));

            var fieldTypes = typeof(BlindPrompt).GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Select(f => f.FieldType);
            Assert.That(fieldTypes, Is.All.Matches<Type>(t => t == typeof(string) || t == typeof(IReadOnlyList<string>) || t == typeof(JObject)
                                                             || t == typeof(IReadOnlyList<HistoryTurn>)));
        }

        [Test]
        public void ABlindPromptsHistoryHoldsTheTurnsAndNothingOfThePromptsAnswer()
        {
            var set = fixture.Load();
            var prompt = set.FindCategory("command_with_target").Prompts.First(p => p.Id == "target_002");
            prompt.History = new List<HistoryTurn>
            {
                new HistoryTurn { Stimulus = "Walk with me a while.", Action = "follow_player", Target = "no_target", Statement = "Lead on." },
            };
            prompt.History[0].Extra["secret"] = JToken.FromObject("SECRET_TURN_FIELD");

            var blind = BlindPrompt.Queue(set, set.LoadLabels(), "Bo").First(p => p.Id == "target_002");
            string seen = JsonConvert.SerializeObject(blind);

            Assert.That(blind.History, Has.Count.EqualTo(1));
            Assert.That(blind.History[0].Stimulus, Is.EqualTo("Walk with me a while."));
            Assert.That(blind.History[0].Action, Is.EqualTo("follow_player"));
            Assert.That(blind.History[0], Is.Not.SameAs(prompt.History[0]), "The view holds the set's own turn, which it could change.");
            foreach (string secret in new[] { "attack_target", "wolf\"", "SECRET_NOTE", "SECRET_TURN_FIELD", "Maxim", "expected" })
            {
                Assert.That(seen, Does.Not.Contain(secret), $"The blind prompt holds '{secret}':\n{seen}");
            }
        }

        // ---- The agreement cohort (#148) ----------------------------------------------------------

        private AgreementCohort WriteCohort(params string[] ids)
        {
            var manifest = new JObject
            {
                ["issue"] = 148,
                ["categories"] = new JArray { new JObject { ["id"] = "any", ["promptIds"] = new JArray(ids) } },
            };
            File.WriteAllText(Path.Combine(fixture.Folder, AgreementCohort.FileName), manifest.ToString());
            return AgreementCohort.FindBeside(fixture.Load());
        }

        [Test]
        public void WithACohortTheQueueHoldsOnlyItsPrompts()
        {
            var cohort = WriteCohort("target_002", "situation_001");
            var set = fixture.Load();

            var queue = BlindPrompt.Queue(set, set.LoadLabels(), "Bo", cohort: cohort);

            Assert.That(queue.Select(p => p.Id), Is.EquivalentTo(new[] { "target_002", "situation_001" }));
            Assert.That(BlindPrompt.Queue(set, set.LoadLabels(), "Bo").Count, Is.GreaterThan(queue.Count), "Without the cohort, the whole set is asked.");
        }

        [Test]
        public void WithACohortTheProgressCountsOnlyItsPrompts()
        {
            var cohort = WriteCohort("target_001", "target_002", "situation_001");
            var set = fixture.Load();

            // Maxim wrote all three, so he is asked none of them; Bo is asked the three.
            Assert.That(LabelProgress.TotalFor(set, "Bo", cohort), Is.EqualTo(3));
            Assert.That(LabelProgress.TotalFor(set, "Maxim", cohort), Is.EqualTo(0));
            Assert.That(LabelProgress.TotalFor(set, "Bo"), Is.EqualTo(set.Prompts.Count()));
        }

        [Test]
        public void ACohortGivesAwayNoAnswer()
        {
            // The cohort is ids only: what Label mode is given with it is still blind.
            var cohort = WriteCohort("target_002");
            var set = fixture.Load();
            string seen = JsonConvert.SerializeObject(BlindPrompt.Queue(set, set.LoadLabels(), "Bo", cohort: cohort))
                        + JsonConvert.SerializeObject(cohort);

            foreach (string secret in new[] { "attack_target", "wolf\"", "SECRET_NOTE", "Maxim", "expected" })
            {
                Assert.That(seen, Does.Not.Contain(secret), $"Label mode with a cohort holds '{secret}'.");
            }
        }

        [Test]
        public void NoCohortFileIsNoCohortAndABrokenOneIsInvalidData()
        {
            Assert.That(AgreementCohort.FindBeside(fixture.Load()), Is.Null);

            File.WriteAllText(Path.Combine(fixture.Folder, AgreementCohort.FileName), "{ \"categories\": [] }");
            Assert.That(() => AgreementCohort.FindBeside(fixture.Load()), Throws.TypeOf<InvalidDataException>());

            File.WriteAllText(Path.Combine(fixture.Folder, AgreementCohort.FileName), "not json");
            Assert.That(() => AgreementCohort.FindBeside(fixture.Load()), Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public void OurCohortIsTheSeventySixPeoplesPromptsAndAllAreInTheSet()
        {
            var set = PromptSet.Load(Path.Combine(EvalFixtures.OurSetFolder(), "agent.json"));
            var cohort = AgreementCohort.FindBeside(set);

            Assert.That(cohort, Is.Not.Null, "Our set has no agreement-subset.json beside it.");
            Assert.That(cohort.Count, Is.EqualTo(76));
            Assert.That(cohort.PromptIds.Where(id => set.Prompts.All(p => p.Id != id)), Is.Empty, "A cohort id is not in our set.");
            Assert.That(set.Prompts.Where(p => cohort.Contains(p.Id)).SelectMany(p => p.LabelledBy).Distinct(),
                Has.None.EqualTo("claude (tech lead session)"), "An AI-written prompt is in the people's cohort.");
        }

        [Test]
        public void TheQueueGivesAwayNoAnswerNoteOrLabel()
        {
            var set = fixture.Load();
            fixture.WriteLabels("Ana", "command_with_target", "{ \"target_001\": \"follow_player/no_target\" }");

            var queue = BlindPrompt.Queue(set, set.LoadLabels(), "Bo");
            string seen = JsonConvert.SerializeObject(queue);

            Assert.That(queue.Select(p => p.Id), Does.Contain("target_002"));
            foreach (string secret in new[] { "attack_target", "wolf\"", "SECRET_NOTE", "Maxim", "Ana", "follow_player", "stop_following", "labels", "expected" })
            {
                Assert.That(seen, Does.Not.Contain(secret), $"The blind queue holds '{secret}':\n{seen}");
            }
        }

        [Test]
        public void TheLabelViewHoldsNothingThatHasTheAnswers()
        {
            var forbidden = new[]
            {
                typeof(PromptSet), typeof(PromptCategory), typeof(EvalPrompt), typeof(ExpectedTarget),
                typeof(AnnotatorLabels), typeof(AgreementReport), typeof(PromptAgreement),
            };

            foreach (var type in new[] { typeof(PromptSetLabelView), typeof(LabelChoices) })
            {
                foreach (var field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
                {
                    foreach (var reached in Unwrap(field.FieldType))
                    {
                        Assert.That(forbidden.Contains(reached), Is.False, $"{type.Name}.{field.Name} can reach {reached.Name}.");
                    }
                }
            }
        }

        [Test]
        public void TheQueueLeavesOutWhatYouWroteAndWhatYouAnswered()
        {
            var set = fixture.Load();
            fixture.WriteLabels("Ana", "command_with_target", "{ \"target_001\": \"move_to/bridge\" }");

            var forAna = BlindPrompt.Queue(set, set.LoadLabels(), "Ana").Select(p => p.Id).ToList();
            var forMaxim = BlindPrompt.Queue(set, set.LoadLabels(), "Maxim").Select(p => p.Id).ToList();

            Assert.That(forAna, Is.EqualTo(new[] { "target_002", "target_003", "situation_001" }),
                "Ana answered target_001, and situation_002 already has her settled label.");
            Assert.That(forMaxim, Is.Empty, "Maxim wrote every prompt.");
        }

        [Test]
        public void TheQueueCanBeOneCategory()
        {
            var set = fixture.Load();

            Assert.That(BlindPrompt.Queue(set, set.LoadLabels(), "Bo", "situation").Select(p => p.Id),
                Is.EqualTo(new[] { "situation_001", "situation_002" }));
        }

        [Test]
        public void ChangingABlindPromptChangesNothingInTheSet()
        {
            var set = fixture.Load();
            var blind = BlindPrompt.Queue(set, set.LoadLabels(), "Bo")[0];

            blind.PreconditionState["isFollowing"] = true;

            Assert.That((bool)set.Prompts.First().PreconditionState["isFollowing"], Is.False);
        }

        [Test]
        public void TheLabelViewAsksInAnOrderOfItsOwnForEachAnnotator()
        {
            // Sixty prompts in four categories: the set's order groups them, which tells an
            // annotator a prompt sits among the impossible requests.
            var queue = Enumerable.Range(1, 60).Select(i => $"c{i / 15}_{i:000}").ToList();
            var set = fixture.Load();
            var category = set.FindCategory("situation");
            category.Prompts.Clear();
            foreach (string id in queue)
            {
                category.Prompts.Add(new EvalPrompt { Id = id, Label = "Situation", Stimulus = id });
            }

            var blind = BlindPrompt.Queue(set, Array.Empty<AnnotatorLabels>(), "Bo", "situation");
            var forBo = PromptSetLabelView.Shuffled("Bo", blind).Select(p => p.Id).ToList();
            var forAna = PromptSetLabelView.Shuffled("Ana", blind).Select(p => p.Id).ToList();

            Assert.That(forBo, Is.EquivalentTo(queue), "Every prompt is still asked once.");
            Assert.That(forBo, Is.Not.EqualTo(queue), "The set's order would group prompts by category.");
            Assert.That(forBo, Is.EqualTo(PromptSetLabelView.Shuffled("Bo", blind).Select(p => p.Id).ToList()), "Reopening keeps the order.");
            Assert.That(forAna, Is.Not.EqualTo(forBo));
        }

        [Test]
        public void TheChoicesAreTheActionsTheStateAllowsWithNoneLast()
        {
            var choices = LabelChoices.From(fixture.Load());

            Assert.That(choices.ActionsAvailableIn(new JObject { ["isFollowing"] = false }),
                Is.EqualTo(new[] { "follow_player", "move_to", "attack_target", "none" }));
            Assert.That(choices.ActionsAvailableIn(new JObject { ["isFollowing"] = true }),
                Is.EqualTo(new[] { "follow_player", "stop_following", "move_to", "attack_target", "none" }));
            Assert.That(choices.Targets, Is.EqualTo(new[] { "bridge", "docks", "wolf" }));
        }

        [Test]
        public void TheTargetsOfferedAreTheOnesTheCheckAllows()
        {
            var set = fixture.Load();
            set.FindAction("stop_following").TakesNoTarget = true;
            var choices = LabelChoices.From(set);

            Assert.That(choices.TargetsFor("move_to"), Is.EqualTo(new[] { "bridge", "docks", "wolf" }), "Required.");
            Assert.That(choices.TargetsFor("follow_player"), Is.EqualTo(new[] { "no_target", "bridge", "docks", "wolf" }), "Optional.");
            Assert.That(choices.TargetsFor("stop_following"), Is.EqualTo(new[] { "no_target" }), "Never takes one.");
            Assert.That(choices.TargetsFor("none"), Is.EqualTo(new[] { "no_target" }));
        }

        [Test]
        public void AnAnswerGoesToTheAnnotatorsOwnFileAndNoOther()
        {
            var set = fixture.Load();
            fixture.WriteLabels("Ana", "command_with_target", "{ \"target_001\": \"move_to/bridge\" }");
            string anaFile = set.LabelPath("Ana", "command_with_target");
            string anaBefore = File.ReadAllText(anaFile);
            string promptsBefore = File.ReadAllText(set.CategoryPath(set.FindCategory("command_with_target")));

            Assert.That(set.RecordLabel("Bo", "target_002", new LabelAnswer("attack_target", "wolf")), Is.True);

            string boFile = Path.Combine(fixture.Folder, "labels", "Bo", "command_with_target.json");
            var saved = JObject.Parse(File.ReadAllText(boFile));
            Assert.That((string)saved["labels"]["target_002"], Is.EqualTo("attack_target/wolf"));
            Assert.That((string)saved["annotator"], Is.EqualTo("Bo"));
            Assert.That(File.ReadAllText(anaFile), Is.EqualTo(anaBefore));
            Assert.That(File.ReadAllText(set.CategoryPath(set.FindCategory("command_with_target"))), Is.EqualTo(promptsBefore));
        }

        [Test]
        public void AnAnswerOnceGivenIsNotReplaced()
        {
            var set = fixture.Load();
            set.RecordLabel("Bo", "target_002", new LabelAnswer("attack_target", "wolf"));

            Assert.That(set.RecordLabel("Bo", "target_002", new LabelAnswer("none", "no_target")), Is.False);

            var bo = set.LoadLabels().Single(l => l.Annotator == "Bo");
            Assert.That(bo.Labels["target_002"], Is.EqualTo("attack_target/wolf"));
        }

        [Test]
        public void AnswersInTwoCategoriesGoToTwoFiles()
        {
            var set = fixture.Load();
            set.RecordLabel("Bo", "target_002", new LabelAnswer("attack_target", "wolf"));
            set.RecordLabel("Bo", "situation_001", new LabelAnswer("none", "no_target"));

            Assert.That(set.LoadLabels().Where(l => l.Annotator == "Bo").Select(l => l.Category),
                Is.EquivalentTo(new[] { "command_with_target", "situation" }));
        }

        [TestCase("")]
        [TestCase(" Ana")]
        [TestCase("a/b")]
        [TestCase("..")]
        [TestCase("Ana:2")]
        public void ANameThatCannotBeAFolderIsRefused(string name)
        {
            Assert.That(PromptSet.ValidateAnnotator(name, out _), Is.False);
            Assert.That(() => fixture.Load().RecordLabel(name, "target_001", new LabelAnswer("none", "no_target")), Throws.ArgumentException);
        }

        // The type itself and every type argument inside it: a List<EvalPrompt> reaches EvalPrompt.
        private static IEnumerable<Type> Unwrap(Type type)
        {
            yield return type;
            if (type.IsArray)
            {
                foreach (var inner in Unwrap(type.GetElementType())) yield return inner;
            }

            if (type.IsGenericType)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    foreach (var inner in Unwrap(argument)) yield return inner;
                }
            }
        }
    }
}
