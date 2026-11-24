using System;
using System.Collections.Generic;
using System.Linq;

namespace Agenerela
{
    /// <summary>
    /// An observation source that summarises the agent's targets: one line per category instead of
    /// one line per target, each naming its targets by their registered ids, verbatim (build plan
    /// §2.8, #151). "place: bridge (the wooden bridge over the stream), well (the stone well) and
    /// docks (where supply boats unload)." So eleven templated lines become two, for the A/B run
    /// that asks which form a small model reads better.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Deterministic, with no model call.</b> §2.8 allows a summarising source to call a model,
    /// under three conditions. This one needs none of that: the same targets give the same lines,
    /// byte for byte, so it is cached in effect, adds no latency worth measuring, and a run that
    /// uses it varies nothing between prompts but the prompt. Its ids are the registered ones, as
    /// §2.8's second condition asks. An evaluation run records it in its arm
    /// (<c>EvalArmConditions.SummariseTargets</c>), as the third asks.
    /// </para>
    /// <para>
    /// <b>Groups</b> come in the order their first target was registered, and targets keep their
    /// order within a group. A target with no category goes in a group of its own, headed
    /// <see cref="UncategorisedHeading"/>. A group's heading is <see cref="Headings"/>' wording for
    /// its category, or the category followed by a colon. A target that implements
    /// <see cref="IDescribedTarget"/> gets its description in brackets, its first letter lowered and
    /// its end punctuation dropped, unless <see cref="IncludeDescriptions"/> is off.
    /// </para>
    /// <para>
    /// It summarises only what <see cref="AgentContext.Targets"/> holds, so it needs no scene and
    /// changes nothing in the game.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var summary = new TargetSummarySource();
    /// summary.Headings["place"] = "Places nearby:";
    /// agent.ObservationSources.Add(summary);
    /// </code>
    /// </example>
    public sealed class TargetSummarySource : IObservationSource
    {
        /// <summary><see cref="UncategorisedHeading"/>'s default.</summary>
        public const string DefaultUncategorisedHeading = "Here:";

        /// <summary>
        /// How each category's line starts, by category id, such as <c>Places nearby:</c> for
        /// <c>place</c>. Empty by default: a category not named here is headed by its id and a colon.
        /// </summary>
        public Dictionary<string, string> Headings { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>How the line of targets with no category starts; <see cref="DefaultUncategorisedHeading"/> unless set.</summary>
        public string UncategorisedHeading = DefaultUncategorisedHeading;

        /// <summary>Whether each id is followed by its description in brackets; on by default.</summary>
        public bool IncludeDescriptions = true;

        /// <inheritdoc/>
        public IEnumerable<string> Observe(AgentContext ctx)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException(nameof(ctx));
            }

            return Summarise(ctx.Targets.Ids.Select(id =>
            {
                ctx.Targets.TryGet(id, out object target);
                return (id, (target as IDescribedTarget)?.Description, ctx.Targets.CategoryOf(id));
            }));
        }

        /// <summary>
        /// The lines for <paramref name="targets"/>, each an id, its description or null, and its
        /// category or null, in the order they are registered. Blank ids are skipped.
        /// </summary>
        public IReadOnlyList<string> Summarise(IEnumerable<(string Id, string Description, string Category)> targets)
        {
            var groups = new List<(string Category, List<string> Entries)>();
            foreach (var (id, description, category) in targets ?? Enumerable.Empty<(string, string, string)>())
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                string key = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
                int index = groups.FindIndex(g => g.Category == key);
                if (index < 0)
                {
                    groups.Add((key, new List<string>()));
                    index = groups.Count - 1;
                }

                groups[index].Entries.Add(Entry(id.Trim(), description));
            }

            return groups.Select(g => $"{Heading(g.Category)} {Join(g.Entries)}.").ToList();
        }

        private string Heading(string category)
        {
            if (category == null)
            {
                return string.IsNullOrWhiteSpace(UncategorisedHeading) ? DefaultUncategorisedHeading : UncategorisedHeading.Trim();
            }

            return Headings.TryGetValue(category, out string heading) && !string.IsNullOrWhiteSpace(heading)
                ? heading.Trim()
                : category + ":";
        }

        private string Entry(string id, string description)
        {
            if (!IncludeDescriptions || string.IsNullOrWhiteSpace(description))
            {
                return id;
            }

            string text = description.Trim().TrimEnd('.', '!', '?', ';', ',');
            if (text.Length == 0)
            {
                return id;
            }

            // "The wooden bridge" reads as "the wooden bridge" mid-sentence; "NPC" stays as it is.
            if (char.IsUpper(text[0]) && (text.Length == 1 || !char.IsUpper(text[1])))
            {
                text = char.ToLowerInvariant(text[0]) + text.Substring(1);
            }

            return $"{id} ({text})";
        }

        private static string Join(List<string> entries)
        {
            return entries.Count == 1
                ? entries[0]
                : string.Join(", ", entries.Take(entries.Count - 1)) + " and " + entries[entries.Count - 1];
        }
    }
}
