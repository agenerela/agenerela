using NUnit.Framework;
using System.Collections.Generic;

namespace Agenerela.Tests
{
    public sealed class CoreTypeTests
    {
        [Test] // AgentIdentity.cs test
        public void IdentityStoresNameRolePersonality()
        {
            var identity = new AgentIdentity
            {
                Name = "Gate Guard",
                Role = "Village Sentry",
                Personality = "Short and formal.",
                Goals = "Keep the gate safe."
            };

            Assert.That(identity.Name, Is.EqualTo("Gate Guard"));
            Assert.That(identity.Role, Is.EqualTo("Village Sentry"));
            Assert.That(identity.Personality, Is.EqualTo("Short and formal."));
            Assert.That(identity.Goals, Is.EqualTo("Keep the gate safe."));     
        }

        [Test] // AgentContext.cs test
        public void ContextStoresStimulusObservationsStateTargetRegistry()
        {
            var identity = new AgentIdentity { Name = "Gate Guard"};

            var observations = new[]
            {
                "The gate is open.",
                "It is daytime."
            };

            var state = new Dictionary<string, object>
            {
                { "isFollowing", false}
            };

            var targets = new TargetRegistry();
            targets.Register("tower", new object());

            var context = new AgentContext(
                identity,
                "Go to the tower", // Stimulus assignment
                observations,
                state,
                targets);

            Assert.That(context.Identity.Name, Is.EqualTo("Gate Guard"));
            Assert.That(context.Stimulus, Is.EqualTo("Go to the tower"));
            Assert.That(context.Observations.Count, Is.EqualTo(2));
            Assert.That(context.Observations, Does.Contain("The gate is open."));
            Assert.That(context.State["isFollowing"], Is.EqualTo(false));
            Assert.That(context.Targets.Contains("tower"), Is.True);
        }

        [Test] // AgentDecision.cs test
        public void DecisionStoresActionTargetStatement()
        {
            var move = new AgentDecision("move_to", "tower", "On my way to the tower.");
            Assert.That(move.ActionId, Is.EqualTo("move_to"));
            Assert.That(move.TargetId, Is.EqualTo("tower"));
            Assert.That(move.Statement, Is.EqualTo("On my way to the tower."));

            var idle = new AgentDecision("none", "no_target", "");
            Assert.That(idle.ActionId, Is.EqualTo("none"));
            Assert.That(idle.TargetId, Is.EqualTo("no_target"));
            Assert.That(idle.Statement, Is.EqualTo(""));    
        }
    }
}
