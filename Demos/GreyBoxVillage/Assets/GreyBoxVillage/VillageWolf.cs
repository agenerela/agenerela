using Agenerela;
using UnityEngine;
using UnityEngine.AI;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// A wolf that roams the woods and sometimes wanders toward the village. Struck by the guard,
    /// it runs to its den and stays hidden for a while, and cannot be named while it is gone.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class VillageWolf : MonoBehaviour
    {
        public Transform[] WanderPoints;
        public Transform[] ProwlPoints;
        public Transform Den;
        [Range(0, 1)] public float ProwlChance = 0.35f;
        public float HiddenSeconds = 60f;
        [Tooltip("Left behind the first time the wolf is driven off while the wolf quest is on.")]
        public Pickup Pelt;
        public QuestBook Quests;

        public bool IsHidden { get; private set; }
        /// <summary>True while it runs for its den after being driven off: no longer news to anyone, and not a target.</summary>
        public bool IsFleeing => fleeing;

        private NavMeshAgent agent;
        private Renderer[] renderers;
        private Targetable targetable;
        private float nextMove, reappearAt;
        private bool fleeing;

        private NavMeshAgent navigation => agent != null ? agent : agent = GetComponent<NavMeshAgent>();

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            targetable = GetComponent<Targetable>();
        }

        /// <summary>Back to roaming, seen and targetable. Used between tests.</summary>
        public void ResetState()
        {
            fleeing = false;
            IsHidden = false;
            if (targetable == null) targetable = GetComponent<Targetable>();
            if (targetable != null) targetable.IsCurrentlyTargetable = true;
        }

        /// <summary>
        /// Comes out of hiding at <paramref name="from"/> and walks toward <paramref name="toward"/>,
        /// then stands there for <paramref name="holdSeconds"/> before it roams again. The presenter's F2.
        /// </summary>
        public void Approach(Vector3 from, Vector3 toward, float holdSeconds)
        {
            Place(from);
            Show(true);
            if (navigation.isOnNavMesh) navigation.SetDestination(toward);
            nextMove = Time.time + holdSeconds;
        }

        /// <summary>
        /// In its den at <paramref name="den"/>, hidden for <see cref="HiddenSeconds"/>, then roaming
        /// again. The presenter's reset, so a stage that follows is not interrupted by it.
        /// </summary>
        public void Rest(Vector3 den)
        {
            Place(den);
            if (navigation.isOnNavMesh) navigation.ResetPath();
            Show(false);
        }

        private void Place(Vector3 at)
        {
            fleeing = false;
            if (navigation.isOnNavMesh) navigation.Warp(at);
            else transform.position = at;
        }

        /// <summary>Runs to the den, then hides.</summary>
        public void Flee()
        {
            if (IsHidden || fleeing) return;
            fleeing = true;
            var quest = Quests != null ? Quests.Get("quest_wolf") : null;
            if (Pelt != null && !Pelt.gameObject.activeSelf && quest != null && quest.State == QuestState.Active)
                Pelt.Drop(transform.position);
            navigation.speed = 7f;
            if (navigation.isOnNavMesh && Den != null) navigation.SetDestination(Den.position);
            if (targetable == null) targetable = GetComponent<Targetable>();
            if (targetable != null) targetable.IsCurrentlyTargetable = false;
        }

        private void Update()
        {
            if (!navigation.isOnNavMesh) return;

            if (IsHidden)
            {
                if (Time.time >= reappearAt) Show(true);
                return;
            }

            if (fleeing)
            {
                if (!navigation.pathPending && navigation.remainingDistance < 1f) Show(false);
                return;
            }

            if (Time.time >= nextMove && !navigation.pathPending && navigation.remainingDistance < 1f)
            {
                var points = ProwlPoints != null && ProwlPoints.Length > 0 && Random.value < ProwlChance ? ProwlPoints : WanderPoints;
                if (points != null && points.Length > 0)
                    navigation.SetDestination(points[Random.Range(0, points.Length)].position);
                nextMove = Time.time + Random.Range(6f, 14f);
            }
        }

        private void Show(bool visible)
        {
            if (renderers == null) renderers = GetComponentsInChildren<Renderer>();
            if (targetable == null) targetable = GetComponent<Targetable>();
            IsHidden = !visible;
            fleeing = false;
            foreach (var r in renderers) r.enabled = visible;
            if (targetable != null) targetable.IsCurrentlyTargetable = visible;
            navigation.speed = 3f;
            if (!visible) reappearAt = Time.time + HiddenSeconds;
            else nextMove = Time.time + 2f;
        }
    }
}
