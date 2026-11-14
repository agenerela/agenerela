using System;
using Newtonsoft.Json;

namespace Agenerela
{
    /// <summary>
    /// One statement a guard replaced while keeping the move (#266 item 4,
    /// <see cref="GuardVerdict.ReplaceStatement"/>): which guard, why, what the statement was and what
    /// it became. Recorded in <see cref="DecisionTelemetry.StatementsReplaced"/> and written to the
    /// decision log, so a line the game would otherwise patch in its handler is visible where every
    /// other verdict is.
    /// </summary>
    [Serializable]
    public sealed class ReplacedStatement
    {
        /// <summary>The guard's name, as <see cref="DecisionTelemetry.GuardsFired"/> records it.</summary>
        [JsonProperty("guard")] public string Guard;

        /// <summary>The guard's reason, word for word.</summary>
        [JsonProperty("reason")] public string Reason;

        /// <summary>The statement before the guard replaced it: the model's, or an earlier guard's replacement.</summary>
        [JsonProperty("replaced")] public string Replaced;

        /// <summary>What it became; empty when the guard dropped it.</summary>
        [JsonProperty("statement")] public string Statement;

        public ReplacedStatement()
        {
        }

        public ReplacedStatement(string guard, string reason, string replaced, string statement)
        {
            Guard = guard;
            Reason = reason;
            Replaced = replaced;
            Statement = statement;
        }

        /// <summary>"guard: reason", the form a log line reads easily.</summary>
        public override string ToString()
        {
            return $"{Guard}: {Reason}";
        }
    }
}
