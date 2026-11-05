using System;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// One annotator's answer to one prompt: an action and a target, written <c>action/target</c>
    /// in label files and in a prompt's <c>labels</c>, such as <c>move_to/village_gate</c>.
    /// </summary>
    public readonly struct LabelAnswer : IEquatable<LabelAnswer>
    {
        public LabelAnswer(string action, string target)
        {
            Action = action;
            Target = target;
        }

        public string Action { get; }

        public string Target { get; }

        /// <summary>Reads <c>action/target</c>. Neither an action id nor a target id holds a <c>/</c>.</summary>
        public static bool TryParse(string text, out LabelAnswer answer)
        {
            answer = default;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            int slash = text.IndexOf('/');
            if (slash <= 0 || slash == text.Length - 1 || text.IndexOf('/', slash + 1) >= 0)
            {
                return false;
            }

            answer = new LabelAnswer(text.Substring(0, slash), text.Substring(slash + 1));
            return true;
        }

        public override string ToString() => $"{Action}/{Target}";

        public bool Equals(LabelAnswer other) => Action == other.Action && Target == other.Target;

        public override bool Equals(object obj) => obj is LabelAnswer other && Equals(other);

        public override int GetHashCode() => ToString().GetHashCode();
    }
}
