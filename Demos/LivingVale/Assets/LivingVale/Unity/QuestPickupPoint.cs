using LivingVale.Game;
using UnityEngine;

namespace LivingVale.Unity
{
    /// <summary>A world interaction point whose separate visual follows the saved pickup state.</summary>
    public sealed class QuestPickupPoint : MonoBehaviour
    {
        [SerializeField] private ValeBootstrap bootstrap;
        [SerializeField] private string pickupId;
        [SerializeField] private GameObject visual;

        public string PickupId => pickupId;

        public void Configure(ValeBootstrap owner, string pickupId, GameObject visual)
        {
            bootstrap = owner;
            this.pickupId = pickupId;
            this.visual = visual;
        }

        private void Start()
        {
            bootstrap.Routes.Changed += Render;
            bootstrap.Quests.Changed += Render;
            bootstrap.Inventory.Changed += Render;
            Render();
        }

        private void Render()
        {
            // Keep this component alive so a load can make the visual available again.
            if (visual != null)
            {
                visual.SetActive(pickupId == QuestRoutes.PickupWreck || !bootstrap.Routes.IsDone(pickupId));
            }
        }

        private void OnDestroy()
        {
            if (bootstrap != null && bootstrap.Routes != null)
            {
                bootstrap.Routes.Changed -= Render;
                bootstrap.Quests.Changed -= Render;
                bootstrap.Inventory.Changed -= Render;
            }
        }
    }
}
