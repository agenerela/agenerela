using Demos.Shared;
using UnityEngine;
using UnityEngine.AI;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// A scripted villager, not an agent: it follows <see cref="VillageRoutine"/>'s day (the well at
    /// dawn, its work, the square at midday, the campfire at dusk, home after dark with its window
    /// lit), takes cover when the alarm rings, and goes back to its day when the bell stops.
    /// </summary>
    /// <remarks>
    /// <see cref="Step"/> is one look at the hour and the path, run every frame in Play mode and called
    /// directly by the EditMode tests, where nothing walks. Arrival is read from where the villager
    /// stands, not from the NavMesh, so a test can stand it somewhere and step it.
    /// </remarks>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class Villager : MonoBehaviour
    {
        public VillageRoutine Routine;
        [Tooltip("Where it works by day: a spot of the routine, such as market or fields.")]
        public string Job = VillageRoutine.Market;
        [Tooltip("Just outside its front door: it walks here after dark and goes in.")]
        public Transform Home;
        [Tooltip("Shines at its door while it is home.")]
        public Light HomeLight;

        /// <summary>The routine's place it is at or walking to; <see cref="VillageRoutine.Shelter"/> while in cover.</summary>
        public string Place { get; private set; }
        /// <summary>Where it is walking now.</summary>
        public Vector3 Goal { get; private set; }
        /// <summary>Inside its house: out of sight, its light on.</summary>
        public bool IsHome { get; private set; }
        public bool InCover => inCover;

        private NavMeshAgent agent;
        private float nextMove;
        private bool inCover, hasGoal;
        private Renderer[] renderers;
        private NameTag nameTag;

        private NavMeshAgent navigation => agent != null ? agent : agent = GetComponent<NavMeshAgent>();

        public void TakeCover(Transform shelter)
        {
            inCover = true;
            ComeOut();
            Place = VillageRoutine.Shelter;
            navigation.speed = 5.5f;
            if (shelter != null) Walk(shelter.position + new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-1.5f, 1.5f)));
        }

        /// <summary>The bell has stopped: back to whatever the hour says, from where it stands.</summary>
        public void AllClear()
        {
            inCover = false;
            navigation.speed = 2f;
            Place = null;
            nextMove = Time.time + Random.Range(1f, 4f);
        }

        /// <summary>Back at <paramref name="at"/>, out of cover and out of doors. The presenter's reset.</summary>
        public void ResetTo(Vector3 at)
        {
            AllClear();
            ComeOut();
            hasGoal = false;
            Put(at);
        }

        /// <summary>
        /// Straight to where the hour has it, without walking: home and indoors after dark. After a
        /// jump in time (the presenter's F5), so nobody is left outside a gate shut for the night.
        /// </summary>
        public void CatchUp()
        {
            if (inCover) return;
            Place = null;
            Step();
            Put(Goal);
            Step();
        }

        private void Update() => Step();

        /// <summary>One look at the hour and the path: a new place when the hour changes it, another point there when it has stood a while, and indoors once home.</summary>
        public void Step()
        {
            if (inCover) return;
            string place = Routine != null ? Routine.PlaceFor(this) : null;
            if (place == null) return;
            if (place != Place)
            {
                Place = place;
                if (place != VillageRoutine.Home) ComeOut();
                Pick();
                return;
            }
            if (!Arrived) return;
            if (place == VillageRoutine.Home)
            {
                if (!IsHome) GoIn();
                return;
            }
            if (Time.time >= nextMove) Pick();
        }

        /// <summary>Within a metre of where it was walking, ignoring height.</summary>
        public bool Arrived
        {
            get
            {
                var d = transform.position - Goal;
                d.y = 0;
                return hasGoal && d.magnitude < 1f;
            }
        }

        // Another point of the place, or the door when the place is home.
        private void Pick()
        {
            nextMove = Time.time + Random.Range(4f, 10f);
            if (Place == VillageRoutine.Home)
            {
                if (Home != null) Walk(Home.position);
                return;
            }
            var points = Routine.PointsOf(Place);
            if (points.Length == 0) return;
            // A point nobody else is making for, so two villagers never stand inside each other.
            int start = Random.Range(0, points.Length);
            var p = points[start];
            for (int i = 0; i < points.Length; i++)
            {
                var candidate = points[(start + i) % points.Length];
                if (Routine.Taken(candidate.position, this)) continue;
                p = candidate;
                break;
            }
            Walk(p.position + new Vector3(Random.Range(-0.8f, 0.8f), 0, Random.Range(-0.8f, 0.8f)));
        }

        private void Walk(Vector3 to)
        {
            Goal = to;
            hasGoal = true;
            if (navigation.isActiveAndEnabled && navigation.isOnNavMesh) navigation.SetDestination(to);
        }

        private void Put(Vector3 at)
        {
            if (navigation.isActiveAndEnabled && navigation.isOnNavMesh)
            {
                navigation.Warp(at);
                if (hasGoal) navigation.SetDestination(Goal);
            }
            else transform.position = at;
        }

        private void GoIn()
        {
            IsHome = true;
            Show(false);
            if (navigation.isActiveAndEnabled && navigation.isOnNavMesh) navigation.ResetPath();
        }

        private void ComeOut()
        {
            if (!IsHome) return;
            IsHome = false;
            Show(true);
        }

        private void Show(bool visible)
        {
            renderers ??= GetComponentsInChildren<Renderer>(true);
            nameTag ??= GetComponent<NameTag>();
            foreach (var r in renderers) if (r != null) r.enabled = visible;
            if (nameTag != null) nameTag.enabled = visible;
            if (HomeLight != null) HomeLight.enabled = !visible;
        }
    }
}
