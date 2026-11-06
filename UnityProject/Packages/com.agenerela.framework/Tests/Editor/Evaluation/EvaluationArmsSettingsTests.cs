using Agenerela.Editor;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Agenerela.Tests.Evaluation
{
    // Project Settings → Agenerela names the two evaluation arms beside the default (DR-013, #145),
    // saved where a player finds them. These write real assets in a folder of their own.
    public sealed class EvaluationArmsSettingsTests
    {
        private const string Folder = "Assets/EvaluationArmsSettingsTests";
        private const string SettingsAt = Folder + "/Resources/" + AgenerelaSettings.ResourceName + ".asset";

        [SetUp]
        public void SetUp()
        {
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
        public void TheArmsAreSavedBesideTheDefaultWhereAPlayerFindsThem()
        {
            var fallback = ConfigAsset("OllamaLocal");
            var a = ConfigAsset("ArmA");
            var b = ConfigAsset("ArmB");

            AgenerelaSettingsPage.SetDefault(fallback, SettingsAt);
            AgenerelaSettingsPage.Set(AgenerelaSettingsPage.Slot.ArmA, a, SettingsAt);
            AgenerelaSettingsPage.Set(AgenerelaSettingsPage.Slot.ArmB, b, SettingsAt);

            AgenerelaSettings.Forget();
            var settings = Resources.Load<AgenerelaSettings>(AgenerelaSettings.ResourceName);
            Assert.That(settings.DefaultProvider, Is.SameAs(fallback));
            Assert.That(settings.EvaluationArmA, Is.SameAs(a));
            Assert.That(settings.EvaluationArmB, Is.SameAs(b));
        }

        [Test]
        public void SettingAnArmLeavesTheDefaultAlone()
        {
            var fallback = ConfigAsset("OllamaLocal");
            AgenerelaSettingsPage.SetDefault(fallback, SettingsAt);

            AgenerelaSettingsPage.Set(AgenerelaSettingsPage.Slot.ArmB, ConfigAsset("ArmB"), SettingsAt);

            Assert.That(ProviderConfig.Resolve(), Is.SameAs(fallback), "Agents never read the arms.");
        }

        [Test]
        public void SettingAnArmCreatesTheSettingsWhenThereAreNone()
        {
            var a = ConfigAsset("ArmA");

            var settings = AgenerelaSettingsPage.Set(AgenerelaSettingsPage.Slot.ArmA, a, SettingsAt);

            Assert.That(settings, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(settings), Is.EqualTo(SettingsAt));
            Assert.That(settings.DefaultProvider, Is.Null);
        }

        private static OllamaProviderConfig ConfigAsset(string name)
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", "EvaluationArmsSettingsTests");
            }

            var config = ScriptableObject.CreateInstance<OllamaProviderConfig>();
            AssetDatabase.CreateAsset(config, $"{Folder}/{name}.asset");
            return config;
        }
    }
}
