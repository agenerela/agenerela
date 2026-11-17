using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Tests
{
    // Typed parameters beside action and target (DR-023, first step, #266 item 2): an action
    // declares closed choices; a call that offers it sends the paired schema with the parameters
    // after target and before statement, and the action list with their values; the reply's values
    // are parsed into AgentDecision.Arguments, defaulted or rejected when not offered, checked again
    // at Execute, handed to the handler typed, and recorded. An agent that declares none sends what
    // it always sent. Every await of DecideAsync goes through Fixtures.NotCancelled.
    public sealed class ParameterTests
    {
        private const string Blight = "{\"action\":\"blight\",\"target\":\"orsk\",\"strength\":\"major\",\"statement\":\"The wells of Orsk turn sour.\"}";

        private static ActionParameter Strength(string @default = null) => new ActionParameter
        {
            Name = "strength", Description = "how hard it strikes", Values = new[] { "minor", "major" }, Default = @default,
        };

        // A storyteller with two countries: blight asks for a strength, bless asks for nothing.
        private static Agent Storyteller(FakeProvider fake, out List<AgentDecision> ran, ActionParameter strength = null,
            Func<AgentContext, string, string, Availability> valueCheck = null, bool withParameter = true)
        {
            var actions = new[]
            {
                new ActionDefinition
                {
                    Id = "blight", Description = "A country falls sick.", RequiresTarget = true,
                    ExampleStimulus = "Strike {0}.", PreferredExampleTargets = new[] { "orsk" }, ExampleStatement = "A sickness creeps over {0}.",
                    Parameters = withParameter ? new[] { strength ?? Strength() } : null,
                },
                new ActionDefinition { Id = "bless", Description = "A country prospers.", RequiresTarget = true },
            };

            var agent = new Agent(new AgentIdentity { Name = "The Teller", Role = "the storyteller of a realm" }, actions, fake);
            var executed = new List<AgentDecision>();
            ran = executed;
            agent.Bind("blight", new DelegateActionHandler(_ => true, (ctx, d) => executed.Add(d)) { IsAvailableValue = valueCheck });
            agent.Bind("bless", new FakeHandler());
            agent.Targets.Register("orsk", new object());
            agent.Targets.Register("kesh", new object());
            return agent;
        }

        private static string Json(DecisionLogLine line) => Newtonsoft.Json.JsonConvert.SerializeObject(line, DecisionLogFormat.Settings);

        // ---- Off unless declared ----------------------------------------------------------------

        [Test]
        public async Task AnAgentThatDeclaresNoneSendsTheFlatSchemaAndNoActionList()
        {
            var fake = new FakeProvider("{\"action\":\"blight\",\"target\":\"orsk\",\"statement\":\"\"}");
            var result = await Fixtures.NotCancelled(Storyteller(fake, out _, withParameter: false).DecideAsync("Tell this turn's event."));

            Assert.That(fake.LastRequest.Schema.Pairs, Is.Null, "No parameter, no pairing.");
            Assert.That(fake.LastRequest.SystemPrompt, Does.Not.Contain("Allowed actions:"));
            Assert.That(fake.LastRequest.FewShotBlock, Does.Not.Contain("strength"));
            Assert.That(result.Decision.Arguments, Is.Empty);
            Assert.That(result.Telemetry.Arguments, Is.Empty);
        }

        // ---- The request ------------------------------------------------------------------------

        [Test]
        public async Task ADeclaredParameterPairsTheSchemaWithItBetweenTargetAndStatement()
        {
            var fake = new FakeProvider(Blight);
            await Fixtures.NotCancelled(Storyteller(fake, out _).DecideAsync("Tell this turn's event."));

            var schema = fake.LastRequest.Schema;
            Assert.That(schema.Fields.Select(f => f.Name), Is.EqualTo(new[] { "action", "target", "statement" }), "The flat fields are what they were.");
            Assert.That(schema.Pairs.Select(p => string.Join(",", p.Actions) + ":" + string.Join(",", p.Parameters.Select(f => f.Name))),
                Is.EqualTo(new[] { "blight:strength", "bless:", "none:" }), "blight and bless take the same targets, and still do not share a branch.");

            string json = new JsonSchemaSerializer().Serialize(schema);
            Assert.That(json, Does.StartWith(
                "{\n" +
                "  \"anyOf\": [\n" +
                "    {\n" +
                "      \"type\": \"object\",\n" +
                "      \"properties\": {\n" +
                "        \"action\": {\n" +
                "          \"type\": \"string\",\n" +
                "          \"description\": \"The one action to take now, from the allowed list.\",\n" +
                "          \"enum\": [\"blight\"]\n" +
                "        },\n" +
                "        \"target\": {\n" +
                "          \"type\": \"string\",\n" +
                "          \"description\": \"What the action applies to. Use no_target when none of the listed targets applies.\",\n" +
                "          \"enum\": [\"orsk\", \"kesh\"]\n" +
                "        },\n" +
                "        \"strength\": {\n" +
                "          \"type\": \"string\",\n" +
                "          \"description\": \"how hard it strikes\",\n" +
                "          \"enum\": [\"minor\", \"major\"]\n" +
                "        },\n" +
                "        \"statement\": {\n" +
                "          \"type\": \"string\",\n" +
                "          \"description\": \"What the agent says or announces. One or two short sentences, or empty.\"\n" +
                "        }\n" +
                "      },\n" +
                "      \"required\": [\"action\", \"target\", \"strength\", \"statement\"],\n" +
                "      \"propertyOrdering\": [\"action\", \"target\", \"strength\", \"statement\"],\n" +
                "      \"additionalProperties\": false\n" +
                "    },\n"));
            Assert.That(json.Split(new[] { "\"strength\": {" }, StringSplitOptions.None).Length, Is.EqualTo(2), "Only blight's branch asks for it.");
        }

        [Test]
        public async Task ThePromptListsTheActionWithItsValuesAndTheExampleShowsOne()
        {
            var fake = new FakeProvider(Blight);
            await Fixtures.NotCancelled(Storyteller(fake, out _).DecideAsync("Tell this turn's event."));

            Assert.That(fake.LastRequest.SystemPrompt, Does.Contain(
                "Allowed actions:\n" +
                "- blight: A country falls sick. strength (how hard it strikes): minor or major.\n" +
                "- bless: A country prospers.\n" +
                "- none: Take no action."), "Listed although ListActionsAndTargets is off: a local model never reads the schema.");
            Assert.That(fake.LastRequest.FewShotBlock, Does.Contain(
                "Player: \"Strike orsk.\" -> action: blight, target: orsk, strength: minor, statement: \"A sickness creeps over orsk.\""),
                "Between the target and the statement, where the reply gives it.");
        }

        [Test]
        public async Task TheExampleShowsTheExampleValueThenTheDefault()
        {
            var fake = new FakeProvider(Blight);
            var shown = Strength("minor");
            shown.ExampleValue = "major";
            await Fixtures.NotCancelled(Storyteller(fake, out _, shown).DecideAsync("Tell this turn's event."));
            Assert.That(fake.LastRequest.FewShotBlock, Does.Contain("target: orsk, strength: major,"));

            var defaulted = new FakeProvider(Blight);
            await Fixtures.NotCancelled(Storyteller(defaulted, out _, new ActionParameter { Name = "strength", Values = new[] { "minor", "major" }, Default = "major" })
                .DecideAsync("Tell this turn's event."));
            Assert.That(defaulted.LastRequest.FewShotBlock, Does.Contain("target: orsk, strength: major,"));
            Assert.That(defaulted.LastRequest.SystemPrompt, Does.Contain("- blight: A country falls sick. strength: minor or major."), "No description, no brackets.");
        }

        [Test]
        public async Task WithNoTargetRegisteredTheSchemaIsPairedWithoutATargetField()
        {
            var fake = new FakeProvider("{\"action\":\"set_taxes\",\"rate\":\"3\",\"statement\":\"\"}");
            var agent = new Agent(new AgentIdentity { Name = "Kesh" }, new[]
            {
                new ActionDefinition
                {
                    Id = "set_taxes", Description = "Set the tax rate.", TakesNoTarget = true,
                    Parameters = new[] { new ActionParameter { Name = "rate", Values = new[] { "1", "2", "3" } } },
                },
            }, fake);
            AgentDecision ran = null;
            agent.Bind("set_taxes", new DelegateActionHandler(_ => true, (ctx, d) => ran = d));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Your move."));

            var schema = fake.LastRequest.Schema;
            Assert.That(schema.Fields.Select(f => f.Name), Is.EqualTo(new[] { "action", "statement" }));
            string json = new JsonSchemaSerializer().Serialize(schema);
            Assert.That(json, Does.Contain("\"required\": [\"action\", \"rate\", \"statement\"]"));
            Assert.That(json, Does.Not.Contain("\"target\""));
            Assert.That(fake.LastRequest.SystemPrompt, Does.Contain("- set_taxes: Set the tax rate. rate: 1, 2 or 3."));

            Assert.That(result.Verdict, Is.Null);
            Assert.That(agent.Execute(result), Is.True);
            Assert.That(ran.TryGetNumber("rate", out int rate) ? rate : -1, Is.EqualTo(3));
        }

        // ---- The decision -----------------------------------------------------------------------

        [Test]
        public async Task TheChosenValueIsInTheDecisionTheHandlerTelemetryAndTheLog()
        {
            var fake = new FakeProvider(Blight);
            var agent = Storyteller(fake, out var ran);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event."));

            Assert.That(result.Verdict, Is.Null);
            Assert.That(result.Decision.Arguments.Select(a => a.Key + "=" + a.Value), Is.EqualTo(new[] { "strength=major" }));
            Assert.That(result.Decision.TryGetArgument("strength", out string strength) ? strength : null, Is.EqualTo("major"));
            Assert.That(result.Telemetry.Arguments, Is.EqualTo(new Dictionary<string, string> { { "strength", "major" } }));
            Assert.That(result.Telemetry.ArgumentsDefaulted, Is.Empty);

            Assert.That(agent.Execute(result), Is.True);
            Assert.That(ran.Single().Arguments["strength"], Is.EqualTo("major"));

            var line = DecisionLogLine.From(new DecisionFeedEntry(agent, result, DecisionStage.Decided, null, DateTime.UtcNow, 1));
            Assert.That(Json(line), Does.Contain("\"arguments\":{\"strength\":\"major\"}"));
            Assert.That(line.ArgumentsDefaulted, Is.Null);
            Assert.That(line.ValuesUnavailable, Is.Null);
        }

        [Test]
        public async Task ADecisionWithNoArgumentsWritesNoneInTheLog()
        {
            var fake = new FakeProvider("{\"action\":\"bless\",\"target\":\"kesh\",\"statement\":\"\"}");
            var agent = Storyteller(fake, out _);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event."));

            Assert.That(result.Verdict, Is.Null);
            Assert.That(result.Decision.Arguments, Is.Empty);
            var line = DecisionLogLine.From(new DecisionFeedEntry(agent, result, DecisionStage.Decided, null, DateTime.UtcNow, 1));
            Assert.That(Json(line), Does.Not.Contain("rguments"));
        }

        [Test]
        public async Task AMissingOrUnofferedValueTakesTheDeclaredDefaultAndIsRecorded()
        {
            foreach (string reply in new[]
            {
                "{\"action\":\"blight\",\"target\":\"orsk\",\"statement\":\"\"}",
                "{\"action\":\"blight\",\"target\":\"orsk\",\"strength\":\"catastrophic\",\"statement\":\"\"}",
            })
            {
                var fake = new FakeProvider(reply);
                var agent = Storyteller(fake, out var ran, Strength("minor"));

                var result = await Fixtures.NotCancelled(agent.DecideAsync("Make it catastrophic."));

                Assert.That(result.Verdict, Is.Null, reply);
                Assert.That(result.Decision.Arguments["strength"], Is.EqualTo("minor"), reply);
                Assert.That(result.Telemetry.ArgumentsDefaulted, Is.EqualTo(new[] { "strength" }), reply);
                Assert.That(result.ModelDecision.Arguments.ContainsKey("strength"), Is.EqualTo(reply.Contains("catastrophic")),
                    "The model's own answer is kept as it wrote it.");

                var line = DecisionLogLine.From(new DecisionFeedEntry(agent, result, DecisionStage.Decided, null, DateTime.UtcNow, 1));
                Assert.That(line.ArgumentsDefaulted, Is.EqualTo(new[] { "strength" }));

                Assert.That(agent.Execute(result), Is.True);
                Assert.That(ran.Single().Arguments["strength"], Is.EqualTo("minor"));
            }
        }

        [Test]
        public async Task WithNoDefaultAMissingOrUndeclaredValueIsRejectedAndNothingRuns()
        {
            var expected = new Dictionary<string, string>
            {
                { "{\"action\":\"blight\",\"target\":\"orsk\",\"statement\":\"\"}", "the action needs 'strength', and the decision has none." },
                { "{\"action\":\"blight\",\"target\":\"orsk\",\"strength\":\"catastrophic\",\"statement\":\"\"}", "'catastrophic' is not a value of 'strength'." },
            };

            foreach (var reply in expected)
            {
                var fake = new FakeProvider(reply.Key);
                var agent = Storyteller(fake, out var ran);

                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("SchemaLegalityGuard rejected 'blight'"));
                var result = await Fixtures.NotCancelled(agent.DecideAsync("Make it catastrophic."));

                Assert.That(result.Verdict.Value.Kind, Is.EqualTo(GuardVerdictKind.Reject), reply.Key);
                Assert.That(result.Verdict.Value.Reason, Is.EqualTo(reply.Value));
                Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
                Assert.That(result.Telemetry.Arguments, Is.Empty, "A stopped decision holds none.");
                Assert.That(agent.Execute(result), Is.False);
                Assert.That(ran, Is.Empty);
            }
        }

        [Test]
        public async Task AGuardThatReplacesTheStatementKeepsTheArguments()
        {
            var fake = new FakeProvider(Blight);
            var agent = Storyteller(fake, out _);
            agent.Guards.Add(new DelegateGuard("title", input => GuardVerdict.ReplaceStatement("too long", "Sour wells.")));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event."));

            Assert.That(result.Decision.Statement, Is.EqualTo("Sour wells."));
            Assert.That(result.Decision.Arguments["strength"], Is.EqualTo("major"));
        }

        // ---- Values per decision ----------------------------------------------------------------

        [Test]
        public async Task AValueItsCheckRejectsIsAbsentFromTheSchemaAndThePromptAndRecorded()
        {
            var fake = new FakeProvider("{\"action\":\"blight\",\"target\":\"orsk\",\"strength\":\"minor\",\"statement\":\"\"}");
            var agent = Storyteller(fake, out _, valueCheck: (ctx, parameter, value) =>
                parameter == "strength" && value == "major" ? Availability.No("orsk was struck hard last turn") : Availability.Yes);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event."));

            Assert.That(fake.LastRequest.Schema.Pairs[0].Parameters.Single().AllowedValues, Is.EqualTo(new[] { "minor" }));
            Assert.That(fake.LastRequest.SystemPrompt, Does.Contain("- blight: A country falls sick. strength (how hard it strikes): minor."));
            Assert.That(fake.LastRequest.SystemPrompt, Does.Not.Contain("major"));
            Assert.That(result.Verdict, Is.Null);
            Assert.That(result.Telemetry.ValuesUnavailable.Single().ToString(), Is.EqualTo("blight.strength = major: orsk was struck hard last turn"));

            var line = DecisionLogLine.From(new DecisionFeedEntry(agent, result, DecisionStage.Decided, null, DateTime.UtcNow, 1));
            Assert.That(line.ValuesUnavailable.Single().ToString(), Is.EqualTo("blight.strength = major: orsk was struck hard last turn"));

            var preview = agent.Preview("Tell this turn's event.");
            Assert.That(preview.ValuesUnavailable.Single().Value, Is.EqualTo("major"));
        }

        [Test]
        public async Task AReplyNamingAValueNotOfferedIsRejected()
        {
            var fake = new FakeProvider(Blight);
            var agent = Storyteller(fake, out var ran, valueCheck: (ctx, parameter, value) => value != "major");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("SchemaLegalityGuard rejected 'blight'"));
            var result = await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event."));

            Assert.That(result.Verdict.Value.Kind, Is.EqualTo(GuardVerdictKind.Reject));
            Assert.That(result.Verdict.Value.Reason, Is.EqualTo("the value 'major' of 'strength' was not offered: it was not available for this decision."));
            Assert.That(agent.Execute(result), Is.False);
            Assert.That(ran, Is.Empty);
        }

        [Test]
        public async Task AnActionWhoseParameterHasNoValueLeftIsLeftOut()
        {
            var fake = new FakeProvider("{\"action\":\"bless\",\"target\":\"kesh\",\"statement\":\"\"}");
            var agent = Storyteller(fake, out _, valueCheck: (ctx, parameter, value) => Availability.No("the realm is at peace"));

            await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event."));

            Assert.That(fake.LastRequest.Schema.Fields[0].AllowedValues, Is.EqualTo(new[] { "bless", ActionRegistry.None }));
            Assert.That(fake.LastRequest.Schema.Pairs, Is.Null, "With blight gone no offered action declares a parameter.");

            var preview = agent.Preview("Tell this turn's event.");
            Assert.That(preview.LeftOut.Single().Reason, Is.EqualTo(
                "One of its parameters has no value its per-value check allows: strength 'minor' (the realm is at peace), strength 'major' (the realm is at peace)."));
        }

        [Test]
        public async Task ExecuteChecksTheValueAgainAndRefusesOneGoneStale()
        {
            bool majorAllowed = true;
            var fake = new FakeProvider(Blight);
            var agent = Storyteller(fake, out var ran, valueCheck: (ctx, parameter, value) =>
                value != "major" || majorAllowed ? Availability.Yes : Availability.No("orsk was struck hard already"));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event."));
            Assert.That(result.Verdict, Is.Null);

            majorAllowed = false;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("strength"));
            Assert.That(agent.Execute(result, out ActionResult outcome), Is.False);

            Assert.That(ran, Is.Empty, "No handler runs on a value the game no longer allows (hard rule 5).");
            Assert.That(outcome.Status, Is.EqualTo(ExecutionStatus.Refused));
            Assert.That(outcome.Reason, Is.EqualTo("'strength' cannot be 'major' right now: orsk was struck hard already"));
            Assert.That(outcome.AvailabilityReason, Is.EqualTo("orsk was struck hard already"));
        }

        [Test]
        public void TheLegalityGuardRefusesAHandBuiltDecisionWithABadOrUnknownArgument()
        {
            var agent = Storyteller(new FakeProvider(Blight), out _);
            var ctx = new AgentContext(new AgentIdentity { Name = "The Teller" }, "", Array.Empty<string>(), null, agent.Targets, agent.Actions, null);
            var guard = new SchemaLegalityGuard();

            GuardVerdict Inspect(params KeyValuePair<string, string>[] arguments) => guard.Inspect(new GuardInput(ctx, null, null,
                new AgentDecision("blight", "orsk", "", arguments), new DecideOptions(), new DecisionTelemetry()));

            Assert.That(Inspect(new KeyValuePair<string, string>("strength", "major")).Kind, Is.EqualTo(GuardVerdictKind.Pass));
            Assert.That(Inspect(new KeyValuePair<string, string>("strength", "huge")).Reason, Is.EqualTo("'huge' is not a value of 'strength'."));
            Assert.That(Inspect().Reason, Is.EqualTo("the action needs 'strength', and the decision has none."));
            Assert.That(Inspect(new KeyValuePair<string, string>("strength", "major"), new KeyValuePair<string, string>("turns", "3")).Reason,
                Is.EqualTo("the action has no parameter 'turns'."));
        }

        // ---- The code front door ----------------------------------------------------------------

        private sealed class Realm
        {
            public string Struck;
            public string Strength;
            public int Turns;
            public bool MajorAllowed = true;

            [AgentAction("blight", "A country falls sick.", RequiresTarget = true)]
            [AgentParameter("strength", "how hard it strikes", "minor", "major", Order = 0)]
            [AgentParameter("turns", "how long it lasts", "1", "3", "5", Order = 1, Default = "1")]
            public void Blight(string country, [Argument("strength")] string strength, [Argument("turns")] int turns)
            {
                Struck = country;
                Strength = strength;
                Turns = turns;
            }

            [AvailableValue("blight", "strength")]
            public Availability StrengthAllowed(string value) =>
                value != "major" || MajorAllowed ? Availability.Yes : Availability.No("struck hard last turn");
        }

        [Test]
        public async Task AMethodDeclaresParametersAndReceivesThemTyped()
        {
            var fake = new FakeProvider("{\"action\":\"blight\",\"target\":\"orsk\",\"strength\":\"major\",\"turns\":\"3\",\"statement\":\"\"}");
            var agent = new Agent(new AgentIdentity { Name = "The Teller" }, new ActionDefinition[0], fake);
            var realm = new Realm();
            agent.Actions.RegisterMethods(realm);
            agent.Targets.Register("orsk", "orsk");

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event."));

            Assert.That(fake.LastRequest.Schema.Pairs[0].Parameters.Select(f => f.Name), Is.EqualTo(new[] { "strength", "turns" }), "By Order.");
            Assert.That(result.Verdict, Is.Null);
            Assert.That(agent.Execute(result), Is.True);
            Assert.That((realm.Struck, realm.Strength, realm.Turns), Is.EqualTo(("orsk", "major", 3)));
        }

        [Test]
        public async Task AMethodsValueCheckLeavesTheValueOut()
        {
            var fake = new FakeProvider("{\"action\":\"blight\",\"target\":\"orsk\",\"strength\":\"minor\",\"turns\":\"1\",\"statement\":\"\"}");
            var agent = new Agent(new AgentIdentity { Name = "The Teller" }, new ActionDefinition[0], fake);
            agent.Actions.RegisterMethods(new Realm { MajorAllowed = false });
            agent.Targets.Register("orsk", "orsk");

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event."));

            Assert.That(fake.LastRequest.Schema.Pairs[0].Parameters[0].AllowedValues, Is.EqualTo(new[] { "minor" }));
            Assert.That(fake.LastRequest.Schema.Pairs[0].Parameters[1].AllowedValues, Is.EqualTo(new[] { "1", "3", "5" }), "A parameter with no check offers every value.");
            Assert.That(result.Telemetry.ValuesUnavailable.Single().Reason, Is.EqualTo("struck hard last turn"));
        }

        private sealed class UnknownArgument
        {
            [AgentAction("blight", "A country falls sick.")]
            [AgentParameter("strength", null, "minor", "major")]
            public void Blight([Argument("force")] string force) { }
        }

        private sealed class WordsAsNumber
        {
            [AgentAction("blight", "A country falls sick.")]
            [AgentParameter("strength", null, "minor", "major")]
            public void Blight([Argument("strength")] int strength) { }
        }

        private sealed class CheckForNothing
        {
            [AgentAction("blight", "A country falls sick.")]
            [AgentParameter("strength", null, "minor", "major")]
            public void Blight() { }

            [AvailableValue("blight", "turns")]
            public bool Allowed(string value) => true;
        }

        private sealed class ParameterWithoutAction
        {
            [AgentParameter("strength", null, "minor", "major")]
            public void Blight() { }
        }

        [Test]
        public void TheReaderRefusesAMismatchedArgumentOrCheck()
        {
            Assert.That(() => new ActionRegistry().RegisterMethods(new UnknownArgument()),
                Throws.ArgumentException.With.Message.Contains("no [AgentParameter] with that name"));
            Assert.That(() => new ActionRegistry().RegisterMethods(new WordsAsNumber()),
                Throws.ArgumentException.With.Message.Contains("not every value of that parameter is a whole number"));
            Assert.That(() => new ActionRegistry().RegisterMethods(new CheckForNothing()),
                Throws.ArgumentException.With.Message.Contains("declares no [AgentParameter] with that name"));
            Assert.That(() => new ActionRegistry().RegisterMethods(new ParameterWithoutAction()),
                Throws.ArgumentException.With.Message.Contains("has [AgentParameter] but no [AgentAction]"));
        }

        // ---- Registration -----------------------------------------------------------------------

        [Test]
        public void RegistrationRefusesAParameterThatCouldNotBeSent()
        {
            void Register(params ActionParameter[] parameters) => new ActionRegistry().Register(
                new ActionDefinition { Id = "blight", Parameters = parameters }, new FakeHandler());

            Assert.That(() => Register(new ActionParameter { Name = "strength", Values = new string[0] }),
                Throws.ArgumentException.With.Message.Contains("has no values"));
            Assert.That(() => Register(new ActionParameter { Name = "strength", Values = new[] { "minor", "" } }),
                Throws.ArgumentException.With.Message.Contains("empty value"));
            Assert.That(() => Register(new ActionParameter { Name = "strength", Values = new[] { "minor", "minor" } }),
                Throws.ArgumentException.With.Message.Contains("twice"));
            Assert.That(() => Register(new ActionParameter { Name = "target", Values = new[] { "a" } }),
                Throws.ArgumentException.With.Message.Contains("a field of the decision itself"));
            Assert.That(() => Register(new ActionParameter { Name = "Strength", Values = new[] { "a" } }),
                Throws.ArgumentException.With.Message.Contains("uppercase"));
            Assert.That(() => Register(new ActionParameter { Name = "strength", Values = new[] { "minor" }, Default = "major" }),
                Throws.ArgumentException.With.Message.Contains("not one of its values"));
            Assert.That(() => Register(Strength(), Strength()),
                Throws.ArgumentException.With.Message.Contains("declares the parameter 'strength' twice"));
            Assert.That(() => Register(Strength("minor")), Throws.Nothing);
        }

        [Test]
        public void ANumberListIsOneWhoseValuesAllReadAsWholeNumbers()
        {
            Assert.That(new ActionParameter { Values = new[] { "1", "3", "-5" } }.IsNumber, Is.True);
            Assert.That(new ActionParameter { Values = new[] { "1", "3.5" } }.IsNumber, Is.False);
            Assert.That(Strength().IsNumber, Is.False);

            var decision = new AgentDecision("blight", "orsk", "", new[] { new KeyValuePair<string, string>("turns", "3"), new KeyValuePair<string, string>("strength", "major") });
            Assert.That(decision.TryGetNumber("turns", out int turns) && turns == 3, Is.True);
            Assert.That(decision.TryGetNumber("strength", out _), Is.False);
            Assert.That(decision.Arguments.Keys, Is.EqualTo(new[] { "turns", "strength" }), "In the order given.");
            Assert.That(new AgentDecision("blight", "orsk", "").Arguments, Is.Empty);
        }

        // ---- Memory -----------------------------------------------------------------------------

        [Test]
        public async Task ARememberedDecisionKeepsItsArgumentsBeforeTheStatement()
        {
            var fake = new FakeProvider(Blight);
            await Fixtures.NotCancelled(Storyteller(fake, out _).DecideAsync("Tell this turn's event."));
            var decision = new AgentDecision("blight", "orsk", "The wells turn sour.", new[] { new KeyValuePair<string, string>("strength", "major") });

            string whole = RollingHistory.Write(decision, fake.LastRequest.Schema);

            Assert.That(whole, Is.EqualTo("{\"action\":\"blight\",\"target\":\"orsk\",\"strength\":\"major\",\"statement\":\"The wells turn sour.\"}"));
            Assert.That(RollingHistory.Rewrite(whole, null), Is.EqualTo("{\"action\":\"blight\",\"target\":\"orsk\",\"strength\":\"major\"}"),
                "The move alone keeps them (#251).");
        }
    }
}
