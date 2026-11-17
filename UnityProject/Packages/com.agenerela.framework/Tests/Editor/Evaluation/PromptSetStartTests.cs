using System.Collections.Generic;
using System.IO;
using System.Linq;
using Agenerela.Editor.Evaluation;
using Agenerela.Evaluation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Agenerela.Tests.Evaluation
{
    /// <summary>
    /// The Prompt Set window's first run (#158): <b>Open the project's evaluation set</b> finds the
    /// set and the profile beside it, and our own set has a profile its examples never match.
    /// </summary>
    public sealed class PromptSetStartTests
    {
        private const string OurSetPath = "Assets/Evaluation/agent.json";
        private const string OurProfilePath = "Assets/Evaluation/VillageGuard.asset";

        private EvalFixtures fixture;
        private readonly List<Object> created = new List<Object>();

        [SetUp]
        public void MakeTheFixture() => fixture = new EvalFixtures();

        [TearDown]
        public void CleanUp()
        {
            fixture.Dispose();
            Fixtures.DestroyCreated();
            foreach (var asset in created)
            {
                Object.DestroyImmediate(asset);
            }

            created.Clear();
        }

        [Test]
        public void TheConventionalSetComesFirstAndOnlySetFilesCount()
        {
            var sets = PromptSetStart.OrderSets(new[]
            {
                "Assets/Zeta/agent.json",
                "Assets/Evaluation/agent.json",
                "Assets/Alpha/agent.json",
                "Assets/Alpha/secret_agent.json",
                "Assets/Evaluation/README.md",
                "Assets/Evaluation/agent.json",
            });

            Assert.That(sets, Is.EqualTo(new[] { "Assets/Evaluation/agent.json", "Assets/Alpha/agent.json", "Assets/Zeta/agent.json" }));
        }

        [Test]
        public void TheProfileBesideTheSetIsTheOneInItsFolder()
        {
            // All three agree with the set, so only the folder can tell them apart.
            var guard = Fixtures.VillageGuardProfile();
            var below = Fixtures.VillageGuardProfile();
            var elsewhere = Fixtures.VillageGuardProfile();

            var found = PromptSetStart.ProfileBeside("Assets/Eval/agent.json", fixture.Load(), new[]
            {
                Entry("Assets/Eval/Sub/Below.asset", below),
                Entry("Assets/Other/Elsewhere.asset", elsewhere),
                Entry("Assets/Eval/Guard.asset", guard),
            });

            Assert.That(found, Is.SameAs(guard));
        }

        [Test]
        public void AmongSeveralProfilesBesideTheSetTheOneThatAgreesWithItIsTaken()
        {
            var guard = Fixtures.VillageGuardProfile();
            var stranger = Profile("stranger");

            var found = PromptSetStart.ProfileBeside("Assets/Eval/agent.json", fixture.Load(), new[]
            {
                Entry("Assets/Eval/Stranger.asset", stranger),
                Entry("Assets/Eval/Guard.asset", guard),
            });

            Assert.That(found, Is.SameAs(guard));
        }

        [Test]
        public void NoProfileIsGuessedWhenNoneOrSeveralFit()
        {
            var set = fixture.Load();
            var one = Fixtures.VillageGuardProfile();
            var two = Fixtures.VillageGuardProfile();

            Assert.That(PromptSetStart.ProfileBeside("Assets/Eval/agent.json", set, new[] { Entry("Assets/Other/Guard.asset", one) }), Is.Null);
            Assert.That(PromptSetStart.ProfileBeside("Assets/Eval/agent.json", set, new[]
            {
                Entry("Assets/Eval/One.asset", one),
                Entry("Assets/Eval/Two.asset", two),
            }), Is.Null);
        }

        [Test]
        public void AnotherSetTakesItsRememberedProfileThenTheOneBesideIt()
        {
            var current = Profile("current");
            var remembered = Profile("remembered");
            var beside = Profile("beside");

            Assert.That(PromptSetStart.ProfileWhenOpening(false, current, remembered, () => beside), Is.SameAs(remembered));
            Assert.That(PromptSetStart.ProfileWhenOpening(false, current, null, () => beside), Is.SameAs(beside));
            Assert.That(PromptSetStart.ProfileWhenOpening(false, current, null, () => null), Is.SameAs(current));
        }

        [Test]
        public void OpeningTheSetAlreadyOpenFillsOnlyAMissingProfile()
        {
            // The set chosen, its profile cleared: the start button still makes the window ready.
            var current = Profile("current");
            var beside = Profile("beside");

            Assert.That(PromptSetStart.ProfileWhenOpening(true, null, null, () => beside), Is.SameAs(beside));
            Assert.That(PromptSetStart.ProfileWhenOpening(true, current, null, () => beside), Is.SameAs(current));
        }

        [Test]
        public void OurProjectsButtonOpensOurSetAndItsProfile()
        {
            string folder = EvalFixtures.OurSetFolder();

            Assert.That(PromptSetStart.FindSets().FirstOrDefault(), Is.EqualTo(OurSetPath));

            var set = PromptSet.Load(Path.Combine(folder, "agent.json"));
            var profile = PromptSetStart.FindProfileBeside(OurSetPath, set);
            Assert.That(profile, Is.Not.Null.And.SameAs(AssetDatabase.LoadAssetAtPath<AgentProfile>(OurProfilePath)));
        }

        [Test]
        public void OurProfileHasEveryActionOurSetDeclares()
        {
            var set = PromptSet.Load(Path.Combine(EvalFixtures.OurSetFolder(), "agent.json"));
            var profile = OurProfile();

            Assert.That(profile.Validate(out string problem), Is.True, problem);
            Assert.That(PromptSetChecks.CompareWithProfile(set, profile), Is.Empty);
        }

        [Test]
        public void NoPromptOfOursIsOneOfOurProfilesExamples()
        {
            // Hard rule 4, checked with the examples our set is actually measured with. Every
            // example is filled in, so the check below has something to match against.
            var set = PromptSet.Load(Path.Combine(EvalFixtures.OurSetFolder(), "agent.json"));
            var profile = OurProfile();
            Assert.That(profile.Actions.Select(a => a.Action.ExampleStimulus), Has.All.Not.Empty);
            Assert.That(profile.IdleExampleStimulus, Is.Not.Empty);

            var examples = PromptSetChecks.Check(set, profile).Where(p => p.Rule == PromptRule.StimulusIsExample);

            Assert.That(examples.Select(p => p.ToString()), Is.Empty);
        }

        private static AgentProfile OurProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AgentProfile>(OurProfilePath);
            Assert.That(profile, Is.Not.Null, OurProfilePath + " is missing.");
            return profile;
        }

        private AgentProfile Profile(string name)
        {
            var profile = ScriptableObject.CreateInstance<AgentProfile>();
            profile.name = name;
            profile.Identity.Name = name;
            created.Add(profile);
            return profile;
        }

        private static KeyValuePair<string, AgentProfile> Entry(string path, AgentProfile profile)
        {
            return new KeyValuePair<string, AgentProfile>(path, profile);
        }
    }
}
