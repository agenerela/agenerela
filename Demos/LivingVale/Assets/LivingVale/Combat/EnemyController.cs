using System;
using UnityEngine;

namespace LivingVale.Combat
{
    [DefaultExecutionOrder(250)]
    [RequireComponent(typeof(CombatBody), typeof(CharacterController))]
    public sealed class EnemyController : MonoBehaviour
    {
        [SerializeField] private EnemyArchetype archetype;
        [SerializeField] private Transform visual;

        private CharacterController controller;
        private CombatBody player;
        private EnemyBrain brain;
        private EnemyBrainTuning tuning;
        private Quaternion homeRotation;
        private Quaternion visualRotation;
        private Vector3 visualScale;
        private Vector3 lungeDirection;
        private float verticalSpeed;
        private bool defeatRaised;

        public EnemyArchetype Archetype => archetype;
        public CombatBody Body { get; private set; }
        public EnemyState State => brain != null ? brain.State : EnemyState.Idle;
        public bool IsEngaged => brain != null && brain.IsEngaged;
        public Vector3 Home { get; private set; }
        public event Action<EnemyController> Defeated;

        public void Configure(EnemyArchetype archetype, Transform visual)
        {
            this.archetype = archetype;
            this.visual = visual;
        }

        private void Awake()
        {
            Body = GetComponent<CombatBody>();
            controller = GetComponent<CharacterController>();
            Home = transform.position;
            homeRotation = transform.rotation;
            tuning = archetype == EnemyArchetype.Wolf ? EnemyBrainTuning.Wolf() : EnemyBrainTuning.Raider();
            brain = new EnemyBrain(tuning);
            brain.Reset(Time.time);
            if (visual != null)
            {
                visualRotation = visual.localRotation;
                visualScale = visual.localScale;
            }
        }

        private void Start()
        {
            // CombatBody creates its combatant in Awake; every Awake has completed before Start.
            Body.Combatant.Defeated += OnDefeated;
            var bodies = FindObjectsByType<CombatBody>(FindObjectsSortMode.None);
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i].IsPlayer)
                {
                    player = bodies[i];
                    break;
                }
            }

            if (Body.Combatant.IsDefeated)
            {
                OnDefeated(Body.Combatant);
            }
        }

        private void Update()
        {
            float now = Time.time;
            bool targetAvailable = player != null && player.isActiveAndEnabled &&
                player.Combatant != null && !player.Combatant.IsDefeated;
            Vector3 position = transform.position;
            var perception = new EnemyPerception(
                targetAvailable ? GroundDistance(position, player.transform.position) : float.PositiveInfinity,
                targetAvailable ? GroundDistance(Home, player.transform.position) : float.PositiveInfinity,
                GroundDistance(position, Home), targetAvailable, Body.Combatant.IsStaggered(now),
                Body.Combatant.IsDefeated);
            EnemyState previous = brain.State;
            brain.Step(in perception, now);

            if (brain.State == EnemyState.Defeat)
            {
                OnDefeated(Body.Combatant);
                return;
            }

            if (brain.State == EnemyState.Pursue || brain.State == EnemyState.Telegraph)
            {
                FacePlayer();
            }

            if (brain.State != previous)
            {
                if (brain.State == EnemyState.Attack)
                {
                    FacePlayer();
                    lungeDirection = transform.forward;
                    Body.Combatant.TryStartAttack(now);
                }
                else if (previous == EnemyState.Attack)
                {
                    Body.Combatant.CancelSwing(now);
                }
            }

            Vector3 velocity = Vector3.zero;
            if (brain.State == EnemyState.Idle && brain.ReturningHome)
            {
                velocity = WalkTowards(Home, 0f);
                if (velocity.sqrMagnitude > 0.001f)
                {
                    Face(velocity);
                }
            }
            else if (brain.State == EnemyState.Pursue && targetAvailable)
            {
                // Step just inside the threshold so float rounding cannot strand pursuit at its edge.
                velocity = WalkTowards(player.transform.position, tuning.AttackRange * 0.95f);
            }
            else if (brain.State == EnemyState.Attack)
            {
                velocity = lungeDirection * tuning.LungeSpeed;
            }

            Move(velocity);
            if (brain.State == EnemyState.Attack && targetAvailable)
            {
                AttackSwing swing = Body.Combatant.ActiveSwing;
                if (swing != null && swing.IsActive(now) && MeleeReach.InArc(transform.position,
                    transform.forward, player.transform.position, tuning.HitRange, tuning.HitHalfAngleDegrees))
                {
                    player.Combatant.ReceiveHit(swing, now);
                }
            }

            PoseVisual(now);
        }

        public void ResetToHome()
        {
            if (Body == null || Body.Combatant == null || defeatRaised || Body.Combatant.IsDefeated)
            {
                return;
            }

            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.SetPositionAndRotation(Home, homeRotation);
            controller.enabled = wasEnabled;
            verticalSpeed = 0f;
            lungeDirection = Vector3.zero;
            Body.Combatant.Restore();
            brain.Reset(Time.time);
            PoseVisual(Time.time);
        }

        private Vector3 WalkTowards(Vector3 destination, float stoppingDistance)
        {
            Vector3 offset = destination - transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance <= stoppingDistance || Time.deltaTime <= 0f)
            {
                return Vector3.zero;
            }

            float speed = Mathf.Min(tuning.MoveSpeed, (distance - stoppingDistance) / Time.deltaTime);
            return offset / distance * speed;
        }

        private void FacePlayer()
        {
            if (player != null)
            {
                Face(player.transform.position - transform.position);
            }
        }

        private void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), 540f * Time.deltaTime);
            }
        }

        private void Move(Vector3 velocity)
        {
            if (!controller.enabled)
            {
                return;
            }

            if (controller.isGrounded && verticalSpeed < 0f)
            {
                verticalSpeed = -2f;
            }

            verticalSpeed = Mathf.Max(verticalSpeed - 20f * Time.deltaTime, -40f);
            controller.Move((velocity + Vector3.up * verticalSpeed) * Time.deltaTime);
        }

        private void PoseVisual(float now)
        {
            if (visual == null)
            {
                return;
            }

            visual.localScale = visualScale;
            visual.localRotation = visualRotation;
            if (brain.State == EnemyState.Telegraph)
            {
                float progress = Mathf.Clamp01((now - brain.StateEnteredAt) / tuning.TelegraphSeconds);
                visual.localRotation = visualRotation * Quaternion.Euler(-12f - 13f * progress, 0f, 0f);
                visual.localScale = Vector3.Scale(visualScale, new Vector3(1.08f, 0.94f, 1.08f));
            }
            else if (brain.State == EnemyState.Attack)
            {
                visual.localRotation = visualRotation * Quaternion.Euler(20f, 0f, 0f);
            }
            else if (brain.State == EnemyState.Defeat)
            {
                visual.localRotation = visualRotation * Quaternion.Euler(0f, 0f, 85f);
            }
        }

        private void OnDefeated(Combatant _)
        {
            if (defeatRaised)
            {
                return;
            }

            defeatRaised = true;
            var perception = new EnemyPerception(0f, 0f, 0f, false, defeated: true);
            brain.Step(in perception, Time.time);
            controller.enabled = false;
            PoseVisual(Time.time);
            Defeated?.Invoke(this);
        }

        private void OnDestroy()
        {
            if (Body != null && Body.Combatant != null)
            {
                Body.Combatant.Defeated -= OnDefeated;
            }
        }

        private static float GroundDistance(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            offset.y = 0f;
            return offset.magnitude;
        }
    }
}
