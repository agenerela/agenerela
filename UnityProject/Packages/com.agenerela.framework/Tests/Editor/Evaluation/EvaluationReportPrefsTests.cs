using System;
using System.Collections.Generic;
using Agenerela.Editor.Evaluation;
using NUnit.Framework;

namespace Agenerela.Tests.Evaluation
{
    /// <summary>
    /// What the Evaluation Report window remembers between sessions (#146). A dictionary stands in
    /// for EditorPrefs, so no test touches this machine's real preferences.
    /// </summary>
    public sealed class EvaluationReportPrefsTests
    {
        private const string ProjectA = "C:/Games/Village/";
        private const string ProjectB = "C:/Games/Strategy/";

        private Dictionary<string, string> store;

        [SetUp]
        public void EmptyStore() => store = new Dictionary<string, string>();

        [Test]
        public void EveryChoiceSurvivesReopeningTheWindow()
        {
            var before = Prefs(ProjectA);
            before.SetGuid = "set";
            before.ProfileGuid = "profile";
            before.SetConfigGuid(0, "config-a");
            before.SetConfigGuid(1, "config-b");
            before.SetName(0, "control");
            before.SetName(1, "guards-on");
            before.SetCheckTarget(0, false);
            before.SetCheckTarget(1, true);
            before.Order = "Sequential";
            before.LastReport = "C:/Games/Village/Logs/Evaluation/run/report.json";

            var after = Prefs(ProjectA);

            Assert.That((after.SetGuid, after.ProfileGuid, after.Order), Is.EqualTo(("set", "profile", "Sequential")));
            Assert.That((after.ConfigGuid(0), after.ConfigGuid(1)), Is.EqualTo(("config-a", "config-b")));
            Assert.That((after.Name(0), after.Name(1)), Is.EqualTo(("control", "guards-on")));
            Assert.That((after.CheckTarget(0), after.CheckTarget(1)), Is.EqualTo(((bool?)false, (bool?)true)));
            Assert.That(after.LastReport, Is.EqualTo("C:/Games/Village/Logs/Evaluation/run/report.json"));
        }

        [Test]
        public void EachProjectKeepsItsOwn()
        {
            Prefs(ProjectA).SetName(0, "control");

            Assert.That(Prefs(ProjectB).Name(0), Is.Empty);
            Assert.That(Prefs("c:\\games\\village").Name(0), Is.EqualTo("control"), "One project however its path is written.");
        }

        [Test]
        public void AGuardSwitchNeverSetIsNeitherOnNorOff()
        {
            Assert.That(Prefs(ProjectA).CheckTarget(0), Is.Null);
        }

        [Test]
        public void ItSharesNoKeyWithThePromptSetWindow()
        {
            var report = Prefs(ProjectA);
            var promptSet = new PromptSetPrefs(Get, (k, v) => store[k] = v, ProjectA);
            promptSet.SetGuid = "the prompt set window's set";

            Assert.That(report.SetGuid, Is.Empty);
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void ThereAreOnlyTwoArms(int arm)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Prefs(ProjectA).Name(arm));
        }

        private EvaluationReportPrefs Prefs(string project) => new EvaluationReportPrefs(Get, (k, v) => store[k] = v, project);

        private string Get(string key) => store.TryGetValue(key, out string value) ? value : "";
    }
}
