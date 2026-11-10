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
            }

        

        }
    }    
}