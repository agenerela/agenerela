using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// The target a prompt expects: one registered id or <see cref="TargetRegistry.NoTarget"/>, or
    /// an open choice of several ids where any of them is right, such as <c>["bridge", "docks"]</c>.
    /// </summary>
    /// <remarks>
    /// Written as a JSON string when there is one id and as a list for an open choice. A list of
    /// one, which no file of ours has, is written back as a list, so a file round-trips unchanged.
    /// </remarks>
    [JsonConverter(typeof(ExpectedTargetConverter))]
    public sealed class ExpectedTarget : IEquatable<ExpectedTarget>
    {
        /// <summary>The prompt expects <see cref="TargetRegistry.NoTarget"/>.</summary>
        public static readonly ExpectedTarget NoTarget = new ExpectedTarget(TargetRegistry.NoTarget);

        private readonly string[] ids;

        /// <summary>One id, or several for an open choice.</summary>
        /// <exception cref="ArgumentException">No id is given, or one is null.</exception>
        public ExpectedTarget(params string[] ids) : this(ids, ids != null && ids.Length > 1)
        {
        }

        private ExpectedTarget(string[] ids, bool writtenAsList)
        {
            if (ids == null || ids.Length == 0)
            {
                throw new ArgumentException("An expected target needs at least one id.", nameof(ids));
            }

            if (ids.Any(id => id == null))
            {
                throw new ArgumentException("An expected target id cannot be null.", nameof(ids));
            }

            this.ids = (string[])ids.Clone();
            WrittenAsList = writtenAsList;
        }

        /// <summary>Every id that counts as right, in the order the file lists them.</summary>
        public IReadOnlyList<string> Ids => ids;

        /// <summary>True when more than one target is right.</summary>
        public bool IsOpenChoice => ids.Length > 1;

        /// <summary>True when the file holds a JSON list rather than a string.</summary>
        public bool WrittenAsList { get; }

        /// <summary>True when <paramref name="targetId"/> is one of the right answers.</summary>
        public bool Accepts(string targetId) => Array.IndexOf(ids, targetId) >= 0;

        /// <summary>The ids joined with <c>|</c>, the way the window shows an open choice.</summary>
        public override string ToString() => string.Join("|", ids);

        public bool Equals(ExpectedTarget other)
        {
            return other != null && ids.SequenceEqual(other.ids);
        }

        public override bool Equals(object obj) => Equals(obj as ExpectedTarget);

        public override int GetHashCode() => ToString().GetHashCode();

        private sealed class ExpectedTargetConverter : JsonConverter<ExpectedTarget>
        {
            public override ExpectedTarget ReadJson(JsonReader reader, Type objectType, ExpectedTarget existingValue,
                bool hasExistingValue, JsonSerializer serializer)
            {
                var token = JToken.Load(reader);
                switch (token.Type)
                {
                    case JTokenType.Null:
                        return null;
                    case JTokenType.String:
                        return new ExpectedTarget(new[] { (string)token }, false);
                    case JTokenType.Array:
                        return new ExpectedTarget(token.Values<string>().ToArray(), true);
                    default:
                        throw new JsonSerializationException(
                            $"expectedTarget must be a string or a list of strings, not {token.Type}.");
                }
            }

            public override void WriteJson(JsonWriter writer, ExpectedTarget value, JsonSerializer serializer)
            {
                if (value.WrittenAsList)
                {
                    writer.WriteStartArray();
                    foreach (string id in value.ids)
                    {
                        writer.WriteValue(id);
                    }

                    writer.WriteEndArray();
                }
                else
                {
                    writer.WriteValue(value.ids[0]);
                }
            }
        }
    }
}
