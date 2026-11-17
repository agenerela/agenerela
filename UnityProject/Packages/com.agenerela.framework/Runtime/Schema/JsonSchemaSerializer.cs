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
    /// <para>
    /// A schema with <see cref="DecisionSchema.Pairs"/> (DR-020) is written as a root <c>anyOf</c>
    /// with one complete object per pair, each the flat object with its <c>action</c> and
    /// <c>target</c> enums narrowed to the pair's, and nothing else at the root. That is the shape
    /// llama.cpp's grammar builds a union from: it reads <c>oneOf</c> and <c>anyOf</c> alike and
    /// drops any keyword beside them, so a branch that left a field to the root would lose it.
    /// Gemini takes <c>anyOf</c> and not <c>oneOf</c>. A schema without pairs is written exactly as
    /// before.
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

            var pairs = schema.Pairs;
            if (pairs == null)
            {
                Members(json, 1, fields);
            }
            else
            {
                ValidatePairs(fields, pairs);
                Line(json, 1, "\"anyOf\": [");
                for (var p = 0; p < pairs.Count; p++)
                {
                    Line(json, 2, "{");
                    Members(json, 3, Branch(fields, pairs[p]));
                    Line(json, 2, "}" + (p < pairs.Count - 1 ? "," : ""));
                }
                Line(json, 1, "]");
            }

            json.Append('}');
            return json.ToString();
        }

        // The members of one object schema, starting at depth: its type, one string property per
        // field in field order, the required fields, the order again, and no other properties.
        static void Members(StringBuilder json, int depth, IReadOnlyList<SchemaField> fields)
        {
            Line(json, depth, "\"type\": \"object\",");
            Line(json, depth, "\"properties\": {");

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

                Line(json, depth + 1, Quote(field.Name) + ": {");
                for (var m = 0; m < members.Count; m++)
                {
                    Line(json, depth + 2, members[m] + (m < members.Count - 1 ? "," : ""));
                }
                Line(json, depth + 1, "}" + (i < fields.Count - 1 ? "," : ""));
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

            Line(json, depth, "},");
            Line(json, depth, "\"required\": " + Array(required) + ",");
            Line(json, depth, "\"propertyOrdering\": " + Array(ordering) + ",");
            Line(json, depth, "\"additionalProperties\": false");
        }

        // The flat fields with the action and target enums narrowed to one pair's.
        static List<SchemaField> Branch(IReadOnlyList<SchemaField> fields, ActionTargets pair)
        {
            var branch = new List<SchemaField>(fields.Count);
            foreach (var field in fields)
            {
                var values = field.Name == DecisionSchema.ActionFieldName ? pair.Actions
                    : field.Name == DecisionSchema.TargetFieldName ? pair.Targets
                    : field.AllowedValues;

                branch.Add(new SchemaField
                {
                    Name = field.Name,
                    Description = field.Description,
                    AllowedValues = values,
                    Required = field.Required,
                });
            }

            return branch;
        }

        // Pairs a grammar can be built from: each offered action in exactly one pair, and each pair's
        // values drawn from the flat enums, with no empty list, no "" and no repeat, as Validate
        // asks of a field.
        static void ValidatePairs(IReadOnlyList<SchemaField> fields, IReadOnlyList<ActionTargets> pairs)
        {
            SchemaField action = null, target = null;
            foreach (var field in fields)
            {
                if (field.Name == DecisionSchema.ActionFieldName) action = field;
                if (field.Name == DecisionSchema.TargetFieldName) target = field;
            }

            if (action?.AllowedValues == null || target?.AllowedValues == null)
            {
                throw new ArgumentException(
                    "A schema with pairs needs an action field and a target field, each with allowed values.", "schema");
            }

            if (pairs.Count == 0)
            {
                throw new ArgumentException("The schema has an empty list of pairs, which no answer can satisfy.", "schema");
            }

            var paired = new HashSet<string>(StringComparer.Ordinal);
            for (var p = 0; p < pairs.Count; p++)
            {
                CheckValues(pairs[p].Actions, action.AllowedValues, $"Pair {p}'s actions");
                CheckValues(pairs[p].Targets, target.AllowedValues, $"Pair {p}'s targets");

                foreach (string id in pairs[p].Actions)
                {
                    if (!paired.Add(id))
                    {
                        throw new ArgumentException($"The action '{id}' is in two pairs, so its targets are ambiguous.", "schema");
                    }
                }
            }

            foreach (string id in action.AllowedValues)
            {
                if (!paired.Contains(id))
                {
                    throw new ArgumentException(
                        $"The action '{id}' is offered but in no pair, so no answer could choose it.", "schema");
                }
            }
        }

        static void CheckValues(IReadOnlyList<string> values, IReadOnlyList<string> allowed, string what)
        {
            if (values.Count == 0)
            {
                throw new ArgumentException($"{what} are empty, which no answer can satisfy.", "schema");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values)
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException(
                        $"{what} include a null or empty value. Gemini rejects \"\" in an enum with HTTP 400.", "schema");
                }

                if (!seen.Add(value))
                {
                    throw new ArgumentException($"{what} include '{value}' twice.", "schema");
                }

                bool offered = false;
                foreach (string candidate in allowed)
                {
                    offered |= string.Equals(candidate, value, StringComparison.Ordinal);
                }

                if (!offered)
                {
                    throw new ArgumentException($"{what} include '{value}', which the flat field does not offer.", "schema");
                }
            }
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
