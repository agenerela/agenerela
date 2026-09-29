using System.Collections.Generic;
using NUnit.Framework;
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
