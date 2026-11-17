using System.Collections.Generic;
using Agenerela.Editor.Evaluation;
using NUnit.Framework;

namespace Agenerela.Tests.Evaluation
{
    /// <summary>
    /// What the Prompt Set window remembers between sessions (#158). A dictionary stands in for
    /// EditorPrefs, so no test touches this machine's real preferences.
    /// </summary>
    public sealed class PromptSetPrefsTests
    {
        private const string ProjectA = "C:/Games/Village/";
        private const string ProjectB = "C:/Games/Strategy/";

        private Dictionary<string, string> store;

        [SetUp]
        public void EmptyStore() => store = new Dictionary<string, string>();

        [Test]
        public void TheSetProfileAndNameSurviveReopeningTheWindow()
        {
            var before = Prefs(ProjectA);
            before.SetGuid = "set-guid";
            before.RememberProfile("set-guid", "profile-guid");
            before.Annotator = "Hero";

            // Closing and reopening the window builds a new one from the same store.
            var after = Prefs(ProjectA);

            Assert.That(after.SetGuid, Is.EqualTo("set-guid"));
            Assert.That(after.ProfileGuidFor("set-guid"), Is.EqualTo("profile-guid"));
            Assert.That(after.LastProfileGuid, Is.EqualTo("profile-guid"));
            Assert.That(after.Annotator, Is.EqualTo("Hero"));
        }

        [Test]
        public void EachProjectKeepsItsOwn()
        {
            var village = Prefs(ProjectA);
            village.SetGuid = "village-set";
            village.Annotator = "Hero";

            var strategy = Prefs(ProjectB);

            Assert.That(strategy.SetGuid, Is.Empty);
            Assert.That(strategy.Annotator, Is.Empty);
        }

        [Test]
        public void OneProjectIsOneProjectHoweverItsPathIsWritten()
        {
            Prefs("C:/Games/Village").Annotator = "Hero";

            Assert.That(Prefs("c:\\games\\village\\").Annotator, Is.EqualTo("Hero"));
        }

        [Test]
        public void EachSetRemembersItsOwnProfile()
        {
            var prefs = Prefs(ProjectA);
            prefs.RememberProfile("guard-set", "guard-profile");
            prefs.RememberProfile("merchant-set", "merchant-profile");

            Assert.That(prefs.ProfileGuidFor("guard-set"), Is.EqualTo("guard-profile"));
            Assert.That(prefs.ProfileGuidFor("merchant-set"), Is.EqualTo("merchant-profile"));
            Assert.That(prefs.LastProfileGuid, Is.EqualTo("merchant-profile"));
        }

        [Test]
        public void AProfileChosenWithNoSetIsStillRemembered()
        {
            Prefs(ProjectA).RememberProfile("", "profile-guid");

            Assert.That(Prefs(ProjectA).LastProfileGuid, Is.EqualTo("profile-guid"));
            Assert.That(Prefs(ProjectA).ProfileGuidFor(""), Is.Empty);
        }

        private PromptSetPrefs Prefs(string project)
        {
            return new PromptSetPrefs(key => store.TryGetValue(key, out string value) ? value : "", (key, value) => store[key] = value, project);
        }
    }
}
