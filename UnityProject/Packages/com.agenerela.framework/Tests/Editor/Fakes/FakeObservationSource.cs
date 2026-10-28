using System;
using System.Collections.Generic;

namespace Agenerela.Tests
{
    /// <summary>
    /// An observation source with no world behind it: it returns scripted lines, reports scripted
    /// drops, and records every context it is handed, so a test can assert on what the agent asked
    /// and what it sent.
    /// </summary>
    public sealed class FakeObservationSource : IObservationSource, IReportsDroppedObservations
    {
        private readonly List<AgentContext> seen = new List<AgentContext>();

        /// <param name="lines">What every <see cref="Observe"/> returns, until <see cref="Lines"/> changes it.</param>
        public FakeObservationSource(params string[] lines)
        {
            Lines = lines;
        }

        /// <summary>The lines the next <see cref="Observe"/> returns. Null returns null.</summary>
        public string[] Lines;

        /// <summary>What the next <see cref="Observe"/> reports in <see cref="Dropped"/>, as if its own cap cut them.</summary>
        public string[] Drops = Array.Empty<string>();

        /// <summary>When set, <see cref="Observe"/> fails with this: thrown by the call, or after the lines when <see cref="Lazy"/> is set.</summary>
        public Exception Failure;

        /// <summary>
        /// When true, <see cref="Observe"/> returns a lazy sequence, as a <c>yield return</c> body
        /// does: its lines, its <see cref="Dropped"/> and its <see cref="Failure"/> arrive only as
        /// it is enumerated.
        /// </summary>
        public bool Lazy;

        /// <summary>Every context <see cref="Observe"/> was handed, oldest first.</summary>
        public IReadOnlyList<AgentContext> Seen => seen;

        public IReadOnlyList<string> Dropped { get; private set; } = Array.Empty<string>();

        public IEnumerable<string> Observe(AgentContext ctx)
        {
            seen.Add(ctx);

            if (Lazy)
            {
                return Enumerate();
            }

            if (Failure != null)
            {
                throw Failure;
            }

            Dropped = (string[])Drops.Clone();
            return Lines == null ? null : (string[])Lines.Clone();
        }

        private IEnumerable<string> Enumerate()
        {
            Dropped = Array.Empty<string>();

            foreach (string line in Lines ?? Array.Empty<string>())
            {
                yield return line;
            }

            if (Failure != null)
            {
                throw Failure;
            }

            Dropped = (string[])Drops.Clone();
        }
    }
}
