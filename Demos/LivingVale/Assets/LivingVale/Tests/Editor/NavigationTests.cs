using System.Collections.Generic;
using LivingVale.Game;
using LivingVale.Interaction;
using LivingVale.UI;
using LivingVale.World;
using NUnit.Framework;

namespace LivingVale.Tests
{
    /// <summary>
    /// G8a: the quest guide behind the compass and map marker, the compass and map sums, the name
    /// tag rules and the notice area's queue.
    /// </summary>
    public sealed class NavigationTests
    {
        private static readonly string[] AllHerbs = { QuestRoutes.PickupHerb1, QuestRoutes.PickupHerb2, QuestRoutes.PickupHerb3 };

        private static QuestGuideInput State(MainQuestState main) => new QuestGuideInput
        {
            Main = main,
            HerbsLeft = AllHerbs
        };

        private static void AssertGoal(QuestGoal goal, QuestGoalKind kind, string target, string place)
        {
            Assert.IsNotNull(goal, "expected a goal");
            Assert.AreEqual(kind, goal.Kind, goal.ToString());
            Assert.AreEqual(target, goal.TargetId, goal.ToString());
            Assert.AreEqual(place, goal.PlaceId, goal.ToString());
            Assert.IsFalse(string.IsNullOrEmpty(goal.Label));
            Assert.IsFalse(string.IsNullOrEmpty(goal.Instruction));
        }

        // ---------- QuestGuide: the main quest ----------

        [Test]
        public void BeforeAndAtTheOfferTheGoalIsIven()
        {
            AssertGoal(QuestGuide.For(State(MainQuestState.Undiscovered)), QuestGoalKind.Npc, QuestLog.IvenId, ValePlaces.BriarGlen);
            QuestGoal offered = QuestGuide.For(State(MainQuestState.Offered));
            AssertGoal(offered, QuestGoalKind.Npc, QuestLog.IvenId, ValePlaces.BriarGlen);
            StringAssert.Contains("Accept", offered.Instruction);
        }

        [Test]
        public void TheActiveQuestWalksWreckThenFenThenKestThenTheParcel()
        {
            var state = State(MainQuestState.Active);
            AssertGoal(QuestGuide.For(state), QuestGoalKind.Pickup, QuestRoutes.PickupWreck, ValePlaces.OldRoad);

            state.WreckSeen = true;
            AssertGoal(QuestGuide.For(state), QuestGoalKind.Npc, QuestRoutes.FenId, ValePlaces.Reedbank);

            state.FenTestimony = true;
            AssertGoal(QuestGuide.For(state), QuestGoalKind.Npc, QuestRoutes.KestId, ValePlaces.BrokenWatch);

            state.CampCleared = true;
            AssertGoal(QuestGuide.For(state), QuestGoalKind.Pickup, QuestRoutes.PickupParcel, ValePlaces.BrokenWatch);
        }

        [Test]
        public void KnowingWhereTheCampIsPointsAtKestEvenWithoutTheWreck()
        {
            var state = State(MainQuestState.Active);
            state.CampLocated = true;
            AssertGoal(QuestGuide.For(state), QuestGoalKind.Npc, QuestRoutes.KestId, ValePlaces.BrokenWatch);
        }

        [Test]
        public void ARecoveredParcelGoesToIvenUnlessADivisionIsHalfway()
        {
            var state = State(MainQuestState.ParcelRecovered);
            AssertGoal(QuestGuide.For(state), QuestGoalKind.Npc, QuestLog.IvenId, ValePlaces.BriarGlen);
            state.HasPermit = true;
            StringAssert.Contains("Elra", QuestGuide.For(state).Instruction, "with the permit both deliveries are named");

            // Iven agreed: Elra must agree too, which needs Brann's permit first.
            state = State(MainQuestState.ParcelRecovered);
            state.AgreementHealer = true;
            AssertGoal(QuestGuide.For(state), QuestGoalKind.Npc, QuestRoutes.BrannId, ValePlaces.Ashwatch);
            state.HasPermit = true;
            AssertGoal(QuestGuide.For(state), QuestGoalKind.Npc, QuestRoutes.ElraId, ValePlaces.Ashwatch);

            // Elra agreed first: back to Iven for his agreement, then Iven to divide.
            state = State(MainQuestState.ParcelRecovered);
            state.HasPermit = true;
            state.AgreementGarrison = true;
            AssertGoal(QuestGuide.For(state), QuestGoalKind.Npc, QuestLog.IvenId, ValePlaces.BriarGlen);
            state.AgreementHealer = true;
            QuestGoal divide = QuestGuide.For(state);
            AssertGoal(divide, QuestGoalKind.Npc, QuestLog.IvenId, ValePlaces.BriarGlen);
            StringAssert.Contains("Divide", divide.Instruction);
        }

        // ---------- QuestGuide: side quests and no goal ----------

