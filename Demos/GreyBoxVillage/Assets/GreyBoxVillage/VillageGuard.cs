using UnityEngine;
using UnityEngine.AI;

namespace Demos.GreyBoxVillage
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class VillageGuard : MonoBehaviour
    {
        public string Follow(Transform player) => Report("Would follow the player; guard stays at the gate.");
        public string MoveTo(Transform target) => Report("Would move to " + target.name + "; guard stays at the gate.");
        public string Attack(Transform target) => Report("Would attack " + target.name + "; no damage is applied.");

        private string Report(string message)
        {
            Debug.Log("[GreyBoxVillage placeholder] " + message, this);
            return message;
        }
    }
}
