using System.Collections.Generic;
using Agenerela;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>How a line above an agent's head reads: its own words, or a guard's refusal.</summary>
    public enum SpeechKind
    {
        Statement,
        Refusal
    }

    /// <summary>
    /// What an <see cref="AgentMind"/> drives: an agent's body in the village. Its <c>[AgentAction]</c>
    /// methods are the game code each action runs, and it tells the mind what only it knows: what it
    /// is doing, the facts it adds to every request, and the rules its game guards enforce.
    /// </summary>
    /// <remarks>
    /// The guards are the first bodies; the captain and the merchant of later slices are others. The
    /// framework sees none of this, only the agent its <see cref="AgentBehaviour"/> builds.
    /// </remarks>
    public abstract class VillageBody : MonoBehaviour
    {
        /// <summary>Set by <see cref="VillageCommands"/>.</summary>
        [System.NonSerialized] public Transform Player;

        private TextMesh speech;
        private float speechUntil;

        /// <summary>What the agent is doing, as a clause the observations can use.</summary>
        public abstract string Activity { get; }

        /// <summary>True while it walks, follows or chases, so its quiet timer waits.</summary>
        public abstract bool IsBusy { get; }

        /// <summary>The facts about this body sent with every request, after "You are ...".</summary>
        public virtual IEnumerable<string> Facts() { yield break; }

        /// <summary>Called before every decision, with who is asking, so the body can drop what it was doing.</summary>
        public virtual void Attend(string source) { }

        /// <summary>The game's own guards over this agent's decisions, added once when its agent is built.</summary>
        public virtual IEnumerable<IDecisionGuard> Guards() { yield break; }

        /// <summary>What the agent says when a guard stops a decision; null says nothing (<see cref="Agent.RefusalStatement"/>).</summary>
        public virtual string RefusalLine(GuardVerdict verdict) => null;

        /// <summary>Forgets what the agent was doing, without moving it. Used between tests.</summary>
        public abstract void ResetState();

        /// <summary>Shows a line above the agent's head for a few seconds. A refusal reads in amber, tagged with the guard.</summary>
        public void Say(string statement, SpeechKind kind = SpeechKind.Statement, string guard = null)
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
                speech.richText = true;
            }
            string text = Wrap(statement.Trim(), 34);
            if (kind == SpeechKind.Refusal)
            {
                speech.color = new Color(1f, 0.74f, 0.32f);
                if (!string.IsNullOrEmpty(guard)) text += "\n<size=30><color=#d9b98a>stopped by " + guard + "</color></size>";
            }
            else speech.color = new Color(1f, 0.96f, 0.86f);
            speech.text = text;
            speech.gameObject.SetActive(true);
            speechUntil = Time.time + 6f;
        }

        /// <summary>Takes down the line above its head, so an older one never stands for a newer decision.</summary>
        public void Hush()
        {
            if (speech != null) speech.gameObject.SetActive(false);
        }

        /// <summary>The line above its head right now, or null. For tests and the board.</summary>
        public string Speech => speech != null && speech.gameObject.activeSelf ? speech.text : null;

        protected virtual void Update()
        {
            if (speech == null || !speech.gameObject.activeSelf) return;
            speech.transform.rotation = Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;
            if (Time.time > speechUntil) speech.gameObject.SetActive(false);
        }

        protected static string IdOf(Transform target)
        {
            var targetable = target.GetComponentInParent<Targetable>();
            return targetable != null && !string.IsNullOrEmpty(targetable.Id) ? targetable.Id : target.name;
        }

        protected void Report(string message) => Debug.Log("[GreyBoxVillage " + name + "] " + message, this);

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
    }
}
