using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Agenerela.Tests
{
    public class TargetSourceTests
    {
        // Far from anything a scene open in the editor might hold, so its Targetables never
        // fall within a test's radius.
        private static readonly Vector3 Here = new Vector3(10000f, 0f, 10000f);

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void DestroyCreatedObjects()
        {
            foreach (var obj in created)
            {
                Object.DestroyImmediate(obj);
            }

            created.Clear();
        }

        private static AgentContext Context()
        {
            return new AgentContext(new AgentIdentity { Name = "Guard" }, "", null, null, new TargetRegistry(), new ActionRegistry());
        }

        // ---- ExplicitTargetSource: no scene -------------------------------------------------

        [Test]
        public void ExplicitSourceAddsItsSetInOrder()
        {
            var north = new object();
            var south = new object();
            var source = new ExplicitTargetSource().Add("north_border", north).Add("south_border", south);
            var into = new TargetRegistry();

            source.Collect(Context(), into);

            Assert.That(into.Ids, Is.EqualTo(new[] { "north_border", "south_border" }));
            Assert.That(into.TryGet("south_border", out var found), Is.True);
            Assert.That(found, Is.SameAs(south));
        }

        [Test]
        public void EmptyExplicitSourceLeavesTheRegistryEmptyNotNull()
        {
            var into = new TargetRegistry();

            new ExplicitTargetSource().Collect(Context(), into);

            Assert.That(into.Count, Is.EqualTo(0));
            Assert.That(into.Ids, Is.Empty);
        }

        [Test]
        public void ExplicitSourceRejectsABadEntryWhenItIsAdded()
        {
            var source = new ExplicitTargetSource().Add("tower", new object());

            Assert.Throws<ArgumentException>(() => source.Add("tower", new object()));
            Assert.Throws<ArgumentException>(() => source.Add(TargetRegistry.NoTarget, new object()));
            Assert.Throws<ArgumentNullException>(() => source.Add("bridge", null));
        }

        [Test]
        public void ExplicitSourceRemoveTakesATargetOutOfTheSet()
        {
            var source = new ExplicitTargetSource().Add("tower", new object()).Add("bridge", new object());

            Assert.That(source.Remove("tower"), Is.True);
            Assert.That(source.Remove("tower"), Is.False);
            Assert.That(source.Ids, Is.EqualTo(new[] { "bridge" }));
        }

        [Test]
        public void AnIdAnEarlierSourceAddedIsAClearError()
        {
            var into = new TargetRegistry();
            new ExplicitTargetSource().Add("tower", new object()).Collect(Context(), into);

            var second = new ExplicitTargetSource().Add("tower", new object());
            var error = Assert.Throws<InvalidOperationException>(() => second.Collect(Context(), into));

            Assert.That(error.Message, Does.Contain("'tower'"));
            Assert.That(into.Count, Is.EqualTo(1));
        }

        [Test]
        public void CollectRejectsANullRegistry()
        {
            Assert.Throws<ArgumentNullException>(() => new ExplicitTargetSource().Collect(Context(), null));
        }

        // ---- Targetable id rules ------------------------------------------------------------

        [TestCase("tower", true)]
        [TestCase("training_dummy", true)]
        [TestCase("", false)]
        [TestCase(null, false)]
        [TestCase("Training Dummy (1)", false)]
        [TestCase("Tower", false)]
        [TestCase("no_target", false)]
        public void TargetableValidatesItsId(string id, bool valid)
        {
            Assert.That(Targetable.ValidateId(id, out var problem), Is.EqualTo(valid));
            Assert.That(problem, valid ? Is.Null : Is.Not.Null);
        }

        [Test]
        public void OnValidateWarnsAboutAMissingId()
        {
            var targetable = MakeTargetable(null, Vector3.zero);

            LogAssert.Expect(LogType.Warning, new Regex("can never be named"));
            InvokeOnValidate(targetable);
        }

        [Test]
        public void OnValidateWarnsAboutAMalformedId()
        {
            var targetable = MakeTargetable("Training Dummy", Vector3.zero);

            // The problem itself, not just the id: the GameObject is also named "Training Dummy",
            // so a looser pattern would match some other warning about it.
            LogAssert.Expect(LogType.Warning, new Regex("Id 'Training Dummy' cannot have uppercase"));
            InvokeOnValidate(targetable);
        }

        // ---- ProximityTargetSource ----------------------------------------------------------

        [Test]
        public void ProximityFindsTargetablesWithinRadiusNearestFirst()
        {
            MakeTargetable("tower", new Vector3(10f, 0f, 0f));
            MakeTargetable("bridge", new Vector3(3f, 0f, 0f));
            MakeTargetable("far_hill", new Vector3(50f, 0f, 0f));
            var source = Proximity(radius: 18f);
            var into = new TargetRegistry();

            source.Collect(Context(), into);

            Assert.That(into.Ids, Is.EqualTo(new[] { "bridge", "tower" }));
            Assert.That(source.Dropped, Is.Empty, "Out of radius is not dropped by the cap.");
        }

        [Test]
        public void ProximityRegistersEachTargetablesTransform()
        {
            var tower = MakeTargetable("tower", new Vector3(5f, 0f, 0f));
            var into = new TargetRegistry();

            Proximity().Collect(Context(), into);

            Assert.That(into.TryGet("tower", out var found), Is.True);
            Assert.That(found, Is.SameAs(tower.transform));
        }

        [Test]
        public void ProximityWithNothingInRangeLeavesTheRegistryEmpty()
        {
            MakeTargetable("far_hill", new Vector3(50f, 0f, 0f));
            var into = new TargetRegistry();

            Proximity(radius: 5f).Collect(Context(), into);

            Assert.That(into.Count, Is.EqualTo(0));
        }

        [Test]
        public void ProximityCapKeepsTheNearestAndRecordsTheRestNearestFirst()
        {
            MakeTargetable("d", new Vector3(4f, 0f, 0f));
            MakeTargetable("a", new Vector3(1f, 0f, 0f));
            MakeTargetable("c", new Vector3(3f, 0f, 0f));
            MakeTargetable("b", new Vector3(2f, 0f, 0f));
            var source = Proximity(cap: 2);
            var into = new TargetRegistry();

            source.Collect(Context(), into);

            Assert.That(into.Ids, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(source.Dropped, Is.EqualTo(new[] { "c", "d" }));
        }

        [Test]
        public void ProximityDroppedIsResetOnEachCollect()
        {
            var far = MakeTargetable("far", new Vector3(2f, 0f, 0f));
            MakeTargetable("near", new Vector3(1f, 0f, 0f));
            var source = Proximity(cap: 1);
            source.Collect(Context(), new TargetRegistry());

            far.IsCurrentlyTargetable = false;
            source.Collect(Context(), new TargetRegistry());

            Assert.That(source.Dropped, Is.Empty);
        }

        [Test]
        public void ProximityFiltersByCategory()
        {
            MakeTargetable("tower", new Vector3(1f, 0f, 0f), "landmark");
            MakeTargetable("bucket", new Vector3(2f, 0f, 0f), "prop");
            var source = Proximity();
            source.Categories = new[] { "landmark" };
            var into = new TargetRegistry();

            source.Collect(Context(), into);

            Assert.That(into.Ids, Is.EqualTo(new[] { "tower" }));
        }

        [Test]
        public void ProximityFiltersByLayer()
        {
            const int ignoreRaycast = 2;
            MakeTargetable("tower", new Vector3(1f, 0f, 0f));
            MakeTargetable("ghost", new Vector3(2f, 0f, 0f)).gameObject.layer = ignoreRaycast;
            var source = Proximity();
            source.Layers = ~(1 << ignoreRaycast);
            var into = new TargetRegistry();

            source.Collect(Context(), into);

            Assert.That(into.Ids, Is.EqualTo(new[] { "tower" }));
        }

        [Test]
        public void ProximitySkipsWhatIsNotCurrentlyTargetable()
        {
            MakeTargetable("tower", new Vector3(1f, 0f, 0f));
            MakeTargetable("sealed_gate", new Vector3(2f, 0f, 0f)).IsCurrentlyTargetable = false;
            MakeTargetable("disabled", new Vector3(3f, 0f, 0f)).enabled = false;
            MakeTargetable("inactive", new Vector3(4f, 0f, 0f)).gameObject.SetActive(false);
            var into = new TargetRegistry();

            Proximity().Collect(Context(), into);

            Assert.That(into.Ids, Is.EqualTo(new[] { "tower" }));
        }

        [Test]
        public void ProximityNeverOffersTheAgentItself()
        {
            var source = Proximity();
            var self = source.Origin.gameObject.AddComponent<Targetable>();
            self.Id = "guard";
            MakeTargetable("tower", new Vector3(1f, 0f, 0f));
            var into = new TargetRegistry();

            source.Collect(Context(), into);

            Assert.That(into.Ids, Is.EqualTo(new[] { "tower" }));
        }

        [Test]
        public void ProximityDuplicateIdIsAClearError()
        {
            MakeTargetable("dummy", new Vector3(1f, 0f, 0f)).name = "Training Dummy";
            MakeTargetable("dummy", new Vector3(2f, 0f, 0f)).name = "Training Dummy (1)";

            var error = Assert.Throws<InvalidOperationException>(() => Proximity().Collect(Context(), new TargetRegistry()));

            Assert.That(error.Message, Does.Contain("'dummy'"));
            Assert.That(error.Message, Does.Contain("Training Dummy (1)"));
        }

        [Test]
        public void ProximityIdAnEarlierSourceAddedIsAClearError()
        {
            MakeTargetable("tower", new Vector3(1f, 0f, 0f));
            var into = new TargetRegistry();
            new ExplicitTargetSource().Add("tower", new object()).Collect(Context(), into);

            var error = Assert.Throws<InvalidOperationException>(() => Proximity().Collect(Context(), into));

            Assert.That(error.Message, Does.Contain("earlier source"));
        }

        [Test]
        public void ProximityMissingIdIsAClearError()
        {
            MakeTargetable("", new Vector3(1f, 0f, 0f)).name = "Nameless";

            var error = Assert.Throws<InvalidOperationException>(() => Proximity().Collect(Context(), new TargetRegistry()));

            Assert.That(error.Message, Does.Contain("Nameless"));
        }

        [Test]
        public void ProximityMalformedIdBeyondTheCapIsStillAClearError()
        {
            MakeTargetable("tower", new Vector3(1f, 0f, 0f));
            MakeTargetable("", new Vector3(2f, 0f, 0f)).name = "Nameless";

            var error = Assert.Throws<InvalidOperationException>(() => Proximity(cap: 1).Collect(Context(), new TargetRegistry()));

            Assert.That(error.Message, Does.Contain("Nameless"));
        }

        [Test]
        public void ProximityDuplicateIdBeyondTheCapIsStillAClearError()
        {
            MakeTargetable("dummy", new Vector3(1f, 0f, 0f));
            MakeTargetable("dummy", new Vector3(2f, 0f, 0f));

            var error = Assert.Throws<InvalidOperationException>(() => Proximity(cap: 1).Collect(Context(), new TargetRegistry()));

            Assert.That(error.Message, Does.Contain("'dummy'"));
        }

        [Test]
        public void LineOfSightOffersATargetInPlainView()
        {
            AddBox(MakeTargetable("tower", new Vector3(10f, 0f, 0f)).gameObject);

            Assert.That(CollectWithLineOfSight(), Is.EqualTo(new[] { "tower" }));
        }

        [Test]
        public void LineOfSightOffersATargetWithNoCollider()
        {
            MakeTargetable("tower", new Vector3(10f, 0f, 0f));

            Assert.That(CollectWithLineOfSight(), Is.EqualTo(new[] { "tower" }));
        }

        [Test]
        public void LineOfSightHidesATargetBehindAWall()
        {
            AddBox(MakeTargetable("tower", new Vector3(10f, 0f, 0f)).gameObject);
            MakeWall(new Vector3(5f, 0f, 0f));

            Assert.That(CollectWithLineOfSight(), Is.Empty);
        }

        [Test]
        public void LineOfSightIgnoresATriggerInTheWay()
        {
            AddBox(MakeTargetable("tower", new Vector3(10f, 0f, 0f)).gameObject);
            MakeWall(new Vector3(5f, 0f, 0f)).isTrigger = true;

            Assert.That(CollectWithLineOfSight(), Is.EqualTo(new[] { "tower" }));
        }

        [Test]
        public void LineOfSightSeesATargetUnderARigidbodyParent()
        {
            var cart = new GameObject("Cart");
            created.Add(cart);
            cart.transform.position = Here + new Vector3(10f, 0f, 0f);
            cart.AddComponent<Rigidbody>().isKinematic = true;
            var wheel = MakeTargetable("wheel", new Vector3(10f, 0f, 0f));
            wheel.transform.SetParent(cart.transform, true);
            AddBox(wheel.gameObject);

            Assert.That(CollectWithLineOfSight(), Is.EqualTo(new[] { "wheel" }));
        }

        [Test]
        public void LineOfSightIsNotBlockedByTheAgentsOwnColliders()
        {
            AddBox(MakeTargetable("tower", new Vector3(10f, 0f, 0f)).gameObject);
            var source = Proximity();
            source.RequireLineOfSight = true;
            var shield = new GameObject("Shield");
            created.Add(shield);
            shield.transform.SetParent(source.Origin, false);
            shield.transform.localPosition = new Vector3(1f, 0f, 0f);
            AddBox(shield);
            var into = new TargetRegistry();

            Physics.SyncTransforms();
            source.Collect(Context(), into);

            Assert.That(into.Ids, Is.EqualTo(new[] { "tower" }));
        }

        [Test]
        public void ProximityWithoutAnOriginIsAClearError()
        {
            var source = new ProximityTargetSource();

            Assert.Throws<InvalidOperationException>(() => source.Collect(Context(), new TargetRegistry()));
        }

        // ---- helpers ------------------------------------------------------------------------

        private ProximityTargetSource Proximity(float radius = 18f, int cap = 8)
        {
            var agent = new GameObject("Agent");
            created.Add(agent);
            agent.transform.position = Here;
            return new ProximityTargetSource { Origin = agent.transform, Radius = radius, Cap = cap };
        }

        private Targetable MakeTargetable(string id, Vector3 offset, string category = null)
        {
            var obj = new GameObject(id ?? "Targetable");
            created.Add(obj);
            obj.transform.position = Here + offset;
            // AddComponent runs OnValidate before Id is set, which would warn that the empty id
            // can never be named. That warning is about the helper, not the test, so keep it out
            // of the console.
            Debug.unityLogger.logEnabled = false;
            Targetable targetable;
            try
            {
                targetable = obj.AddComponent<Targetable>();
            }
            finally
            {
                Debug.unityLogger.logEnabled = true;
            }

            targetable.Id = id;
            targetable.Category = category;
            return targetable;
        }

        private string[] CollectWithLineOfSight()
        {
            var source = Proximity();
            source.RequireLineOfSight = true;
            var into = new TargetRegistry();

            // Colliders created this frame are not in the physics scene until transforms sync.
            Physics.SyncTransforms();
            source.Collect(Context(), into);

            return new List<string>(into.Ids).ToArray();
        }

        private BoxCollider MakeWall(Vector3 offset)
        {
            var wall = new GameObject("Wall");
            created.Add(wall);
            wall.transform.position = Here + offset;
            var box = wall.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 5f, 5f);
            return box;
        }

        private static BoxCollider AddBox(GameObject obj)
        {
            return obj.AddComponent<BoxCollider>();
        }

        private static void InvokeOnValidate(Targetable targetable)
        {
            typeof(Targetable)
                .GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(targetable, null);
        }
    }
}
