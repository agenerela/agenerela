using System;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// A place the player may walk into but must not: a trigger volume that reports the player
    /// entering and leaving. It decides nothing. <see cref="GuardSenses"/> decides whether the guard
    /// noticed, and only then asks the agent.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RestrictedZone : MonoBehaviour
    {
        /// <summary>The same id as the zone's Targetable, so the model can name it in block_path.</summary>
        public string Id;
        /// <summary>A noun phrase for event text: "the armory".</summary>
        public string DisplayName;
        /// <summary>Where the guard stands to block the way in.</summary>
        public Transform Entrance;

        public bool PlayerInside { get; private set; }
        public event Action<RestrictedZone> PlayerEntered;
        public event Action<RestrictedZone> PlayerLeft;

        private Collider player;

        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            if (PlayerInside || !IsPlayer(other)) return;
            player = other;
            PlayerInside = true;
            PlayerEntered?.Invoke(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (PlayerInside && other == player) Leave();
        }

        // Unity sends no OnTriggerExit when a collider is disabled or teleported out (a respawn, a
        // cutscene), so the zone also checks that the player is still overlapping it.
        private void FixedUpdate()
        {
            if (!PlayerInside) return;
            if (player == null || !player.enabled || !GetComponent<BoxCollider>().bounds.Intersects(player.bounds)) Leave();
        }

        private void Leave()
        {
            PlayerInside = false;
            player = null;
            PlayerLeft?.Invoke(this);
        }

        private static bool IsPlayer(Collider other) => other.GetComponent<CharacterController>() != null;

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}
