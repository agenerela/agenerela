using LivingVale.Interaction;
using UnityEngine;

namespace LivingVale.Unity
{
    public sealed class NpcSpeaker : MonoBehaviour
    {
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [SerializeField] private string role;

        private ValeBootstrap bootstrap;
        private Transform player;
        private NpcDescriptor descriptor;

        public NpcDescriptor Descriptor => descriptor ??= new NpcDescriptor(stableId, displayName, role);
        public Vector3 TalkPoint => transform.position + Vector3.up * 1.5f;

        public void Configure(string id, string npcName, string npcRole)
        {
            stableId = id;
            displayName = npcName;
            role = npcRole;
            descriptor = null;
        }

        public void Bind(ValeBootstrap owner, Transform playerTransform)
        {
            bootstrap = owner;
            player = playerTransform;
        }

        private void Update()
        {
            if (bootstrap == null || bootstrap.Coordinator.ActiveNpc?.Id != Descriptor.Id)
            {
                return;
            }

            Vector3 direction = Vector3.ProjectOnPlane(player.position - transform.position, Vector3.up);
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), 180f * Time.deltaTime);
            }
        }
    }
}
