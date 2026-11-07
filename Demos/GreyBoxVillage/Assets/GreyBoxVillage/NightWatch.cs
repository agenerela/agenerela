using System.Collections.Generic;
using Agenerela;
using Demos.Shared;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// The night watch at the village gate, and its two actions. When night falls the gate is shut,
    /// this guard walks to the gate and takes the watch, and the guard it relieves goes off watch to
    /// the campfire; at dawn the gate opens and both go back to their posts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It sits on Wren alone. <see cref="AgentBehaviour"/> registers the <c>[AgentAction]</c> methods of
    /// every component on its GameObject, so <c>open_gate</c> and <c>close_gate</c> are Wren's and
    /// nobody else's. Bram, the evaluation agent, has the same actions by night as by day, which a
    /// test checks: this component is never on him, and he is only sent to sit by the fire.
    /// </para>
    /// <para>
    /// Availability comes from the gate's state, while this guard has the watch: <c>close_gate</c>
    /// while it is open, <c>open_gate</c> while it is shut and someone asks for it, the player or
    /// the captain. A quiet moment or the guard's own arrival never opens the gate.
    /// </para>
    /// </remarks>
    public sealed class NightWatch : MonoBehaviour
    {
        public VillageGuard Guard;
        public AgentMind Mind;
        public VillageGate Gate;
        public DayNightCycle Day;
        [Tooltip("Where the watch stands: inside the gate.")]
        public Transform WatchPost;
        [Tooltip("The guard who keeps the gate by day, and goes off watch at night.")]
        public VillageGuard Relieved;
        [Tooltip("Where the relieved guard spends the night.")]
        public Transform RelievedPost;

        /// <summary>True from nightfall to dawn.</summary>
        public bool OnWatch { get; private set; }

        private bool reportOnArrival;
        private string dayDetail;

        private void OnEnable()
        {
            if (Day != null) Day.NightChanged += Nightfall;
        }

        private void OnDisable()
        {
            if (Day != null) Day.NightChanged -= Nightfall;
        }

        /// <summary>
        /// Night falls (true) or ends (false): the gate is shut or opened, and the watch changes hands.
        /// <see cref="DayNightCycle.NightChanged"/> calls it; tests call it directly. The handover
        /// comes first: a guard chasing or carrying water when it comes drops that and goes.
        /// </summary>
        public void Nightfall(bool night)
        {
            if (night == OnWatch) return;
            OnWatch = night;
            if (night)
            {
                if (Gate != null) Gate.Close();
                Guard.GoToPost(WatchPost, "hurrying to the village_gate to take the night watch", "keeping the night watch at the village_gate", hurry: true);
                if (Relieved != null && RelievedPost != null)
                    Relieved.GoToPost(RelievedPost, "going off watch to the campfire", "off watch, sitting by the campfire");
                reportOnArrival = true;
                Label(true);
                Report("Night falls: the gate is shut, and " + Mind.Name + " takes the watch.");
            }
            else
            {
                if (Gate != null) Gate.Open();
                Guard.GoToPost(Guard.Post, null, null);
                if (Relieved != null) Relieved.GoToPost(Relieved.Post, null, null);
                reportOnArrival = false;
                Label(false);
                Report("Dawn: the gate is open, and the watch is over.");
            }
        }

        /// <summary>Off the watch at once, the gate open. Used between tests and by the presenter's reset.</summary>
        public void ResetState()
        {
            OnWatch = false;
            reportOnArrival = false;
            Label(false);
            if (Gate != null) Gate.ResetState();
        }

        // The name tag over the guard says what she keeps: the night watch, or her day post.
        private void Label(bool night)
        {
            var tag = GetComponent<NameTag>();
            if (tag == null) return;
            if (dayDetail == null && !night) return;
            dayDetail ??= tag.Detail;
            tag.Detail = night ? "night watch" : dayDetail;
        }

        private void Update()
        {
            // Asked once at the gate, so the audience sees the watch begin. Not if something else
            // took the guard away first: it never reached the gate.
            if (!reportOnArrival || !OnWatch || Guard.IsMoving) return;
            if (WatchPost != null && Vector3.Distance(Guard.transform.position, WatchPost.position) > 3f)
            {
                reportOnArrival = false;
                return;
            }
            reportOnArrival = false;
            _ = Mind.ReportEventAsync("night", "Event", Arrival, DecisionPriority.Background);
        }

        /// <summary>What the guard is told on reaching the gate at nightfall.</summary>
        public string Arrival => "You have reached the village gate and taken the night watch. The gate is " +
                                 (Gate != null && Gate.IsOpen ? "open." : "shut for the night.");

        /// <summary>What only the watch knows, for the guard's observations.</summary>
        public IEnumerable<string> Facts()
        {
            if (!OnWatch) yield break;
            yield return "You have the night watch at the village gate.";
            if (Gate != null) yield return Gate.IsOpen ? "The village gate stands open." : "The village gate is shut for the night.";
        }

        // ---- Actions -------------------------------------------------------------------------

        [AgentAction("open_gate", "Open the village gate to let someone through.")]
        [Example("Please let me through, I live here.")]
        private void OpenGate()
        {
            Gate.Open();
            Guard.Doing("opening the village_gate");
        }

        [Available("open_gate")]
        private bool CanOpen(AgentContext ctx)
        {
            string asking = AgentMind.Moment.Of(ctx)?.AskedBy;
            return OnWatch && Gate != null && !Gate.IsOpen && (asking == "player" || asking == "captain");
        }

        [AgentAction("close_gate", "Shut the village gate.")]
        [Example("Bar the way, nobody else comes in tonight.")]
        private void CloseGate()
        {
            Gate.Close();
            Guard.Doing("shutting the village_gate");
        }

        [Available("close_gate")]
        private bool CanClose(AgentContext ctx) => OnWatch && Gate != null && Gate.IsOpen;

        private void Report(string message) => Debug.Log("[GreyBoxVillage] " + message, this);
    }
}
