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

        public bool IsHidden { get; private set; }

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

        /// <summary>Runs to the den, then hides.</summary>
        public void Flee()
        {
            if (IsHidden || fleeing) return;
            fleeing = true;
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
