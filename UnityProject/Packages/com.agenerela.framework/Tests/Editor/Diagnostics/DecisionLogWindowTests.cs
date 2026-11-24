using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Agenerela.Editor.Diagnostics;
using Agenerela.Editor.UI;
using Agenerela.Tests.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Agenerela.Tests.Diagnostics
{
    /// <summary>
    /// The Decision Log window (#172, screen 3): its rows, filters, guard list, export and
    /// subscription, each driven through the real <see cref="DecisionFeed"/> by agents on a
    /// <see cref="FakeProvider"/>. The tree is built from its UXML and read directly, with no window,
    /// but for the test that the closed window leaves the feed alone.
    /// </summary>
    /// <remarks>
    /// Named for the window, not the log: <c>DecisionLogTests</c> beside it covers the
    /// <see cref="DecisionLog"/> file writer (#190, G7).
    /// </remarks>
    public sealed class DecisionLogWindowTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";
        private const string AttackTheDummy = "{\"action\":\"attack_target\",\"target\":\"training_dummy\",\"statement\":\"Stand back.\"}";
        private const string DoNothing = "{\"action\":\"none\",\"target\":\"no_target\",\"statement\":\"Lovely evening.\"}";

        private readonly object owner = new object();
        private readonly List<Object> created = new List<Object>();
        private DecisionLogStore store;

        [SetUp]
        public void SetUp()
        {
            store = new DecisionLogStore();
            store.Attach(owner);
        }

        [TearDown]
        public void TearDown()
        {
            store.Detach(owner);
            EditorUiFixtures.CloseAll();
            foreach (var obj in created)
            {
                Object.DestroyImmediate(obj);
            }

            created.Clear();
            Fixtures.DestroyCreated();
        }

        // ---- ③ Outcomes -----------------------------------------------------------------------------

        [Test]
        public async Task EachOutcomeGetsItsIcon()
        {
            var executed = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            Assert.That(executed.Execute(await Fixtures.NotCancelled(executed.DecideAsync("Go to the tower."))), Is.True);

            var idle = Fixtures.VillageGuard(new FakeProvider(DoNothing));
            await Fixtures.NotCancelled(idle.DecideAsync("Lovely evening, isn't it?"));

            var contained = Fixtures.VillageGuard(new FakeProvider(AttackTheDummy));
            contained.Guards.Add(FakeGuard.Rewriting("NoAttacks", "attacking is not allowed here."));
            await Fixtures.NotCancelled(contained.DecideAsync("Attack Godzilla."));

            var failing = Fixtures.VillageGuard(new FakeProvider(GoToTheTower) { Failure = new InvalidOperationException("Ollama not reachable") });
            LogAssert.Expect(LogType.Warning, new Regex("could not decide"));
            await Fixtures.NotCancelled(failing.DecideAsync("Go to the bridge."));

            var waiting = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            await Fixtures.NotCancelled(waiting.DecideAsync("Go to the tower when you can."));

            var expected = new[] { Outcome.Executed, Outcome.NoAction, Outcome.Contained, Outcome.Error, Outcome.NotExecuted };
            Assert.That(store.Rows.Select(r => r.Outcome), Is.EqualTo(expected),
                "One row per decision, its Execute entry on the same row, each with the outcome the game saw.");

            for (int i = 0; i < expected.Length; i++)
            {
                var row = DecisionLogUi.MakeRow();
                DecisionLogUi.BindRow(row, store.Rows[i]);
                var icon = row.Q<OutcomeIcon>(DecisionLogUi.RowIconName);
                Assert.That(icon.Outcome, Is.EqualTo(expected[i]));
                Assert.That(icon.image, Is.SameAs(OutcomeIcon.Icon(expected[i])));
            }

            Assert.That(DecisionLogRow.ResultText(store.Rows[3].Line), Is.EqualTo("error: ProviderFailed"), "An error shows its kind.");
            Assert.That(DecisionLogRow.LatencyText(store.Rows[3].Line), Is.EqualTo("n/a"));
        }

        [Test]
        public async Task ExecuteRefusingADecisionCountsAsContained()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            // The tower is gone by the time the game executes it: Execute's re-check refuses.
            agent.Targets.Unregister("tower");
            LogAssert.Expect(LogType.Warning, new Regex("did not execute"));
            Assert.That(agent.Execute(result), Is.False);

            Assert.That(store.Rows, Has.Count.EqualTo(1));
            Assert.That(store.Rows[0].Outcome, Is.EqualTo(Outcome.Contained));
            var guards = DecisionLogDetail.Guards(store.Rows[0].Line);
            Assert.That(guards.Last().Step, Is.EqualTo(GuardStep.RefusedAtExecute));
            Assert.That(guards.Last().Guard, Does.EndWith("@Execute"));
        }

        // ---- ① Rows ---------------------------------------------------------------------------------

        [Test]
        public async Task ARewrittenDecisionShowsBothValues()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(AttackTheDummy));
            agent.Guards.Add(FakeGuard.Rewriting("NoAttacks"));

            await Fixtures.NotCancelled(agent.DecideAsync("Attack Godzilla."));

            var row = DecisionLogUi.MakeRow();
            DecisionLogUi.BindRow(row, store.Rows.Single());
            Assert.That(row.Q<Label>(DecisionLogUi.RowResultName).text, Is.EqualTo("attack_target · training_dummy ⇒ none"));
            Assert.That(row.Q<Label>(DecisionLogUi.RowAskedName).text, Is.EqualTo("Player: “Attack Godzilla.”"));
        }

        [Test]
        public async Task AnUnchangedDecisionShowsOneValue()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(DecisionLogRow.ResultText(store.Rows.Single().Line), Is.EqualTo("move_to · tower"));
        }

        [Test]
        public async Task APlainAgentWithNoGameObjectShowsItsIdentitysName()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(store.Rows.Single().Agent, Is.EqualTo("Village Guard"));
            Assert.That(store.Rows.Single().GameObject, Is.EqualTo(0), "Nothing to select in the scene.");
        }

        [Test]
        public async Task AnAgentOnAGameObjectShowsTheGameObjectsName()
        {
            var config = ScriptableObject.CreateInstance<FakeProviderConfig>();
            config.name = "GateGuardModel";
            config.Reply = DoNothing;
            created.Add(config);
            var profile = Fixtures.VillageGuardProfile();
            profile.Actions.Clear();
            var obj = new GameObject("Guard (Gate)");
            created.Add(obj);
            var guard = obj.AddComponent<AgentBehaviour>();
            guard.FindTargetsNearby = false;
            guard.NoticeNearby = false;
            guard.Profile = profile;
            guard.Provider = config;

            await Fixtures.NotCancelled(guard.DecideAsync("Hello."));

            var row = store.Rows.Single();
            Assert.That(row.Agent, Is.EqualTo("Guard (Gate)"));
            Assert.That(row.GameObject, Is.EqualTo(obj.GetInstanceID()));
            Assert.That(row.ProviderSource, Is.EqualTo("the agent's own Provider field"), "DR-013: which of the three places answered.");
        }

        // ---- ⑤ Guards -------------------------------------------------------------------------------

        [Test]
        public async Task TheGuardListMarksPassedStoppedAndNotRun()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(AttackTheDummy));
            agent.Guards.Add(new FakeGuard("LineOfSight"));
            agent.Guards.Add(FakeGuard.Rewriting("Cooldown", "attacked a moment ago."));
            agent.Guards.Add(new FakeGuard("Curfew"));

            await Fixtures.NotCancelled(agent.DecideAsync("Attack Godzilla."));

            var guards = DecisionLogDetail.Guards(store.Rows.Single().Line);
            Assert.That(guards.Select(g => g.Guard), Is.EqualTo(new[]
            {
                nameof(SchemaLegalityGuard), nameof(TargetGroundingGuard), nameof(TargetCategoryGuard), nameof(TargetAvailabilityGuard),
                "LineOfSight", "Cooldown", "Curfew",
            }), "Built-in and the developer's own, by name, in the order they run.");
            Assert.That(guards.Select(g => g.Step), Is.EqualTo(new[]
            {
                GuardStep.Passed, GuardStep.Passed, GuardStep.Passed, GuardStep.Passed, GuardStep.Passed, GuardStep.Stopped, GuardStep.NotRun,
            }));
            Assert.That(guards[5].Reason, Is.EqualTo("Rewrite to none: attacked a moment ago."));

            var tree = DecisionLogUi.GuardList(store.Rows.Single().Line);
            Assert.That(tree.childCount, Is.EqualTo(7));
            Assert.That(tree.Q("guard-4").ClassListContains("dl-guard--passed"), Is.True);
            Assert.That(tree.Q("guard-5").ClassListContains("dl-guard--stopped"), Is.True);
            Assert.That(tree.Q("guard-6").ClassListContains("dl-guard--not-run"), Is.True);
        }

        [Test]
        public async Task AGuardThatThrowsIsMarkedAndTheRestNotRun()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            agent.Guards.Add(new FakeGuard("Broken", _ => throw new InvalidOperationException("oops")));
            agent.Guards.Add(new FakeGuard("After"));
            LogAssert.Expect(LogType.Warning, new Regex("Broken"));

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            var guards = DecisionLogDetail.Guards(store.Rows.Single().Line);
            Assert.That(store.Rows.Single().Outcome, Is.EqualTo(Outcome.Error));
            Assert.That(guards.Single(g => g.Guard == "Broken").Step, Is.EqualTo(GuardStep.Failed));
            Assert.That(guards.Single(g => g.Guard == "After").Step, Is.EqualTo(GuardStep.NotRun));
        }

        [Test]
        public async Task AProviderErrorRanNoGuard()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower) { Failure = new InvalidOperationException("gone") });
            LogAssert.Expect(LogType.Warning, new Regex("could not decide"));

            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(DecisionLogDetail.Guards(store.Rows.Single().Line), Is.Empty);
        }

        // ---- ② Filters ------------------------------------------------------------------------------

        [Test]
        public async Task FiltersAndSearchNarrowTheRows()
        {
            var gate = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            var bridge = Fixtures.VillageGuard(new FakeProvider(DoNothing));
            gate.Execute(await Fixtures.NotCancelled(gate.DecideAsync("Go to the tower.")));
            await Fixtures.NotCancelled(bridge.DecideAsync("A wolf howls in the woods.", new DecideOptions { StimulusLabel = "Event" }));
            gate.Execute(await Fixtures.NotCancelled(gate.DecideAsync("Go back to the TOWER.")));

            var filter = new DecisionLogFilter();
            Assert.That(filter.Apply(store.Rows), Has.Count.EqualTo(3));
            Assert.That(filter.Apply(store.Rows)[0].Line.Stimulus, Is.EqualTo("Go back to the TOWER."), "Newest first.");

            filter.Search = "tower";
            Assert.That(filter.Apply(store.Rows).Select(r => r.Line.Stimulus), Is.EqualTo(new[] { "Go back to the TOWER.", "Go to the tower." }), "Search ignores case.");

            filter.Search = "event";
            Assert.That(filter.Apply(store.Rows).Single().Line.Stimulus, Is.EqualTo("A wolf howls in the woods."), "The label is searched too.");

            filter.Search = null;
            filter.Hidden.Add(Outcome.Executed);
            Assert.That(filter.Apply(store.Rows).Single().Outcome, Is.EqualTo(Outcome.NoAction));
            Assert.That(filter.Counts(store.Rows)[Outcome.Executed], Is.EqualTo(2), "A hidden outcome keeps its count.");

            filter.Hidden.Clear();
            filter.Agent = "Village Guard";
            Assert.That(DecisionLogFilter.Agents(store.Rows), Is.EqualTo(new[] { "Village Guard" }), "Both are named by their identity.");
            Assert.That(filter.Apply(store.Rows), Has.Count.EqualTo(3));
            filter.Agent = "Nobody";
            Assert.That(filter.Apply(store.Rows), Is.Empty);
        }

        [Test]
        public async Task TheTreeShowsTheFilteredRowsAndCounts()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower again."));

            var ui = new DecisionLogUi(DecisionLogUi.Load());
            var filter = new DecisionLogFilter { Search = "again" };
            ui.Show(new DecisionLogUi.State
            {
                Shown = filter.Apply(store.Rows), All = store.Rows, Filter = filter, Counts = filter.Counts(store.Rows),
                Agents = DecisionLogFilter.Agents(store.Rows), Selected = store.Rows[1],
            });

            Assert.That(ui.Root.Q<ListView>(DecisionLogUi.ListName).itemsSource.Count, Is.EqualTo(1));
            Assert.That(ui.Root.Q<Label>(DecisionLogUi.StatusName).text, Does.StartWith("2 decisions · 1 agent").And.EndWith("1 shown"));
            Assert.That(ui.Root.Q<Label>(DecisionLogUi.TitleName).text, Is.EqualTo("Decided, not executed yet"));
            Assert.That(ui.Root.Q(DecisionLogUi.OfferedActionsName).Query<Chip>().ToList().Select(c => c.Id), Does.Contain("move_to"));
        }

        // ---- The store ------------------------------------------------------------------------------

        [Test]
        public async Task TheCapDropsTheOldestRows()
        {
            store.Cap = 3;
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            for (int i = 1; i <= 5; i++)
            {
                await Fixtures.NotCancelled(agent.DecideAsync("Order " + i));
            }

            Assert.That(store.Rows.Select(r => r.Line.Stimulus), Is.EqualTo(new[] { "Order 3", "Order 4", "Order 5" }));
        }

        [Test]
        public async Task TheRowsSurviveTheSerializationOfADomainReload()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            agent.Execute(await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower.")));
            await Fixtures.NotCancelled(agent.DecideAsync("Wait here."));

            var copy = JsonUtility.FromJson<DecisionLogStore>(JsonUtility.ToJson(store));

            Assert.That(copy.Rows.Select(r => DecisionLogStore.ToJson(r.Line)), Is.EqualTo(store.Rows.Select(r => DecisionLogStore.ToJson(r.Line))));
            Assert.That(copy.Rows.Select(r => r.Outcome), Is.EqualTo(new[] { Outcome.Executed, Outcome.NotExecuted }));
            Assert.That(copy.Rows[0].Agent, Is.EqualTo("Village Guard"));
        }

        [Test]
        public async Task ExportWritesOneObjectPerLineAndReadsBackTheSameRows()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(AttackTheDummy));
            agent.Guards.Add(FakeGuard.Rewriting("NoAttacks"));
            agent.Execute(await Fixtures.NotCancelled(agent.DecideAsync("Attack Godzilla.")));
            var plain = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            plain.Execute(await Fixtures.NotCancelled(plain.DecideAsync("Go to the tower.")));

            var text = DecisionLogStore.ToJsonLines(store.Rows);
            var lines = text.Split('\n').Where(l => l.Length > 0).ToList();
            Assert.That(lines, Has.Count.EqualTo(2), "One line per row: a decision and its execution are one row.");
            Assert.That(text, Does.EndWith("\n"));
            Assert.That(lines.All(l => l.StartsWith("{") && l.EndsWith("}")), Is.True);

            var read = DecisionLogRow.FromLines(DecisionLogReader.Parse(lines, out var skipped));
            Assert.That(skipped, Is.Zero);
            Assert.That(read.Select(r => DecisionLogStore.ToJson(r.Line)), Is.EqualTo(store.Rows.Select(r => DecisionLogStore.ToJson(r.Line))));
            Assert.That(read.Select(r => r.Outcome), Is.EqualTo(store.Rows.Select(r => r.Outcome)));
            Assert.That(read.Select(r => DecisionLogRow.ResultText(r.Line)), Is.EqualTo(new[] { "attack_target · training_dummy ⇒ none", "move_to · tower" }));
        }

        [Test]
        public async Task WithNoWindowAttachedTheFeedHasNoSubscriberFromTheLog()
        {
            Assert.That(DecisionFeed.IsSubscribed(store.OnEntry), Is.True);

            store.Detach(owner);

            Assert.That(store.Subscribed, Is.False);
            Assert.That(DecisionFeed.IsSubscribed(store.OnEntry), Is.False);
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));
            Assert.That(store.Rows, Is.Empty);
        }

        [Test]
        public async Task RecordingOffUnsubscribesAndKeepsTheRows()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            store.Recording = false;
            await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower again."));

            Assert.That(DecisionFeed.IsSubscribed(store.OnEntry), Is.False);
            Assert.That(store.Rows.Single().Line.Stimulus, Is.EqualTo("Go to the tower."));
        }

        [Test]
        public void TheLayoutAndStylesheetImportCleanly()
        {
            var sheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>(DecisionLogUi.SheetPath);

            Assert.That(sheet, Is.Not.Null, DecisionLogUi.SheetPath);
            Assert.That(sheet.importedWithErrors, Is.False);
            Assert.That(sheet.importedWithWarnings, Is.False);
            Assert.That(() => new DecisionLogUi(DecisionLogUi.Load()), Throws.Nothing, "Every named part the tree needs is in the UXML.");
        }

        [Test]
        public void ClosingTheWindowLeavesTheFeedAlone()
        {
            var own = new DecisionLogStore();
            var tree = EditorUiFixtures.Window<DecisionLogWindow>();
            var window = (DecisionLogWindow)tree.Window;
            window.UseStore(own);
            Assert.That(DecisionFeed.IsSubscribed(own.OnEntry), Is.True, "Open, it listens.");

            window.Close();

            Assert.That(DecisionFeed.IsSubscribed(own.OnEntry), Is.False, "Closed, it does not.");
            Assert.That(own.Subscribed, Is.False);
        }
    }
}