        [Test]
        public void AfterTheMainQuestTheHerbsThenTheBadgeAreTheGoal()
        {
            var state = State(MainQuestState.Resolved);
            Assert.IsNull(QuestGuide.For(state), "nothing active: no goal");

            state.Herbs = SideQuestState.Active;
            state.HerbsCarried = 1;
            state.HerbsLeft = new[] { QuestRoutes.PickupHerb2, QuestRoutes.PickupHerb3 };
            QuestGoal herbs = QuestGuide.For(state);
            AssertGoal(herbs, QuestGoalKind.Pickup, QuestRoutes.PickupHerb2, ValePlaces.HerbHollow);
            StringAssert.Contains("1/3", herbs.Instruction);

            // Every spot picked, one bundle sold: Mara sells them.
            state.HerbsCarried = 2;
            state.HerbsLeft = new string[0];
            AssertGoal(QuestGuide.For(state), QuestGoalKind.Npc, QuestGuide.MaraId, ValePlaces.BriarGlen);

            state.HerbsCarried = QuestLog.HerbsNeeded;
            AssertGoal(QuestGuide.For(state), QuestGoalKind.Npc, QuestLog.IvenId, ValePlaces.BriarGlen);

            state.Herbs = SideQuestState.Completed;
            state.Badge = SideQuestState.Active;
            AssertGoal(QuestGuide.For(state), QuestGoalKind.Pickup, QuestRoutes.PickupBadge, ValePlaces.OldRoad);
            state.BadgeFound = true;
            AssertGoal(QuestGuide.For(state), QuestGoalKind.Npc, QuestRoutes.BrannId, ValePlaces.Ashwatch);

            state.Badge = SideQuestState.Completed;
            Assert.IsNull(QuestGuide.For(state), "everything done: no goal");
        }

        [Test]
        public void TheMainQuestComesBeforeAnActiveSideQuest()
        {
            var state = State(MainQuestState.Active);
            state.Herbs = SideQuestState.Active;
            AssertGoal(QuestGuide.For(state), QuestGoalKind.Pickup, QuestRoutes.PickupWreck, ValePlaces.OldRoad);
        }

        [Test]
        public void AnUnknownStateGivesNoGoal()
        {
            Assert.IsNull(QuestGuide.For(State((MainQuestState)99)));
            Assert.IsNull(QuestGuide.For((QuestRoutes)null));
        }

        [Test]
        public void TheGuideReadsTheLiveQuestState()
        {
            var catalog = ItemCatalog.CreateDefault();
            var inventory = PlayerInventory.CreateStarting(catalog);
            using var link = new ConversationTradeLink();
            var quests = new QuestLog(inventory, new Progression(), link);
            var routes = new QuestRoutes(quests, link);

            AssertGoal(QuestGuide.For(routes), QuestGoalKind.Npc, QuestLog.IvenId, ValePlaces.BriarGlen);

            Assert.AreEqual(QuestRejection.None, quests.TryStartSideQuest(QuestLog.HerbsQuestId));
            Assert.IsTrue(routes.TryPickUp(QuestRoutes.PickupHerb1).Applied);
            Assert.IsTrue(routes.TryPickUp(QuestRoutes.PickupWreck).Applied);
            QuestGuideInput input = QuestGuide.InputFrom(routes);
            Assert.AreEqual(1, input.HerbsCarried);
            CollectionAssert.AreEqual(new[] { QuestRoutes.PickupHerb2, QuestRoutes.PickupHerb3 }, input.HerbsLeft);
            Assert.IsTrue(input.WreckSeen);
            Assert.AreEqual(SideQuestState.Active, input.Herbs);
        }

        // ---------- Compass and map sums ----------

        [TestCase(0f, 10f, 0f)]
        [TestCase(10f, 0f, 90f)]
        [TestCase(0f, -10f, 180f)]
        [TestCase(-10f, 0f, 270f)]
        [TestCase(10f, 10f, 45f)]
        public void BearingsAreClockwiseFromNorth(float x, float z, float expected)
        {
            Assert.AreEqual(expected, NavigationMath.Bearing(0f, 0f, x, z), 0.001f);
        }

        [Test]
        public void RelativeBearingsWrapToTheShortWayRound()
        {
            Assert.AreEqual(20f, NavigationMath.Relative(10f, 350f), 0.001f);
            Assert.AreEqual(-20f, NavigationMath.Relative(350f, 10f), 0.001f);
            Assert.AreEqual(-180f, NavigationMath.Relative(180f, 0f), 0.001f);
        }

        [Test]
        public void TheStripPlacesBearingsAcrossItsWidth()
        {
            // Facing east, 180° shown on a 340-wide strip: north-east is a quarter to the left.
            Assert.IsTrue(NavigationMath.StripOffset(45f, 90f, 180f, 340f, out float x));
            Assert.AreEqual(-85f, x, 0.001f);
            Assert.IsTrue(NavigationMath.StripOffset(90f, 90f, 180f, 340f, out x));
            Assert.AreEqual(0f, x, 0.001f);
            Assert.IsFalse(NavigationMath.StripOffset(270f, 90f, 180f, 340f, out x), "behind is off the strip");
        }

