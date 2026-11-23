using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using UnityEngine;

namespace Agenerela
{
    /// <summary>
    /// A closed choice an action asks for beside its target, such as a strength (<c>minor</c> or
    /// <c>major</c>) or a count (<c>1</c>, <c>2</c> or <c>3</c>): its name, what it means, and the
    /// values the model may pick (DR-023, first step).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Declared in <see cref="ActionDefinition.Parameters"/>, on an asset or with
    /// <see cref="AgentParameterAttribute"/>. An action that declares one is always offered in the
    /// paired schema (DR-020), its parameters after <c>target</c> and before <c>statement</c>: a value
    /// placed after the statement was written into it in 3 of 6 replies (findings, 14 November 2026).
    /// The model reads the values in the prompt's action list, which a call that offers such an action
    /// always sends, since a local model never reads the schema.
    /// </para>
    /// <para>
    /// Every value is a string in the schema. A parameter whose values all read as whole numbers is a
    /// number list (<see cref="IsNumber"/>), and a handler may take it as an <c>int</c>. An open number
    /// range and a free string are not supported: a small model invents numbers.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class ActionParameter
    {
        /// <summary>snake_case, the key the model writes the value under, such as <c>strength</c>.</summary>
        [Tooltip("snake_case. The key the model writes the value under, such as 'strength'.")]
        [JsonProperty("name")] public string Name;

        /// <summary>What the value means, shown beside the values in the prompt's action list. Optional.</summary>
        [Tooltip("Optional. What the value means, such as 'how hard it strikes'. Shown in the action list.")]
        [JsonProperty("description", NullValueHandling = NullValueHandling.Ignore)] public string Description;

        /// <summary>The values the model may pick, in the order the schema and the prompt list them. At least one.</summary>
        [Tooltip("The values the model may pick, such as 'minor' and 'major', or '1', '2' and '3'.")]
        [JsonProperty("values")] public string[] Values;

        /// <summary>
        /// The value a decision takes when the reply leaves this parameter out or names a value not
        /// offered, recorded in <see cref="DecisionTelemetry.ArgumentsDefaulted"/>. Empty, the default,
        /// means none: such a reply is rejected by <see cref="SchemaLegalityGuard"/>.
        /// </summary>
        [Tooltip("Optional. The value used when the reply leaves this out or names one not offered. " +
                 "Leave empty to reject such a reply instead.")]
        [JsonProperty("default", NullValueHandling = NullValueHandling.Ignore)] public string Default;

        /// <summary>
        /// The value this action's example shows, so the few-shot block demonstrates the field. Empty,
        /// the default, shows <see cref="Default"/>, else the first value offered.
        /// </summary>
        [Tooltip("Optional. The value this action's example shows. Never an evaluation prompt's answer.")]
        [JsonProperty("exampleValue", NullValueHandling = NullValueHandling.Ignore)] public string ExampleValue;

        /// <summary>Whether <see cref="Default"/> names a value.</summary>
        [JsonIgnore] public bool HasDefault => !string.IsNullOrEmpty(Default);

        /// <summary>
        /// Whether every value reads as a whole number in the invariant culture, so a handler may take
        /// this parameter as an <c>int</c>.
        /// </summary>
        [JsonIgnore]
        public bool IsNumber
        {
            get
            {
                if (Values == null || Values.Length == 0) return false;
                foreach (string value in Values)
                {
                    if (!TryReadNumber(value, out _)) return false;
                }

                return true;
            }
        }

        /// <summary>Whether <paramref name="value"/> is one of <see cref="Values"/>, compared ordinally.</summary>
        public bool Allows(string value)
        {
            if (Values == null || value == null) return false;
            foreach (string allowed in Values)
            {
                if (string.Equals(allowed, value, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        // A field name the decision already uses, or will in DR-023's second step.
        private static bool IsReserved(string name)
        {
            return name == DecisionSchema.ActionFieldName || name == DecisionSchema.TargetFieldName ||
                   name == DecisionSchema.StatementFieldName || name == "also_targets";
        }

        /// <summary>
        /// Whether this parameter can be sent: a valid snake_case name that is not a field of the
        /// decision, at least one value, none empty or repeated, none with a comma, a control
        /// character or whitespace at either end, and a default and an example value that are among
        /// the values. Gemini rejects <c>""</c> in an enum, so an empty value is refused.
        /// </summary>
        public static bool Validate(ActionParameter parameter, out string problem)
        {
            if (parameter == null)
            {
                problem = "A parameter is null.";
                return false;
            }

            if (!ActionDefinition.ValidateId(parameter.Name, out problem))
            {
                problem = "Parameter name: " + problem;
                return false;
            }

            if (IsReserved(parameter.Name))
            {
                problem = $"Parameter name '{parameter.Name}' is a field of the decision itself.";
                return false;
            }

            if (parameter.Values == null || parameter.Values.Length == 0)
            {
                problem = $"Parameter '{parameter.Name}' has no values, so no answer could give one.";
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in parameter.Values)
            {
                if (string.IsNullOrEmpty(value) || value.Trim().Length == 0)
                {
                    problem = $"Parameter '{parameter.Name}' has an empty value. Gemini rejects \"\" in an enum.";
                    return false;
                }

                // The prompt lists the values as "a, b or c", on one line.
                if (value != value.Trim() || value.IndexOf(',') >= 0 || HasControlCharacter(value))
                {
                    problem = $"Parameter '{parameter.Name}' has the value '{value}': a value cannot start or end with " +
                              "whitespace, or hold a comma or a control character.";
                    return false;
                }

                if (!seen.Add(value))
                {
                    problem = $"Parameter '{parameter.Name}' lists '{value}' twice.";
                    return false;
                }
            }

            if (parameter.HasDefault && !parameter.Allows(parameter.Default))
            {
                problem = $"Parameter '{parameter.Name}' defaults to '{parameter.Default}', which is not one of its values.";
                return false;
            }

            if (!string.IsNullOrEmpty(parameter.ExampleValue) && !parameter.Allows(parameter.ExampleValue))
            {
                problem = $"Parameter '{parameter.Name}' shows '{parameter.ExampleValue}' in its example, which is not one of its values.";
                return false;
            }

            problem = null;
            return true;
        }

        private static bool HasControlCharacter(string value)
        {
            foreach (char c in value)
            {
                if (char.IsControl(c)) return true;
            }

            return false;
        }

        /// <summary>A copy, its arrays copied too, so a registered definition never shares one with its source.</summary>
        public ActionParameter Copy()
        {
            return new ActionParameter
            {
                Name = Name,
                Description = Description,
                Values = Values == null ? null : (string[])Values.Clone(),
                Default = Default,
                ExampleValue = ExampleValue,
            };
        }

        // A whole number written plainly: an optional minus and digits, read in the invariant culture.
        internal static bool TryReadNumber(string value, out int number)
        {
            return int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number);
        }
    }
}
