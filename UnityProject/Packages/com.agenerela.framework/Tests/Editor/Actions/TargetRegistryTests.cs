using System;
using NUnit.Framework;

namespace Agenerela.Tests
{
    public class TargetRegistryTests
    {
        [Test]
        public void NewRegistryIsEmpty()
        {
            var registry = new TargetRegistry();

            Assert.That(registry.Count, Is.EqualTo(0));
            Assert.That(registry.Ids, Is.Empty);
        }

        [Test]
        public void RegisterLookupAndContainsWork()
        {
            var registry = new TargetRegistry();
            var target = new object();

            registry.Register("bridge", target);

            Assert.That(registry.Count, Is.EqualTo(1));
            Assert.That(registry.Contains("bridge"), Is.True);
            Assert.That(registry.TryGet("bridge", out var found), Is.True);
            Assert.That(found, Is.SameAs(target));
        }

        [Test]
        public void IdsPreserveInsertionOrder()
        {
            var registry = new TargetRegistry();

            registry.Register("bridge", new object());
            registry.Register("tower", new object());
            registry.Register("sword", new object());

            Assert.That(registry.Ids, Is.EqualTo(new[] { "bridge", "tower", "sword" }));
        }

        [Test]
        public void UnregisterRemovesTargetAndReturnsWhetherItExisted()
        {
            var registry = new TargetRegistry();
            registry.Register("bridge", new object());

            Assert.That(registry.Unregister("bridge"), Is.True);
            Assert.That(registry.Unregister("bridge"), Is.False);
            Assert.That(registry.Contains("bridge"), Is.False);
            Assert.That(registry.Count, Is.EqualTo(0));
        }

        [Test]
        public void RegisterRejectsDuplicateIds()
        {
            var registry = new TargetRegistry();
            registry.Register("bridge", new object());

            var exception = Assert.Throws<ArgumentException>(() => registry.Register("bridge", new object()));

            Assert.That(exception.Message, Does.Contain("already registered"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(" bridge")]
        [TestCase("bridge ")]
        public void RegisterRejectsMalformedIds(string id)
        {
            var registry = new TargetRegistry();

            var exception = Assert.Throws<ArgumentException>(() => registry.Register(id, new object()));

            Assert.That(exception.Message, Does.Contain("Target IDs"));
        }
    }
}