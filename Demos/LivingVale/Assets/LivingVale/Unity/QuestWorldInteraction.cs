using LivingVale.Game;
using LivingVale.UI;
using UnityEngine;

namespace LivingVale.Unity
{
    /// <summary>Offers the nearest unfinished world pickup after NPC focus has been resolved.</summary>
    [DefaultExecutionOrder(310)]
    public sealed class QuestWorldInteraction : MonoBehaviour
    {
        [SerializeField] private ValeBootstrap bootstrap;
        [SerializeField] private ChatOverlay overlay;
        [SerializeField] private NpcInteraction npcInteraction;

        private QuestPickupPoint[] points;

        public void Configure(ValeBootstrap owner, ChatOverlay sceneOverlay, NpcInteraction interaction)
        {
            bootstrap = owner;
            overlay = sceneOverlay;
            npcInteraction = interaction;
        }

        private void Start()
        {
            points = FindObjectsByType<QuestPickupPoint>(FindObjectsSortMode.InstanceID);
        }

        private void Update()
        {
            if (npcInteraction.HasFocus || bootstrap.Coordinator.IsOpen || bootstrap.WorldInputBlocked)
            {
                return;
            }

            QuestPickup pickup = null;
            float nearest = 2.5f * 2.5f;
            foreach (var point in points)
            {
                if (point == null || !point.isActiveAndEnabled || bootstrap.Routes.IsDone(point.PickupId))
                {
                    continue;
                }

                Vector3 offset = point.transform.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= nearest)
                {
                    QuestPickup candidate = bootstrap.Routes.FindPickup(point.PickupId);
                    if (candidate != null)
                    {
                        pickup = candidate;
                        nearest = offset.sqrMagnitude;
                    }
                }
            }

            if (pickup == null)
            {
                return;
            }

            // Locked pickups still explain themselves through TryPickUp's authored receipt.
            overlay.SetFocusPrompt($"E — {pickup.Verb} {pickup.DisplayName}");
            if (bootstrap.TalkPressed && !LivingVale.Combat.CombatDirector.EnemyEngaged)
            {
                bootstrap.Routes.TryPickUp(pickup.Id);
            }
        }
    }
}
