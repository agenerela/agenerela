using System.Collections.Generic;
using LivingVale.Interaction;
using LivingVale.Unity;
using UnityEngine;

namespace LivingVale.Combat
{
    /// <summary>
    /// Ties combat to the rest of the game: damage closes the open conversation, an engaged enemy
    /// keeps chat from opening, a defeated enemy hands its loot to <see cref="LootSink"/> once, and
    /// a defeated player pays <see cref="DefeatPenalty"/>, respawns in the village and the fight
    /// around them resets.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class CombatDirector : MonoBehaviour
    {
        [SerializeField] private ValeBootstrap bootstrap;
        [SerializeField] private CombatBody player;
        [SerializeField] private Transform respawnPoint;
        [SerializeField, Min(0f)] private float respawnDelay = 2.5f;

        /// <summary>Enemies this far from the defeat point, or engaged anywhere, reset when the player respawns.</summary>
        [SerializeField, Min(0f)] private float resetRadius = 40f;

        private readonly List<EnemyController> enemies = new List<EnemyController>();
        private readonly HashSet<EnemyController> looted = new HashSet<EnemyController>();
        private ILootSink lootSink = new LogLootSink();
        private IDefeatPenalty defeatPenalty = new NoDefeatPenalty();
        private float respawnAt = float.PositiveInfinity;
        private Vector3 defeatPosition;

        /// <summary>The director in the loaded scene, or null when the scene has no combat.</summary>
        public static CombatDirector Active { get; private set; }

        /// <summary>True while any enemy is fighting the player. Chat refuses to open then (design §8).</summary>
        public static bool EnemyEngaged => Active != null && Active.AnyEngaged;

        public CombatBody Player => player;
        public IReadOnlyList<EnemyController> Enemies => enemies;
        public bool PlayerDefeated => player != null && player.Combatant != null && player.Combatant.IsDefeated;
        public float SecondsUntilRespawn => PlayerDefeated ? Mathf.Max(0f, respawnAt - Time.time) : 0f;

        public bool AnyEngaged
        {
            get
            {
                foreach (var enemy in enemies)
                {
                    if (enemy != null && enemy.IsEngaged)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>Where loot goes. Defaults to a log line; the inventory replaces it (G3).</summary>
        public ILootSink LootSink
        {
            get => lootSink;
            set => lootSink = value ?? new LogLootSink();
        }

        /// <summary>What a defeat costs. Defaults to nothing; the inventory's gold penalty replaces it (G3).</summary>
        public IDefeatPenalty DefeatPenalty
        {
            get => defeatPenalty;
            set => defeatPenalty = value ?? new NoDefeatPenalty();
        }

        public void Configure(ValeBootstrap owner, CombatBody playerBody, Transform respawn)
        {
            bootstrap = owner;
            player = playerBody;
            respawnPoint = respawn;
        }

        private void OnEnable()
        {
            Active = this;
        }

        private void Start()
        {
            player.Combatant.Damaged += OnPlayerDamaged;
            player.Combatant.Defeated += OnPlayerDefeated;
            enemies.AddRange(FindObjectsByType<EnemyController>(FindObjectsSortMode.InstanceID));
            foreach (var enemy in enemies)
            {
                enemy.Defeated += OnEnemyDefeated;
            }
        }

        private void Update()
        {
            if (PlayerDefeated && Time.time >= respawnAt)
            {
                Respawn();
            }
        }

        private void OnPlayerDamaged(Combatant _, HitResult hit)
        {
            // Design §8: chat closes on damage. A fully blocked hit still interrupts the talk.
            if (bootstrap != null && bootstrap.Coordinator != null && bootstrap.Coordinator.IsOpen)
            {
                bootstrap.Coordinator.Close(ConversationCloseReason.Interrupted);
            }
        }

        private void OnPlayerDefeated(Combatant _)
        {
            defeatPosition = player.transform.position;
            respawnAt = Time.time + respawnDelay;
            SetPlayerControl(false);
            Debug.Log("[LivingVale] Player defeated; respawning in the village.");
        }

        private void Respawn()
        {
            respawnAt = float.PositiveInfinity;
            defeatPenalty.Apply(player.Combatant.Id);

            // Reset before moving the player, so no enemy re-engages on the respawn frame.
            foreach (var enemy in enemies)
            {
                if (enemy != null && (enemy.IsEngaged ||
                    (enemy.transform.position - defeatPosition).sqrMagnitude <= resetRadius * resetRadius))
                {
                    enemy.ResetToHome();
                }
            }

            var controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }

            Transform target = respawnPoint != null ? respawnPoint : transform;
            player.transform.SetPositionAndRotation(target.position, target.rotation);
            if (controller != null)
            {
                controller.enabled = true;
            }

            player.Combatant.Restore();
            SetPlayerControl(true);
            Debug.Log("[LivingVale] Player respawned.");
        }

        private void SetPlayerControl(bool enabled)
        {
            var movement = player.GetComponent<ThirdPersonPlayer>();
            if (movement != null)
            {
                movement.enabled = enabled;
            }

            var visual = player.Visual;
            if (visual != null)
            {
                visual.localRotation = enabled ? Quaternion.identity : Quaternion.Euler(80f, 0f, 0f);
            }
        }

        private void OnEnemyDefeated(EnemyController enemy)
        {
            // Defeated enemies stay down, but guard the sink against a double event anyway.
            if (!looted.Add(enemy))
            {
                return;
            }

            foreach (var drop in LootTable.For(enemy.Archetype, enemy.Body.Combatant.Id, enemy.transform.position))
            {
                lootSink.Receive(drop);
            }
        }

        private void OnDisable()
        {
            if (Active == this)
            {
                Active = null;
            }
        }

        private void OnDestroy()
        {
            if (player != null && player.Combatant != null)
            {
                player.Combatant.Damaged -= OnPlayerDamaged;
                player.Combatant.Defeated -= OnPlayerDefeated;
            }

            foreach (var enemy in enemies)
            {
                if (enemy != null)
                {
                    enemy.Defeated -= OnEnemyDefeated;
                }
            }
        }
    }
}
