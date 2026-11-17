using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// How many of an arm's answers carry each <see cref="ReplyFlag"/>, and how many carry any. A
    /// second score beside the five outcomes, never part of them: <see cref="OutcomeCounts.Accuracy"/>
    /// does not read it.
    /// </summary>
    /// <remarks>
    /// An answer with no decision, a pipeline error, has no statement and is not checked, so
    /// <see cref="Checked"/> can be less than the arm's total. A report written before reply checks
    /// existed reads back with every count 0.
    /// </remarks>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ReplyCounts
    {
        /// <summary>The answers whose statement was checked: every one that had a decision.</summary>
        [JsonProperty("checked")] public int Checked;

        [JsonProperty("echo")] public int Echo;
        [JsonProperty("empty")] public int Empty;
        [JsonProperty("tooLong")] public int TooLong;
        [JsonProperty("leak")] public int Leak;

        /// <summary>The answers with at least one flag. Less than the flags' sum when one answer has several.</summary>
        [JsonProperty("flagged")] public int Flagged;

        /// <summary>The count for <paramref name="flag"/>.</summary>
        public int this[ReplyFlag flag]
        {
            get
            {
                switch (flag)
                {
                    case ReplyFlag.Echo: return Echo;
                    case ReplyFlag.Empty: return Empty;
                    case ReplyFlag.TooLong: return TooLong;
                    case ReplyFlag.Leak: return Leak;
                    default: throw new ArgumentOutOfRangeException(nameof(flag), flag, null);
                }
            }
        }

        internal void Add(IReadOnlyCollection<ReplyFinding> findings)
        {
            Checked++;
            foreach (var flag in findings.Select(f => f.Flag).Distinct())
            {
                switch (flag)
                {
                    case ReplyFlag.Echo: Echo++; break;
                    case ReplyFlag.Empty: Empty++; break;
                    case ReplyFlag.TooLong: TooLong++; break;
                    case ReplyFlag.Leak: Leak++; break;
                    default: throw new ArgumentOutOfRangeException(nameof(flag), flag, null);
                }
            }

            if (findings.Count > 0)
            {
                Flagged++;
            }
        }
    }
}
