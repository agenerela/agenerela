using UnityEngine;
using UnityEngine.AI;

namespace Demos.GreyBoxVillage
{
    /// <summary>Wanders between the square's points; takes cover when the alarm rings.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class Villager : MonoBehaviour
    {
        public Transform[] WanderPoints;
        private NavMeshAgent agent;
        private float nextMove;
        private bool inCover;

        private NavMeshAgent navigation => agent != null ? agent : agent = GetComponent<NavMeshAgent>();

        public void TakeCover(Transform shelter)
        {
            inCover = true;
            navigation.speed = 5.5f;
            if (navigation.isOnNavMesh && shelter != null)
                navigation.SetDestination(shelter.position + new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-1.5f, 1.5f)));
        }

        public void AllClear()
        {
            inCover = false;
            navigation.speed = 2f;
            nextMove = Time.time + Random.Range(1f, 4f);
        }

        private void Update()
        {
            if (inCover || !navigation.isOnNavMesh || WanderPoints == null || WanderPoints.Length == 0) return;
            if (Time.time >= nextMove && !navigation.pathPending && navigation.remainingDistance < 0.8f)
            {
                navigation.SetDestination(WanderPoints[Random.Range(0, WanderPoints.Length)].position);
                nextMove = Time.time + Random.Range(3f, 9f);
            }
        }
    }
}
