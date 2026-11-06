using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agenerela;
using LivingVale.Agenerela;
using LivingVale.Game;
using LivingVale.Interaction;
using LivingVale.World;
using NUnit.Framework;

namespace LivingVale.Tests
{
    /// <summary>G4b: landmark discovery, NPC routines, the six region NPCs and the layout's walking times.</summary>
    public sealed class RegionTests
    {
        // ---------- Discovery ----------

        [Test]
        public void EnteringAPlaceTheFirstTimeAwardsItsXpOnce()
        {
            var progression = new Progression();
            var discovery = new PlaceDiscovery(progression);
            var heard = new List<string>();
            discovery.Discovered += place => heard.Add(place.Id);
            ValePlace reedbank = ValePlaces.Get(ValePlaces.Reedbank);

            Assert.AreSame(reedbank, discovery.Visit(reedbank.X, reedbank.Z));
            Assert.IsNull(discovery.Visit(reedbank.X + 3f, reedbank.Z), "a second step inside is not a second discovery");
            Assert.IsNull(discovery.Visit(0f, -80f), "between places");
            Assert.IsNull(discovery.Visit(reedbank.X, reedbank.Z), "coming back is not a discovery");

            Assert.AreEqual(PlaceDiscovery.Xp, progression.Xp);
            Assert.IsTrue(progression.HasAwarded("discover_place_reedbank"));
            CollectionAssert.AreEqual(new[] { ValePlaces.Reedbank }, heard);
            Assert.AreSame(reedbank, discovery.Current);
        }

        [Test]
        public void TheStartingVillageIsKnownAndPaysNothing()
        {
            var progression = new Progression();
            var discovery = new PlaceDiscovery(progression);

            Assert.IsNull(discovery.Visit(0f, 0f));
            Assert.AreEqual(ValePlaces.BriarGlen, discovery.Current?.Id);
            Assert.IsTrue(discovery.IsDiscovered(ValePlaces.BriarGlen));
            Assert.AreEqual(0, progression.Xp);
        }

        [Test]
        public void EveryOtherPlaceIsALandmarkWithItsOwnEventId()
        {
            var progression = new Progression();
            var discovery = new PlaceDiscovery(progression);
            foreach (var place in ValePlaces.All.Where(p => p.Id != ValePlaces.BriarGlen))
            {
                Assert.AreSame(place, discovery.Visit(place.X, place.Z), place.Id);
                Assert.IsTrue(progression.HasAwarded(PlaceDiscovery.EventPrefix + place.Id), place.Id);
            }

            Assert.AreEqual(5 * PlaceDiscovery.Xp, progression.Xp);
        }

