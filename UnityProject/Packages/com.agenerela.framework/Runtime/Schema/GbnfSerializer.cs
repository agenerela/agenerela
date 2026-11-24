using System;
using System.Collections.Generic;
using System.Text;

namespace Agenerela
{
    /// <summary>
    /// Translates a provider-neutral decision schema into a GBNF grammar for llama.cpp.
    /// </summary>
    public sealed class GbnfSerializer
    {
        
        /// <summary>
        /// Returns a grammar that accepts JSON objects matching <paramref name="schema"/>.
        /// Fields stay in schema order; enum fields accept only their listed values.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="schema"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// A field is null, unnamed or duplicated, or its allowed values are empty,
        /// null, empty strings or duplicated.
        /// </exception>
        public string Serialize(DecisionSchema schema)
        {
            if (schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            var fields = schema.Fields;
            Validate(fields);

            if (schema.Pairs != null)
            {
                return SerializePairs(fields, schema.Pairs);
            }

            var grammar = new StringBuilder();
            AppendRule(grammar, "root", "ws flat-object ws");
            AppendRule(grammar, "flat-object",
                GbnfTerminal("{") + " ws fields-0 ws " + GbnfTerminal("}"));

            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                AppendRule(grammar, "field-" + i,
                    GbnfTerminal(JsonQuote(field.Name)) + " ws " + GbnfTerminal(":") + " ws value-" + i);

                if (field.AllowedValues == null)
                {
                    AppendRule(grammar, "value-" + i, "json-string");
                }
                else
                {
                    var alternatives = new List<string>(field.AllowedValues.Count);
                    foreach (var value in field.AllowedValues)
                    {
                        alternatives.Add(GbnfTerminal(JsonQuote(value)));
                    }

                    AppendRule(grammar, "value-" + i, "(" + string.Join(" | ", alternatives) + ")");
                }
            }

            AppendFieldSequenceRules(grammar, fields, string.Empty);
            AppendJsonStringRules(grammar);
            AppendRule(grammar, "ws", "[ \\t\\n]{0,20}");

            return grammar.ToString();
        }

        // A paired schema is a choice of complete objects. Each object's action and target
        // enums are narrowed to that pair, matching JsonSchemaSerializer's root anyOf branches.
        private static string SerializePairs(IReadOnlyList<SchemaField> fields,
            IReadOnlyList<ActionTargets> pairs)
        {
            ValidatePairs(fields, pairs);

            var grammar = new StringBuilder();
            var branches = new List<string>(pairs.Count);
            for (var i = 0; i < pairs.Count; i++)
            {
                branches.Add("pair-" + i + "-object");
            }

            AppendRule(grammar, "root", "ws (" + string.Join(" | ", branches) + ") ws");
            for (var i = 0; i < pairs.Count; i++)
            {
                AppendPairObjectRules(grammar, fields, pairs[i], i);
            }

            AppendJsonStringRules(grammar);
            AppendRule(grammar, "ws", "[ \\t\\n]{0,20}");
            return grammar.ToString();
        }

        private static void AppendPairObjectRules(StringBuilder grammar,
            IReadOnlyList<SchemaField> fields, ActionTargets pair, int pairIndex)
        {
            var prefix = "pair-" + pairIndex + "-";
            AppendRule(grammar, prefix + "object",
                GbnfTerminal("{") + " ws " + prefix + "fields-0 ws " + GbnfTerminal("}"));

            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                var values = field.AllowedValues;
                if (field.Name == DecisionSchema.ActionFieldName)
                {
                    values = pair.Actions;
                }
                else if (field.Name == DecisionSchema.TargetFieldName)
                {
                    values = pair.Targets;
                }

                AppendRule(grammar, prefix + "field-" + i,
                    GbnfTerminal(JsonQuote(field.Name)) + " ws " + GbnfTerminal(":") + " ws " + prefix + "value-" + i);

                if (values == null)
                {
                    AppendRule(grammar, prefix + "value-" + i, "json-string");
                }
                else
                {
                    var alternatives = new List<string>(values.Count);
                    foreach (var value in values)
                    {
                        alternatives.Add(GbnfTerminal(JsonQuote(value)));
                    }

                    AppendRule(grammar, prefix + "value-" + i,
                        "(" + string.Join(" | ", alternatives) + ")");
                }
            }

            AppendFieldSequenceRules(grammar, fields, prefix);
        }

        private static void AppendJsonStringRules(StringBuilder grammar)
        {
            AppendRule(grammar, "json-string",
                GbnfTerminal("\"") + " json-char* " + GbnfTerminal("\""));
            AppendRule(grammar, "json-char",
                @"[^""\\\x7F\x00-\x1F] | ""\\"" ([""\\/bfnrt] | ""u"" [0-9a-fA-F]{4})");
        }

