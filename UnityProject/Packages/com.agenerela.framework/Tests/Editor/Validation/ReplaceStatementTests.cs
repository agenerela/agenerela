using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Agenerela.Tests
{
    // A guard that keeps the move and replaces only its statement (#266 item 4,
    // GuardVerdict.ReplaceStatement): the action and target stand, the statement is the guard's, the
    // later guards still judge the move, and telemetry, the decision log and memory record what
    // happened. Every await of DecideAsync goes through Fixtures.NotCancelled.
    public sealed class ReplaceStatementTests
    {
        private const string Story = "Orsk burned the granaries of Kesh.";
        private const string Fixed = "Fire took the granaries of Kesh.";

        private static string Reply(string statement = Story) =>
            $"{{\"action\":\"famine\",\"target\":\"kesh\",\"statement\":\"{statement}\"}}";

        private static FakeGuard History(string statement = Fixed) =>
            new FakeGuard("HistoryCheck", input => input.Decision.Statement.Contains("Orsk")
                ? GuardVerdict.ReplaceStatement("the story names a country that did nothing.", statement)
                : GuardVerdict.Pass);

        // ---- The verdict ------------------------------------------------------------------------

        [Test]
        public void TheVerdictCarriesItsStatementAndNeedsAReason()
        {
            var verdict = GuardVerdict.ReplaceStatement("the story is wrong.", Fixed);

            Assert.That(verdict.Kind, Is.EqualTo(GuardVerdictKind.ReplaceStatement));
            Assert.That(verdict.Statement, Is.EqualTo(Fixed));
            Assert.That(GuardVerdict.ReplaceStatement("drop it.", null).Statement, Is.Empty, "Null drops the statement.");
            Assert.That(GuardVerdict.RewriteToNone("no.").Statement, Is.Null, "Other kinds carry none.");
            Assert.That(GuardVerdict.Pass.Statement, Is.Null);
            Assert.Throws<ArgumentException>(() => GuardVerdict.ReplaceStatement(" ", Fixed));
            Assert.That((int)GuardVerdictKind.ReplaceStatement, Is.EqualTo(3), "New kinds go at the end: the kind is serialized as its position.");
        }

        // ---- Keeping the move -------------------------------------------------------------------

        [Test]
        public async Task TheMoveStandsWithTheGuardsStatementAndIsRecorded()
        {
            var fake = new FakeProvider(Reply());
            var agent = Teller(fake, out var famine);
            agent.Guards.Add(History());

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event."));

            Assert.That(result.Decision.ActionId, Is.EqualTo("famine"));
            Assert.That(result.Decision.TargetId, Is.EqualTo("kesh"));
            Assert.That(result.Decision.Statement, Is.EqualTo(Fixed));
            Assert.That(result.ModelDecision.Statement, Is.EqualTo(Story), "The model's words stay readable.");
            Assert.That(result.Verdict, Is.Null, "Not a stop.");
            Assert.That(result.Telemetry.Verdict, Is.Null);
            Assert.That(result.Telemetry.GuardsFired, Is.EqualTo(new[] { "HistoryCheck" }));

            var replaced = result.Telemetry.StatementsReplaced.Single();
            Assert.That(replaced.Guard, Is.EqualTo("HistoryCheck"));
            Assert.That(replaced.Reason, Is.EqualTo("the story names a country that did nothing."));
            Assert.That(replaced.Replaced, Is.EqualTo(Story));
            Assert.That(replaced.Statement, Is.EqualTo(Fixed));

            Assert.That(agent.Execute(result), Is.True);
            Assert.That(famine.Executions.Single().Decision.Statement, Is.EqualTo(Fixed), "The handler gets the new line.");
        }

        [Test]
        public async Task AnEmptyStatementDropsTheLine()
        {
            var fake = new FakeProvider(Reply());
            var agent = Teller(fake, out _);
            agent.Guards.Add(History(statement: null));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event."));

            Assert.That(result.Decision.ActionId, Is.EqualTo("famine"));
            Assert.That(result.Decision.Statement, Is.Empty);
            Assert.That(result.Telemetry.StatementsReplaced.Single().Statement, Is.Empty);
        }

        [Test]
        public async Task TheDecisionLogLineCarriesTheReplacement()
        {
            var fake = new FakeProvider(Reply());
            var agent = Teller(fake, out _);
            agent.Guards.Add(History());

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event."));
            var line = DecisionLogLine.From(Entry(agent, result));

            Assert.That(line.Decision.Statement, Is.EqualTo(Fixed));
            Assert.That(line.ModelDecision.Statement, Is.EqualTo(Story));
            Assert.That(line.Verdict, Is.Null);
            Assert.That(line.GuardsFired, Is.EqualTo(new[] { "HistoryCheck" }));
            Assert.That(line.StatementsReplaced.Single().Replaced, Is.EqualTo(Story));

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(line);
            Assert.That(json, Does.Contain("\"statementsReplaced\":[{\"guard\":\"HistoryCheck\""));
            Assert.That(DecisionLogReader.TryParse(json, out var read), Is.True);
            Assert.That(read.StatementsReplaced.Single().Statement, Is.EqualTo(Fixed), "It reads back.");
        }

        [Test]
        public async Task ALineWithNoReplacementIsWrittenAsBefore()
        {
            var fake = new FakeProvider(Reply("A dry summer in Kesh."));
            var agent = Teller(fake, out _);
            agent.Guards.Add(History());

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event."));

            Assert.That(result.Telemetry.StatementsReplaced, Is.Empty);
            Assert.That(result.Telemetry.GuardsFired, Is.Empty);
            Assert.That(ReferenceEquals(result.Decision, result.ModelDecision), Is.True);
            Assert.That(DecisionLogLine.From(Entry(agent, result)).StatementsReplaced, Is.Null, "Left out of the line, so it is unchanged.");
        }

        [Test]
        public async Task MemoryRemembersTheLineTheAgentActuallySaid()
        {
            var fake = new FakeProvider(Reply());
            var agent = Teller(fake, out _);
            agent.Guards.Add(History());
            agent.Memory = new RollingHistory();

            await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event."));
            await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event."));

            Assert.That(fake.LastRequest.History.Last(), Does.Contain(Fixed).And.Not.Contain("Orsk"));
        }

        [Test]
        public async Task NoRefusalLineIsAskedForAReplacement()
        {
            var fake = new FakeProvider(Reply());
            var agent = Teller(fake, out _);
            agent.Guards.Add(History());

            await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event.", new DecideOptions { AskForRefusalLine = true }));

            Assert.That(fake.Requests.Count, Is.EqualTo(1));
        }

        // ---- The pipeline goes on ---------------------------------------------------------------

        [Test]
        public async Task LaterGuardsJudgeTheMoveWithTheNewStatement()
        {
            var fake = new FakeProvider(Reply());
            var agent = Teller(fake, out _);
            var after = new FakeGuard("After");
            agent.Guards.Add(History());
            agent.Guards.Add(after);

            await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event."));

            var seen = after.Inputs.Single();
            Assert.That(seen.Decision.Statement, Is.EqualTo(Fixed));
            Assert.That(seen.Decision.ActionId, Is.EqualTo("famine"));
        }

        [Test]
        public async Task ALaterStopStillStopsAndTheReplacementStaysRecorded()
        {
            var fake = new FakeProvider(Reply());
            var agent = Teller(fake, out var famine);
            agent.Guards.Add(History());
            agent.Guards.Add(FakeGuard.Rewriting("Fairness", "kesh was struck last turn."));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event."));

            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
            Assert.That(result.Decision.Statement, Is.Empty, "A stop leaves no line, as before.");
            Assert.That(result.Verdict.Value.Guard, Is.EqualTo("Fairness"));
            Assert.That(result.Telemetry.GuardsFired, Is.EqualTo(new[] { "HistoryCheck", "Fairness" }));
            Assert.That(result.Telemetry.StatementsReplaced.Single().Guard, Is.EqualTo("HistoryCheck"));
            Assert.That(agent.Execute(result), Is.True);
            Assert.That(famine.Executions, Is.Empty);
        }

        [Test]
        public async Task TwoReplacementsChain()
        {
            var fake = new FakeProvider(Reply());
            var agent = Teller(fake, out _);
            agent.Guards.Add(History());
            agent.Guards.Add(new FakeGuard("Shorten", input => GuardVerdict.ReplaceStatement("too long.", "Fire in Kesh.")));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event."));

            Assert.That(result.Decision.Statement, Is.EqualTo("Fire in Kesh."));
            Assert.That(result.Telemetry.StatementsReplaced.Select(r => r.Replaced), Is.EqualTo(new[] { Story, Fixed }));
            Assert.That(result.Telemetry.GuardsFired, Is.EqualTo(new[] { "HistoryCheck", "Shorten" }));
        }

        [Test]
        public async Task OnACallThatAskedForNoStatementItIsPassedOver()
        {
            var fake = new FakeProvider(Reply());
            var agent = Teller(fake, out _);
            agent.Guards.Add(new FakeGuard("Always", input => GuardVerdict.ReplaceStatement("always.", "A line.")));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Tell this turn's event.", new DecideOptions { AskForStatement = false }));

            Assert.That(result.Decision.ActionId, Is.EqualTo("famine"));
            Assert.That(result.Decision.Statement, Is.Empty, "No statement is put into a call that asked for none.");
            Assert.That(result.Telemetry.StatementsReplaced, Is.Empty);
            Assert.That(result.Telemetry.GuardsFired, Is.Empty);
        }

        // ---- Helpers ----------------------------------------------------------------------------

        // A storyteller with one event that strikes a country.
        private static Agent Teller(FakeProvider fake, out FakeHandler famine)
        {
            var actions = new[] { new ActionDefinition { Id = "famine", Description = "A famine strikes a country.", RequiresTarget = true } };
            var agent = new Agent(new AgentIdentity { Name = "Storyteller", Role = "the teller of the realm's events" }, actions, fake);
            famine = new FakeHandler();
            agent.Bind("famine", famine);
            agent.Targets.Register("kesh", new object(), "country");
            agent.Targets.Register("orsk", new object(), "country");
            return agent;
        }

        private static DecisionFeedEntry Entry(Agent agent, DecisionResult result)
        {
            return new DecisionFeedEntry(agent, result, DecisionStage.Decided, null, DateTime.UtcNow, 1);
        }
    }
}
