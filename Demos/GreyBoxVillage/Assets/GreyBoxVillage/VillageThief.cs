using Agenerela;
using UnityEngine;
using UnityEngine.AI;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// A scripted thief, not an agent: it slips up to Odile's counter, takes a ware and runs for the
    /// eastern road. Odile sees it and is asked, and may call a guard (<c>call_guard</c>); a guard who
    /// can see it running may <c>chase</c> it. Caught, it drops the ware, which goes back on the
    /// counter. If it reaches the road it is gone, and the ware with it until the stall restocks.
    /// </summary>
    /// <remarks>
    /// Its <see cref="Targetable"/> is <c>thief</c>, of category <c>stranger</c>. While it is not out
    /// its GameObject is inactive, as the scene saves it, so no agent can name it, see it or read it
    /// in its observations, in Edit mode too. The steps are public so the EditMode tests, where
    /// nothing walks, can take them one at a time; in Play mode <see cref="Update"/> takes them as the
    /// thief arrives.
    /// </remarks>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class VillageThief : MonoBehaviour
    {
        public enum Phase
        {
            Hidden,
            Approaching,
            Running,
            Caught,
            Escaped
        }

        public VillageCommands Village;
        public VillageMerchant Merchant;
        [Tooltip("Where it appears when the presenter sends it.")]
        public Transform Entry;
        [Tooltip("Where it stands to reach over Odile's counter.")]
        public Transform Counter;
        [Tooltip("Where it gets away: the far end of the eastern road.")]
        public Transform Escape;
        public float SneakSpeed = 2.6f;
        [Tooltip("Slower than a guard's chase, so a guard who sets off in time catches it.")]
        public float RunSpeed = 2.5f;
        [Tooltip("Seconds it stands caught before it is led away.")]
        public float CaughtSeconds = 3f;

        public Phase State { get; private set; } = Phase.Hidden;
        /// <summary>True from the moment it takes the ware until it is caught or gets away.</summary>
        public bool IsRunning => State == Phase.Running;
        /// <summary>The ware it took, while it has it.</summary>
        public Targetable Loot { get; private set; }
        /// <summary>Counts the thefts, so a guard's senses report each one once.</summary>
        public int Theft { get; private set; }
        /// <summary>The guard who caught it last, if any.</summary>
        public VillageGuard CaughtBy { get; private set; }

        private NavMeshAgent agent;
        private float leaveAt;

        private NavMeshAgent Navigation => agent != null ? agent : agent = GetComponent<NavMeshAgent>();

        /// <summary>Out of the village: inactive, so not targetable. Used between tests and by the presenter's reset.</summary>
        public void ResetState()
        {
            State = Phase.Hidden;
            Loot = null;
            CaughtBy = null;
            Stop();
            gameObject.SetActive(false);
        }

        /// <summary>Appears at <see cref="Entry"/> and heads for the counter. The presenter's F3.</summary>
        public void Begin()
        {
            // A thief sent again while one runs: the ware it holds goes back on the counter first.
            if (State == Phase.Running && Loot != null && Merchant != null) Merchant.Recover(Loot);
            ResetState();
            // Placed before it wakes, so its NavMeshAgent lands on the NavMesh there.
            if (Entry != null) transform.position = Entry.position;
            gameObject.SetActive(true);
            State = Phase.Approaching;
            Navigation.speed = SneakSpeed;
            Go(Counter);
            Report("A hooded stranger is making for Odile's stall.");
        }

        /// <summary>Takes the first ware on the counter and runs. Odile sees it, and is asked.</summary>
        public void Grab()
        {
            if (State != Phase.Approaching) return;
            Loot = Merchant != null ? Merchant.FirstOnCounter() : null;
            if (Loot == null)
            {
                // An empty counter: nothing to take, so it slinks off.
                Leave();
                return;
            }
            Merchant.Lose(Loot);
            Theft++;
            State = Phase.Running;
            Navigation.speed = RunSpeed;
            Go(Escape);
            Report("The thief took the " + Loot.Id + " and is running for the eastern_road.");
            var odile = Merchant.GetComponent<AgentMind>();
            if (odile != null)
                _ = odile.ReportEventAsync("thief", "Event",
                    "A hooded stranger has snatched the " + Loot.Id + " from your counter and is running for the eastern_road!",
                    DecisionPriority.Background);
        }

        /// <summary>Caught by a guard: it drops the ware, which goes back on the counter.</summary>
        public void Catch(VillageGuard by)
        {
            if (State != Phase.Running) return;
            State = Phase.Caught;
            CaughtBy = by;
            Stop();
            var ware = Loot;
            Loot = null;
            if (Merchant != null && ware != null) Merchant.Recover(ware);
            leaveAt = Time.time + CaughtSeconds;
            var mind = by != null ? by.GetComponent<AgentMind>() : null;
            string who = mind != null ? mind.Name : by != null ? by.name : "A guard";
            Toast(who + " caught the thief. The " + (ware != null ? ware.Id : "ware") + " is back on Odile's counter.");
            Report(who + " caught the thief.");
        }

        /// <summary>Reached the road with the ware: gone, and the ware with it until the stall restocks.</summary>
        public void GetAway()
        {
            if (State != Phase.Running) return;
            string ware = Loot != null ? Loot.Id : "ware";
            Loot = null;
            State = Phase.Escaped;
            Stop();
            gameObject.SetActive(false);
            Toast("The thief got away with the " + ware + ".");
            Report("The thief got away.");
        }

        private void Update()
        {
            switch (State)
            {
                case Phase.Approaching when Arrived(Counter):
                    Grab();
                    break;
                case Phase.Running when Arrived(Escape):
                    GetAway();
                    break;
                case Phase.Caught when Time.time >= leaveAt:
                    Leave();
                    break;
            }
        }

        private bool Arrived(Transform point)
        {
            if (point == null) return true;
            var nav = Navigation;
            if (nav.isOnNavMesh) return !nav.pathPending && nav.remainingDistance <= nav.stoppingDistance + 0.4f;
            return Vector3.Distance(transform.position, point.position) < 0.8f;
        }

        // Led away, or slinking off: out of the world.
        private void Leave()
        {
            State = Phase.Hidden;
            Stop();
            gameObject.SetActive(false);
        }

        private void Go(Transform point)
        {
            if (point == null || !Navigation.isActiveAndEnabled || !Navigation.isOnNavMesh) return;
            Navigation.isStopped = false;
            Navigation.SetDestination(point.position);
        }

        private void Stop()
        {
            if (!Navigation.isActiveAndEnabled || !Navigation.isOnNavMesh) return;
            Navigation.isStopped = true;
            Navigation.ResetPath();
        }

        private void Toast(string text)
        {
            if (Village != null && Village.Hud != null) Village.Hud.Toast(text, 5f);
        }

        private void Report(string message) => Debug.Log("[GreyBoxVillage] " + message, this);
    }
}
