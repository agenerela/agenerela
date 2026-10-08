using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Agenerela.Tests
{
    public class JsonSchemaSerializerTests
    {
        // The following-agent example from #8, as #9 states it: follow_player is absent because
        // the agent is already following, and two targets are registered. This exact string is
        // the golden file. A test checkout on Windows may turn this literal's line endings into
        // CRLF, so it is compared with them normalised to the serializer's \n.
        const string FollowingAgentJson = @"{
  ""type"": ""object"",
  ""properties"": {
    ""action"": {
      ""type"": ""string"",
      ""description"": ""The one action to take now, from the allowed list."",
      ""enum"": [""stop_following"", ""move_to"", ""none""]
    },
    ""target"": {
      ""type"": ""string"",
      ""description"": ""What the action applies to. Use no_target when none of the listed targets applies."",
      ""enum"": [""no_target"", ""bridge"", ""tower""]
    },
    ""statement"": {
      ""type"": ""string"",
      ""description"": ""What the agent says or announces. One or two short sentences, or empty.""
    }
  },
  ""required"": [""action"", ""target"", ""statement""],
  ""propertyOrdering"": [""action"", ""target"", ""statement""],
  ""additionalProperties"": false
}";

        // The only place these tests construct a DecisionSchema. #8 owns the type and its
        // description constants, and these tests do not retype the descriptions: the golden
        // string above is the one place they are written out, which is what proves #8's
        // constants match it.
        static DecisionSchema Schema(params SchemaField[] fields) => new DecisionSchema(fields);

        static SchemaField Action(params string[] allowed) => new SchemaField
        {
            Name = "action",
            Description = DecisionSchema.ActionDescription,
            AllowedValues = allowed,
            Required = true,
        };

        static SchemaField Target(params string[] allowed) => new SchemaField
        {
            Name = "target",
            Description = DecisionSchema.TargetDescription,
            AllowedValues = allowed,
            Required = true,
        };

        static SchemaField Statement() => new SchemaField
        {
            Name = "statement",
            Description = DecisionSchema.StatementDescription,
            AllowedValues = null,
            Required = true,
        };

        static DecisionSchema FollowingAgent() => Schema(
            Action("stop_following", "move_to", "none"),
            Target(TargetRegistry.NoTarget, "bridge", "tower"),
            Statement());

        static string Serialize(DecisionSchema schema) => new JsonSchemaSerializer().Serialize(schema);

        [Test]
        public void FollowingAgentMatchesTheGoldenFile()
        {
            Assert.That(Serialize(FollowingAgent()), Is.EqualTo(FollowingAgentJson.Replace("\r\n", "\n")));
        }

        [Test]
        public void OutputIsValidJsonThatRoundTrips()
        {
            var json = Serialize(FollowingAgent());

            var parsed = JObject.Parse(json);
            var reparsed = JObject.Parse(parsed.ToString());

            Assert.That(JToken.DeepEquals(parsed, reparsed), Is.True);
            Assert.That(JToken.DeepEquals(parsed, JObject.Parse(FollowingAgentJson)), Is.True);
        }

        [Test]
        public void FieldOrderIsPreserved()
        {
            var parsed = JObject.Parse(Serialize(FollowingAgent()));
            var expected = new[] { "action", "target", "statement" };

            Assert.That(((JObject)parsed["properties"]).Properties().Select(p => p.Name), Is.EqualTo(expected));
            Assert.That(parsed["propertyOrdering"].Values<string>(), Is.EqualTo(expected));
            Assert.That(parsed["required"].Values<string>(), Is.EqualTo(expected));
        }

        [Test]
        public void FieldsAreNeverReordered()
        {
            // Not a shape #8 builds; it shows the serializer translates the order it is given
            // rather than imposing one of its own, so the order rule has exactly one owner.
            var parsed = JObject.Parse(Serialize(Schema(Statement(), Action("none"))));
            var expected = new[] { "statement", "action" };

            Assert.That(((JObject)parsed["properties"]).Properties().Select(p => p.Name), Is.EqualTo(expected));
            Assert.That(parsed["propertyOrdering"].Values<string>(), Is.EqualTo(expected));
        }

        [Test]
        public void EnumValuesKeepTheirOrder()
        {
            var parsed = JObject.Parse(Serialize(FollowingAgent()));

            Assert.That(parsed["properties"]["action"]["enum"].Values<string>(),
                Is.EqualTo(new[] { "stop_following", "move_to", "none" }));
            Assert.That(parsed["properties"]["target"]["enum"].Values<string>(),
                Is.EqualTo(new[] { "no_target", "bridge", "tower" }));
        }

        [Test]
        public void EmptyTargetRegistryShapeHasNoTargetProperty()
        {
            // #8 drops the target field entirely when no target is registered.
            var parsed = JObject.Parse(Serialize(Schema(Action("stop_following", "none"), Statement())));

            Assert.That(((JObject)parsed["properties"]).Properties().Select(p => p.Name),
                Is.EqualTo(new[] { "action", "statement" }));
            Assert.That(parsed["required"].Values<string>(), Is.EqualTo(new[] { "action", "statement" }));
            Assert.That(parsed["propertyOrdering"].Values<string>(), Is.EqualTo(new[] { "action", "statement" }));
        }

        [Test]
        public void FreeFormFieldHasNoEnum()
        {
            var statement = (JObject)JObject.Parse(Serialize(FollowingAgent()))["properties"]["statement"];

            Assert.That(statement.ContainsKey("enum"), Is.False);
            Assert.That(statement["type"].Value<string>(), Is.EqualTo("string"));
        }

        [Test]
        public void OnlyRequiredFieldsAreListedAsRequired()
        {
            var optional = Statement();
            optional.Required = false;

            var parsed = JObject.Parse(Serialize(Schema(Action("none"), optional)));

            Assert.That(parsed["required"].Values<string>(), Is.EqualTo(new[] { "action" }));
            Assert.That(parsed["propertyOrdering"].Values<string>(), Is.EqualTo(new[] { "action", "statement" }));
        }

        [Test]
        public void AdditionalPropertiesAreForbidden()
        {
            var parsed = JObject.Parse(Serialize(FollowingAgent()));

            Assert.That(parsed["additionalProperties"].Value<bool>(), Is.False);
        }

        [Test]
        public void NeverEmitsAnEmptyEnumValue()
        {
            var properties = (JObject)JObject.Parse(Serialize(FollowingAgent()))["properties"];
            var values = properties.Properties()
                .Select(p => p.Value["enum"])
                .Where(e => e != null)
                .SelectMany(e => e.Values<string>());

            Assert.That(values, Has.None.Empty);
        }

        [TestCase("")]
        [TestCase(null)]
        public void RejectsAnEmptyOrNullEnumValue(string value)
        {
            // Gemini answers "" in an enum with HTTP 400. Thrown here so no provider ever sends it.
            var exception = Assert.Throws<ArgumentException>(
                () => Serialize(Schema(Action("move_to", "none"), Target("bridge", value), Statement())));

            Assert.That(exception.Message, Does.Contain("target"));
        }

        [Test]
        public void RejectsAnEmptyEnum()
        {
            Assert.Throws<ArgumentException>(() => Serialize(Schema(Action(), Statement())));
        }

        [Test]
        public void RejectsADuplicateEnumValue()
        {
            Assert.Throws<ArgumentException>(() => Serialize(Schema(Action("none", "none"), Statement())));
        }

        [Test]
        public void RejectsTwoFieldsWithTheSameName()
        {
            Assert.Throws<ArgumentException>(() => Serialize(Schema(Action("none"), Statement(), Statement())));
        }

        [TestCase("")]
        [TestCase(null)]
        public void RejectsAnUnnamedField(string name)
        {
            var field = Statement();
            field.Name = name;

            Assert.Throws<ArgumentException>(() => Serialize(Schema(Action("none"), field)));
        }

        [Test]
        public void RejectsANullSchema()
        {
            Assert.Throws<ArgumentNullException>(() => new JsonSchemaSerializer().Serialize(null));
        }

        [Test]
        public void EscapesTextThatWouldBreakTheJson()
        {
            var field = Statement();
            field.Description = "Say \"hello\" \\ goodbye\nthen stop.";

            var parsed = JObject.Parse(Serialize(Schema(Action("none"), field)));

            Assert.That(parsed["properties"]["statement"]["description"].Value<string>(), Is.EqualTo(field.Description));
        }
    }
}
