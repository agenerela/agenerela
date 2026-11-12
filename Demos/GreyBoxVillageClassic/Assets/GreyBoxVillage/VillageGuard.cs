using System.Collections.Generic;
using Agenerela;
using UnityEngine;
using UnityEngine.AI;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// The guard's body: the game code each action runs. Which action runs, and when, is the
    /// agent's decision; these methods only carry it out on the NavMesh.
    /// </summary>
    /// <remarks>
    /// The first four actions are the evaluation agent's (UnityProject/Assets/Evaluation/agent.json);
    /// the rest are this demo's own. The few-shot examples are deliberately not phrases from the
    /// evaluation set (hard rule 4). A local model never reads the schema, so these examples are
    /// how it learns each action exists.
    ///
    /// For the same reason the availability checks carry most of the weight: an action that is not
    /// offered cannot be picked, whatever the model makes of a line. They read who is asking from
    /// the agent's State, which <see cref="AgentMind"/> sets before each decision: the source
    /// of the stimulus under <see cref="AgentMind.AskedBy"/> and the distance of a wolf the guard can see under
    /// <see cref="AgentMind.WolfMetres"/>.
    ///
    /// Bram and Wren are both guards: the same body, each with its own post, patrol and profile.
    /// </remarks>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class VillageGuard : VillageBody
    {
        /// <summary>The game guard that keeps a guard from attacking the village's own people.</summary>
        public const string NeverAttackVillagers = "never_attack_villagers";

        /// <summary>Where the guard stands when nothing else is going on: Bram's gate, Wren's bridge.</summary>
        public Transform Post;
        public VillageAlarm Alarm;
        public Transform[] PatrolRoute;
        public float FollowDistance = 2.5f;
        public float StrikeDistance = 1.8f;
        [Tooltip("The bell is offered for a wolf the guard can see within this distance, or when the player speaks.")]
        public float AlarmWolfDistance = 15f;

        public bool IsFollowing { get; private set; }
        public bool IsPatrolling { get; private set; }
        public bool IsMoving => destination != null || chase != null || IsPatrolling;
        public override bool IsBusy => IsFollowing || IsMoving;

        /// <summary>What the guard is doing, as a clause the observations can use.</summary>
        public override string Activity => activity ??= AtPost;

        private string activity;

        private NavMeshAgent navigation;
        private Transform destination;
        private string destinationId;
        private Transform chase;
        private bool attacking;
        private int patrolIndex;

        // ---- The evaluation agent's actions --------------------------------------------------

        [AgentAction("follow_player", "Walk behind the player wherever they go.")]
        [Example("Tag along behind me.")]
        private void Follow()
        {
            Clear();
            IsFollowing = true;
            activity = "following the player";
            Report("Following the player.");
        }

        // Only the player starts or ends an escort: a wolf, a trespass or a quiet moment asks for neither.
        [Available("follow_player")]
        private bool CanFollow(AgentContext ctx) => !IsFollowing && Asker(ctx) == "player";

        [AgentAction("stop_following", "Stop following the player and stay where you are.")]
        [Example("You can stay put now.")]
        private void StopFollowing()
        {
            Clear();
            Halt();
            activity = "standing still";
            Report("Holding position.");
        }

        [Available("stop_following")]
        private bool CanStop(AgentContext ctx) => IsFollowing && Asker(ctx) == "player";

        [AgentAction("move_to", "Walk to a place.", RequiresTarget = true)]
        [Example("Go and stand by the {0}.", "well", "bridge")]
        private void MoveTo(Transform target)
        {
            Clear();
            Walk(target, IdOf(target));
            activity = "walking to the " + destinationId;
            Report("Walking to the " + destinationId + ".");
        }

        [AgentAction("attack_target", "Attack something.", RequiresTarget = true)]
        [Example("Strike the {0}.", "training_dummy", "wolf")]
        private void Attack(Transform target)
        {
            Clear();
            attacking = true;
            if (target.GetComponent<VillageWolf>() != null) chase = target;
            Walk(target, IdOf(target));
            activity = "charging the " + destinationId;
            Report("Charging the " + destinationId + ".");
        }

        // ---- This demo's own actions ---------------------------------------------------------

        [AgentAction("warn_player", "Warn the player and stay where you are.")]
        [Example("A stranger is poking around where they should not be.")]
        private void WarnPlayer()
        {
            Clear();
            Halt();
            if (Player != null) Face(Player.position);
            activity = "warning the player";
            Report("Warning the player.");
        }

        // A quiet moment is, by the game's own rule, one with nothing to warn about.
        [Available("warn_player")]
        private bool CanWarn(AgentContext ctx) => Asker(ctx) != "quiet";

        [AgentAction("block_path", "Stand in the entrance of a place so nobody gets in.", RequiresTarget = true)]
        [Example("Nobody gets into the {0}.", "armory", "lords_keep")]
        private void BlockPath(Transform target)
        {
            Clear();
            var zone = target.GetComponent<RestrictedZone>();
            Walk(zone != null && zone.Entrance != null ? zone.Entrance : target, IdOf(target));
            activity = "blocking the way into the " + destinationId;
            Report("Blocking the way into the " + destinationId + ".");
        }

        [AgentAction("sound_alarm", "Ring the alarm bell so villagers take cover.")]
        [Example("Raiders are coming over the hill!")]
        private void SoundAlarm()
        {
            if (Alarm != null) Alarm.Ring(this);
            Report("Rang the alarm bell.");
        }

        // The game sees a wolf and knows how far away it is; a raid or a fire it does not model, so
        // the player can still report one.
        [Available("sound_alarm")]
        private bool CanSoundAlarm(AgentContext ctx) => Alarm != null && !Alarm.IsSounding &&
            (Asker(ctx) == "player" || ctx.State.TryGetValue(AgentMind.WolfMetres, out var d) && (float)d <= AlarmWolfDistance);

        [AgentAction("patrol", "Walk a round of the village, watching for trouble.")]
        [Example("Your watch at the gate still has hours to run.")]
        private void Patrol()
        {
            Clear();
            if (PatrolRoute == null || PatrolRoute.Length == 0) return;
            IsPatrolling = true;
            patrolIndex = NearestPatrolPoint();
            Walk(PatrolRoute[patrolIndex], IdOf(PatrolRoute[patrolIndex]));
            activity = "patrolling the village";
            Report("Starting a patrol.");
        }

        // Patrol fills the time when nothing is happening, so it is offered on the quiet timer and
        // nowhere else; anything else that asks breaks a patrol off first (Attend).
        [Available("patrol")]
        private bool CanPatrol(AgentContext ctx) =>
            Asker(ctx) == "quiet" && !IsPatrolling && !IsFollowing && PatrolRoute != null && PatrolRoute.Length > 0;

        private static string Asker(AgentContext ctx) => ctx.State.TryGetValue(AgentMind.AskedBy, out var source) ? source as string : null;

        // ---- What only the guard knows ------------------------------------------------------

        public override IEnumerable<string> Facts()
        {
            yield return IsFollowing ? "You are following the player." : "You are not following the player.";
            if (Alarm != null && Alarm.IsSounding) yield return "The alarm bell is ringing.";
        }

        /// <summary>
        /// Breaks off a patrol so the guard can attend to whatever is asking. A patrol is what the
        /// guard does while nothing happens, so anything but a quiet moment stops it.
        /// </summary>
        public override void Attend(string source)
        {
            if (source == "quiet" || !IsPatrolling) return;
            Clear();
            Halt();
            activity = "standing still, your patrol broken off";
            Report("Breaking off the patrol.");
        }

        /// <summary>
        /// A guard never attacks the village's own people: a guard rule, not availability, because it
        /// depends on the target the model picks. <c>attack_target</c> stays offered for the wolf and
        /// the training dummy, and an attack on a villager or the other guard is contained.
        /// </summary>
        public override IEnumerable<IDecisionGuard> Guards()
        {
            yield return new DelegateGuard(NeverAttackVillagers, input =>
            {
                var d = input.Decision;
                if (d.ActionId != "attack_target" || !input.Context.Targets.TryGet(d.TargetId, out var target)) return GuardVerdict.Pass;
                var targetable = (target as Component)?.GetComponentInParent<Targetable>();
                return targetable != null && targetable.Category == "person"
                    ? GuardVerdict.RewriteToNone(d.TargetId + " is one of the village's people, and guards never attack them.")
                    : GuardVerdict.Pass;
            });
        }

        public override string RefusalLine(GuardVerdict verdict) =>
            verdict.Guard == NeverAttackVillagers ? "I won't raise my sword against our own."
            : verdict.Guard == "TargetGroundingGuard" ? "There's no such thing here."
            : null;

        // ---- Body ----------------------------------------------------------------------------

        public override void ResetState()
        {
            Clear();
            Halt();
            activity = AtPost;
        }

        private string AtPost => "standing at the " + (Post != null ? IdOf(Post) : "village_gate");

        private NavMeshAgent Navigation => navigation != null ? navigation : navigation = GetComponent<NavMeshAgent>();

        private void Clear()
        {
            IsFollowing = false;
            IsPatrolling = false;
            attacking = false;
            chase = null;
            destination = null;
        }

        private void Walk(Transform target, string id)
        {
            destination = target;
            destinationId = id;
            if (Navigation.isOnNavMesh)
            {
                Navigation.isStopped = false;
                Navigation.SetDestination(target.position);
            }
        }

        private void Halt()
        {
            destination = null;
            if (Navigation.isOnNavMesh)
            {
                Navigation.isStopped = true;
                Navigation.ResetPath();
            }
        }

        protected override void Update()
        {
            base.Update();
            if (!Navigation.isOnNavMesh) return;

            if (IsFollowing && Player != null)
            {
                bool far = Vector3.Distance(transform.position, Player.position) > FollowDistance;
                Navigation.isStopped = !far;
                if (far) Navigation.SetDestination(Player.position);
                return;
            }

            if (chase != null)
            {
                var wolf = chase.GetComponent<VillageWolf>();
                if (wolf == null || wolf.IsHidden)
                {
                    activity = "standing where the wolf fled";
                    Clear();
                    Halt();
                    return;
                }
                Navigation.SetDestination(chase.position);
                if (Vector3.Distance(transform.position, chase.position) <= StrikeDistance + 0.5f)
                {
                    wolf.Flee();
                    activity = "standing where the wolf fled";
                    Report("Struck the wolf; it runs for the woods.");
                    Clear();
                    Halt();
                }
                return;
            }

            if (destination != null && !Navigation.pathPending &&
                Navigation.remainingDistance <= Navigation.stoppingDistance + 0.3f)
            {
                if (IsPatrolling)
                {
                    patrolIndex = (patrolIndex + 1) % PatrolRoute.Length;
                    Walk(PatrolRoute[patrolIndex], IdOf(PatrolRoute[patrolIndex]));
                    return;
                }
                if (attacking) Report("Struck the " + destinationId + ".");
                activity = attacking ? "standing by the " + destinationId + " after striking it"
                    : Activity.StartsWith("blocking") ? Activity.Replace("blocking the way", "standing in the entrance, blocking the way")
                    : "standing at the " + destinationId;
                destination = null;
                attacking = false;
            }
        }

        private int NearestPatrolPoint()
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < PatrolRoute.Length; i++)
            {
                float d = Vector3.Distance(transform.position, PatrolRoute[i].position);
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }

        private void Face(Vector3 point)
        {
            var flat = point - transform.position;
            flat.y = 0;
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat);
        }
    }
}
