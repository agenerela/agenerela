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
    /// </remarks>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class VillageGuard : MonoBehaviour
    {
        /// <summary>Set by <see cref="VillageCommands"/>.</summary>
        [System.NonSerialized] public Transform Player;
        public VillageAlarm Alarm;
        public Transform[] PatrolRoute;
        public float FollowDistance = 2.5f;
        public float StrikeDistance = 1.8f;

        public bool IsFollowing { get; private set; }
        public bool IsPatrolling { get; private set; }
        public bool IsMoving => destination != null || chase != null || IsPatrolling;

        /// <summary>What the guard is doing, as a clause the observations can use.</summary>
        public string Activity { get; private set; } = "standing at the village_gate";

        private NavMeshAgent navigation;
        private Transform destination;
        private string destinationId;
        private Transform chase;
        private bool attacking;
        private int patrolIndex;
        private TextMesh speech;
        private float speechUntil;

        // ---- The evaluation agent's actions --------------------------------------------------

        [AgentAction("follow_player", "Walk behind the player wherever they go.")]
        [Example("Tag along behind me.")]
        private void Follow()
        {
            Clear();
            IsFollowing = true;
            Activity = "following the player";
            Report("Following the player.");
        }

        [Available("follow_player")]
        private bool CanFollow() => !IsFollowing;

        [AgentAction("stop_following", "Stop following or patrolling and stay where you are.")]
        [Example("You can stay put now.")]
        private void StopFollowing()
        {
            Clear();
            Halt();
            Activity = "standing still";
            Report("Holding position.");
        }

        [Available("stop_following")]
        private bool CanStop() => IsFollowing || IsPatrolling;

        [AgentAction("move_to", "Walk to a place.", RequiresTarget = true)]
        [Example("Go and stand by the {0}.", "well", "bridge")]
        private void MoveTo(Transform target)
        {
            Clear();
            Walk(target, IdOf(target));
            Activity = "walking to the " + destinationId;
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
            Activity = "charging the " + destinationId;
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
            Activity = "warning the player";
            Report("Warning the player.");
        }

        [AgentAction("block_path", "Stand in the entrance of a place so nobody gets in.", RequiresTarget = true)]
        [Example("Nobody gets into the {0}.", "armory", "lords_keep")]
        private void BlockPath(Transform target)
        {
            Clear();
            var zone = target.GetComponent<RestrictedZone>();
            Walk(zone != null && zone.Entrance != null ? zone.Entrance : target, IdOf(target));
            Activity = "blocking the way into the " + destinationId;
            Report("Blocking the way into the " + destinationId + ".");
        }

        [AgentAction("sound_alarm", "Ring the alarm bell so villagers take cover.")]
        [Example("Raiders are coming over the hill!")]
        private void SoundAlarm()
        {
            if (Alarm != null) Alarm.Ring();
            Report("Rang the alarm bell.");
        }

        [Available("sound_alarm")]
        private bool CanSoundAlarm() => Alarm != null && !Alarm.IsSounding;

        [AgentAction("patrol", "Walk a round of the village, watching for trouble.")]
        [Example("Keep an eye on the village while it is calm.")]
        private void Patrol()
        {
            Clear();
            if (PatrolRoute == null || PatrolRoute.Length == 0) return;
            IsPatrolling = true;
            patrolIndex = NearestPatrolPoint();
            Walk(PatrolRoute[patrolIndex], IdOf(PatrolRoute[patrolIndex]));
            Activity = "patrolling the village";
            Report("Starting a patrol.");
        }

        [Available("patrol")]
        private bool CanPatrol() => !IsPatrolling && !IsFollowing && PatrolRoute != null && PatrolRoute.Length > 0;

        // ---- Body ----------------------------------------------------------------------------

        /// <summary>Shows the guard's statement above its head for a few seconds.</summary>
        public void Say(string statement)
        {
            if (string.IsNullOrWhiteSpace(statement)) return;
            if (speech == null)
            {
                var go = new GameObject("speech");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0, 3.1f, 0);
                speech = go.AddComponent<TextMesh>();
                speech.anchor = TextAnchor.LowerCenter;
                speech.alignment = TextAlignment.Center;
                speech.fontSize = 48;
                speech.characterSize = 0.11f;
                speech.color = new Color(1f, 0.92f, 0.6f);
            }
            speech.text = Wrap(statement.Trim(), 34);
            speech.gameObject.SetActive(true);
            speechUntil = Time.time + 6f;
        }

        /// <summary>Forgets what the guard was doing, without moving it. Used between tests.</summary>
        public void ResetState()
        {
            Clear();
            Halt();
            Activity = "standing at the village_gate";
        }

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

        private void Update()
        {
            if (speech != null && speech.gameObject.activeSelf)
            {
                speech.transform.rotation = Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;
                if (Time.time > speechUntil) speech.gameObject.SetActive(false);
            }

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
                    Activity = "standing where the wolf fled";
                    Clear();
                    Halt();
                    return;
                }
                Navigation.SetDestination(chase.position);
                if (Vector3.Distance(transform.position, chase.position) <= StrikeDistance + 0.5f)
                {
                    wolf.Flee();
                    Activity = "standing where the wolf fled";
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
                Activity = attacking ? "standing by the " + destinationId + " after striking it"
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

        private static string IdOf(Transform target)
        {
            var targetable = target.GetComponentInParent<Targetable>();
            return targetable != null && !string.IsNullOrEmpty(targetable.Id) ? targetable.Id : target.name;
        }

        private static string Wrap(string text, int width)
        {
            var words = text.Split(' ');
            var result = new System.Text.StringBuilder();
            int line = 0;
            foreach (var word in words)
            {
                if (line > 0 && line + word.Length + 1 > width) { result.Append('\n'); line = 0; }
                else if (line > 0) { result.Append(' '); line++; }
                result.Append(word);
                line += word.Length;
            }
            return result.ToString();
        }

        private void Report(string message) => Debug.Log("[GreyBoxVillage guard] " + message, this);
    }
}
