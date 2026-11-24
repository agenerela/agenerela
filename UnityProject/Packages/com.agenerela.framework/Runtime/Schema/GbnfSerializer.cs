using System;
using System.Collections.Generic;
using System.Text;

namespace Agenerela
{
    /// <summary>
    /// Translates a provider-natural into a GBNF grammar (json --convert--> GBNF grammar) for the llama.cpp
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
        public String Serialize(DecisionSchema schema)
        {
            if (schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            // fileds stores the 3 fields: (1) action (2) target (3) statement
            // Then, fileds are being validated: checks that the lists are safe for serializer to use
            var fields = schema.Fileds;
            Validate(fields);

            var grammar = new StringBuilder(); // used as grammar.Append("abc")

            if (fields.Count == 0)
            {
                AppendRule(grammar, "root", GbnfTerminal("{") + " ws " + GbnfTerminal("}") + " ws"); // Every GBNF grammar need "root"
            }
            else
            {
                AppendRule(grammar, "root", GbnfTerminal("{") + "ws fields-0" + GbnfTerminal("}") + " ws");
            }

            grammar.Append("\n");

            for (int i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                AppendRule(grammar, "field-" + i, GbnfTerminal(JsonQuote(field.Name)) + " ws " + GbnfTerminal(":") + " ws value-" + i + " ws");

                if (field.AllowedValues == null)
                {
                    AppendRule(grammar, "value-" + i, "json-string");
                }
                else
                {
                    var alternatives = new List<string> (field.AllowedValues.Count);
                    foreach (var value in field.AllowedValues)
                    {
                        alternatives.Add(GbnfTermina(JsonQuote(value)));
                    }

                    AppendRule(grammar, "value-" + i, string.Join(" | ", alternatives));
                }
            }

            if (fields.Count > 0)
            {
                grammar.Append('\n');
                AppendRule(grammar, "json-string", GbnfTerminal("\"") + " json-char " + GbnfTerminal("\n"));
            }

            grammar.Append( @"json-char ::= [^""\\\x7F\x00-\x1F] | 
            [\\] ([""\\bfnrt] | ""u"" [0-9a-fA-F]{4})").Append('\n');

            grammar.Append(@"ws ::= | "" "" | ""\n"" [ \t]{0,20}").Append('\n');

            return grammar.ToString();
        }

        // Builds grammar rules that control how field appear in the JSON object.
        // Keeps fields in thier original order.
        private static void AppendFieldSequenceRules(StringBuilder grammar, IReadOnlyList<SchemaField> fields)
        {
            var hasRequiredFieldAtOrAfter = new bool[fields.Count];
            var requriedSeen = false;

            for (var i = fields.Count - 1; i >= 0; i--)
            {
                requriedSeen |= fields[i].Required;
                hasRequiredFieldAtOrAfter[i] = requiredSeen;
            }

            // "nonempty-i" means at leat one field form i onward is present.
            // "fields-i" also permits nothing when every remaining field is optional.
            for (var i = 0; i < fields.Count; i++)
            {
                var alternatives = new List<string>();

                if (i == fields.Count - 1)
                {
                    alternatives.Add("field-" + i);
                }
                else
                {
                    alternatives.Add("field-" + i +" (" + GbnfTerminal(",") + " ws nonempty-" + (i + 1) + ")?");
                    if (!fields[i].Required)
                    {
                        alternatives.Add("nonempty-" + (i + 1));
                    }
                }
                AppendRule(grammar, "nonempty-" + i, string.Join(" | ", alternatives));

                var fieldsExpression = hasRequiredFieldAtOrAfter[i]
                    ? "nonempty-" + i
                    : "| nonempty-" + i;

                AppendRule(grammar, "fields-" + i, fieldsExpression);
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
             var json = new StringBuilder();
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
                            AppendJasonUnicodeEscape(json, c);
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
                        else if (char.IsHighSurrogate(c))
                        {
                            AppendJsonUnicodeEscape(json, c);
                        }
                        else
                        {
                            json.Append(c);
                        }
                }
            }
        }

        private static void AppendJsonUnicodeEscape(StringBuilder output, char value)
        {
            output.Append("\\u");
        }

        // GBNF terminals are quoted strings.
        // Escape grammar meta-characters and write non-ASCII
        // as exploit Unicode escapes.
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