using LivingVale.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace LivingVale.Combat
{
    /// <summary>
    /// The player's attack, block and dodge. Left mouse attacks, right mouse blocks, Space dodges.
    /// Combat input is refused while typing, while a chat is open, while the cursor is free and
    /// while the pointer is over UI, so a click on the chat can never swing (design §4 and §8).
    /// Its own input map keeps G1's bootstrap untouched.
    /// </summary>
    // After movement (200) and CombatBody (100), before EnemyController (250): a block or dodge
    // pressed this frame is up before an enemy swing lands in the same frame.
    [DefaultExecutionOrder(240)]
    [RequireComponent(typeof(CombatBody), typeof(CharacterController))]
    public sealed class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private ValeBootstrap bootstrap;
        [SerializeField] private Transform bladePivot;
        [SerializeField, Min(0f)] private float reach = 2.3f;
        [SerializeField, Range(0f, 180f)] private float halfArc = 65f;
        [SerializeField, Min(0f)] private float dodgeSpeed = 8f;
        [SerializeField, Min(0f)] private float softAimRange = 3.5f;

        // The blade only cuts in the middle of the swing: anticipation first, recovery after.
        private const float HitWindowStart = 0.25f;
        private const float HitWindowEnd = 0.8f;

        private static readonly Quaternion RestPose = Quaternion.Euler(60f, 25f, 0f);
        private static readonly Quaternion BlockPose = Quaternion.Euler(-10f, -80f, 0f);

        private InputActionMap actions;
        private InputAction attack;
        private InputAction blockAction;
        private InputAction dodge;
        private CombatBody body;
        private CharacterController controller;
        private Vector3 dodgeDirection;

        public Combatant Combatant => body.Combatant;

        /// <summary>
        /// True when a mouse button or Space must not reach combat this frame. Exposed so the HUD
        /// and tests of the scene can show why an input was ignored.
        /// </summary>
        public bool CombatInputBlocked =>
            bootstrap == null || bootstrap.WorldInputBlocked || bootstrap.Coordinator.IsOpen ||
            Cursor.lockState != CursorLockMode.Locked || PointerOverUi();

        public void Configure(ValeBootstrap owner, Transform blade)
        {
            bootstrap = owner;
            bladePivot = blade;
        }

        private void Awake()
        {
            body = GetComponent<CombatBody>();
            controller = GetComponent<CharacterController>();
            actions = new InputActionMap("Living Vale combat");
            attack = actions.AddAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
            blockAction = actions.AddAction("Block", InputActionType.Button, "<Mouse>/rightButton");
            dodge = actions.AddAction("Dodge", InputActionType.Button, "<Keyboard>/space");
        }

        private void OnEnable()
        {
            actions.Enable();
        }

        private void Update()
        {
            float now = Time.time;
            Combatant self = body.Combatant;
            bool blocked = CombatInputBlocked;

            // Block first: a held guard refuses the attack pressed in the same frame.
            self.SetBlocking(!blocked && blockAction.IsPressed(), now);
            if (!blocked && dodge.WasPressedThisFrame() && self.TryDodge(now))
            {
                dodgeDirection = DodgeDirection();
            }

            if (!blocked && attack.WasPressedThisFrame() && self.TryStartAttack(now) != null)
            {
                SoftAim();
            }

            if (self.IsDodging(now) && !self.IsDefeated)
            {
                controller.Move(dodgeDirection * (dodgeSpeed * Time.deltaTime));
            }

            StrikeEnemies(now);
            PoseBlade(now);
        }

        private void StrikeEnemies(float now)
        {
            AttackSwing swing = body.Combatant.ActiveSwing;
            CombatDirector director = CombatDirector.Active;
            if (swing == null || !swing.IsActive(now) || director == null)
            {
                return;
            }

            float progress = swing.Progress(now);
            if (progress < HitWindowStart || progress > HitWindowEnd)
            {
                return;
            }

            foreach (var enemy in director.Enemies)
            {
                if (enemy == null || !enemy.isActiveAndEnabled)
                {
                    continue;
                }

                Combatant victim = enemy.Body.Combatant;
                if (!swing.HasStruck(victim) &&
                    MeleeReach.InArc(transform.position, transform.forward, enemy.transform.position, reach, halfArc))
                {
                    victim.ReceiveHit(swing, now);
                }
            }
        }

        private void SoftAim()
        {
            // Design §4: free aim with a soft turn toward a nearby enemy, no lock-on.
            CombatDirector director = CombatDirector.Active;
            if (director == null)
            {
                return;
            }

            EnemyController best = null;
            float bestDistance = softAimRange * softAimRange;
            foreach (var enemy in director.Enemies)
            {
                if (enemy == null || enemy.Body.Combatant.IsDefeated)
                {
                    continue;
                }

                Vector3 offset = enemy.transform.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude < bestDistance && Vector3.Angle(transform.forward, offset) < 100f)
                {
                    best = enemy;
                    bestDistance = offset.sqrMagnitude;
                }
            }

            if (best != null)
            {
                Vector3 offset = best.transform.position - transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(offset);
                }
            }
        }

        private Vector3 DodgeDirection()
        {
            Vector2 input = bootstrap.Movement;
            Transform view = Camera.main != null ? Camera.main.transform : transform;
            Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 direction = forward * input.y + right * input.x;
            return direction.sqrMagnitude > 0.01f ? direction.normalized : -transform.forward;
        }

        private void PoseBlade(float now)
        {
            if (bladePivot == null)
            {
                return;
            }

            Combatant self = body.Combatant;
            bladePivot.gameObject.SetActive(!self.IsDefeated);
            AttackSwing swing = self.ActiveSwing;
            if (swing != null && swing.IsActive(now))
            {
                bladePivot.localRotation = Quaternion.Euler(15f, Mathf.Lerp(-85f, 85f, swing.Progress(now)), 0f);
            }
            else
            {
                bladePivot.localRotation = Quaternion.RotateTowards(bladePivot.localRotation,
                    self.IsBlocking ? BlockPose : RestPose, 900f * Time.deltaTime);
            }
        }

        private static bool PointerOverUi()
        {
            EventSystem events = EventSystem.current;
            return events != null && events.IsPointerOverGameObject();
        }

        private void OnDisable()
        {
            actions?.Disable();
            if (body != null && body.Combatant != null)
            {
                body.Combatant.SetBlocking(false, Time.time);
            }
        }

        private void OnDestroy()
        {
            actions?.Dispose();
        }
    }
}