        private static void ValidatePairs(IReadOnlyList<SchemaField> fields,
            IReadOnlyList<ActionTargets> pairs)
        {
            SchemaField action = null;
            SchemaField target = null;
            foreach (var field in fields)
            {
                if (field.Name == DecisionSchema.ActionFieldName) action = field;
                if (field.Name == DecisionSchema.TargetFieldName) target = field;
            }

            if (action?.AllowedValues == null || target?.AllowedValues == null)
            {
                throw new ArgumentException(
                    "A schema with pairs needs an action field and a target field, each with allowed values.",
                    "schema");
            }

            if (pairs.Count == 0)
            {
                throw new ArgumentException(
                    "The schema has an empty list of pairs, which no answer can satisfy.", "schema");
            }

            var pairedActions = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < pairs.Count; i++)
            {
                ValidatePairValues(pairs[i].Actions, action.AllowedValues, $"Pair {i}'s actions");
                ValidatePairValues(pairs[i].Targets, target.AllowedValues, $"Pair {i}'s targets");

                foreach (var id in pairs[i].Actions)
                {
                    if (!pairedActions.Add(id))
                    {
                        throw new ArgumentException(
                            $"The action '{id}' is in two pairs, so its targets are ambiguous.", "schema");
                    }
                }
            }

            foreach (var id in action.AllowedValues)
            {
                if (!pairedActions.Contains(id))
                {
                    throw new ArgumentException(
                        $"The action '{id}' is offered but in no pair, so no answer could choose it.", "schema");
                }
            }
        }

        private static void ValidatePairValues(IReadOnlyList<string> values,
            IReadOnlyList<string> allowed, string description)
        {
            if (values.Count == 0)
            {
                throw new ArgumentException($"{description} are empty, which no answer can satisfy.", "schema");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException(
                        $"{description} include a null or empty value. Gemini rejects \"\" in an enum with HTTP 400.",
                        "schema");
                }

                if (!seen.Add(value))
                {
                    throw new ArgumentException($"{description} include '{value}' twice.", "schema");
                }

                var isAllowed = false;
                foreach (var candidate in allowed)
                {
                    isAllowed |= string.Equals(candidate, value, StringComparison.Ordinal);
                }

                if (!isAllowed)
                {
                    throw new ArgumentException(
                        $"{description} include '{value}', which the flat field does not offer.", "schema");
                }
            }
        }

        // Builds grammar rules that control how fields appear in the JSON object, in their
        // original order.
        private static void AppendFieldSequenceRules(StringBuilder grammar, IReadOnlyList<SchemaField> fields,
            string prefix)
        {
            var hasRequiredFieldAtOrAfter = new bool[fields.Count + 1];
            var requiredSeen = false;

            for (var i = fields.Count - 1; i >= 0; i--)
            {
                requiredSeen |= fields[i].Required;
                hasRequiredFieldAtOrAfter[i] = requiredSeen;
            }

            AppendRule(grammar, prefix + "fields-" + fields.Count, GbnfTerminal(string.Empty));

            // "nonempty-i" requires at least one field from i onward. "fields-i" also permits
            // the empty sequence when every remaining field is optional.
            for (var i = fields.Count - 1; i >= 0; i--)
            {
                var field = prefix + "field-" + i;
                var nonempty = prefix + "nonempty-" + i;
                string expression;

                if (i == fields.Count - 1)
                {
                    expression = field;
                }
                else
                {
                    var suffix = prefix + "nonempty-" + (i + 1);
                    expression = field + " (ws " + GbnfTerminal(",") + " ws " + suffix + ")?";
                    if (!fields[i].Required)
                    {
                        expression += " | " + suffix;
                    }
                }
                AppendRule(grammar, nonempty, "(" + expression + ")");

                var fieldsExpression = hasRequiredFieldAtOrAfter[i]
                    ? nonempty
                    : "(" + nonempty + " | " + GbnfTerminal(string.Empty) + ")";

                AppendRule(grammar, prefix + "fields-" + i, fieldsExpression);
            }
        }
        
        // Validate checks that the field and their allowed values are usable
        // before the serializer builds the grammar.
        private static void Validate(IReadOnlyList<SchemaField> fields)
        {
            if (fields == null)
            {
                throw new ArgumentException("The schema has no field list.", "schema");
            }

            var names = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];

                if (field == null)
                {
                    throw new ArgumentException($"Schema field {i} is null.", "schema");
                }

                if (string.IsNullOrEmpty(field.Name))
                {
                    throw new ArgumentException($"Schema field {i} has no name.", "schema");
                }

                if (!names.Add(field.Name))
                {
                    throw new ArgumentException(
                        $"The schema has two fields named '{field.Name}'. A JSON object cannot hold both.",
                        "schema");
                }

                if (field.AllowedValues == null)
                {
                    continue;
                }

                if (field.AllowedValues.Count == 0)
                {
                    throw new ArgumentException(
                        $"Field '{field.Name}' has an empty list of allowed values, which no answer can satisfy. " +
                        "Use null for a free-form field, or leave the field out.",
                        "schema");
                }

                var values = new HashSet<string>(StringComparer.Ordinal);
                foreach (var value in field.AllowedValues)
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        throw new ArgumentException(
                            $"Field '{field.Name}' allows a null or empty value. " +
                            $"A target field uses '{TargetRegistry.NoTarget}' instead.",
                            "schema");
                    }

                    if (!values.Add(value))
                    {
                        throw new ArgumentException(
                            $"Field '{field.Name}' allows '{value}' twice.",
                            "schema");
                    }
                }
            }
        }

        private static void AppendRule(StringBuilder grammar, string name, string expression)
        {
             grammar.Append(name).Append(" ::= ").Append(expression).Append('\n');
        }

        // Encodes a string as JSON text, then as a GBNF terminal that mathces that text.
        private static string JsonQuote(string value)
        {
            var json = new StringBuilder(value.Length + 2);
            json.Append('"');

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];

                switch (c)
                {
                    case '"':
                        json.Append("\\\"");
                        break;
                    case '\\':
                        json.Append("\\\\");
                        break;
                    case '\b':
                        json.Append("\\b");
                        break;
                    case '\f':
                        json.Append("\\f");
                        break;
                    case '\n':
                        json.Append("\\n");
                        break;
                    case '\r':
                        json.Append("\\r");
                        break;
                    case '\t':
                        json.Append("\\t");
                        break;
                    default:
                        if (c < 0x20 || c == 0x7f)
                        {
                            AppendJsonUnicodeEscape(json, c);
                        }
                        else if (char.IsHighSurrogate(c))
                        {
                            if (i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                            {
                                json.Append(c).Append(value[++i]);
                            }
                            else
                            {
                                AppendJsonUnicodeEscape(json, c);
                            }
                        }
                        else if (char.IsSurrogate(c))
                        {
                            AppendJsonUnicodeEscape(json, c);
                        }
                        else
                        {
                            json.Append(c);
                        }
                        break;
                }
            }

            json.Append('"');
            return json.ToString();
        }

        private static void AppendJsonUnicodeEscape(StringBuilder output, char value)
        {
            output.Append("\\u");
            AppendHex4(output, value);
        }

        // GBNF terminals are quoted strings. Escape grammar metacharacters and non-ASCII
        // characters so ids containing quotes, backslashes or Unicode remain valid literals.
        private static string GbnfTerminal(string value)
        {
            var terminal = new StringBuilder();
            terminal.Append('"');

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];

                if (c == '"')
                {
                    terminal.Append("\\\"");
                }
                else if (c == '\\')
                {
                    terminal.Append("\\\\");
                }
                else if (char.IsHighSurrogate(c) && i + 1 < value.Length &&
                         char.IsLowSurrogate(value[i + 1]))
                {
                    var codePoint = char.ConvertToUtf32(c, value[++i]);
                    terminal.Append("\\U");
                    AppendHex8(terminal, codePoint);
                }
                else if (c > 0x7f)
                {
                    terminal.Append("\\u");
                    AppendHex4(terminal, c);
                }
                else
                {
                    terminal.Append(c);
                }
            }

            terminal.Append('"');
            return terminal.ToString();
        }

        private static void AppendHex4(StringBuilder output, int value)
        {
            const string Hex = "0123456789abcdef";

            output.Append(Hex[(value >> 12) & 0xf]);
            output.Append(Hex[(value >> 8) & 0xf]);
            output.Append(Hex[(value >> 4) & 0xf]);
            output.Append(Hex[value & 0xf]);
        }

        private static void AppendHex8(StringBuilder output, int value)
        {
            const string Hex = "0123456789abcdef";

            output.Append(Hex[(value >> 28) & 0xf]);
            output.Append(Hex[(value >> 24) & 0xf]);
            output.Append(Hex[(value >> 20) & 0xf]);
            output.Append(Hex[(value >> 16) & 0xf]);
            output.Append(Hex[(value >> 12) & 0xf]);
            output.Append(Hex[(value >> 8) & 0xf]);
            output.Append(Hex[(value >> 4) & 0xf]);
            output.Append(Hex[value & 0xf]);
        }
    }    
}
