using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Agenerela.Tests
{
    // The scripted provider as an asset (#190, G6): what a scene selects to present a game with the
    // model off.
    public sealed class ScriptedProviderConfigTests
    {
        private const string GoToTheTower = "Go to the tower.";

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            AgenerelaSettings.Override = null;
            AgenerelaSettings.Forget();
            foreach (var obj in created)
            {
                Object.DestroyImmediate(obj);
            }

            created.Clear();
            Fixtures.DestroyCreated();
        }

        [Test]
        public void TheCreateMenuMakesAScriptedProvider()
        {
            var menu = typeof(ScriptedProviderConfig).GetCustomAttribute<CreateAssetMenuAttribute>();

            Assert.That(menu, Is.Not.Null);
            Assert.That(menu.menuName, Is.EqualTo("Agenerela/Scripted Provider"));
            Assert.That(menu.fileName, Is.EqualTo("Scripted"));
        }

        [Test]
        public async Task ANewConfigHasEveryAgentDoNothingAndSayNothing()
        {
            var config = Make("Scripted");

            var result = await Fixtures.NotCancelled(Fixtures.VillageGuard(config.Provider).DecideAsync(GoToTheTower));

            Assert.That(config.Replies, Has.Count.EqualTo(1));
            Assert.That(result.Decision.ActionId, Is.EqualTo(ActionRegistry.None));
            Assert.That(result.Decision.TargetId, Is.EqualTo(TargetRegistry.NoTarget));
            Assert.That(result.Decision.Statement, Is.Empty);
        }

        [Test]
        public async Task EntriesAnswerByAgentAndStimulusInListOrderAfterTheirLatency()
        {
            var config = Make("ScriptedVillage");
            config.Replies = new List<ScriptedProviderConfig.Entry>
            {
                Entry("", "", "none", "no_target", "Hm."),
                Entry("Village Guard", GoToTheTower, "move_to", "tower", "On my way.", 0.2f),
                Entry("Village Guard", "", "none", "no_target", "Guard's default."),
                Entry("Village Guard", GoToTheTower, "none", "no_target", "Again?"),
            };
            var guard = Fixtures.VillageGuard(config.Provider);
            var other = Fixtures.VillageGuard(config.Provider);
            other.Identity.Name = "Gate Captain";

            var first = await Fixtures.NotCancelled(guard.DecideAsync(GoToTheTower));
            var second = await Fixtures.NotCancelled(guard.DecideAsync(GoToTheTower));
            var third = await Fixtures.NotCancelled(guard.DecideAsync(GoToTheTower));
            var anything = await Fixtures.NotCancelled(guard.DecideAsync("Anything."));
            var captain = await Fixtures.NotCancelled(other.DecideAsync(GoToTheTower));

            Assert.That(first.Decision.TargetId, Is.EqualTo("tower"));
            Assert.That(first.Telemetry.LatencySeconds, Is.GreaterThanOrEqualTo(0.19f));
            Assert.That(second.Decision.Statement, Is.EqualTo("Again?"));
            Assert.That(third.Decision.Statement, Is.EqualTo("Again?"), "The last repeats.");
            Assert.That(anything.Decision.Statement, Is.EqualTo("Guard's default."));
            Assert.That(captain.Decision.Statement, Is.EqualTo("Hm."));
            Assert.That(first.Telemetry.ProviderConfig, Is.EqualTo("ScriptedVillage"), "Telemetry names the config.");
            Assert.That(first.Telemetry.ProviderName, Is.EqualTo("scripted"));
        }

        [Test]
        public async Task ItsRefusalLineAnswersAStoppedDecision()
        {
            var config = Make("Scripted");
            config.Replies[0] = Entry("", "", "move_to", "tower", "On my way.");
            config.RefusalLine = "Not there.";
            var guard = Fixtures.VillageGuard(config.Provider);
            guard.Guards.Add(FakeGuard.Rewriting("Stopper"));

            var stopped = await Fixtures.NotCancelled(guard.DecideAsync(GoToTheTower, new DecideOptions { AskForRefusalLine = true }));

            Assert.That(stopped.Decision.Statement, Is.EqualTo("Not there."));
        }

        [Test]
        public async Task CodeCanScriptTheProviderTheConfigBuilt()
        {
            var config = Make("Scripted");
            config.Scripted.Otherwise(ScriptedReply.Fail(ProviderErrorKind.Unreachable));

            var result = await Fixtures.NotCancelled(Fixtures.VillageGuard(config.Provider).DecideAsync(GoToTheTower));

            Assert.That(config.Scripted, Is.SameAs(config.Provider));
            Assert.That(result.Telemetry.ErrorKind, Is.EqualTo(PipelineErrorKind.ProviderUnreachable));
        }

        [Test]
        public void AnEntryWithNoActionOrTargetNamesTheConfigAndTheEntry()
        {
            var noAction = Make("Broken");
            noAction.Replies.Add(Entry("Village Guard", GoToTheTower, " ", "tower", ""));
            var noTarget = Make("AlsoBroken");
            noTarget.Replies.Add(Entry("Village Guard", GoToTheTower, "move_to", "", ""));
            var slow = Make("TooSlow");
            slow.Replies[0].LatencySeconds = -1f;

            var actionError = Assert.Throws<InvalidOperationException>(() => _ = noAction.Provider);
            var targetError = Assert.Throws<InvalidOperationException>(() => _ = noTarget.Provider);
            var latencyError = Assert.Throws<InvalidOperationException>(() => _ = slow.Provider);

            Assert.That(actionError.Message, Does.Contain("'Broken'").And.Contain("Replies[1]").And.Contain("Action"));
            Assert.That(targetError.Message, Does.Contain("'AlsoBroken'").And.Contain("Replies[1]").And.Contain("Target"));
            Assert.That(latencyError.Message, Does.Contain("'TooSlow'").And.Contain("Replies[0]").And.Contain("LatencySeconds"));
        }

        [Test]
        public async Task AnEditOutsidePlayModeReachesTheNextProvider()
        {
            var config = Make("Scripted");
            var before = config.Provider;
            config.Replies[0].Statement = "Edited.";

            config.Changed(playing: false);
            var result = await Fixtures.NotCancelled(Fixtures.VillageGuard(config.Provider).DecideAsync(GoToTheTower));

            Assert.That(config.Provider, Is.Not.SameAs(before));
            Assert.That(result.Decision.Statement, Is.EqualTo("Edited."));
        }

        [Test]
        public async Task AsTheProjectDefaultItAnswersEveryAgentWithoutAConfig()
        {
            var config = Make("Scripted");
            var settings = ScriptableObject.CreateInstance<AgenerelaSettings>();
            created.Add(settings);
            settings.DefaultProvider = config;
            AgenerelaSettings.Override = settings;

            var result = await Fixtures.NotCancelled(Fixtures.VillageGuard(ProviderConfig.Resolve().Provider).DecideAsync(GoToTheTower));

            Assert.That(result.Telemetry.ProviderName, Is.EqualTo("scripted"));
            Assert.That(result.Telemetry.ProviderConfig, Is.EqualTo("Scripted"));
        }

        private ScriptedProviderConfig Make(string name)
        {
            var config = ScriptableObject.CreateInstance<ScriptedProviderConfig>();
            config.name = name;
            created.Add(config);
            return config;
        }

        private static ScriptedProviderConfig.Entry Entry(string agent, string stimulus, string action, string target,
                                                         string statement, float latency = 0f)
        {
            return new ScriptedProviderConfig.Entry
            {
                Agent = agent,
                Stimulus = stimulus,
                Action = action,
                Target = target,
                Statement = statement,
                LatencySeconds = latency,
            };
        }
    }
}