        [Test]
        public void TheMapPutsNorthUpAndEastRight()
        {
            Assert.AreEqual((240f, 240f), NavigationMath.ToMap(0f, 0f, 480f), "the Glen is the centre");
            Assert.AreEqual((0f, 0f), NavigationMath.ToMap(-300f, -300f, 480f), "south-west corner");
            Assert.AreEqual((480f, 480f), NavigationMath.ToMap(300f, 300f, 480f), "north-east corner");

            ValePlace ashwatch = ValePlaces.Get(ValePlaces.Ashwatch);
            ValePlace watch = ValePlaces.Get(ValePlaces.BrokenWatch);
            var (au, av) = NavigationMath.ToMap(ashwatch.X, ashwatch.Z, 480f);
            var (wu, wv) = NavigationMath.ToMap(watch.X, watch.Z, 480f);
            Assert.Greater(au, wu, "Ashwatch is east of Broken Watch");
            Assert.Greater(av, wv, "Ashwatch is north of Broken Watch");
            Assert.AreEqual((480f, 0f), NavigationMath.ToMap(900f, -900f, 480f), "outside the valley clamps to the edge");
        }

        [Test]
        public void DistancesReadInMetresThenKilometres()
        {
            Assert.AreEqual("12 m", NavigationMath.Distance(12.4f));
            Assert.AreEqual("0 m", NavigationMath.Distance(-1f));
            Assert.AreEqual("1.2 km", NavigationMath.Distance(1240f));
        }

        // ---------- Map ----------

        [Test]
        public void TheMapPanelIsOpaqueAndItsBackdropDimsInLinearSpace()
        {
            // Linear colour space blends alpha in linear light; anything short of 1 shows the scene
            // through the map (G8b), and a backdrop under about 0.8 barely dims it.
            Assert.AreEqual(UnityEngine.QualitySettings.activeColorSpace, UnityEngine.ColorSpace.Linear,
                "these alphas were chosen for linear space");
            Assert.AreEqual(1f, MapOverlay.PanelColour.a);
            Assert.GreaterOrEqual(MapOverlay.BackdropColour.a, 0.8f);
        }

        // ---------- Name tags ----------

        [Test]
        public void NameTagsShowNearFadeOutAndHideBehindWallsAndInChat()
        {
            Assert.AreEqual(1f, NameTagRules.Alpha(4f, true, false));
            Assert.AreEqual(1f, NameTagRules.Alpha(NameTagRules.FadeStart, true, false));
            Assert.AreEqual(0.5f, NameTagRules.Alpha(12.5f, true, false), 0.001f);
            Assert.AreEqual(0f, NameTagRules.Alpha(NameTagRules.Range, true, false), 0.001f);
            Assert.AreEqual(0f, NameTagRules.Alpha(16f, true, false), "out of range");
            Assert.AreEqual(0f, NameTagRules.Alpha(4f, false, false), "a wall in the way");
            Assert.AreEqual(0f, NameTagRules.Alpha(4f, true, true), "chatting with this NPC");
            Assert.AreEqual(0f, NameTagRules.Alpha(float.NaN, true, false));
        }

        // ---------- Notice queue ----------

        private static List<string> Texts(NoticeQueue queue)
        {
            var texts = new List<string>();
            foreach (var notice in queue.Visible)
            {
                texts.Add(notice.Text);
            }

            return texts;
        }

        [Test]
        public void NoticesStackOldestFirstAndExtrasWaitTheirTurn()
        {
            var queue = new NoticeQueue(maxVisible: 3);
            queue.Push("one", 4f, 0f);
            queue.Push("two", 4f, 1f);
            queue.Push("three", 4f, 1f);
            queue.Push("four", 4f, 1f);
            CollectionAssert.AreEqual(new[] { "one", "two", "three" }, Texts(queue));
            Assert.AreEqual(1, queue.WaitingCount);

            queue.Tick(4.5f);
            CollectionAssert.AreEqual(new[] { "two", "three", "four" }, Texts(queue), "'one' expired; 'four' moves up");

            // A waiting notice's time starts when it shows (4.5), not when it was pushed (1).
            queue.Tick(5.5f);
            CollectionAssert.AreEqual(new[] { "four" }, Texts(queue));
            queue.Tick(8.6f);
            CollectionAssert.IsEmpty(Texts(queue));
        }

        [Test]
        public void AKeyedNoticeUpdatesInPlaceAndStaysUntilCleared()
        {
            var queue = new NoticeQueue();
            queue.Push("Discovered: Reedbank", 4f, 0f);
            queue.Push("loading gemma…", 0f, 0f, key: "model");
            queue.Push("Picked a herb", 4f, 0f);
            queue.Push("gemma did not load", 0f, 1f, key: "model");
            CollectionAssert.AreEqual(new[] { "Discovered: Reedbank", "gemma did not load", "Picked a herb" }, Texts(queue));

            queue.Tick(100f);
            CollectionAssert.AreEqual(new[] { "gemma did not load" }, Texts(queue), "sticky outlives the timed ones");

            queue.Push("", 0f, 101f, key: "model");
            CollectionAssert.IsEmpty(Texts(queue), "an empty keyed notice clears it");
        }
    }
}
