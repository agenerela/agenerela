using NUnit.Framework;

namespace Agenerela.Tests
{
    public class ActionDefinitionTests
    {
        [TestCase("follow_player")]
        [TestCase("move_to")]
        [TestCase("build")]
        [TestCase("assign_worker")]
        [TestCase("none")]
        public void ValidateIdAcceptsSnakeCase(string id)
        {
            Assert.That(ActionDefinition.ValidateId(id, out string problem), Is.True);
            Assert.That(problem, Is.Null);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ValidateIdRejectsEmptyIds(string id)
        {
            Assert.That(ActionDefinition.ValidateId(id, out string problem), Is.False);
            Assert.That(problem, Does.Contain("empty"));
        }

        [TestCase("Follow_Player")]
        [TestCase("FOLLOW")]
        [TestCase("moveTo")]
        public void ValidateIdRejectsUppercase(string id)
        {
            Assert.That(ActionDefinition.ValidateId(id, out string problem), Is.False);
            Assert.That(problem, Does.Contain("uppercase"));
        }

        [TestCase("follow player")]
        [TestCase(" follow")]
        [TestCase("follow ")]
        public void ValidateIdRejectsSpaces(string id)
        {
            Assert.That(ActionDefinition.ValidateId(id, out string problem), Is.False);
            Assert.That(problem, Does.Contain("spaces"));
        }
    }
}
