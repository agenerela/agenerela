using System.Collections.Generic;
using System.IO;
using GreyBoxStrategy.Realm;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GreyBoxStrategy.Screen.Tests
{
    // The world's lore (#20) on the screen's side: the loremaster's proposal kept per world, model and prompt version,
    // as written, and the ruler's checked line reaching the court's portrait prompt.
    public sealed class WorldLoreCacheTests
    {
        private string folder;

        [SetUp]
        public void MakeFolder() => folder = Path.Combine(Path.GetTempPath(), "lore-cache-" + System.Guid.NewGuid().ToString("N"));

        [TearDown]
        public void RemoveFolder() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }

        private static WorldMap World(int seed) => MapGenerator.Generate(seed, 5).Map;

        [Test]
        public void AProposal_IsReadBackAsWritten_UnderItsWorldAndModel()
        {
            var world = World(7);
            var cache = new WorldLoreCache(folder);
            string key = WorldLoreCache.Key(world, "qwen3.5:4b");
            Assert.That(cache.Read(key), Is.Null);

            cache.Write(key, new LoreProposal { WorldName = "Ashen Reach", RealmNames = new List<string> { "Velmora", null }, Backstory = "It began. " });
            var back = cache.Read(key);
            Assert.That(back.WorldName, Is.EqualTo("Ashen Reach"));
            Assert.That(back.RealmNames, Is.EqualTo(new[] { "Velmora", null }));
            Assert.That(back.Backstory, Is.EqualTo("It began. "), "As written: the checks run on every read.");

            Assert.That(key, Does.StartWith($"v{WorldLoremaster.Version}-qwen3.5-4b-"));
            Assert.That(WorldLoreCache.Key(world, "qwen3.5:2b"), Is.Not.EqualTo(key), "Another model asks again.");
            Assert.That(WorldLoreCache.Key(World(21), "qwen3.5:4b"), Is.Not.EqualTo(key), "Another world asks again.");
            Assert.That(WorldLoreCache.Key(World(7), "qwen3.5:4b"), Is.EqualTo(key), "The same world finds it.");
        }

        [Test]
        public void AnUnreadableFile_IsNoProposal()
        {
            var cache = new WorldLoreCache(folder);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "lore-k.json"), "{ not json");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("lore cache could not be read"));
            Assert.That(cache.Read("k"), Is.Null);
        }

        [Test]
        public void ARulersLine_ReachesTheirPortraitPrompt_Capped()
        {
            var subject = new RulerSubject { Id = "velmora", Name = "Velmora", Persona = "builder", Backstory = "A patient builder of walls and granaries." };
            Assert.That(RulerArtPrompts.Portrait(7, subject).Prompt, Does.Contain("A patient builder of walls and granaries"));
        }
    }
}
