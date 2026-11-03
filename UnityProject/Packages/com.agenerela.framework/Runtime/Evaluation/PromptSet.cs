using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// A labelled prompt set: the agent it is measured against and its prompts, one file per
    /// category. The set file declares the agent's actions and targets and lists the category
    /// files; the format is in <c>Documentation~/evaluation.md</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A set only means something against one agent. "Go stand at the north tower." is a command
    /// in a village with a north tower and an impossible request without one, so every developer
    /// writes a set for their own agent.
    /// </para>
    /// <para>
    /// Annotators' blind answers are kept apart from the prompts, one file per annotator and
    /// category under <see cref="LabelsFolder"/>. They reach a prompt only when a person settles
    /// it with <see cref="Settle"/>.
    /// </para>
    /// </remarks>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class PromptSet
    {
        /// <summary>The folder beside the set file that holds every annotator's answers.</summary>
        public const string LabelsFolder = "labels";

        /// <summary>The agent's display name.</summary>
        [JsonProperty("agent")] public string Agent;

        /// <summary>Every action the agent can take, <c>none</c> included.</summary>
        [JsonProperty("actions")] public List<DeclaredAction> Actions = new List<DeclaredAction>();

        /// <summary>Every target the agent has registered.</summary>
        [JsonProperty("targets")] public List<DeclaredTarget> Targets = new List<DeclaredTarget>();

        /// <summary>The categories, in the order the window lists them.</summary>
        [JsonProperty("categories")] public List<PromptCategory> Categories = new List<PromptCategory>();

        [JsonExtensionData] public IDictionary<string, JToken> Extra = new Dictionary<string, JToken>();

        /// <summary>The set file's full path; null for a set that was never loaded or saved.</summary>
        public string FilePath { get; private set; }

        /// <summary>The folder the set file is in, which category files and labels are relative to.</summary>
        public string Folder => FilePath == null ? null : Path.GetDirectoryName(FilePath);

        /// <summary>Every prompt, category by category.</summary>
        public IEnumerable<EvalPrompt> Prompts => Categories.SelectMany(c => c.Prompts);

        /// <summary>
        /// Reads the set file at <paramref name="path"/> and every category file it lists. A
        /// category file that does not exist yet is an empty category.
        /// </summary>
        /// <exception cref="InvalidDataException">A file is not valid JSON, or a category file names another category.</exception>
        public static PromptSet Load(string path)
        {
            string fullPath = Path.GetFullPath(path);
            PromptSet set;
            try
            {
                set = PromptSetJson.Read<PromptSet>(PromptSetJson.ReadFile(fullPath));
            }
            catch (JsonException e)
            {
                throw new InvalidDataException($"{fullPath} is not a prompt set: {e.Message}", e);
            }

            if (set == null)
            {
                throw new InvalidDataException($"{fullPath} is empty.");
            }

            set.FilePath = fullPath;
            foreach (var category in set.Categories)
            {
                set.LoadCategory(category);
            }

            return set;
        }

        /// <summary>
        /// A new, empty set for <paramref name="profile"/>: its actions and <c>none</c>, no targets,
        /// and no categories. Nothing is written until <see cref="Save"/>.
        /// </summary>
        public static PromptSet CreateFor(AgentProfile profile, string path)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            var set = new PromptSet
            {
                Agent = profile.Identity != null ? profile.Identity.Name : profile.name,
                FilePath = Path.GetFullPath(path),
            };

            foreach (var asset in profile.Actions)
            {
                if (asset != null && asset.Action != null && asset.Action.Id != ActionRegistry.None)
                {
                    set.Actions.Add(new DeclaredAction { Id = asset.Action.Id, NeedsTarget = asset.Action.RequiresTarget });
                }
            }

            set.Actions.Add(new DeclaredAction { Id = ActionRegistry.None, NeedsTarget = false });
            return set;
        }

        /// <summary>Writes the set file. Category files are written by <see cref="SaveCategory"/>.</summary>
        public void Save()
        {
            RequirePath();
            PromptSetJson.WriteFile(FilePath, this);
        }

        /// <summary>Writes one category's file, creating it and its folder when they do not exist.</summary>
        public void SaveCategory(PromptCategory category)
        {
            RequirePath();
            var file = new PromptSetJson.CategoryFile
            {
                Category = category.Id,
                Prompts = category.Prompts,
                Extra = category.FileExtra,
            };
            PromptSetJson.WriteFile(CategoryPath(category), file);
        }

        /// <summary>The full path of <paramref name="category"/>'s file.</summary>
        public string CategoryPath(PromptCategory category)
        {
            RequirePath();
            return Path.GetFullPath(Path.Combine(Folder, category.File));
        }

        /// <summary>The category with id <paramref name="id"/>, or null.</summary>
        public PromptCategory FindCategory(string id) => Categories.FirstOrDefault(c => c.Id == id);

        /// <summary>The declared action with id <paramref name="id"/>, or null.</summary>
        public DeclaredAction FindAction(string id) => Actions.FirstOrDefault(a => a.Id == id);

        /// <summary>True for a declared target id. <c>no_target</c> is not one.</summary>
        public bool IsRegisteredTarget(string id) => Targets.Any(t => t.Id == id);

        /// <summary>The prompt with id <paramref name="id"/> and its category, or false.</summary>
        public bool TryFind(string id, out EvalPrompt prompt, out PromptCategory category)
        {
            foreach (var c in Categories)
            {
                foreach (var p in c.Prompts)
                {
                    if (p.Id == id)
                    {
                        prompt = p;
                        category = c;
                        return true;
                    }
                }
            }

            prompt = null;
            category = null;
            return false;
        }

        /// <summary>The next unused id in <paramref name="category"/>: <c>target_012</c> after <c>target_011</c>.</summary>
        public string NextId(PromptCategory category)
        {
            string prefix = category.Prefix + "_";
            int highest = 0;
            foreach (var prompt in Prompts)
            {
                if (prompt.Id != null && prompt.Id.StartsWith(prefix, StringComparison.Ordinal)
                    && int.TryParse(prompt.Id.Substring(prefix.Length), out int n))
                {
                    highest = Math.Max(highest, n);
                }
            }

            return $"{prefix}{highest + 1:000}";
        }

        /// <summary>
        /// Every annotator's answers, from <c>labels/&lt;annotator&gt;/&lt;category&gt;.json</c>. The
        /// folder names the annotator and the file names the category.
        /// </summary>
        public IReadOnlyList<AnnotatorLabels> LoadLabels()
        {
            var all = new List<AnnotatorLabels>();
            string root = LabelsPath();
            if (!Directory.Exists(root))
            {
                return all;
            }

            foreach (string folder in Directory.GetDirectories(root).OrderBy(f => f, StringComparer.Ordinal))
            {
                foreach (string file in Directory.GetFiles(folder, "*.json").OrderBy(f => f, StringComparer.Ordinal))
                {
                    var labels = PromptSetJson.Read<AnnotatorLabels>(PromptSetJson.ReadFile(file)) ?? new AnnotatorLabels();
                    labels.Annotator = Path.GetFileName(folder);
                    labels.Category = Path.GetFileNameWithoutExtension(file);
                    labels.Labels = labels.Labels ?? new Dictionary<string, string>();
                    all.Add(labels);
                }
            }

            return all;
        }

        /// <summary>
        /// Records <paramref name="annotator"/>'s answer to one prompt in their own file, and nobody
        /// else's. An answer once given is never replaced: findings.md asks for each person's first
        /// answer, even when they change their mind.
        /// </summary>
        /// <returns>False when the annotator had already answered the prompt; the file is left alone.</returns>
        /// <exception cref="ArgumentException">The annotator's name cannot be a folder name, or the prompt is not in the set.</exception>
        public bool RecordLabel(string annotator, string promptId, LabelAnswer answer)
        {
            if (!ValidateAnnotator(annotator, out string problem)) throw new ArgumentException(problem, nameof(annotator));
            if (!TryFind(promptId, out _, out var category)) throw new ArgumentException($"No prompt '{promptId}' in the set.", nameof(promptId));

            string path = LabelPath(annotator, category.Id);
            var labels = File.Exists(path)
                ? PromptSetJson.Read<AnnotatorLabels>(PromptSetJson.ReadFile(path)) ?? new AnnotatorLabels()
                : new AnnotatorLabels();
            labels.Labels = labels.Labels ?? new Dictionary<string, string>();

            if (labels.Labels.ContainsKey(promptId))
            {
                return false;
            }

            labels.Annotator = annotator;
            labels.Category = category.Id;
            labels.Labels[promptId] = answer.ToString();
            PromptSetJson.WriteFile(path, labels);
            return true;
        }

        /// <summary>The full path of <paramref name="annotator"/>'s file for one category.</summary>
        public string LabelPath(string annotator, string categoryId)
        {
            return Path.Combine(LabelsPath(), annotator, categoryId + ".json");
        }

        /// <summary>
        /// Settles a prompt, which only a person decides (findings.md): the chosen answer becomes the
        /// expected one, every annotator's answer in <paramref name="agreement"/> is kept in the
        /// prompt's <c>labels</c>, and each of them joins <c>labelledBy</c>. An answer already in
        /// <c>labels</c> is never replaced. The label files are not touched. Writes the category file.
        /// </summary>
        /// <param name="note">Why the answer is what it is; null or empty keeps the prompt's note.</param>
        public void Settle(PromptAgreement agreement, string expectedAction, ExpectedTarget expectedTarget, string note = null)
        {
            if (agreement == null) throw new ArgumentNullException(nameof(agreement));
            if (expectedTarget == null) throw new ArgumentNullException(nameof(expectedTarget));
            if (!TryFind(agreement.PromptId, out var prompt, out var category))
            {
                throw new ArgumentException($"No prompt '{agreement.PromptId}' in the set.", nameof(agreement));
            }

            prompt.Labels = prompt.Labels ?? new Dictionary<string, string>();
            foreach (var answer in agreement.Answers)
            {
                if (!prompt.Labels.ContainsKey(answer.Key))
                {
                    prompt.Labels[answer.Key] = answer.Value.ToString();
                }

                if (!prompt.LabelledBy.Contains(answer.Key))
                {
                    prompt.LabelledBy.Add(answer.Key);
                }
            }

            prompt.ExpectedAction = expectedAction;
            prompt.ExpectedTarget = expectedTarget;
            if (!string.IsNullOrEmpty(note))
            {
                prompt.Note = note;
            }

            SaveCategory(category);
        }

        /// <summary>
        /// True when <paramref name="name"/> can be an annotator: not empty, no surrounding spaces,
        /// and usable as a folder name on every platform.
        /// </summary>
        public static bool ValidateAnnotator(string name, out string problem)
        {
            problem = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                problem = "The annotator's name is empty";
            }
            else if (name != name.Trim())
            {
                problem = "The annotator's name starts or ends with a space";
            }
            else if (name == "." || name == ".." || name.IndexOfAny(UnsafeNameChars) >= 0)
            {
                problem = $"'{name}' cannot be a folder name. Leave out / \\ : * ? \" < > |";
            }

            return problem == null;
        }

        // Path.GetInvalidFileNameChars differs by platform; a name made on a Mac must open on Windows.
        private static readonly char[] UnsafeNameChars = "/\\:*?\"<>|".ToCharArray();

        private string LabelsPath()
        {
            RequirePath();
            return Path.Combine(Folder, LabelsFolder);
        }

        private void LoadCategory(PromptCategory category)
        {
            category.Prompts.Clear();
            string path = CategoryPath(category);
            if (!File.Exists(path))
            {
                return;
            }

            PromptSetJson.CategoryFile file;
            try
            {
                file = PromptSetJson.Read<PromptSetJson.CategoryFile>(PromptSetJson.ReadFile(path));
            }
            catch (JsonException e)
            {
                throw new InvalidDataException($"{path} is not a category file: {e.Message}", e);
            }

            if (file == null)
            {
                return;
            }

            if (file.Category != category.Id)
            {
                throw new InvalidDataException(
                    $"{path} holds category '{file.Category}', but the set lists it as '{category.Id}'.");
            }

            category.Prompts.AddRange(file.Prompts.Where(p => p != null));
            category.FileExtra = file.Extra;
        }

        private void RequirePath()
        {
            if (FilePath == null)
            {
                throw new InvalidOperationException("The set has no file: load it with PromptSet.Load or make it with CreateFor.");
            }
        }
    }
}
