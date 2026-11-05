using System;
using System.Collections.Generic;
using System.Text;

namespace Agenerela
{

    // Translates a provider-natural into a GBNF grammar (json --convert--> GBNF grammar) for the llama.cpp
 public sealed class GbnfSerializer
    {
        // Returns grammar that accepts JSON objects        
        public String Serialize(DecisionSchema schema)
        {
            if (schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            var fields = schema.Fileds;
            Validate(fields);

            var grammar = new StringBuildr;

        }
    }    
}