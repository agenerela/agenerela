using LivingVale.Interaction;
using LivingVale.UI;
using UnityEngine;

namespace LivingVale.Unity
{
    [DefaultExecutionOrder(300)]
    public sealed class NpcInteraction : MonoBehaviour
    {
        [SerializeField] private ValeBootstrap bootstrap;
        [SerializeField] private ChatOverlay overlay;

        private NpcSpeaker[] speakers;

        public void Configure(ValeBootstrap owner, ChatOverlay sceneOverlay)
        {
            bootstrap = owner;
            overlay = sceneOverlay;
        }

        private void Start()
        {
            speakers = FindObjectsByType<NpcSpeaker>(FindObjectsSortMode.InstanceID);
        }

        private void Update()
        {
            NpcSpeaker focused = null;
            NpcSpeaker active = null;
            float nearest = 3f * 3f;
            foreach (var speaker in speakers)
            {
                if (speaker == null || !speaker.isActiveAndEnabled)
                {
                    continue;
                }

                if (speaker.Descriptor.Id == bootstrap.Coordinator.ActiveNpc?.Id)
                {
                    active = speaker;
                }

                // Measure from feet to feet so NPC capsule height does not consume talk range.
                float squaredDistance = (speaker.transform.position - transform.position).sqrMagnitude;
                if (squaredDistance <= nearest && HasLineOfSight(speaker))
                {
                    focused = speaker;
                    nearest = squaredDistance;
                }
            }

            if (bootstrap.Coordinator.IsOpen &&
                (active == null || (active.transform.position - transform.position).sqrMagnitude > 36f))
            {
                bootstrap.Coordinator.Close(active == null
                    ? ConversationCloseReason.Interrupted : ConversationCloseReason.LeftRange);
            }

            overlay.SetFocusPrompt(bootstrap.WorldInputBlocked || focused == null
                ? "" : $"E — Talk to {focused.Descriptor.DisplayName}");
            if (focused != null && bootstrap.TalkPressed)
            {
                bootstrap.Coordinator.Open(focused.Descriptor);
            }
        }

        private bool HasLineOfSight(NpcSpeaker speaker)
        {
            Vector3 eye = transform.position + Vector3.up * 1.5f;
            Vector3 direction = speaker.TalkPoint - eye;
            if (!Physics.Raycast(eye, direction.normalized, out var hit, direction.magnitude,
                ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            return hit.collider.GetComponentInParent<NpcSpeaker>() == speaker;
        }
    }
}
