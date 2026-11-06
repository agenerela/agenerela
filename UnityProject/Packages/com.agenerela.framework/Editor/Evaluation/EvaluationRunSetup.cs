using System;
using System.Collections.Generic;
using System.Linq;
using Agenerela.Evaluation;
using Agenerela.Providers;

namespace Agenerela.Editor.Evaluation
{
    /// <summary>
    /// What the Evaluation Report window's <b>Run A/B</b> will run (#146): the set, the profile and
    /// the two arms, why it cannot run yet, and the checks the run makes, in plain words. Kept apart
    /// from the drawing so it can be tested.
    /// </summary>
    internal sealed class EvaluationRunSetup
    {
        /// <summary>The default names for two arms on one config, the control first (measuring.md, rule 1).</summary>
        internal const string ControlName = "control", GuardsOnName = "guards-on";

        internal PromptSet Set;

        /// <summary>Why the chosen set did not load; null when it did or none is chosen.</summary>
        internal string SetError;

        internal AgentProfile Profile;
        internal readonly Arm A = new Arm(), B = new Arm();

        /// <summary>The order the person chose; two arms on two models always run one after the other.</summary>
        internal EvalArmOrder ChosenOrder = EvalArmOrder.Interleaved;

        /// <summary>One arm as the window holds it: a config, a name and the grounding guard's switch.</summary>
        internal sealed class Arm
        {
            internal ProviderConfig Config;
            internal string Name = "";
            internal bool CheckTarget = true;
        }

        /// <summary>The model an Ollama config asks for; null for any other provider, which cannot say.</summary>
        internal static string ModelOf(ProviderConfig config)
        {
            return config is OllamaProviderConfig ollama ? ollama.Model : null;
        }

        /// <summary>
        /// True when both arms are on Ollama and on two different models: they cannot be
        /// interleaved, since both would be resident (hard rule 3), so they run one after the other.
        /// </summary>
        internal bool TwoModels
        {
            get
            {
                string a = ModelOf(A.Config), b = ModelOf(B.Config);
                return a != null && b != null && !SameModel(a, b);
            }
        }

        /// <summary>The order the run will use.</summary>
        internal EvalArmOrder Order => TwoModels ? EvalArmOrder.Sequential : ChosenOrder;

        // As Ollama names them: "qwen3.5:2b" and "Qwen3.5:2B" are one model, and so are "x" and "x:latest".
        internal static bool SameModel(string a, string b)
        {
            return string.Equals(Normal(a), Normal(b), StringComparison.OrdinalIgnoreCase);

            string Normal(string m)
            {
                m = (m ?? "").Trim();
                return m.EndsWith(":latest", StringComparison.OrdinalIgnoreCase) ? m.Substring(0, m.Length - ":latest".Length) : m;
            }
        }

        /// <summary>
        /// The names and switches two arms start with once their configs are chosen: one config
        /// twice is a control without the grounding guard against the same with it; two configs
        /// are named after themselves, both with the guard on.
        /// </summary>
        internal void NameArmsFromConfigs()
        {
            if (A.Config != null && A.Config == B.Config)
            {
                A.Name = ControlName;
                A.CheckTarget = false;
                B.Name = GuardsOnName;
                B.CheckTarget = true;
                return;
            }

            A.Name = A.Config != null ? A.Config.name : "";
            B.Name = B.Config != null ? B.Config.name : "";
            A.CheckTarget = B.CheckTarget = true;
            if (A.Name.Length > 0 && string.Equals(A.Name, B.Name, StringComparison.OrdinalIgnoreCase))
            {
                A.Name += "-A";
                B.Name += "-B";
            }
        }

        /// <summary>
        /// Sets arm <paramref name="arm"/>'s config (0 for A, 1 for B). Names and switches nobody has
        /// changed from what <see cref="NameArmsFromConfigs"/> gave follow the new configs; ones a
        /// person typed are kept.
        /// </summary>
        internal void ChangeConfig(int arm, ProviderConfig config)
        {
            if (arm != 0 && arm != 1)
            {
                throw new ArgumentOutOfRangeException(nameof(arm), arm, "0 for arm A, 1 for arm B.");
            }

            bool automatic = NamesAreAutomatic();
            (arm == 0 ? A : B).Config = config;
            if (automatic)
            {
                NameArmsFromConfigs();
            }
        }

        /// <summary>True when both names are empty, or are what <see cref="NameArmsFromConfigs"/> gives the configs chosen now.</summary>
        internal bool NamesAreAutomatic()
        {
            if (string.IsNullOrWhiteSpace(A.Name) && string.IsNullOrWhiteSpace(B.Name))
            {
                return true;
            }

            var probe = new EvaluationRunSetup();
            probe.A.Config = A.Config;
            probe.B.Config = B.Config;
            probe.NameArmsFromConfigs();
            return probe.A.Name == A.Name && probe.B.Name == B.Name
                && probe.A.CheckTarget == A.CheckTarget && probe.B.CheckTarget == B.CheckTarget;
        }

