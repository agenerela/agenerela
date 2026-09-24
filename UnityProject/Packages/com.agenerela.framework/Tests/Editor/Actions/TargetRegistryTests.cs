using System;
using System.Collections.Generic;
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

        [Test]
        public void RegisterRejectsReservedNoTargetId()
        {
            var registry = new TargetRegistry();

            var exception = Assert.Throws<ArgumentException>(
                () => registry.Register(TargetRegistry.NoTarget, new object()));

            Assert.That(exception.Message, Does.Contain("reserved"));
            Assert.That(registry.Count, Is.EqualTo(0));
        }

        [Test]
        public void RegisterRejectsNullTarget()
        {
            var registry = new TargetRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.Register("bridge", null));
            Assert.That(registry.Count, Is.EqualTo(0));
        }

        [Test]
        public void TryGetReturnsFalseAndNullForUnknownId()
        {
            var registry = new TargetRegistry();
            registry.Register("bridge", new object());

            Assert.That(registry.TryGet("tower", out var found), Is.False);
            Assert.That(found, Is.Null);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(" bridge")]
        [TestCase("no_target")]
        public void LookupsReturnFalseForIdsThatCannotBeRegistered(string id)
        {
            var registry = new TargetRegistry();
            registry.Register("bridge", new object());

            // Any exception here fails the test: these ids come from untrusted provider output.
            Assert.That(registry.TryGet(id, out var found), Is.False);
            Assert.That(found, Is.Null);
            Assert.That(registry.Contains(id), Is.False);
            Assert.That(registry.Unregister(id), Is.False);
            Assert.That(registry.Ids, Is.EqualTo(new[] { "bridge" }));
        }

        [Test]
        public void IdsKeepOrderAfterRemovingFromTheMiddle()
        {
            var registry = new TargetRegistry();
            registry.Register("bridge", new object());
            registry.Register("tower", new object());
            registry.Register("sword", new object());

            registry.Unregister("tower");

            Assert.That(registry.Ids, Is.EqualTo(new[] { "bridge", "sword" }));
        }

        [Test]
        public void IdCanBeRegisteredAgainAfterUnregister()
        {
            var registry = new TargetRegistry();
            registry.Register("bridge", new object());
            registry.Unregister("bridge");
            var replacement = new object();

            registry.Register("bridge", replacement);

            Assert.That(registry.TryGet("bridge", out var found), Is.True);
            Assert.That(found, Is.SameAs(replacement));
            Assert.That(registry.Ids, Is.EqualTo(new[] { "bridge" }));
        }

        [Test]
        public void IdsIsALiveReadOnlyView()
        {
            var registry = new TargetRegistry();
            var ids = registry.Ids;

            registry.Register("bridge", new object());

            Assert.That(ids, Is.EqualTo(new[] { "bridge" }));
            Assert.That(((ICollection<string>)ids).IsReadOnly, Is.True);
        }
    }
}