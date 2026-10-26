using System;
using System.Collections.Generic;

namespace Agenerela.Tests
{
    /// <summary>
    /// A guard with no rule of its own: it answers with a scripted verdict, or with what a
    /// function makes of the input, and records every input it is handed, so a test can assert
    /// on what reached it and in what order.
    /// </summary>
    public sealed class FakeGuard : IDecisionGuard
    {
        private readonly Func<GuardInput, GuardVerdict> inspect;
        private readonly List<GuardInput> inputs = new List<GuardInput>();

        /// <param name="name">What <see cref="Name"/> reports, which telemetry records.</param>
        /// <param name="inspect">The verdict for an input. Null passes everything.</param>
        public FakeGuard(string name, Func<GuardInput, GuardVerdict> inspect = null)
        {
            Name = name;
            this.inspect = inspect;
        }

        /// <summary>A guard that rewrites every decision to <c>none</c>, for <paramref name="reason"/>.</summary>
        public static FakeGuard Rewriting(string name, string reason = "the fake judged it wrong.")
        {
            return new FakeGuard(name, _ => GuardVerdict.RewriteToNone(reason));
        }

        /// <summary>A guard that rejects every decision, for <paramref name="reason"/>.</summary>
        public static FakeGuard Rejecting(string name, string reason = "the fake rejected it.")
        {
            return new FakeGuard(name, _ => GuardVerdict.Reject(reason));
        }

        public string Name { get; }

        /// <summary>Every input inspected, oldest first.</summary>
        public IReadOnlyList<GuardInput> Inputs => inputs;

        public GuardVerdict Inspect(GuardInput input)
        {
            inputs.Add(input);
            return inspect == null ? GuardVerdict.Pass : inspect(input);
        }
    }
}
