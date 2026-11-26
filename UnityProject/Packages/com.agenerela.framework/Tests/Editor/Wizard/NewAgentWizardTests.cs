using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Agenerela.Editor;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Agenerela.Tests.Wizard
{
    // Tests for NewAgentWizard. WizardFixtureGuard.cs is the script the wizard writes for
    // FixtureAnswers. It compiles, so matching it proves the wizard writes code that compiles. No test
    // calls Create, because writing a script into Assets would recompile in the middle of the run.
    public sealed class NewAgentWizardTests
    {
        // Test files are written here and deleted after each test.
        private const string Folder = "Assets/__NewAgentWizardTests";
        private const string FixturePath = "Packages/com.agenerela.framework/Tests/Editor/Wizard/WizardFixtureGuard.cs";
        private const string SettingsAt = Folder + "/Resources/" + AgenerelaSettings.ResourceName + ".asset";

        // In-memory objects made by a test, destroyed after it.
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
            AgenerelaSettings.Override = null;
            AgenerelaSettings.Forget();
            foreach (Object made in created)
            {
                Object.DestroyImmediate(made);
            }

            created.Clear();
        }

        [Test]
        public void TheScriptIsTheCompiledFixture()
        {
            // Git may check the file out with Windows line endings; the wizard writes \n.
            string fixture = File.ReadAllText(FixturePath).Replace("\r\n", "\n");

            Assert.That(NewAgentWizard.Script(FixtureAnswers()), Is.EqualTo(fixture),
                "Change WizardFixtureGuard.cs with the template, so the tests compile what it writes.");
        }

        [Test]
        public void TheAssetPathWritesAnAssetPerActionAndListsThemOnTheProfile()
        {
            NewAgentWizard.Answers answers = FixtureAnswers();
            answers.AsAssets = true;

            NewAgentWizard.WriteAssets(answers);

            AgentProfile profile = AssetDatabase.LoadAssetAtPath<AgentProfile>(Folder + "/WizardFixtureGuardProfile.asset");
            string[] ids = { "move_to", "follow_player", "speak_only", "attack_target", "wave" };
            Assert.That(profile, Is.Not.Null, "The profile is written.");
            Assert.That(profile.Identity.Name, Is.EqualTo("Wizard fixture guard"));
            Assert.That(profile.Identity.Role, Is.EqualTo("A guard the tests build."));
            Assert.That(profile.Actions.Select(a => a.Action.Id), Is.EqualTo(ids));
            Assert.That(profile.Actions.Select(AssetDatabase.GetAssetPath), Is.EqualTo(ids.Select(id => $"{Folder}/Actions/{id}.asset")),
                "Each action is its own asset, not one held inside the profile.");

            ActionDefinition moveTo = profile.Actions[0].Action;
            ActionDefinition followPlayer = profile.Actions[1].Action;
            ActionDefinition wave = profile.Actions[4].Action;
            Assert.That(moveTo.RequiresTarget && !moveTo.TakesNoTarget, Is.True, "move_to needs a target.");
            Assert.That(!followPlayer.RequiresTarget && followPlayer.TakesNoTarget, Is.True, "follow_player never takes one.");
            Assert.That(!wave.RequiresTarget && !wave.TakesNoTarget, Is.True, "wave's is optional.");
            Assert.That(moveTo.ExampleStimulus, Is.EqualTo("Wait by the {0}."));
            Assert.That(wave.ExampleStimulus, Is.Null, "An action with no example gets none, not an invented one.");
        }

        [Test]
        public void AnExistingFileStopsTheWizardAndIsNamed()
        {
            string profilePath = Folder + "/WizardFixtureGuardProfile.asset";
            AgenerelaSettingsPage.CreateFolders(Folder);
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<AgentProfile>(), profilePath);

            // Calls Check rather than Create: if Check failed to throw, Create would write a second copy
            // of the fixture class and break compiling.
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => NewAgentWizard.Check(FixtureAnswers()));

            Assert.That(error.Message, Does.Contain(profilePath));
        }

        // Each id passes ActionDefinition.ValidateId but would write a method name that does not compile.
        [TestCase("2fast")]
        [TestCase("!!")]
        [TestCase("über")]
        [TestCase("open-gate")]
        public void AnIdThatIsNotAMethodNameIsRefused(string id)
        {
            Assert.Throws<ArgumentException>(() => NewAgentWizard.Check(Answers("Gate guard", id)));
        }

        // Each pair writes the same method twice, or a method named like the class.
        [TestCase("Gate guard", "open_gate", "open__gate")]
        [TestCase("Gate guard", "move", "can_move")]
        [TestCase("Gate guard", "gate_guard", "wave")]
        public void IdsThatWriteTheSameNameAreRefused(string agentName, string first, string second)
        {
            ArgumentException error = Assert.Throws<ArgumentException>(() => NewAgentWizard.Check(Answers(agentName, first, second)));

            Assert.That(error.Message, Does.Contain("Rename the action"));
        }

        [Test]
        public void ANameOutsideAToZIsRefused()
        {
            Assert.Throws<ArgumentException>(() => NewAgentWizard.Check(Answers("Ärzte", "wave")));
        }

        [Test]
        public void AFolderWithDotDotIsRefused()
        {
            NewAgentWizard.Answers answers = Answers("Gate guard", "wave");
            answers.Folder = "Assets/../Elsewhere";

            Assert.Throws<ArgumentException>(() => NewAgentWizard.Check(answers));
        }

        [Test]
        public void AClassThatAlreadyExistsIsRefused()
        {
            // WizardFixtureGuard is compiled in this test assembly; a folder with no files in it
            // leaves the class as the only clash.
            NewAgentWizard.Answers answers = FixtureAnswers();
            answers.Folder = Folder + "/Elsewhere";

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => NewAgentWizard.Check(answers));

            Assert.That(error.Message, Does.Contain("A class called WizardFixtureGuard already exists"));
        }

        [Test]
        public void AProjectWithNoProviderGetsAnOllamaDefault()
        {
            // Skipped when this project has its own settings, so the test never changes them.
            if (AgenerelaSettingsPage.Find() != null)
            {
                Assert.Ignore("This project has its own Agenerela settings.");
            }

            OllamaProviderConfig provider = NewAgentWizard.EnsureDefaultProvider(Folder, SettingsAt);

            Assert.That(provider, Is.Not.Null, "A provider is made.");
            Assert.That(AssetDatabase.GetAssetPath(provider), Is.EqualTo(Folder + "/OllamaLocal.asset"));
            AgenerelaSettings.Forget();
            Assert.That(AgenerelaSettings.ActiveProvider, Is.SameAs(provider), "It is what an agent with no provider of its own uses.");
        }

        [Test]
        public void AnOllamaLocalAlreadyThereBecomesTheDefault()
        {
            // Skipped when this project has its own settings, so the test never changes them.
            if (AgenerelaSettingsPage.Find() != null)
            {
                Assert.Ignore("This project has its own Agenerela settings.");
            }

            AgenerelaSettingsPage.CreateFolders(Folder);
            OllamaProviderConfig existing = ScriptableObject.CreateInstance<OllamaProviderConfig>();
            AssetDatabase.CreateAsset(existing, Folder + "/OllamaLocal.asset");

            OllamaProviderConfig provider = NewAgentWizard.EnsureDefaultProvider(Folder, SettingsAt);

            Assert.That(provider, Is.SameAs(existing), "The asset already there is used, not replaced.");
            AgenerelaSettings.Forget();
            Assert.That(AgenerelaSettings.ActiveProvider, Is.SameAs(existing));
        }

        [Test]
        public void AnExistingDefaultIsLeftAlone()
        {
            AgenerelaSettings settings = Make<AgenerelaSettings>();
            OllamaProviderConfig existing = Make<OllamaProviderConfig>();
            settings.DefaultProvider = existing;
            AgenerelaSettings.Override = settings;

            OllamaProviderConfig made = NewAgentWizard.EnsureDefaultProvider(Folder, SettingsAt);

            Assert.That(made, Is.Null, "Nothing is made.");
            Assert.That(settings.DefaultProvider, Is.SameAs(existing), "The default is not replaced.");
            Assert.That(File.Exists(Folder + "/OllamaLocal.asset"), Is.False, "No second provider is written.");
        }

        // The answers the fixture was generated from: every starter, plus one action with an optional
        // target, no example and a quote in its description.
        private static NewAgentWizard.Answers FixtureAnswers()
        {
            NewAgentWizard.Answers answers = new NewAgentWizard.Answers
            {
                Name = "Wizard fixture guard",
                Role = "A guard the tests build.",
                Folder = Folder,
            };
            answers.Actions.AddRange(NewAgentWizard.Starters);
            answers.Actions.Add(new NewAgentWizard.ActionChoice
            {
                Id = "wave",
                Description = "Wave at someone, \"friendly\" like.",
                Target = NewAgentWizard.TargetKind.Optional,
            });
            return answers;
        }

        // Answers for an agent with the given action ids, each with a description.
        private static NewAgentWizard.Answers Answers(string agentName, params string[] ids)
        {
            NewAgentWizard.Answers answers = new NewAgentWizard.Answers { Name = agentName, Folder = Folder };
            answers.Actions.AddRange(ids.Select(id => new NewAgentWizard.ActionChoice { Id = id, Description = "Does a thing." }));
            return answers;
        }

        private T Make<T>() where T : ScriptableObject
        {
            T made = ScriptableObject.CreateInstance<T>();
            created.Add(made);
            return made;
        }
    }
}