        [Test]
        public void ADiscoveryIsSavedAndNeverPaysAgainAfterLoading()
        {
            string directory = Path.Combine(Path.GetTempPath(), "LivingValeRegionTests_" + Guid.NewGuid().ToString("N"));
            try
            {
                var first = new SavedGame(directory);
                ValePlace hollow = ValePlaces.Get(ValePlaces.HerbHollow);
                Assert.IsNotNull(first.Discovery.Visit(hollow.X, hollow.Z));
                Assert.IsTrue(first.Save.TrySave(out string saveError), saveError);

                var second = new SavedGame(directory);
                Assert.IsFalse(second.Discovery.IsDiscovered(ValePlaces.HerbHollow), "a new game has not been there");
                Assert.IsTrue(second.Save.TryLoad(out string loadError), loadError);

                Assert.IsTrue(second.Discovery.IsDiscovered(ValePlaces.HerbHollow));
                Assert.AreEqual(PlaceDiscovery.Xp, second.Progression.Xp);
                Assert.IsNull(second.Discovery.Visit(hollow.X, hollow.Z));
                Assert.AreEqual(PlaceDiscovery.Xp, second.Progression.Xp);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private sealed class Session : ITradeSession
        {
            public long Generation { get; set; } = 1;
            public string ActiveNpcId { get; set; }
        }

        private sealed class SavedGame
        {
            public readonly Progression Progression = new Progression();
            public readonly PlaceDiscovery Discovery;
            public readonly SaveGame Save;

            public SavedGame(string directory)
            {
                var catalog = ItemCatalog.CreateDefault();
                var inventory = PlayerInventory.CreateStarting(catalog);
                var session = new Session();
                var trade = new TradeService(inventory, new[] { VendorStock.CreateMara(catalog) }, session);
                var quests = new QuestLog(inventory, Progression, session);
                Discovery = new PlaceDiscovery(Progression);
                Save = new SaveGame(inventory, trade, quests, Progression, directory);
            }
        }

        // ---------- Routines ----------

        private static NpcRoutine Square() => new NpcRoutine(new[]
        {
            new RoutineStop(0f, 0f, 2f), new RoutineStop(4f, 0f, 1f)
        }, 2f);

        [Test]
        public void ARoutineIdlesThenWalksToTheNextStopAtItsSpeed()
        {
            var routine = Square();

            routine.Tick(1f, held: false);
            Assert.AreEqual(0f, routine.X, 1e-4f, "still idling at the first stop");
            routine.Tick(1f, held: false); // The pause ends.
            routine.Tick(1f, held: false);

            Assert.AreEqual(2f, routine.X, 1e-4f, "one second at 2 m/s");
            Assert.IsTrue(routine.Moving);
            Assert.AreEqual(1f, routine.HeadingX, 1e-4f);
        }

        [Test]
        public void ARoutineLoopsBackToItsFirstStop()
        {
            var routine = Square();
            for (int i = 0; i < 60; i++)
            {
                routine.Tick(0.1f, held: false);
            }

            // 2 s idle, 2 s walk, 1 s idle, then on the way back: x has to be falling again.
            Assert.AreEqual(0, routine.StopIndex);
            Assert.Less(routine.X, 4f);
            Assert.Less(routine.HeadingX, 0f);
        }

        [Test]
        public void AHeldRoutineDoesNotMoveAndResumesTheSameLeg()
        {
            var routine = Square();
            routine.Tick(2f, held: false);
            routine.Tick(0.5f, held: false);
            float x = routine.X;

            for (int i = 0; i < 20; i++)
            {
                routine.Tick(0.5f, held: true);
                Assert.IsFalse(routine.Moving);
            }

            Assert.AreEqual(x, routine.X, 1e-5f, "a conversation holds the NPC where it stands");
            routine.Tick(0.5f, held: false);
            Assert.Greater(routine.X, x);
            Assert.AreEqual(1, routine.StopIndex);
        }

        [Test]
        public void ASingleStopNeverMoves()
        {
            var routine = new NpcRoutine(new[] { new RoutineStop(3f, 4f, 0f) }, 1f);
            routine.Tick(5f, held: false);
            Assert.AreEqual(3f, routine.X);
            Assert.AreEqual(4f, routine.Z);
            Assert.IsFalse(routine.Moving);
        }

        // ---------- The six region NPCs ----------

        private static readonly (string id, string name, string role, string place)[] ContentList =
        {
            ("npc_sella", "Sella", "Innkeeper", ValePlaces.BriarGlen),
            ("npc_toma", "Toma", "Smith", ValePlaces.Reedbank),
            ("npc_fen", "Fen", "Farm worker", ValePlaces.Reedbank),
            ("npc_brann", "Brann", "Gate guard", ValePlaces.Ashwatch),
            ("npc_elra", "Elra", "Castle steward", ValePlaces.Ashwatch),
            ("npc_kest", "Kest", "Camp leader", ValePlaces.BrokenWatch)
        };

        [Test]
        public void TheRegionNpcsAreTheContentListsSix()
        {
            CollectionAssert.AreEqual(ContentList.Select(c => c.id), RegionNpcs.All.Select(n => n.Id));
            foreach (var (id, name, role, place) in ContentList)
            {
                RegionNpc npc = RegionNpcs.Get(id);
                Assert.AreEqual(name, npc.Name, id);
                Assert.AreEqual(role, npc.Role, id);
                Assert.AreEqual(place, npc.PlaceId, id);
            }
        }

        [Test]
        public void EveryRoutineStaysInsideItsPlace()
        {
            foreach (var npc in RegionNpcs.All)
            {
                Assert.That(npc.Stops.Count, Is.InRange(2, 3), npc.Id);
                foreach (var stop in npc.Stops)
                {
                    Assert.AreEqual(npc.PlaceId, ValePlaces.At(stop.X, stop.Z)?.Id, $"{npc.Id} at ({stop.X}, {stop.Z})");
                }
            }
        }

        [Test]
        public void NoTwoSpeakersCanCompeteForTheSameFocus()
        {
            // Mara and Iven stand still in the Glen. Every region stop keeps more than the 6 m talk range
            // from every other speaker's stops, so the 3 m focus is never ambiguous and leaving one
            // conversation's range is never needed to reach the next speaker's focus.
            var stands = new List<(string id, float x, float z)> { ("npc_mara", -5f, 4.5f), ("npc_iven", 3f, 3f) };
            foreach (var npc in RegionNpcs.All)
            {
                stands.AddRange(npc.Stops.Select(s => (npc.Id, s.X, s.Z)));
            }

            foreach (var a in stands)
            {
                foreach (var b in stands.Where(b => b.id != a.id))
                {
                    float dx = a.x - b.x, dz = a.z - b.z;
                    Assert.Greater(Math.Sqrt(dx * dx + dz * dz), 6.0, $"{a.id} and {b.id}");
                }
            }
        }

        [Test]
        public void EveryRegionNpcHasAnAuthoredPersonaWithKnowledge()
        {
            foreach (var npc in RegionNpcs.All)
            {
                NpcPersona persona = ValeNpcPersonas.All.SingleOrDefault(p => p.NpcId == npc.Id);
                Assert.IsNotNull(persona, npc.Id);
                Assert.AreEqual(npc.Name, persona.Name);
                Assert.That(persona.Knowledge.Count, Is.InRange(3, 6), npc.Id);
                StringAssert.Contains(ValeNpcPersonas.SpeakingRules, persona.Personality, npc.Id);
            }
        }

        [Test]
        public async Task EveryRegionNpcTalksThroughTheAgentDriverWithOnlyItsOwnKnowledge()
        {
            var provider = new FakeLlmProvider { Reply = FakeLlmProvider.Json("none", "no_target", "Hm.") };
            var driver = new AgenerelaNpcDecisionDriver(provider, "fake");
            long id = 0;
            foreach (var npc in RegionNpcs.All)
            {
                var descriptor = new NpcDescriptor(npc.Id, npc.Name, npc.Role);
                var decide = driver.DecideAsync(new NpcTurn(++id, descriptor, "Hello.", Array.Empty<ChatLine>()), CancellationToken.None);
                await Run.NotCancelled(decide);

                Assert.AreEqual(NpcReplyFailure.None, decide.Result.Failure, npc.Id);
                Assert.AreEqual(ActionRegistry.None, decide.Result.Decision.ActionId, npc.Id);
                var request = provider.LastRequest;
                StringAssert.Contains(npc.Name, request.SystemPrompt, npc.Id);
                NpcPersona own = driver.PersonaFor(descriptor);
                StringAssert.Contains(own.Knowledge[0], request.Observations, npc.Id);
                foreach (var other in RegionNpcPersonas.All.Where(p => p.NpcId != npc.Id))
                {
                    StringAssert.DoesNotContain(other.Knowledge[1], request.Observations, $"{npc.Id} must not know {other.NpcId}'s facts");
                }
            }
        }

        [Test]
        public async Task EveryRegionNpcHasScriptedLinesOfItsOwn()
        {
            var driver = new ScriptedNpcDecisionDriver(seed: 3) { NormalDelay = TimeSpan.Zero };
            long id = 0;
            foreach (var npc in RegionNpcs.All)
            {
                var descriptor = new NpcDescriptor(npc.Id, npc.Name, npc.Role);
                var decide = driver.DecideAsync(new NpcTurn(++id, descriptor, "hello", Array.Empty<ChatLine>()), CancellationToken.None);
                await Run.NotCancelled(decide);

                AgentDecision decision = decide.Result.Decision;
                Assert.AreEqual(ActionRegistry.None, decision.ActionId, npc.Id);
                Assert.AreNotEqual("I don't think we've met.", decision.Statement, $"{npc.Id} has no script");
            }
        }

        // ---------- Travel ----------

        [Test]
        public void NeighboursAreFortyFiveToNinetySecondsApartAtWalkingPace()
        {
            // Design §2's target, centre to centre at the player's 3 m/s walk (ThirdPersonPlayer.walkSpeed).
            const float walk = 3f;
            foreach (var (from, to) in ValePlaces.Paths)
            {
                ValePlace a = ValePlaces.Get(from), b = ValePlaces.Get(to);
                double seconds = Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z)) / walk;
                Assert.That(seconds, Is.InRange(45.0, 90.0), $"{from} to {to}");
            }
        }
    }
}
