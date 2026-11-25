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
    // What Create → Agenerela → New Agent writes (#175). WizardFixtureGuard.cs, beside this file, is
    // the script the wizard writes for FixtureAnswers, committed so the tests compile it: the first
    // test proves the template's output is that text, so the template provably compiles. No test
    // calls NewAgentWizard.Create, which writes a script into Assets and so starts a compile mid-run.
    // None attaches the fixture to a GameObject either: Unity refuses a script that lives under an
    // Editor folder, so the script running its assets is left to the hand check in a fresh project.
    public sealed class NewAgentWizardTests
    {
        // Deleted after every test.
        private const string Folder = "Assets/__NewAgentWizardTests";
        private const string FixturePath = "Packages/com.agenerela.framework/Tests/Editor/Wizard/WizardFixtureGuard.cs";
        private const string SettingsAt = Folder + "/Resources/" + AgenerelaSettings.ResourceName + ".asset";

        // Objects a test made in memory only, destroyed after it.
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
            // Git may check the fixture out with Windows line endings; the wizard always writes \n.
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
            Assert.That(moveTo.ExampleStimulus, Is.EqualTo("Keep watch over by the {0}."));
            Assert.That(wave.ExampleStimulus, Is.Null, "An action with no example gets none, not an invented one.");
        }

        [Test]
        public void AnExistingFileStopsTheWizardAndIsNamed()
        {
            string profilePath = Folder + "/WizardFixtureGuardProfile.asset";
            AgenerelaSettingsPage.CreateFolders(Folder);
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<AgentProfile>(), profilePath);

            // Check, which Create calls before it writes anything: were it Create, a Check that
            // failed to throw would write a second WizardFixtureGuard class and break the compile.
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => NewAgentWizard.Check(FixtureAnswers()));

            Assert.That(error.Message, Does.Contain(profilePath));
        }

        [Test]
        public void AProjectWithNoProviderGetsAnOllamaDefault()
        {
            // SetDefault edits the project's own settings wherever they are, so a project that has
            // some is left alone, as AgenerelaSettingsPageTests does.
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

        // The fixed answers WizardFixtureGuard.cs was written from: every starter, and one action of
        // the developer's own with an optional target, no example and a quote in its description.
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

        private T Make<T>() where T : ScriptableObject
        {
            T made = ScriptableObject.CreateInstance<T>();
            created.Add(made);
            return made;
        }
    }
}
