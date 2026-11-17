using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Agenerela
{
    /// <summary>
    /// Reads a provider's reply into an <see cref="AgentDecision"/>, or says why it cannot. Used by
    /// <see cref="Agent.DecideAsync"/>, which turns a reply this rejects into a
    /// <see cref="DecisionOutcome.PipelineError"/> result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It checks the reply's shape, never its legality. An action or target id that is a non-empty
    /// string is kept as written, even one the request did not offer: the model did answer, just
    /// wrongly, and that is for <see cref="Agent.Execute"/> to refuse and for Phase 3's guards to
    /// contain. It is why <see cref="AgentDecision"/> accepts ids that are not registered (#2), and
    /// why <see cref="DecisionOutcome.PipelineError"/> means a reply that could not be read, not a
    /// wrong one.
    /// </para>
    /// <para>
    /// The reply must be one JSON object, with nothing but whitespace around it. Newtonsoft's reader
    /// is lenient about quoting and comments, and what it reads unambiguously is accepted; a
    /// property named twice is not, since either value could be the answer. Fields are found by
    /// name, so their order does not matter here, though it matters to the model (DR-004).
    /// </para>
    /// </remarks>
    internal static class DecisionParser
    {
        private static readonly JsonLoadSettings Settings = new JsonLoadSettings
        {
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
            LineInfoHandling = LineInfoHandling.Ignore,
        };

        /// <summary>
        /// Reads <paramref name="reply"/>. <c>action</c> must be a non-empty string. <c>target</c>
        /// must be one too, and may be left out only when <paramref name="schema"/> had no target
        /// field, which means no target was registered; the decision's target is then
        /// <see cref="TargetRegistry.NoTarget"/>. <c>statement</c> missing or null is empty, and
        /// otherwise must be a string. Any other field is ignored, and so is a <c>statement</c>
        /// when <paramref name="schema"/> asked for none (<see cref="DecideOptions.AskForStatement"/>,
        /// #266): the decision's statement is then empty, whatever the provider sent.
        /// <para>
        /// The parameters <paramref name="schema"/> asks of the chosen action (DR-023) are read by
        /// name into <see cref="AgentDecision.Arguments"/>, in the schema's order: a string as
        /// written, a whole number as its invariant text. One missing or null is left out, and one of
        /// another type is a problem. Whether a value is one the schema offered is not checked here.
        /// </para>
        /// </summary>
        /// <param name="schema">The schema the request carried, which says whether it asked for a target.</param>
        /// <param name="problem">Why the reply was rejected, or null.</param>
        public static bool TryParse(string reply, DecisionSchema schema, out AgentDecision decision, out string problem)
        {
            decision = null;

            if (!TryReadObject(reply, out var answer, out problem))
            {
                return false;
            }

            if (!ReadId(answer, DecisionSchema.ActionFieldName, out string action, out problem))
            {
                return false;
            }

            string target = TargetRegistry.NoTarget;
            // A null target reads like a missing one, as a null statement reads like an empty one.
            var targetToken = answer[DecisionSchema.TargetFieldName];
            if ((targetToken != null && targetToken.Type != JTokenType.Null) || AsksForTarget(schema))
            {
                if (!ReadId(answer, DecisionSchema.TargetFieldName, out target, out problem))
                {
                    return false;
                }
            }

            string statement = "";
            var statementToken = schema.AsksForStatement ? answer[DecisionSchema.StatementFieldName] : null;
            if (statementToken != null && statementToken.Type != JTokenType.Null)
            {
                if (statementToken.Type != JTokenType.String)
                {
                    problem = $"'{DecisionSchema.StatementFieldName}' is not a string.";
                    return false;
                }

                statement = (string)statementToken;
            }

            List<KeyValuePair<string, string>> arguments = null;
            foreach (var parameter in DecisionSchema.ParametersOf(schema, action))
            {
                var token = answer[parameter.Name];
                if (token == null || token.Type == JTokenType.Null)
                {
                    continue;
                }

                string value;
                if (token.Type == JTokenType.String)
                {
                    value = (string)token;
                }
                else if (token.Type == JTokenType.Integer)
                {
                    // A number list's value written bare, 2 for "2": unambiguous, so it is read.
                    value = ((long)token).ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    problem = $"'{parameter.Name}' is not a string.";
                    return false;
                }

                arguments = arguments ?? new List<KeyValuePair<string, string>>();
                arguments.Add(new KeyValuePair<string, string>(parameter.Name, value));
            }

            decision = new AgentDecision(action, target, statement, arguments);
            problem = null;
            return true;
        }

        /// <summary>
        /// Reads the reply to a refusal line's request (#109), by the same rules as
        /// <see cref="TryParse"/>. Unlike a decision's, <c>statement</c> is all this reply is for,
        /// so missing or null it is a problem. An empty one is an answer. Any other field is ignored.
        /// </summary>
        /// <param name="problem">Why the reply was rejected, or null.</param>
        public static bool TryParseStatement(string reply, out string statement, out string problem)
        {
            statement = null;

            if (!TryReadObject(reply, out var answer, out problem))
            {
                return false;
            }

            var token = answer[DecisionSchema.StatementFieldName];
            if (token == null || token.Type == JTokenType.Null)
            {
                problem = $"The reply has no '{DecisionSchema.StatementFieldName}'.";
                return false;
            }

            if (token.Type != JTokenType.String)
            {
                problem = $"'{DecisionSchema.StatementFieldName}' is not a string.";
                return false;
            }

            statement = (string)token;
            problem = null;
            return true;
        }

        // The reply as one JSON object, with nothing but whitespace around it.
        private static bool TryReadObject(string reply, out JObject answer, out string problem)
        {
            answer = null;

            if (string.IsNullOrWhiteSpace(reply))
            {
                problem = "The reply was empty.";
                return false;
            }

            try
            {
                // DateParseHandling.None, or a statement such as "2026-10-10" would come back as a
                // DateTime and be rewritten when read as a string.
                using (var reader = new JsonTextReader(new StringReader(reply)) { DateParseHandling = DateParseHandling.None })
                {
                    var root = JToken.ReadFrom(reader, Settings);

                    // Anything after the object is a second answer, or chatter around the first.
                    if (reader.Read())
                    {
                        problem = "The reply has more after its JSON object.";
                        return false;
                    }

                    answer = root as JObject;
                    if (answer == null)
                    {
                        problem = $"The reply is JSON, but {root.Type} rather than an object.";
                        return false;
                    }
                }
            }
            catch (JsonException e)
            {
                problem = "The reply is not one JSON object. " + e.Message;
                return false;
            }

            problem = null;
            return true;
        }

        // A field that names something: present, a string, and not blank. Kept exactly as written.
        private static bool ReadId(JObject answer, string field, out string id, out string problem)
        {
            id = null;
            var token = answer[field];

            if (token == null)
            {
                problem = $"The reply has no '{field}'.";
                return false;
            }

            if (token.Type != JTokenType.String)
            {
                problem = $"'{field}' is not a string.";
                return false;
            }

            id = (string)token;
            if (string.IsNullOrWhiteSpace(id))
            {
                problem = $"'{field}' is empty.";
                return false;
            }

            problem = null;
            return true;
        }

        private static bool AsksForTarget(DecisionSchema schema)
        {
            foreach (var field in schema.Fields)
            {
                if (field.Name == DecisionSchema.TargetFieldName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
