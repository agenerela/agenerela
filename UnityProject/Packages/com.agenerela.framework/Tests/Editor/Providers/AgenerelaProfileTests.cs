using System;
using System.Collections.Generic;
using System.Reflection;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Agenerela.Tests
{
    // Settings profiles (DR-026): Development and Production each name a provider config, and the
    // active one is chosen by AGENERELA_PROFILE, then the Editor's choice, then the build type.
    // A project without profiles resolves as it always did.
    public sealed class AgenerelaProfileTests
    {
        private readonly List<Object> created = new List<Object>();
        private string savedProfileVariable;

        [SetUp]
        public void SetUp()
        {
            // The machine's own choice must not decide these tests.
            savedProfileVariable = Environment.GetEnvironmentVariable(AgenerelaSettings.ProfileVariable);
            Environment.SetEnvironmentVariable(AgenerelaSettings.ProfileVariable, null);
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable(AgenerelaSettings.ProfileVariable, savedProfileVariable);
            AgenerelaSettings.Override = null;
            AgenerelaSettings.Forget();
            foreach (var obj in created)
            {
                Object.DestroyImmediate(obj);
            }

            created.Clear();
        }

        [TestCase("Production", ProfileChoice.Automatic, true, true, ProfileKind.Production)]
        [TestCase("prod", ProfileChoice.Development, true, true, ProfileKind.Production)]
        [TestCase("DEV", ProfileChoice.Production, false, false, ProfileKind.Development)]
        [TestCase("development", ProfileChoice.Automatic, false, false, ProfileKind.Development)]
        [TestCase(null, ProfileChoice.Automatic, true, true, ProfileKind.Development)]
        [TestCase(null, ProfileChoice.Production, true, true, ProfileKind.Production)]
        [TestCase(null, ProfileChoice.Development, true, true, ProfileKind.Development)]
        [TestCase(null, ProfileChoice.Automatic, false, true, ProfileKind.Development)]
        [TestCase(null, ProfileChoice.Automatic, false, false, ProfileKind.Production)]
        [TestCase(null, ProfileChoice.Development, false, false, ProfileKind.Production)]
        [TestCase("staging", ProfileChoice.Automatic, false, false, ProfileKind.Production)]
        public void TheVariableThenTheEditorsChoiceThenTheBuildChoose(string variable, ProfileChoice editor, bool isEditor,
                                                                     bool isDebugBuild, ProfileKind expected)
        {
            var kind = AgenerelaSettings.Choose(variable, editor, isEditor, isDebugBuild, out string why);

            Assert.That(kind, Is.EqualTo(expected));
            Assert.That(why, Is.Not.Empty);
        }

        [Test]
        public void AVariableNamingNoProfileIsIgnoredAndSaysSo()
        {
            AgenerelaSettings.Choose("staging", ProfileChoice.Automatic, true, true, out string why);

            Assert.That(why, Does.Contain("staging"));
            Assert.That(why, Does.Contain("ignored"));
        }

        [Test]
        public void AReleaseBuildNeverReadsTheEditorsChoice()
        {
            Assert.That(AgenerelaSettings.Choose(null, ProfileChoice.Development, false, false, out _), Is.EqualTo(ProfileKind.Production),
                "A choice made for the Editor never ships.");
        }

        [Test]
        public void AProjectWithoutProfilesResolvesItsDefaultAsBefore()
        {
            var fallback = Make<FakeProviderConfig>("Default");
            Settings(fallback, null, null);

            Assert.That(ProviderConfig.Resolve(), Is.SameAs(fallback));
            Assert.That(AgenerelaSettings.ActiveProfile, Is.Null);
        }

        [Test]
        public void TheEditorUsesTheDevelopmentProfilesConfig()
        {
            var fallback = Make<FakeProviderConfig>("Default");
            var development = Make<FakeProviderConfig>("Patient");
            var production = Make<FakeProviderConfig>("FailFast");
            Settings(fallback, Profile(development), Profile(production));

            Assert.That(AgenerelaSettings.ActiveProfileKind, Is.EqualTo(ProfileKind.Development), "The Editor runs Development.");
            Assert.That(ProviderConfig.Resolve(), Is.SameAs(development));
        }

        [Test]
        public void TheEditorsChoiceAndTheVariableSwitchToProduction()
        {
            var development = Make<FakeProviderConfig>("Patient");
            var production = Make<FakeProviderConfig>("FailFast");
            var settings = Settings(null, Profile(development), Profile(production));

            settings.EditorProfile = ProfileChoice.Production;
            Assert.That(ProviderConfig.Resolve(), Is.SameAs(production));

            settings.EditorProfile = ProfileChoice.Automatic;
            Environment.SetEnvironmentVariable(AgenerelaSettings.ProfileVariable, "production");
            Assert.That(ProviderConfig.Resolve(), Is.SameAs(production));
            Assert.That(AgenerelaSettings.ActiveProfileReason, Does.Contain(AgenerelaSettings.ProfileVariable));
        }

        [Test]
        public void AnAgentsOwnConfigStillComesFirst()
        {
            var own = Make<FakeProviderConfig>("Own");
            Settings(null, Profile(Make<FakeProviderConfig>("Patient")), null);

            Assert.That(ProviderConfig.Resolve(own), Is.SameAs(own));
        }

        [Test]
        public void AProfileWithoutAConfigFallsBackToTheDefault()
        {
            var fallback = Make<FakeProviderConfig>("Default");
            Settings(fallback, Profile(null), null);

            Assert.That(ProviderConfig.Resolve(), Is.SameAs(fallback));
        }

        [Test]
        public void TheCreateMenuMakesASettingsProfile()
        {
            var menu = typeof(AgenerelaProfile).GetCustomAttribute<CreateAssetMenuAttribute>();

            Assert.That(menu, Is.Not.Null);
            Assert.That(menu.menuName, Is.EqualTo("Agenerela/Settings Profile"));
        }

        [Test]
        public void AnEndpointVariableReplacesTheEndpointWhenItIsSet()
        {
            string url = "AGENERELA_TEST_URL_" + Guid.NewGuid().ToString("N");
            string key = "AGENERELA_TEST_KEY_" + Guid.NewGuid().ToString("N");
            try
            {
                Environment.SetEnvironmentVariable(url, "http://192.0.2.7:11436");
                Environment.SetEnvironmentVariable(key, "k3y");
                var config = Make<OllamaProviderConfig>("OllamaHome");
                config.EndpointVariable = url;
                config.ApiKeyVariable = key;

                var settings = config.ToSettings();

                Assert.That(settings.Endpoint, Is.EqualTo("http://192.0.2.7:11436"));
                Assert.That(settings.ApiKey, Is.EqualTo("k3y"));
                Assert.That(config.UsesFallbackEndpoint(), Is.False);
                Assert.That(JsonUtility.ToJson(config), Does.Not.Contain("192.0.2.7"), "The asset holds the variable's name only.");
            }
            finally
            {
                Environment.SetEnvironmentVariable(url, null);
                Environment.SetEnvironmentVariable(key, null);
            }
        }

        [Test]
        public void WithoutTheEndpointVariableTheFallbackGetsNoKey()
        {
            string url = "AGENERELA_TEST_URL_" + Guid.NewGuid().ToString("N");
            string key = "AGENERELA_TEST_KEY_" + Guid.NewGuid().ToString("N");
            try
            {
                Environment.SetEnvironmentVariable(key, "k3y");
                var config = Make<OllamaProviderConfig>("OllamaHome");
                config.EndpointVariable = url;
                config.ApiKeyVariable = key;

                var settings = config.ToSettings();

                Assert.That(settings.Endpoint, Is.EqualTo("http://127.0.0.1:11434"), "A teammate without the server gets a local Ollama.");
                Assert.That(settings.ApiKey, Is.Empty, "The server's key never goes to another address.");
                Assert.That(config.UsesFallbackEndpoint(), Is.True);
            }
            finally
            {
                Environment.SetEnvironmentVariable(key, null);
            }
        }

        [Test]
        public void AConfigsRetryPolicyReachesItsSettingsAsACopy()
        {
            var config = Make<OllamaProviderConfig>("OllamaHome");
            config.Retry = RetryPolicy.Patient;

            var settings = config.ToSettings();

            Assert.That(settings.Retry, Is.Not.SameAs(config.Retry));
            Assert.That(settings.Retry.DeadlineSeconds, Is.EqualTo(600f));
            Assert.That(settings.Retry.MaxAttempts, Is.EqualTo(RetryPolicy.Patient.MaxAttempts));
        }

        private AgenerelaSettings Settings(ProviderConfig fallback, AgenerelaProfile development, AgenerelaProfile production)
        {
            var settings = Make<AgenerelaSettings>("Settings");
            settings.DefaultProvider = fallback;
            settings.Development = development;
            settings.Production = production;
            AgenerelaSettings.Override = settings;
            return settings;
        }

        private AgenerelaProfile Profile(ProviderConfig config)
        {
            var profile = Make<AgenerelaProfile>("Profile");
            profile.DefaultProvider = config;
            return profile;
        }

        private T Make<T>(string name) where T : ScriptableObject
        {
            var obj = ScriptableObject.CreateInstance<T>();
            obj.name = name;
            created.Add(obj);
            return obj;
        }
    }
}
