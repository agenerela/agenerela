using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>Something lying in the world the player can pick up with E. Bobs and turns so it is noticed.</summary>
    public sealed class Pickup : MonoBehaviour
    {
        public string ItemId;
        public int Count = 1;
        public Transform Visual;
        private Vector3 rest;

        public string Name => ItemCatalog.Get(ItemId)?.Name ?? ItemId;

        private void Start()
        {
            if (Visual != null) rest = Visual.localPosition;
        }

        private void Update()
        {
            if (Visual == null) return;
            Visual.localPosition = rest + Vector3.up * (0.12f + Mathf.Sin(Time.time * 2f + transform.position.x) * 0.06f);
            Visual.Rotate(0, 50f * Time.deltaTime, 0, Space.World);
        }

        /// <summary>Puts it in the inventory and takes it out of the world.</summary>
        public void Collect(PlayerInventory inventory)
        {
            inventory.Add(ItemId, Count);
            gameObject.SetActive(false);
        }

        /// <summary>Drops it here, back in the world. The wolf's pelt is dropped this way.</summary>
        public void Drop(Vector3 at)
        {
            transform.position = at;
            gameObject.SetActive(true);
        }
    }
}
