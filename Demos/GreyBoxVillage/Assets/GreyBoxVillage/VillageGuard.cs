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
    /// the call's <see cref="AgentMind.Moment"/>, which <see cref="AgentMind"/> sends with each
    /// decision: the source of the stimulus and the distance of a wolf the guard can see.
    ///
    /// Bram and Wren are both guards: the same body, each with its own post, patrol and profile.
    /// Wren also keeps the night watch and answers trouble: those actions sit on her own
    /// <see cref="NightWatch"/> and <see cref="Responder"/>, so Bram, the evaluation agent, keeps
    /// exactly the actions he had.
    /// </remarks>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class VillageGuard : VillageBody
    {
        /// <summary>The game guard that keeps a guard from attacking the village's own people.</summary>
        public const string NeverAttackVillagers = "never_attack_villagers";

        /// <summary>Where the guard stands by day when nothing else is going on: Bram's gate, Wren's bridge.</summary>
        public Transform Post;
        public VillageAlarm Alarm;
        public Transform[] PatrolRoute;
        [Tooltip("The fire the guard knows about, and carries water to when its Responder fetches water.")]
        public VillageFire Fire;
        [Tooltip("Carried while fetching water; hidden otherwise.")]
        public GameObject Bucket;
        public float FollowDistance = 2.5f;
        public float StrikeDistance = 1.8f;
        [Tooltip("How close a chased thief must be to be caught.")]
        public float CatchDistance = 1.6f;
        [Tooltip("Running speed while chasing; the guard walks at its NavMeshAgent's speed otherwise.")]
        public float ChaseSpeed = 7f;
        [Tooltip("Seconds to fill the bucket at the well, and to throw it on the fire.")]
        public float BucketSeconds = 0.8f;
        [Tooltip("The bell is offered for a wolf the guard can see within this distance, or when the player speaks.")]
        public float AlarmWolfDistance = 15f;

        public bool IsFollowing { get; private set; }
        public bool IsPatrolling { get; private set; }
        public bool IsChasing => chase != null;
        public bool IsFetchingWater => fetching;
        public bool IsMoving => destination != null || chase != null || IsPatrolling || fetching;
        public override bool IsBusy => IsFollowing || IsMoving;
        /// <summary>Where the guard stands now when nothing else is going on: its <see cref="Post"/>, or a night post.</summary>
        public Transform CurrentPost => post != null ? post : Post;

        /// <summary>What the guard is doing, as a clause the observations can use.</summary>
        public override string Activity => activity ??= AtPost;

        private string activity;

        private NavMeshAgent navigation;
        private Transform destination;
        private string destinationId;
        private Transform chase;
        private bool attacking;
        private int patrolIndex;
        private Transform post;
        private string arrivedActivity;
        private bool fetching, carrying;
        private float bucketUntil = -1f;
        private float walkSpeed = -1f;

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
        private bool CanFollow(AgentContext ctx) => !IsFollowing && Asking(ctx) == "player";

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
        private bool CanStop(AgentContext ctx) => IsFollowing && Asking(ctx) == "player";

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
            // Something that runs is run after: the wolf, and a thief making off, which a guard
            // must run to catch at all.
            if (target.GetComponent<VillageWolf>() != null) chase = target;
            if (target.GetComponent<VillageThief>() is VillageThief thief && thief.IsRunning)
            {
                chase = target;
                Run(true);
            }
            Walk(target, IdOf(target));
            activity = "charging the " + destinationId;
            Report("Charging the " + destinationId + ".");
        }

        // ---- This demo's own actions ---------------------------------------------------------

        /// <summary>
        /// Runs after a thief or the wolf until it is caught, flees or gets away. Wren's
        /// <c>chase</c> (<see cref="Responder"/>); Bram has no such action.
        /// </summary>
        public void Chase(Transform target)
        {
            Clear();
            chase = target;
            Run(true);
            Walk(target, IdOf(target));
            activity = "chasing the " + destinationId;
            Report("Chasing the " + destinationId + ".");
        }

        /// <summary>
        /// Carries buckets from the well to <see cref="Fire"/> until it is out. Wren's
        /// <c>fetch_water</c> (<see cref="Responder"/>); Bram has no such action.
        /// </summary>
        public void FetchWater()
        {
            Clear();
            if (Fire == null || Fire.Well == null) return;
            fetching = true;
            carrying = false;
            Walk(Fire.Well, IdOf(Fire.Well));
            activity = "running to the well for water";
            Report("Fetching water for the fire.");
        }

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
        private bool CanWarn(AgentContext ctx) => Asking(ctx) != "quiet";

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

        // The game sees a wolf and knows how far away it is; a raid, or a fire anywhere but the
        // stall, it does not model, so the player can still report one. The stall's fire asks for
        // water (fetch_water), not the bell.
        [Available("sound_alarm")]
        private bool CanSoundAlarm(AgentContext ctx) => Alarm != null && !Alarm.IsSounding &&
            (Asking(ctx) == "player" || AgentMind.Moment.Of(ctx)?.WolfMetres <= AlarmWolfDistance);

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
        // The night watch stays at the gate.
        [Available("patrol")]
        private bool CanPatrol(AgentContext ctx) =>
            Asking(ctx) == "quiet" && !IsPatrolling && !IsFollowing && PatrolRoute != null && PatrolRoute.Length > 0 && !OnNightWatch;

        public override string Duty => OnNightWatch ? "the night watch at the gate" : null;

        private bool OnNightWatch
        {
            get
            {
                var watch = GetComponent<NightWatch>();
                return watch != null && watch.OnWatch;
            }
        }

        // ---- What only the guard knows ------------------------------------------------------

        public override IEnumerable<string> Facts()
        {
            foreach (var fact in base.Facts()) yield return fact;
            yield return IsFollowing ? "You are following the player." : "You are not following the player.";
            if (Alarm != null && Alarm.IsSounding) yield return "The alarm bell is ringing.";
            if (Fire != null && Fire.IsBurning)
                yield return "There is a fire at " + Fire.Where + ": " + Fire.Trips + " of " + Fire.TripsToPutOut + " buckets thrown.";
            var watch = GetComponent<NightWatch>();
            if (watch != null)
                foreach (var fact in watch.Facts()) yield return fact;
        }

        /// <summary>
        /// Sends the guard to a post it keeps from now on, such as the gate for the night watch, or
        /// back to its day <see cref="Post"/>. The activity reads <paramref name="walking"/> on the way
        /// and <paramref name="there"/> on arrival; null for "walking back to" and "standing at" the post.
        /// With <paramref name="hurry"/> it runs there.
        /// </summary>
        public void GoToPost(Transform target, string walking, string there, bool hurry = false)
        {
            if (target == null) return;
            post = target == Post ? null : target;
            Clear();
            Run(hurry);
            Walk(target, IdOf(target));
            activity = walking ?? "walking back to the " + destinationId;
            arrivedActivity = there;
        }

        /// <summary>Sets what the guard is doing, for an action another component runs (the night watch's gate actions).</summary>
        public void Doing(string what) => activity = what;

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
            foreach (var guard in base.Guards()) yield return guard;
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

        private static readonly IReadOnlyList<string> SuggestedLines = System.Array.AsReadOnly(new[]
        { "Walk with me.", "What's going on?", "Head to the market.", "Ring the alarm!", "Wait here." });

        public override IReadOnlyList<string> Suggestions => SuggestedLines;

        // chase runs after something that runs: the thief or the wolf.
        protected override bool TargetFits(string actionId, object target) =>
            actionId == "chase"
                ? target is Component c && (c.GetComponent<VillageThief>() != null || c.GetComponent<VillageWolf>() != null)
                : base.TargetFits(actionId, target);

        public override string RefusalLine(GuardVerdict verdict) => base.RefusalLine(verdict) ??
            (verdict.Guard == NeverAttackVillagers ? "I won't raise my sword against our own."
            : verdict.Guard == "TargetGroundingGuard" ? "There's no such thing here."
            : null);

        // ---- Body ----------------------------------------------------------------------------

        public override void ResetState()
        {
            post = null;
            Clear();
            Halt();
            activity = AtPost;
        }

        private string AtPost => "standing at the " + (CurrentPost != null ? IdOf(CurrentPost) : "village_gate");

        private NavMeshAgent Navigation => navigation != null ? navigation : navigation = GetComponent<NavMeshAgent>();

        private void Clear()
        {
            IsFollowing = false;
            IsPatrolling = false;
            attacking = false;
            chase = null;
            destination = null;
            arrivedActivity = null;
            fetching = carrying = false;
            bucketUntil = -1f;
            if (Bucket != null && Bucket.activeSelf) Bucket.SetActive(false);
            Run(false);
        }

        // Runs while chasing; walks otherwise.
        private void Run(bool running)
        {
            if (walkSpeed < 0) walkSpeed = Navigation.speed;
            Navigation.speed = running ? ChaseSpeed : walkSpeed;
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
                UpdateChase();
                return;
            }

            if (fetching)
            {
                UpdateFetching();
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
                    : arrivedActivity != null ? arrivedActivity
                    : Activity.StartsWith("blocking") ? Activity.Replace("blocking the way", "standing in the entrance, blocking the way")
                    : "standing at the " + destinationId;
                destination = null;
                attacking = false;
                arrivedActivity = null;
                Run(false);
            }
        }

        // After the wolf or the thief until it is caught, flees or gets away. The wolf and the
        // attack_target chase it started both end here.
        private void UpdateChase()
        {
            var wolf = chase.GetComponent<VillageWolf>();
            var thief = chase.GetComponent<VillageThief>();
            bool gone = wolf != null ? wolf.IsHidden : thief == null || !thief.IsRunning || !thief.isActiveAndEnabled;
            if (gone)
            {
                activity = wolf != null ? "standing where the wolf fled"
                    : thief != null && thief.State == VillageThief.Phase.Caught && thief.CaughtBy == this ? "holding the thief you caught"
                    : thief != null && thief.State == VillageThief.Phase.Caught ? "standing where the thief was caught"
                    : "standing where the thief got away";
                Clear();
                Halt();
                return;
            }
            Navigation.SetDestination(chase.position);
            float reach = wolf != null ? StrikeDistance + 0.5f : CatchDistance;
            if (Vector3.Distance(transform.position, chase.position) > reach) return;
            if (wolf != null)
            {
                wolf.Flee();
                activity = "standing where the wolf fled";
                Report((attacking ? "Struck" : "Caught up with") + " the wolf; it runs for the woods.");
            }
            else
            {
                thief.Catch(this);
                activity = "holding the thief you caught";
                Report("Caught the thief.");
            }
            Clear();
            Halt();
        }

        // To the well, fill the bucket, to the fire, throw it; again until the fire is out.
        private void UpdateFetching()
        {
            if (Fire == null || !Fire.IsBurning)
            {
                activity = "standing by the market, the fire out";
                Clear();
                Halt();
                return;
            }
            if (bucketUntil >= 0)
            {
                if (Time.time < bucketUntil) return;
                bucketUntil = -1f;
                if (!carrying)
                {
                    carrying = true;
                    if (Bucket != null) Bucket.SetActive(true);
                    Walk(Fire.ThrowSpot != null ? Fire.ThrowSpot : Fire.transform, "fire");
                    activity = "carrying a bucket of water to the fire";
                }
                else
                {
                    carrying = false;
                    if (Bucket != null) Bucket.SetActive(false);
                    bool outNow = Fire.Douse();
                    Report(outNow ? "Threw the last bucket; the fire is out." : "Threw a bucket on the fire.");
                    if (outNow) return;
                    Walk(Fire.Well, IdOf(Fire.Well));
                    activity = "running back to the well for more water";
                }
                return;
            }
            if (destination != null && !Navigation.pathPending &&
                Navigation.remainingDistance <= Navigation.stoppingDistance + 0.4f)
            {
                destination = null;
                bucketUntil = Time.time + BucketSeconds;
                activity = carrying ? "throwing water on the fire" : "filling a bucket at the well";
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
