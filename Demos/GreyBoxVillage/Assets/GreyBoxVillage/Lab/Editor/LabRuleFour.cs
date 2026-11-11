using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Agenerela.Evaluation;
using UnityEngine;

namespace Demos.GreyBoxVillage.Lab
{
    /// <summary>
    /// Hard rule 4 for the bench: no scenario line may be a few-shot example the agents are shown, or
    /// an evaluation prompt (UnityProject/Assets/Evaluation, the curated set and the interim one),
    /// and no example may be one of them. A line matches when it is the other once case, spacing and
    /// end punctuation are set aside (<see cref="PromptSetChecks.MatchesExample"/>, where an example's
    /// {0} stands for any target), or when it holds the other whole, for a line of three words or more.
    /// </summary>
    public static class LabRuleFour
    {
        [Serializable] private sealed class PromptFile { public List<Prompt> prompts = new List<Prompt>(); }
        [Serializable] private sealed class Prompt { public string stimulus = ""; }

        /// <summary>The evaluation folders, from the GreyBoxVillage project.</summary>
        public static IEnumerable<string> EvaluationFolders()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../UnityProject/Assets/Evaluation"));
            yield return Path.Combine(root, "prompts");
            yield return Path.Combine(root, "Interim", "prompts");
        }

        /// <summary>Every evaluation prompt's stimulus, from every set's prompt files.</summary>
        public static List<string> EvaluationPrompts() =>
            EvaluationFolders().Where(Directory.Exists)
                .SelectMany(f => Directory.GetFiles(f, "*.json"))
                .SelectMany(f => JsonUtility.FromJson<PromptFile>(File.ReadAllText(f)).prompts)
                .Select(p => p.stimulus)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

        /// <summary>Every line a scenario writes for the bench: what the player says, and the words an event's arg puts in an agent's mouth.</summary>
        public static IEnumerable<(string Where, string Line)> Lines(LabScenarioFile file)
        {
            foreach (var s in file.scenarios)
                for (int i = 0; i < s.turns.Count; i++)
                {
                    var t = s.turns[i];
                    if (!string.IsNullOrWhiteSpace(t.say)) yield return (s.id + " turn " + (i + 1), t.say);
                    // The captain's arg is "place|words"; the merchant's is her words.
                    string words = t.@event == "captain" ? (t.arg ?? "").Split(new[] { '|' }, 2).Last() : t.@event == "merchant" ? t.arg : null;
                    if (!string.IsNullOrWhiteSpace(words)) yield return (s.id + " turn " + (i + 1) + " (words)", words);
                }
        }

        /// <summary>Every problem, one line each: which scenario line matches which prompt or example.</summary>
        public static List<string> Check(IEnumerable<(string Where, string Line)> lines, IEnumerable<string> prompts, IEnumerable<string> examples)
        {
            var problems = new List<string>();
            var promptList = prompts.ToList();
            var exampleList = examples.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct().ToList();
            foreach (var (where, line) in lines)
            {
                foreach (var p in promptList)
                    if (Same(line, p)) problems.Add(where + ": \"" + line + "\" is the evaluation prompt \"" + p + "\"");
                foreach (var e in exampleList)
                    if (Same(line, e)) problems.Add(where + ": \"" + line + "\" is the few-shot example \"" + e + "\"");
            }
            return problems;
        }

        /// <summary>Whether two lines are one for hard rule 4: equal as PromptSetChecks reads them, or one holding the other whole.</summary>
        public static bool Same(string line, string other)
        {
            if (PromptSetChecks.MatchesExample(line, other) || PromptSetChecks.MatchesExample(other, line)) return true;
            string a = Normal(line), b = Normal(other);
            return Holds(a, b) || Holds(b, a);
        }

        // Whether text holds part, three words or more, whole, with any {0} in it standing for a word or more.
        private static bool Holds(string text, string part)
        {
            if (part.Split(' ').Length < 3) return false;
            string pattern = @"(^|\W)" + Regex.Escape(part).Replace(@"\{0}", @"\w[\w ]*") + @"($|\W)";
            return Regex.IsMatch(text, pattern);
        }

        private static string Normal(string s) =>
            Regex.Replace((s ?? "").ToLowerInvariant().Replace('’', '\''), @"\s+", " ").Trim().TrimEnd('.', '!', '?', ',', ';', ':', '"').Trim('"');
    }
}
