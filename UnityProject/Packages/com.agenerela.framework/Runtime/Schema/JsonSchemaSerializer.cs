using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Agenerela
{
    /// <summary>
    /// Translates a provider-neutral <see cref="DecisionSchema"/> (#8) into JSON Schema, the
    /// dialect Ollama and the cloud APIs accept and compile into a sampling grammar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A translation layer and nothing more: it adds no field, drops no field and never
    /// reorders them. The order it is given is the order the model generates in, and
    /// <c>action</c> first is worth +11.7 accuracy points (build plan §2.3).
    /// </para>
    /// <para>
    /// <c>propertyOrdering</c> is emitted always, not only for providers whose
    /// <see cref="Providers.ProviderCapabilities.NeedsExplicitPropertyOrdering"/> is set.
    /// Gemini needs the order stated, other consumers ignore a keyword they do not know, and
    /// one output shape keeps one golden file. If a vendor ever rejects the keyword, a
    /// per-dialect options object is where that switch goes.
    /// </para>
    /// <para>
    /// The output is formatted with two-space indentation and <c>\n</c> line endings on
    /// every platform, so the same schema always produces the same string.
    /// </para>
    /// </remarks>
    public sealed class JsonSchemaSerializer
    {
        const string Indent = "  ";

        /// <summary>
        /// Returns <paramref name="schema"/> as a JSON Schema object: one string property per
        /// field in field order, an <c>enum</c> for each field with allowed values, the
        /// required fields listed in <c>required</c>, the order stated again in
        /// <c>propertyOrdering</c>, and <c>additionalProperties</c> false.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="schema"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// The schema could not be sent as it is: a field is null, unnamed or named twice, or
        /// a field's allowed values are empty or contain a null, <c>""</c> or a duplicate.
        /// Gemini answers an empty enum string with HTTP 400, and an empty enum allows no
        /// answer at all, so these are thrown here rather than discovered by a provider.
        /// </exception>
        public string Serialize(DecisionSchema schema)
        {
            if (schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            var fields = schema.Fields;
            Validate(fields);

            var json = new StringBuilder();
            json.Append("{\n");
            Line(json, 1, "\"type\": \"object\",");
            Line(json, 1, "\"properties\": {");

            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                var members = new List<string> { "\"type\": \"string\"" };

                if (field.Description != null)
                {
                    members.Add("\"description\": " + Quote(field.Description));
                }

                if (field.AllowedValues != null)
                {
                    members.Add("\"enum\": " + Array(field.AllowedValues));
                }

                Line(json, 2, Quote(field.Name) + ": {");
                for (var m = 0; m < members.Count; m++)
                {
                    Line(json, 3, members[m] + (m < members.Count - 1 ? "," : ""));
                }
                Line(json, 2, "}" + (i < fields.Count - 1 ? "," : ""));
            }

            var required = new List<string>();
            var ordering = new List<string>();
            foreach (var field in fields)
            {
                ordering.Add(field.Name);
                if (field.Required)
                {
                    required.Add(field.Name);
                }
            }

            Line(json, 1, "},");
            Line(json, 1, "\"required\": " + Array(required) + ",");
            Line(json, 1, "\"propertyOrdering\": " + Array(ordering) + ",");
            Line(json, 1, "\"additionalProperties\": false");
            json.Append('}');
            return json.ToString();
        }

        static void Validate(IReadOnlyList<SchemaField> fields)
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
                            $"Field '{field.Name}' allows a null or empty value. Gemini rejects \"\" in an enum " +
                            $"with HTTP 400; a target field uses '{TargetRegistry.NoTarget}' instead.",
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

        static void Line(StringBuilder json, int depth, string text)
        {
            for (var i = 0; i < depth; i++)
            {
                json.Append(Indent);
            }

            json.Append(text).Append('\n');
        }

        // An inline array, ["a", "b"], so a long enum still reads as one line.
        static string Array(IReadOnlyList<string> values)
        {
            var quoted = new string[values.Count];
            for (var i = 0; i < values.Count; i++)
            {
                quoted[i] = Quote(values[i]);
            }

            return "[" + string.Join(", ", quoted) + "]";
        }

        // Newtonsoft does the escaping, so a quote, backslash or control character in a
        // developer's description still yields valid JSON.
        static string Quote(string value) => JsonConvert.ToString(value);
    }
}
