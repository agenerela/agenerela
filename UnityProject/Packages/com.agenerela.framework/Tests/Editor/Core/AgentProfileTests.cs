using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Agenerela.Tests
{
    public sealed class AgentProfileTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void DestroyCreatedAssets()
        {
            foreach (var asset in created)
            {
                Object.DestroyImmediate(asset);
            }

            created.Clear();
        }

        [Test]
        public void NewProfileHasTheNeutralIdleExampleAndNoActions()
        {
            var profile = NewProfile();

            Assert.That(profile.IdleExampleStimulus, Is.EqualTo("Nothing has changed since last time."));
            Assert.That(profile.Actions, Is.Empty);
        }

        [Test]
        public void TheDefaultIdleExampleIsTheOneANewProfileHas()
        {
            // So an agent built in code can be given the same one in a single line (#190).
            Assert.That(NewProfile().IdleExampleStimulus, Is.EqualTo(AgentProfile.DefaultIdleExampleStimulus));
        }

        [Test]
        public void ANewProfilesIdleExampleHasNoStatement()
        {
            // So a profile sends what it always did until someone writes one (#190, G14).
            Assert.That(NewProfile().IdleExampleStatement, Is.Empty);
        }

        [Test]
        public void ValidateAcceptsAWellFormedProfile()
        {
            var profile = NewProfile();
            profile.Actions.Add(NewAction("follow_player"));
            profile.Actions.Add(NewAction("move_to"));

            Assert.That(profile.Validate(out string problem), Is.True);
            Assert.That(problem, Is.Null);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ValidateRejectsAnEmptyIdentityName(string agentName)
        {
            var profile = NewProfile(agentName);

            Assert.That(profile.Validate(out string problem), Is.False);
            Assert.That(problem, Does.Contain("Identity.Name"));
        }

        [Test]
        public void ValidateRejectsAMissingIdentity()
        {
            var profile = NewProfile();
            profile.Identity = null;

            Assert.That(profile.Validate(out string problem), Is.False);
            Assert.That(problem, Does.Contain("Identity.Name"));
        }

        [Test]
        public void ValidateRejectsANullActionEntry()
        {
            var profile = NewProfile();
            profile.Actions.Add(NewAction("follow_player"));
            profile.Actions.Add(null);

            Assert.That(profile.Validate(out string problem), Is.False);
            Assert.That(problem, Does.Contain("Actions[1]"));
        }

        [Test]
        public void ValidateRejectsTheSameActionIdTwice()
        {
            var profile = NewProfile();
            profile.Actions.Add(NewAction("move_to"));
            profile.Actions.Add(NewAction("follow_player"));
            profile.Actions.Add(NewAction("move_to"));

            Assert.That(profile.Validate(out string problem), Is.False);
            Assert.That(problem, Does.Contain("move_to"));
            Assert.That(problem, Does.Contain("twice"));
        }

        [Test]
        public void BlankActionIdsAreNotReportedAsDuplicates()
        {
            // A blank id is the action asset's own warning. Two half-filled entries must
            // not read as "listed twice".
            var profile = NewProfile();
            profile.Actions.Add(NewAction(""));
            profile.Actions.Add(NewAction(""));

            Assert.That(profile.Validate(out string problem), Is.True);
            Assert.That(problem, Is.Null);
        }

        [Test]
        public void ANewProfileHasNoKnowledgeAndNoStatementRules()
        {
            // So a profile sends what it always did until someone writes them (#190, Living Vale #14).
            var profile = ScriptableObject.CreateInstance<AgentProfile>();
            created.Add(profile);

            Assert.That(profile.Identity.Knowledge, Is.Not.Null.And.Empty);
            Assert.That(profile.Identity.StatementRules, Is.Empty);
        }

        [Test]
        public void AProfileSavedBeforeKnowledgeExistedStillLoadsWithNone()
        {
            // An asset written before #190's Living Vale #14, as Assets/Evaluation/VillageGuard.asset
            // is: its identity has no Knowledge and no StatementRules.
            const string folder = "Assets/AgentProfileTestsOldAsset";
            const string path = folder + "/OldProfile.asset";
            var probe = ScriptableObject.CreateInstance<AgentProfile>();
            string script = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(probe)));
            Object.DestroyImmediate(probe);
            Assert.That(script, Is.Not.Empty);

            try
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(path, string.Join("\n",
                    "%YAML 1.1",
                    "%TAG !u! tag:unity3d.com,2011:",
                    "--- !u!114 &11400000",
                    "MonoBehaviour:",
                    "  m_ObjectHideFlags: 0",
                    "  m_CorrespondingSourceObject: {fileID: 0}",
                    "  m_PrefabInstance: {fileID: 0}",
                    "  m_PrefabAsset: {fileID: 0}",
                    "  m_GameObject: {fileID: 0}",
                    "  m_Enabled: 1",
                    "  m_EditorHideFlags: 0",
                    "  m_Script: {fileID: 11500000, guid: " + script + ", type: 3}",
                    "  m_Name: OldProfile",
                    "  m_EditorClassIdentifier: ",
                    "  Identity:",
                    "    Name: Village guard",
                    "    Role: a guard of the village",
                    "    Personality: Brief and dutiful.",
                    "    Goals: Keep the village safe.",
                    "  Actions: []",
                    "  IdleExampleStimulus: Fine day for it.",
                    ""));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

                var profile = AssetDatabase.LoadAssetAtPath<AgentProfile>(path);

                Assert.That(profile, Is.Not.Null);
                Assert.That(profile.Identity.Name, Is.EqualTo("Village guard"));
                Assert.That(profile.Identity.Goals, Is.EqualTo("Keep the village safe."));
                Assert.That(profile.Identity.Knowledge, Is.Not.Null.And.Empty);
                Assert.That(profile.Identity.StatementRules ?? "", Is.Empty);
                Assert.That(profile.IdleExampleStimulus, Is.EqualTo("Fine day for it."));
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private AgentProfile NewProfile(string agentName = "Village Guard")
        {
            var profile = ScriptableObject.CreateInstance<AgentProfile>();
            profile.Identity = new AgentIdentity { Name = agentName };
            created.Add(profile);
            return profile;
        }

        private ActionDefinitionAsset NewAction(string id)
        {
            var asset = ScriptableObject.CreateInstance<ActionDefinitionAsset>();
            asset.Action = new ActionDefinition { Id = id };
            created.Add(asset);
            return asset;
        }
    }
}