        /// <summary>
        /// Everything the set breaks or disagrees with the profile about, as the runner would refuse
        /// it: empty when it can run, and when no set or profile is chosen yet.
        /// </summary>
        internal IReadOnlyList<string> SetProblems()
        {
            if (Set == null || Profile == null)
            {
                return Array.Empty<string>();
            }

            // The window asks on every repaint, so the answer is kept until the set or the profile
            // changes; the window loads the set afresh each time it gains focus.
            if (checkedSet != Set || checkedProfile != Profile)
            {
                problems = PromptSetChecks.Check(Set, Profile).Select(p => $"{p.PromptId}: {p.Message}")
                    .Concat(PromptSetChecks.CompareWithProfile(Set, Profile))
                    .ToList();
                checkedSet = Set;
                checkedProfile = Profile;
            }

            return problems;
        }

        private PromptSet checkedSet;
        private AgentProfile checkedProfile;
        private IReadOnlyList<string> problems;

        /// <summary>
        /// Why <b>Run A/B</b> is greyed out, in the words the window shows under it; null when it
        /// can run. The first reason only, in the order a person fixes them.
        /// </summary>
        internal string WhyNotRunnable()
        {
            if (SetError != null) return "The set did not load: " + SetError;
            if (Set == null) return "Choose a prompt set first (step 1).";
            if (Profile == null) return "Choose the profile the set is measured against (step 2).";
            if (A.Config == null && B.Config == null) return "Choose the two arms' provider configs (step 3).";
            if (A.Config == null) return "Choose arm A's provider config (step 3).";
            if (B.Config == null) return "Choose arm B's provider config (step 3).";
            if (string.IsNullOrWhiteSpace(A.Name) || string.IsNullOrWhiteSpace(B.Name)) return "Give each arm a name: the report tells them apart by it.";
            if (string.Equals(A.Name.Trim(), B.Name.Trim(), StringComparison.OrdinalIgnoreCase)) return $"Both arms are named '{A.Name.Trim()}': the report tells them apart by name.";

            var problems = SetProblems();
            if (problems.Count > 0)
            {
                return $"The set has {problems.Count} problem{(problems.Count == 1 ? "" : "s")} the runner refuses, listed below. Fix {(problems.Count == 1 ? "it" : "them")} in the Prompt Set window or the profile.";
            }

            if (!Set.Prompts.Any()) return "The set has no prompts yet: write some in the Prompt Set window.";
            return null;
        }

        /// <summary>
        /// Arms that differ in nothing the window sets: a run of one set-up against itself. Allowed,
        /// since it measures the noise, but the window says so.
        /// </summary>
        internal bool ArmsAreTheSame => A.Config != null && A.Config == B.Config && A.CheckTarget == B.CheckTarget;

        /// <summary>What the run will check and do before and between the arms, one plain sentence each.</summary>
        internal IReadOnlyList<string> Checks(int warmUpRequests)
        {
            var lines = new List<string>();
            int prompts = Set == null ? 0 : Set.Prompts.Count();
            lines.Add(Order == EvalArmOrder.Interleaved
                ? $"Interleaved: each of the {prompts} prompts is asked in arm A, then in arm B, so drift over the session hits both alike."
                : TwoModels
                    ? $"One after the other: all {prompts} prompts in arm A, its model unloaded, then all of arm B. The arms are on two models, which would both be loaded if interleaved (hard rule 3). Drift over the session is not shared, so repeat a close result."
                    : $"One after the other, as you chose: all {prompts} prompts in arm A, its model unloaded, then all of arm B. Drift over the session is not shared, so repeat a close result.");

            foreach (var (letter, arm) in new[] { ("A", A), ("B", B) })
            {
                if (arm.Config == null)
                {
                    continue;
                }

                string model = ModelOf(arm.Config);
                lines.Add(model != null
                    ? $"Arm {letter} ({Name(arm)}) on Ollama, {model}: refuses to start while another model is loaded, records Ollama's version, sends {warmUpRequests} warm-up requests that are never scored, then records how much of the model is in GPU memory."
                    : $"Arm {letter} ({Name(arm)}) through {arm.Config.name}: this provider cannot say which models are loaded or its version, so the report says \"not checked\". {warmUpRequests} warm-up requests are still sent.");
            }

            lines.Add("Every prompt gets a fresh agent with no memory, its own state, and the set's targets. Nothing is executed: the run scores what the agent chose.");
            return lines;
        }

        private static string Name(Arm arm) => string.IsNullOrWhiteSpace(arm.Name) ? "unnamed" : arm.Name.Trim();

        /// <summary>The runner for this set-up. Call only once <see cref="WhyNotRunnable"/> is null.</summary>
        /// <exception cref="InvalidOperationException">The set-up cannot run yet; the message says why.</exception>
        internal EvalRunner Build()
        {
            string why = WhyNotRunnable();
            if (why != null)
            {
                throw new InvalidOperationException(why);
            }

            var runner = new EvalRunner(Set, Profile,
                new EvalArm(A.Name, A.Config, new DecideOptions { CheckTarget = A.CheckTarget }),
                new EvalArm(B.Name, B.Config, new DecideOptions { CheckTarget = B.CheckTarget }));
            runner.Order = Order;
            return runner;
        }
    }
}
