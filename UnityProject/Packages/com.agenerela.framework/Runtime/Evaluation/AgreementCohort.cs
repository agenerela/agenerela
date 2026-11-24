using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// A fixed subset of a set's prompts that a second person labels for agreement (#148), read from
    /// <c>agreement-subset.json</c> beside the set file. Only its prompt ids are read: the file
    /// holds no answers, so a cohort is safe to hand to Label mode.
    /// </summary>
    /// <remarks>
    /// The set grows past its cohort (#147), so Label mode asks a second annotator only the cohort's
    /// prompts while such a file is there, unless they choose the whole set.
    /// </remarks>
    public sealed class AgreementCohort
    {
        /// <summary>The file a set's cohort is read from, in the set file's folder.</summary>
        public const string FileName = "agreement-subset.json";

        private readonly HashSet<string> ids;

        private AgreementCohort(IEnumerable<string> ids, int? issue)
        {
            this.ids = new HashSet<string>(ids);
            Issue = issue;
        }

        /// <summary>The cohort's prompt ids, in no particular order.</summary>
        public IReadOnlyCollection<string> PromptIds => ids;

        /// <summary>How many prompts the cohort names.</summary>
        public int Count => ids.Count;

        /// <summary>The issue the cohort was selected for, when the file says.</summary>
        public int? Issue { get; }

        /// <summary>True when <paramref name="promptId"/> is one of the cohort's.</summary>
        public bool Contains(string promptId) => promptId != null && ids.Contains(promptId);

        /// <summary>The path a cohort for <paramref name="set"/> would have: <see cref="FileName"/> beside its set file.</summary>
        public static string PathFor(PromptSet set)
        {
            return set?.Folder == null ? null : Path.Combine(set.Folder, FileName);
        }

        /// <summary>
        /// The cohort beside <paramref name="set"/>, or null when there is no such file.
        /// </summary>
        /// <exception cref="InvalidDataException">The file is there but is not a cohort: not JSON, or no prompt ids.</exception>
        public static AgreementCohort FindBeside(PromptSet set)
        {
            string path = PathFor(set);
            return path != null && File.Exists(path) ? Load(path) : null;
        }

        /// <summary>Reads a cohort file: <c>{ "categories": [ { "promptIds": [ ... ] } ] }</c>.</summary>
        /// <exception cref="InvalidDataException">Not JSON, or it names no prompt ids.</exception>
        public static AgreementCohort Load(string path)
        {
            Manifest manifest;
            try
            {
                manifest = PromptSetJson.Read<Manifest>(PromptSetJson.ReadFile(path));
            }
            catch (JsonException e)
            {
                throw new InvalidDataException($"{Path.GetFileName(path)} is not a cohort file: {e.Message}", e);
            }

            var ids = (manifest?.Categories ?? new List<Category>())
                .Where(c => c?.PromptIds != null)
                .SelectMany(c => c.PromptIds)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();
            if (ids.Count == 0)
            {
                throw new InvalidDataException($"{Path.GetFileName(path)} names no prompt ids, so it is not a cohort.");
            }

            return new AgreementCohort(ids, manifest.Issue);
        }

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class Manifest
        {
            [JsonProperty("issue")] public int? Issue;
            [JsonProperty("categories")] public List<Category> Categories;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class Category
        {
            [JsonProperty("promptIds")] public List<string> PromptIds;
        }
    }
}
