using System.Linq;
using System.Reflection;
using Agenerela.Editor;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Agenerela.Tests
{
    // Project Settings → Agenerela, and the project default it saves, read back the way a built
    // player reads it: Resources.Load, with no AssetDatabase on the reading side (#92). These write
    // real assets, in a folder of their own that the tear-down deletes.
    public sealed class AgenerelaSettingsPageTests
    {
        private const string Folder = "Assets/AgenerelaSettingsPageTests";
        private const string SettingsAt = Folder + "/Resources/" + AgenerelaSettings.ResourceName + ".asset";

        [SetUp]
        public void SetUp()
        {
            // The page finds settings wherever they are, so a project with its own would be edited.
            if (AgenerelaSettingsPage.Find() != null)
            {
                Assert.Ignore("This project has its own Agenerela settings.");
            }
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
            AgenerelaSettings.Override = null;
            AgenerelaSettings.Forget();
        }

        [Test]
        public void ThePageIsProjectSettingsAgenerela()
        {
            var create = typeof(AgenerelaSettingsPage)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .Single(m => m.GetCustomAttribute<SettingsProviderAttribute>() != null);

            var page = (SettingsProvider)create.Invoke(null, null);

            Assert.That(page.settingsPath, Is.EqualTo("Project/Agenerela"));
            Assert.That(page.scope, Is.EqualTo(SettingsScope.Project));
        }

        [Test]
        public void SettingADefaultSavesItWhereAPlayerFindsIt()
        {
            var config = ConfigAsset("OllamaLocal");

            AgenerelaSettingsPage.SetDefault(config, SettingsAt);

            AgenerelaSettings.Forget();
            Assert.That(Resources.Load<AgenerelaSettings>(AgenerelaSettings.ResourceName), Is.Not.Null);
            Assert.That(ProviderConfig.Resolve(), Is.SameAs(config));
        }

        [Test]
        public void ChangingTheDefaultEditsTheSameSettings()
        {
            var first = AgenerelaSettingsPage.SetDefault(ConfigAsset("First"), SettingsAt);
            var second = ConfigAsset("Second");

            var settings = AgenerelaSettingsPage.SetDefault(second, SettingsAt);

            Assert.That(settings, Is.SameAs(first));
            Assert.That(ProviderConfig.Resolve(), Is.SameAs(second));
        }

        [Test]
        public void ClearingADefaultThatWasNeverSetCreatesNothing()
        {
            Assert.That(AgenerelaSettingsPage.SetDefault(null, SettingsAt), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<AgenerelaSettings>(SettingsAt), Is.Null);
        }

        [Test]
        public void TheDefaultAssetPathIsAResourcesFolderUnderAssets()
        {
            Assert.That(AgenerelaSettingsPage.AssetPath, Is.EqualTo("Assets/Agenerela/Resources/AgenerelaSettings.asset"));
        }

        // A config saved as an asset, since a saved setting can only refer to one.
        private static OllamaProviderConfig ConfigAsset(string name)
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", "AgenerelaSettingsPageTests");
            }

            var config = ScriptableObject.CreateInstance<OllamaProviderConfig>();
            AssetDatabase.CreateAsset(config, $"{Folder}/{name}.asset");
            return config;
        }
    }
}
