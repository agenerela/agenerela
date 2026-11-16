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
        /// Returnsy a grammar that accepts JSON objects matching <paramref name="schema"/>.
        /// Fields stay in schema order; enum fileds accpet only their listed values --> if {"action" : "talk} is the only listed, then {"action" : "run"} rejected.
        /// A field is null, unnamed or duplicated, or its allowed values are empty --> null, empty strings or duplicated 
        /// </summary>     
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

        private static void AppendFieldSequenceRules(StringBuilder grammar, IReadOnlyList<SchemaField> fields)
        {
            
        }
        
        private static void Validate(IReadOnlyList<SchemaField> fields)
        {
            
        }

        private static void AppendRule(StringBuilder grammar, string name, string expression)
        {
             grammar.Append(name).Append(" ::= ").Append(expression).Append('\n');
        }
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
                }
            }
        }

        private static void AppendJsonUnicodeEscape(StringBuilder output, char value)
        {
            output.Append("\\u");
        }
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
            
        }

        private static void AppendHex8(StringBuilder output, int value)
        {
            
        }
    }    
}