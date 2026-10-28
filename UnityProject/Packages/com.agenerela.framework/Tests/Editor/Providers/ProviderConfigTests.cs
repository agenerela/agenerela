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
    // The provider config asset and the project default (#92, DR-013). Nothing here needs Ollama:
    // an OllamaProviderConfig only builds its provider, which sends nothing until asked.
    public sealed class ProviderConfigTests
    {
        private const string GoToTheTower = "{\"action\":\"move_to\",\"target\":\"tower\",\"statement\":\"On my way.\"}";

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
        public void AnOllamaConfigStartsWithTheProvidersOwnDefaults()
        {
            var config = Make<OllamaProviderConfig>("OllamaLocal");
            var defaults = new OllamaSettings();

            Assert.That(config.Endpoint, Is.EqualTo("http://127.0.0.1:11434"), "127.0.0.1, never localhost (findings, 5 October).");
            Assert.That(config.Endpoint, Is.EqualTo(defaults.Endpoint));
            Assert.That(config.Model, Is.EqualTo(defaults.Model));
            Assert.That(config.ContextSize, Is.EqualTo(defaults.ContextSize));
            Assert.That(config.Temperature, Is.EqualTo(defaults.Temperature));
            Assert.That(config.TimeoutSeconds, Is.EqualTo(defaults.TimeoutSeconds));
            Assert.That(config.MaxInFlight, Is.EqualTo(1), "The scheduler's own default, as the prototype measured safe.");
        }

        [Test]
        public void TheCreateMenuMakesAnOllamaProvider()
        {
            var menu = typeof(OllamaProviderConfig).GetCustomAttribute<CreateAssetMenuAttribute>();

            Assert.That(menu, Is.Not.Null);
            Assert.That(menu.menuName, Is.EqualTo("Agenerela/Ollama Provider"));
        }

        [Test]
        public void AnOllamaConfigBuildsAnOllamaProviderFromItsFields()
        {
            var config = Make<OllamaProviderConfig>("OllamaLocal");
            config.Model = "llama3.2:3b";
            config.TimeoutSeconds = 12f;

            var settings = config.ToSettings();

            Assert.That(settings.Model, Is.EqualTo("llama3.2:3b"));
            Assert.That(settings.TimeoutSeconds, Is.EqualTo(12f));
            Assert.That(config.Provider, Is.TypeOf<OllamaProvider>());
        }

        [Test]
        public void EveryReadGivesTheSameProviderSoAgentsShareOneQueue()
        {
            var config = Make<FakeProviderConfig>("Shared");

            var first = config.Provider;
            var second = config.Provider;

            // The queue is found by the provider instance, so one instance is one queue.
            Assert.That(second, Is.SameAs(first));
            Assert.That(config.Builds, Is.EqualTo(1));
        }

        [Test]
        public void MaxInFlightReachesTheProvidersQueue()
        {
            var config = Make<FakeProviderConfig>("Wide");
            SetMaxInFlight(config, 3);

            Assert.That(RequestScheduler.For(config.Provider).MaxInFlight, Is.EqualTo(3));
        }

        [Test]
        public void AnOllamaConfigsMaxInFlightReachesItsQueueToo()
        {
            var config = Make<OllamaProviderConfig>("OllamaLocal");
            SetMaxInFlight(config, 4);

            Assert.That(RequestScheduler.For(config.Provider).MaxInFlight, Is.EqualTo(4));
        }

        [Test]
        public void AnEditOutsidePlayModeBuildsANewProvider()
        {
            var config = Make<FakeProviderConfig>("Edited");
            var before = config.Provider;

            config.Changed(playing: false);

            Assert.That(config.Provider, Is.Not.SameAs(before));
            Assert.That(config.Builds, Is.EqualTo(2));
        }

        [Test]
        public void AnEditDuringPlayKeepsTheProviderAndUpdatesItsQueue()
        {
            var config = Make<FakeProviderConfig>("Playing");
            var before = config.Provider;
            SetMaxInFlight(config, 2);

            config.Changed(playing: true);

            Assert.That(config.Provider, Is.SameAs(before), "Rebuilding mid-Play would split one config's queue in two.");
            Assert.That(config.Builds, Is.EqualTo(1));
            Assert.That(RequestScheduler.For(before).MaxInFlight, Is.EqualTo(2));
        }

        [Test]
        public void ChangingPlayModeDropsEveryBuiltProvider()
        {
            var config = Make<FakeProviderConfig>("AcrossPlay");
            var before = config.Provider;

            ProviderConfig.ForgetAll();

            Assert.That(config.Provider, Is.Not.SameAs(before));
        }

        [Test]
        public void MaxInFlightBelowOneIsAnErrorNamingTheConfig()
        {
            var config = Make<FakeProviderConfig>("Narrow");
            SetMaxInFlight(config, 0);

            var e = Assert.Throws<InvalidOperationException>(() => _ = config.Provider);

            Assert.That(e.Message, Does.Contain("'Narrow'").And.Contain("MaxInFlight 0"));
            Assert.That(config.Builds, Is.EqualTo(0));
        }

        [Test]
        public void AnInvalidSettingIsAnErrorNamingTheConfigAndTheSetting()
        {
            var config = Make<OllamaProviderConfig>("NoModel");
            config.Model = " ";

            var e = Assert.Throws<InvalidOperationException>(() => _ = config.Provider);

            Assert.That(e.Message, Does.Contain("'NoModel'").And.Contain("Model is empty"));
            Assert.That(e.InnerException, Is.TypeOf<ArgumentException>());
        }

        [Test]
        public void AConfigThatBuildsNothingIsAnErrorNotANullProvider()
        {
            var config = Make<FakeProviderConfig>("Broken");
            config.BuildsNothing = true;

            var e = Assert.Throws<InvalidOperationException>(() => _ = config.Provider);

            Assert.That(e.Message, Does.Contain("FakeProviderConfig.CreateProvider returned null for 'Broken'"));
        }

        [Test]
        public void AnAgentsOwnConfigWinsOverTheDefault()
        {
            var own = Make<FakeProviderConfig>("Own");
            UseDefault(Make<FakeProviderConfig>("Default"));

            Assert.That(ProviderConfig.Resolve(own), Is.SameAs(own));
        }

        [Test]
        public void WithNoConfigOfItsOwnAnAgentGetsTheDefault()
        {
            var fallback = Make<FakeProviderConfig>("Default");
            UseDefault(fallback);

            Assert.That(ProviderConfig.Resolve(), Is.SameAs(fallback));
            Assert.That(ProviderConfig.Resolve(null), Is.SameAs(fallback));
        }

        [Test]
        public void ADestroyedConfigCountsAsNone()
        {
            var fallback = Make<FakeProviderConfig>("Default");
            UseDefault(fallback);
            var own = ScriptableObject.CreateInstance<FakeProviderConfig>();
            Object.DestroyImmediate(own);

            Assert.That(ProviderConfig.Resolve(own), Is.SameAs(fallback));
        }

        [Test]
        public void WithNoDefaultTheErrorSaysHowToMakeOne()
        {
            UseDefault(null);

            var e = Assert.Throws<InvalidOperationException>(() => ProviderConfig.Resolve());

            Assert.That(e.Message, Does.Contain(
                "Create one with Create → Agenerela → Ollama Provider, and set it in Project Settings → Agenerela"));
        }

        [Test]
        public void WithNoSettingsAtAllTheErrorIsTheSame()
        {
            if (AgenerelaSettings.Load() != null)
            {
                Assert.Ignore("This project has its own Agenerela settings.");
            }

            var e = Assert.Throws<InvalidOperationException>(() => ProviderConfig.Resolve());

            Assert.That(e.Message, Is.EqualTo(ProviderConfig.NoConfigMessage));
        }

        [Test]
        public async Task TelemetryRecordsTheConfigThatResolved()
        {
            var fallback = Make<FakeProviderConfig>("OllamaLocal");
            fallback.Reply = GoToTheTower;
            UseDefault(fallback);
            var agent = Fixtures.VillageGuard(ProviderConfig.Resolve().Provider);

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Telemetry.ProviderConfig, Is.EqualTo("OllamaLocal"));
        }

        [Test]
        public async Task TelemetryRecordsNoConfigForAProviderBuiltInCode()
        {
            var agent = Fixtures.VillageGuard(new FakeProvider(GoToTheTower));

            var result = await Fixtures.NotCancelled(agent.DecideAsync("Go to the tower."));

            Assert.That(result.Telemetry.ProviderConfig, Is.Empty);
        }

        [Test]
        public void NewTelemetryHasNoConfig()
        {
            Assert.That(new DecisionTelemetry().ProviderConfig, Is.Empty);
        }

        private T Make<T>(string name) where T : ProviderConfig
        {
            var config = ScriptableObject.CreateInstance<T>();
            config.name = name;
            created.Add(config);
            return config;
        }

        private void UseDefault(ProviderConfig config)
        {
            var settings = ScriptableObject.CreateInstance<AgenerelaSettings>();
            settings.DefaultProvider = config;
            created.Add(settings);
            AgenerelaSettings.Override = settings;
        }

        // Written to the serialized field directly, as the Inspector stores it, without the
        // OnValidate an Inspector edit would also run: the tests call Changed themselves.
        private static void SetMaxInFlight(ProviderConfig config, int value)
        {
            typeof(ProviderConfig).GetField("maxInFlight", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(config, value);
        }
    }
}
