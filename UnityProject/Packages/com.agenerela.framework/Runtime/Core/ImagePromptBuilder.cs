using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Agenerela
{
    /// <summary>
    /// Composes an image prompt so the game's art direction always leads (DR-024, DR-025): the
    /// developer's style line first, then trusted details, then untrusted text capped and cleaned,
    /// then exclusions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Trusted</b> text is the developer's own, such as a country's name and colours
    /// (<see cref="Detail"/>). <b>Untrusted</b> text is anything a model wrote, such as a
    /// storyteller's line or a generated backstory (<see cref="Untrusted"/>): it is cut to a length,
    /// stripped of prompt syntax (brackets, weights, line breaks), and any sentence in it that tries
    /// to change the style or the medium is dropped. That is a floor, not a guarantee; what keeps a
    /// game's look is that the style line comes first and is never the model's.
    /// </para>
    /// <para>
    /// The framework has no default style: every game names its own.
    /// </para>
    /// </remarks>
    public sealed class ImagePromptBuilder
    {
        /// <summary>The longest untrusted text kept from one call of <see cref="Untrusted"/> unless a length is given.</summary>
        public const int DefaultUntrustedLength = 240;

        // A sentence that tries to steer the picture away from the style, or speaks to the image model.
        private static readonly Regex Steering = new Regex(
            @"\b(ignore|disregard|instead|override|forget|style|styled|in the manner of|art ?style|medium|photo(graph)?s?|photorealistic|realistic|3d render|render(ed)? (as|in)|anime|cartoon|pixel art|negative prompt|prompt|camera|lens|watermark|signature|nsfw|nude|naked)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Characters diffusion front ends read as syntax (weights, alternations, embeddings).
        private static readonly Regex Syntax = new Regex(@"[\(\)\[\]\{\}<>|\\:;=_*#@~`^]", RegexOptions.CultureInvariant);

        private static readonly Regex Spaces = new Regex(@"\s+", RegexOptions.CultureInvariant);

        private readonly string style;
        private readonly List<string> parts = new List<string>();
        private readonly List<string> exclusions = new List<string>();

        /// <param name="style">The game's art direction, such as "an oil painting, late-medieval court portrait, muted earth colours". Leads every prompt.</param>
        /// <exception cref="ArgumentException"><paramref name="style"/> is empty.</exception>
        public ImagePromptBuilder(string style)
        {
            if (string.IsNullOrWhiteSpace(style))
            {
                throw new ArgumentException("Name the game's style: it leads every prompt, and the framework has no default.", nameof(style));
            }

            this.style = Tidy(style);
        }

        /// <summary>Adds the developer's own text, kept as written apart from collapsed whitespace. Empty text adds nothing.</summary>
        public ImagePromptBuilder Detail(string text)
        {
            string t = Tidy(text);
            if (t.Length > 0)
            {
                parts.Add(t);
            }

            return this;
        }

        /// <summary>
        /// Adds text a model wrote, cleaned (<see cref="Clean"/>) and cut to <paramref name="maxLength"/>
        /// characters at a word. Text that cleans to nothing adds nothing.
        /// </summary>
        public ImagePromptBuilder Untrusted(string text, int maxLength = DefaultUntrustedLength)
        {
            string t = Clean(text, maxLength);
            if (t.Length > 0)
            {
                parts.Add(t);
            }

            return this;
        }

        /// <summary>Adds what the picture must not show, such as "text, letters, borders". Written last, as "no ...".</summary>
        public ImagePromptBuilder Exclude(string text)
        {
            string t = Tidy(text);
            if (t.Length > 0)
            {
                exclusions.Add(t);
            }

            return this;
        }

        /// <summary>The prompt: the style, each detail and untrusted part in the order added, then "No ..." for the exclusions.</summary>
        public string Build()
        {
            var text = new StringBuilder(EndSentence(style));
            foreach (string part in parts)
            {
                text.Append(' ').Append(EndSentence(part));
            }

            if (exclusions.Count > 0)
            {
                text.Append(" No ").Append(string.Join(", ", exclusions.Select(e => e.TrimEnd('.')))).Append('.');
            }

            return text.ToString();
        }

        /// <summary>
        /// Untrusted text made safe to follow a style line: prompt syntax and control characters
        /// removed, whitespace collapsed, every sentence that tries to change the style or speaks to
        /// the image model dropped, and the rest cut to <paramref name="maxLength"/> at a word.
        /// </summary>
        public static string Clean(string text, int maxLength = DefaultUntrustedLength)
        {
            if (string.IsNullOrEmpty(text) || maxLength <= 0)
            {
                return "";
            }

            var chars = text.Where(c => !char.IsControl(c) || char.IsWhiteSpace(c)).ToArray();
            string flat = Spaces.Replace(Syntax.Replace(new string(chars), " "), " ").Trim();
            var kept = Regex.Split(flat, @"(?<=[.!?])\s+")
                .Select(s => s.Trim())
                .Where(s => s.Length > 0 && !Steering.IsMatch(s));
            string joined = string.Join(" ", kept);
            if (joined.Length <= maxLength)
            {
                return joined;
            }

            int cut = joined.LastIndexOf(' ', maxLength);
            return (cut > maxLength / 2 ? joined.Substring(0, cut) : joined.Substring(0, maxLength)).TrimEnd(',', ' ', '-');
        }

        private static string Tidy(string text) => Spaces.Replace(text ?? "", " ").Trim();

        private static string EndSentence(string text) =>
            text.EndsWith(".", StringComparison.Ordinal) || text.EndsWith("!", StringComparison.Ordinal) || text.EndsWith("?", StringComparison.Ordinal)
                ? text
                : text + ".";
    }
}
