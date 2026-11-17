using System;
using System.Collections.Generic;
using System.Globalization;
using Agenerela.Providers;

namespace Agenerela
{
    /// <summary>
    /// The second built-in guard (build plan §2.5, DR-016): did the model want a target that is not
    /// on the list? A model forced to answer from the list swaps a missing target for a legal one,
    /// so "Attack the dragon" attacks the training dummy, and looks obedient doing it. Its own token
    /// probabilities show the swap: <see cref="TargetGrounding.Read"/> reads how much of them stayed
    /// on the list, and below 0.8 this guard turns the decision into <c>none</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On by default, on every decision, whoever calls: probability that went to something absent
    /// is wrong either way, and a model torn between two listed targets keeps all of it on the list.
    /// <see cref="DecideOptions.CheckTarget"/> turns it off for one call.
    /// </para>
    /// <para>
    /// It records the reading on every decision it sees, in
    /// <see cref="DecisionTelemetry.TargetGrounding"/> and
    /// <see cref="DecisionTelemetry.TargetGroundingStatus"/>, and acts only when the chosen action
    /// needs a target, as the probe did. A reply without token probabilities passes, recorded as
    /// <see cref="GroundingStatus.NoProbabilities"/>, never as checked. So does one whose target it
    /// could not read. It never runs in <see cref="Agent.Execute"/>, which has no reply to read.
    /// </para>
    /// <para>
    /// The threshold is <see cref="DecideOptions.TargetGroundingThreshold"/>, <see cref="DefaultThreshold"/>
    /// unless a call sets it (#244). The share counts only the alternatives the provider reports, so
    /// a model whose probability is spread over more listed targets than that reads low without
    /// wanting anything off the list; <see cref="OllamaSettings.TokenAlternatives"/> says how many
    /// Ollama reports. Any change to the default or to the signal needs an A/B run with a control arm
    /// (§2.5, Limits).
    /// </para>
    /// </remarks>
    public sealed class TargetGroundingGuard : IDecisionGuard
    {
        /// <summary>
        /// The share below which a decision is refused unless a call sets
        /// <see cref="DecideOptions.TargetGroundingThreshold"/>: 0.8, as DR-016 measured it.
        /// </summary>
        public const float DefaultThreshold = 0.8f;

        /// <summary>Always <c>"TargetGroundingGuard"</c>.</summary>
        public string Name => nameof(TargetGroundingGuard);

        /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
        public GuardVerdict Inspect(GuardInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            var telemetry = input.Telemetry;
            if (!input.Options.CheckTarget)
            {
                telemetry.TargetGroundingStatus = GroundingStatus.Off;
                telemetry.TargetGrounding = null;
                return GuardVerdict.Pass;
            }

            var reply = input.Reply;
            var reading = TargetGrounding.Read(reply?.Text, reply?.TokenProbabilities, TargetIds(input.Request));
            bool scored = reading.Status == GroundingStatus.Scored;
            telemetry.TargetGroundingStatus = reading.Status;
            telemetry.TargetGrounding = scored ? reading.Share : (float?)null;

            float threshold = input.Options.TargetGroundingThreshold;
            if (!scored || reading.Share >= threshold || !NeedsTarget(input))
            {
                return GuardVerdict.Pass;
            }

            return GuardVerdict.RewriteToNone(Reason(reading, threshold), NotHere);
        }

        // The target enum the model was offered, or null when the schema had no target field.
        private static IReadOnlyCollection<string> TargetIds(DecisionRequest request)
        {
            if (request == null || request.Schema == null)
            {
                return null;
            }

            foreach (var field in request.Schema.Fields)
            {
                if (field.Name == DecisionSchema.TargetFieldName)
                {
                    return field.AllowedValues;
                }
            }

            return null;
        }

        // SchemaLegalityGuard ran first, so an action other than none is registered here.
        private static bool NeedsTarget(GuardInput input)
        {
            return input.Context.Actions.TryGet(input.Decision.ActionId, out var definition, out _)
                && definition.RequiresTarget;
        }

        // What a refusal line's request says instead of Reason, whose probabilities are for a
        // developer reading telemetry (#109). Unmeasured, and it changes only what is said.
        private const string NotHere = "nothing like that is here.";

        private static string Reason(GroundingReading reading, float threshold)
        {
            string reason = string.Format(CultureInfo.InvariantCulture,
                "Only {0:0.###} of the model's probability at the target stayed on the target list (threshold {1:0.###})",
                reading.Share, threshold);

            return reading.Wanted == null ? reason + "." : reason + $"; it wanted to write \"{reading.Wanted}…\".";
        }
    }
}
