using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Agenerela.Tests
{
    // ProximityObservationSource against real GameObjects, as TargetSourceTests does for the target
    // source, then an agent that carries one. The filters themselves are TargetableQuery's, shared
    // with ProximityTargetSource and covered there in full; these check the source applies them.
    public class ProximityObservationSourceTests
    {
        // Far from anything a scene open in the editor might hold, and from TargetSourceTests' own
        // spot, so their Targetables never fall within a test's radius.
        private static readonly Vector3 Here = new Vector3(-10000f, 0f, 10000f);

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void DestroyCreatedObjects()
        {
            foreach (var obj in created)
            {
                Object.DestroyImmediate(obj);
            }

            created.Clear();
            Fixtures.DestroyCreated();
        }

        private static AgentContext Context()
        {
            return new AgentContext(new AgentIdentity { Name = "Guard" }, "", null, null, new TargetRegistry(), new ActionRegistry());
        }

        [Test]
        public void DescribesTargetablesWithinRadiusNearestFirst()
        {
            MakeTargetable("tower", new Vector3(10f, 0f, 0f), "The watchtower door stands open.");
            MakeTargetable("bridge", new Vector3(3f, 0f, 0f), "A cart blocks the bridge.");
            MakeTargetable("far_hill", new Vector3(50f, 0f, 0f), "The far hill is green.");
            var source = Proximity(radius: 18f);

            var lines = source.Observe(Context());

            Assert.That(lines, Is.EqualTo(new[] { "A cart blocks the bridge.", "The watchtower door stands open." }));
            Assert.That(source.Dropped, Is.Empty, "Out of radius is not dropped by the cap.");
        }

        [Test]
        public void SendsEachDescriptionVerbatimWithNoIdOrDistance()
        {
            const string Odd = "  the tower's door: OPEN (again)...  ";
            MakeTargetable("tower", new Vector3(5f, 0f, 0f), Odd);

            Assert.That(Proximity().Observe(Context()), Is.EqualTo(new[] { Odd }));
        }

        [Test]
        public void TheCapKeepsTheNearestAndReportsTheRestNearestFirst()
        {
            MakeTargetable("d", new Vector3(4f, 0f, 0f), "D is here.");
            MakeTargetable("a", new Vector3(1f, 0f, 0f), "A is here.");
            MakeTargetable("c", new Vector3(3f, 0f, 0f), "C is here.");
            MakeTargetable("b", new Vector3(2f, 0f, 0f), "B is here.");
            IReportsDroppedObservations source = Proximity(cap: 2);

            var lines = ((IObservationSource)source).Observe(Context());

            Assert.That(lines, Is.EqualTo(new[] { "A is here.", "B is here." }));
            Assert.That(source.Dropped, Is.EqualTo(new[] { "C is here.", "D is here." }));
        }

        [Test]
        public void DroppedIsResetOnEachObserve()
        {
            var far = MakeTargetable("far", new Vector3(2f, 0f, 0f), "Far.");
            MakeTargetable("near", new Vector3(1f, 0f, 0f), "Near.");
            var source = Proximity(cap: 1);
            source.Observe(Context());

            far.IsCurrentlyTargetable = false;
            source.Observe(Context());

            Assert.That(source.Dropped, Is.Empty);
        }

        [Test]
        public void AnEmptyDescriptionSaysNothingAndTakesNoPlaceUnderTheCap()
        {
            MakeTargetable("post", new Vector3(1f, 0f, 0f), "");
            MakeTargetable("barrel", new Vector3(2f, 0f, 0f), "   ");
            MakeTargetable("crate", new Vector3(3f, 0f, 0f), null);
            MakeTargetable("tower", new Vector3(4f, 0f, 0f), "The watchtower door stands open.");
            var source = Proximity(cap: 1);

            Assert.That(source.Observe(Context()), Is.EqualTo(new[] { "The watchtower door stands open." }));
            Assert.That(source.Dropped, Is.Empty);
        }

        [Test]
        public void SkipsWhatIsNotCurrentlyTargetable()
        {
            MakeTargetable("tower", new Vector3(1f, 0f, 0f), "The tower stands.");
            MakeTargetable("sealed_gate", new Vector3(2f, 0f, 0f), "The gate is sealed.").IsCurrentlyTargetable = false;
            MakeTargetable("disabled", new Vector3(3f, 0f, 0f), "Disabled.").enabled = false;
            MakeTargetable("inactive", new Vector3(4f, 0f, 0f), "Inactive.").gameObject.SetActive(false);

            Assert.That(Proximity().Observe(Context()), Is.EqualTo(new[] { "The tower stands." }));
        }

        [Test]
        public void NeverDescribesTheAgentItself()
        {
            var source = Proximity();
            var self = source.Origin.gameObject.AddComponent<Targetable>();
            self.Id = "guard";
            self.Description = "A guard stands here.";
            MakeTargetable("tower", new Vector3(1f, 0f, 0f), "The tower stands.");

            Assert.That(source.Observe(Context()), Is.EqualTo(new[] { "The tower stands." }));
        }

        [Test]
        public void FiltersByCategoryAndLayer()
        {
            const int ignoreRaycast = 2;
            MakeTargetable("tower", new Vector3(1f, 0f, 0f), "The tower stands.", "landmark");
            MakeTargetable("bucket", new Vector3(2f, 0f, 0f), "A bucket.", "prop");
            MakeTargetable("ghost", new Vector3(3f, 0f, 0f), "A ghost.", "landmark").gameObject.layer = ignoreRaycast;
            var source = Proximity();
            source.Categories = new[] { "landmark" };
            source.Layers = ~(1 << ignoreRaycast);

            Assert.That(source.Observe(Context()), Is.EqualTo(new[] { "The tower stands." }));
        }

        [Test]
        public void LineOfSightHidesWhatIsBehindAWall()
        {
            MakeTargetable("tower", new Vector3(10f, 0f, 0f), "The tower stands.").gameObject.AddComponent<BoxCollider>();
            MakeTargetable("bridge", new Vector3(-10f, 0f, 0f), "The bridge is clear.").gameObject.AddComponent<BoxCollider>();
            var wall = new GameObject("Wall");
            created.Add(wall);
            wall.transform.position = Here + new Vector3(5f, 0f, 0f);
            wall.AddComponent<BoxCollider>().size = new Vector3(1f, 5f, 5f);
            var source = Proximity();
            source.RequireLineOfSight = true;

            // Colliders created this frame are not in the physics scene until transforms sync.
            Physics.SyncTransforms();

            Assert.That(source.Observe(Context()), Is.EqualTo(new[] { "The bridge is clear." }));
        }

        [Test]
        public void IdsAreNotCheckedForALine()
        {
            MakeTargetable("", new Vector3(1f, 0f, 0f), "Smoke rises from a nameless chimney.");
            MakeTargetable("Training Dummy (1)", new Vector3(2f, 0f, 0f), "A dummy leans on its post.");

            Assert.That(Proximity().Observe(Context()), Is.EqualTo(new[]
            {
                "Smoke rises from a nameless chimney.", "A dummy leans on its post.",
            }), "A line needs no id; the target source rejects a bad one for anything an agent may name.");
        }

        [Test]
        public void ABadSettingIsAClearError()
        {
            Assert.That(Assert.Throws<InvalidOperationException>(() => new ProximityObservationSource().Observe(Context())).Message,
                Does.Contain("ProximityObservationSource has no Origin"));
            Assert.That(Assert.Throws<InvalidOperationException>(() => Proximity(radius: -1f).Observe(Context())).Message,
                Does.Contain("ProximityObservationSource.Radius"));
            Assert.That(Assert.Throws<InvalidOperationException>(() => Proximity(cap: 0).Observe(Context())).Message,
                Does.Contain("ProximityObservationSource.Cap"));
        }

        [Test]
        public void GivenTheSameOptionsItDescribesWhatTheTargetSourceNames()
        {
            MakeTargetable("well", new Vector3(4f, 0f, 0f), "The well is dry.");
            MakeTargetable("bridge", new Vector3(1f, 0f, 0f), "A cart blocks the bridge.");
            MakeTargetable("mill", new Vector3(3f, 0f, 0f), "The mill is quiet.");
            MakeTargetable("tower", new Vector3(2f, 0f, 0f), "The watchtower door stands open.");
            var observer = Proximity(cap: 3);
            var targets = new ProximityTargetSource { Origin = observer.Origin, Radius = observer.Radius, Cap = observer.Cap };
            var into = new TargetRegistry();

            targets.Collect(Context(), into);
            var lines = observer.Observe(Context());

            Assert.That(into.Ids, Is.EqualTo(new[] { "bridge", "tower", "mill" }));
            Assert.That(lines, Is.EqualTo(new[] { "A cart blocks the bridge.", "The watchtower door stands open.", "The mill is quiet." }),
                "One query: each line comes from the object whose id the agent may answer with.");
        }

        // ---- On an agent --------------------------------------------------------------------

        [Test]
        public async Task TheGuardByTheTowerReadsThatItsDoorStandsOpen()
        {
            MakeTargetable("watchtower", new Vector3(3f, 0f, 0f), "The watchtower door stands open.");
            MakeTargetable("far_hill", new Vector3(50f, 0f, 0f), "The far hill is green.");
            var fake = new FakeProvider("{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"\"}");
            var agent = Fixtures.VillageGuard(fake);
            var source = Proximity();
            agent.ObservationSources.Add(source);
            var options = new DecideOptions { Observations = new List<string> { "It is night." } };

            await Fixtures.NotCancelled(agent.DecideAsync("Anything to report?", options));
            source.Origin.position = Here + new Vector3(100f, 0f, 0f);
            await Fixtures.NotCancelled(agent.DecideAsync("Anything to report?", options));

            Assert.That(fake.Requests[0].Observations,
                Is.EqualTo("What you know right now:\n- The watchtower door stands open.\n- It is night."));
            Assert.That(fake.Requests[1].Observations, Is.EqualTo("What you know right now:\n- It is night."),
                "Walked away from the tower, the guard no longer knows its door is open.");
        }

        [Test]
        public async Task AnAgentRecordsWhatTheSourcesCapDropped()
        {
            MakeTargetable("well", new Vector3(4f, 0f, 0f), "The well is dry.");
            MakeTargetable("bridge", new Vector3(1f, 0f, 0f), "A cart blocks the bridge.");
            MakeTargetable("mill", new Vector3(3f, 0f, 0f), "The mill is quiet.");
            var fake = new FakeProvider("{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"\"}");
            var agent = Fixtures.VillageGuard(fake);
            agent.MaxObservations = 1;
            agent.ObservationSources.Add(Proximity(cap: 2));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Anything to report?"));

            Assert.That(fake.LastRequest.Observations, Is.EqualTo("What you know right now:\n- A cart blocks the bridge."));
            Assert.That(result.Telemetry.ObservationsDropped, Is.EqualTo(new[] { "The mill is quiet.", "The well is dry." }),
                "The mill the agent's cap cut, then the well the source's own cap left out: nearest first.");
        }

        // ---- Telemetry ----------------------------------------------------------------------

        [Test]
        public void ObservationsDroppedStartsEmptyAndSurvivesANewtonsoftRoundTrip()
        {
            var telemetry = new DecisionTelemetry();
            Assert.That(telemetry.ObservationsDropped, Is.Not.Null.And.Empty);

            telemetry.ObservationsDropped.Add("The mill is quiet.");
            telemetry.ObservationsDropped.Add("The well is dry.");
            var back = JsonConvert.DeserializeObject<DecisionTelemetry>(JsonConvert.SerializeObject(telemetry));

            Assert.That(back.ObservationsDropped, Is.EqualTo(new[] { "The mill is quiet.", "The well is dry." }));
        }

        // ---- helpers ------------------------------------------------------------------------

        private ProximityObservationSource Proximity(float radius = 18f, int cap = 8)
        {
            var agent = new GameObject("Agent");
            created.Add(agent);
            agent.transform.position = Here;
            return new ProximityObservationSource { Origin = agent.transform, Radius = radius, Cap = cap };
        }

        private Targetable MakeTargetable(string id, Vector3 offset, string description, string category = null)
        {
            var obj = new GameObject(string.IsNullOrEmpty(id) ? "Targetable" : id);
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
            targetable.Description = description;
            targetable.Category = category;
            return targetable;
        }
    }
}
