using System.Collections.Generic;
using System.Linq;
using Agenerela.Editor.Evaluation;
using Agenerela.Evaluation;
using Agenerela.Providers;
using NUnit.Framework;
using UnityEngine;
using static Agenerela.Tests.Evaluation.RunnerFixtures;

namespace Agenerela.Tests.Evaluation
{
    /// <summary>
    /// What the Evaluation Report window's Run A/B will run (#146): why it is greyed out, the names
    /// two arms start with, the order two models force, and the checks it says it makes.
    /// </summary>
    public sealed class EvaluationRunSetupTests
    {
        private RunnerFixtures fixtures;
        private readonly List<ScriptableObject> made = new List<ScriptableObject>();

        private static readonly string Bridge = Prompt("target_001", "Meet me over at the bridge.", "move_to", "bridge", false);

        [SetUp]
        public void SetUp() => fixtures = new RunnerFixtures();

        [TearDown]
        public void TearDown()
        {
            fixtures.Dispose();
            foreach (var asset in made)
            {
                Object.DestroyImmediate(asset);
            }

            made.Clear();
            Fixtures.DestroyCreated();
        }

        [Test]
        public void RunIsGreyedOutWithTheFirstThingStillMissing()
        {
            var setup = new EvaluationRunSetup();
            Assert.That(setup.WhyNotRunnable(), Does.Contain("prompt set").And.Contain("step 1"));

            setup.Set = fixtures.Set(new[] { Bridge });
            Assert.That(setup.WhyNotRunnable(), Does.Contain("profile").And.Contain("step 2"));

            setup.Profile = Fixtures.VillageGuardProfile();
            Assert.That(setup.WhyNotRunnable(), Does.Contain("two arms").And.Contain("step 3"));

            setup.ChangeConfig(0, Config("OllamaLocal", "qwen3.5:2b"));
            Assert.That(setup.WhyNotRunnable(), Does.Contain("arm B"));

            setup.ChangeConfig(1, setup.A.Config);
            Assert.That(setup.WhyNotRunnable(), Is.Null);
        }

        [Test]
        public void ASetThatFailedToLoadSaysWhy()
        {
            var setup = new EvaluationRunSetup { SetError = "agent.json is not JSON" };

            Assert.That(setup.WhyNotRunnable(), Is.EqualTo("The set did not load: agent.json is not JSON"));
        }

        [Test]
        public void TwoArmsWithOneNameAreGreyedOut()
        {
            var setup = Ready();
            setup.B.Name = " CONTROL ";

            Assert.That(setup.WhyNotRunnable(), Does.Contain("Both arms are named 'control'"));
        }

        [Test]
        public void ASetTheRunnerWouldRefuseIsGreyedOutWithItsProblems()
        {
            var setup = Ready();
            setup.Set = fixtures.Set(new[] { Prompt("target_001", "Meet me at the castle.", "move_to", "castle", false) });

            Assert.That(setup.SetProblems(), Has.Some.Contains("target_001").And.Some.Contains("castle"));
            Assert.That(setup.WhyNotRunnable(), Does.Contain("1 problem the runner refuses"));
        }

        [Test]
        public void OneConfigTwiceIsAControlWithoutTheGuardAgainstTheSameWithIt()
        {
            var config = Config("OllamaLocal", "qwen3.5:2b");
            var setup = new EvaluationRunSetup();
            setup.ChangeConfig(0, config);
            setup.ChangeConfig(1, config);

            Assert.That((setup.A.Name, setup.A.CheckTarget), Is.EqualTo((EvaluationRunSetup.ControlName, false)));
            Assert.That((setup.B.Name, setup.B.CheckTarget), Is.EqualTo((EvaluationRunSetup.GuardsOnName, true)));
            Assert.That(setup.ArmsAreTheSame, Is.False);
        }

        [Test]
        public void TwoConfigsAreNamedAfterThemselvesAndTwoOfOneNameAreToldApart()
        {
            var setup = new EvaluationRunSetup();
            setup.A.Config = Config("Ollama2B", "qwen3.5:2b");
            setup.B.Config = Config("Ollama4B", "qwen3.5:4b");
            setup.NameArmsFromConfigs();
            Assert.That(new[] { setup.A.Name, setup.B.Name }, Is.EqualTo(new[] { "Ollama2B", "Ollama4B" }));
            Assert.That(setup.A.CheckTarget && setup.B.CheckTarget, Is.True);

            setup.B.Config = Config("ollama2b", "qwen3.5:4b");
            setup.NameArmsFromConfigs();
            Assert.That(new[] { setup.A.Name, setup.B.Name }, Is.EqualTo(new[] { "Ollama2B-A", "ollama2b-B" }));
        }

        [Test]
        public void ANameAPersonTypedSurvivesChangingAConfig()
        {
            var setup = new EvaluationRunSetup();
            var config = Config("OllamaLocal", "qwen3.5:2b");
            setup.ChangeConfig(0, config);
            setup.ChangeConfig(1, config);
            setup.B.Name = "grounding-v2";

            setup.ChangeConfig(1, Config("OllamaOther", "qwen3.5:2b"));

            Assert.That(setup.B.Name, Is.EqualTo("grounding-v2"));
            Assert.That(setup.A.Name, Is.EqualTo(EvaluationRunSetup.ControlName));
        }

        [Test]
        public void ArmsOnTwoModelsRunOneAfterTheOtherWhateverWasChosen()
        {
            var setup = Ready();
            setup.ChosenOrder = EvalArmOrder.Interleaved;
            setup.B.Config = Config("Ollama4B", "qwen3.5:4b");

            Assert.That(setup.TwoModels, Is.True);
            Assert.That(setup.Order, Is.EqualTo(EvalArmOrder.Sequential));
            Assert.That(setup.Checks(3)[0], Does.Contain("two models").And.Contain("hard rule 3"));
        }

        [TestCase("qwen3.5:2b", "Qwen3.5:2B")]
        [TestCase("llama3.1", "llama3.1:latest")]
        public void OneModelWrittenTwoWaysIsStillOneModel(string a, string b)
        {
            var setup = Ready();
            setup.A.Config = Config("One", a);
            setup.B.Config = Config("Two", b);

            Assert.That(setup.TwoModels, Is.False);
            Assert.That(setup.Order, Is.EqualTo(EvalArmOrder.Interleaved));
        }

        [Test]
        public void TheChecksSayWhatOllamaIsAskedAndWhatAnotherProviderCannotSay()
        {
            var setup = Ready();
            setup.B.Config = fixtures.Config(new FakeProvider(), "FakeB");
            setup.B.Name = "other";

            var checks = setup.Checks(3);

            Assert.That(checks[0], Does.StartWith("Interleaved: each of the 1 prompts"));
            Assert.That(checks, Has.Some.Contains("Arm A (control) on Ollama, qwen3.5:2b").And.Some.Contains("another model is loaded").And.Some.Contains("3 warm-up requests"));
            Assert.That(checks, Has.Some.Contains("Arm B (other) through FakeB").And.Some.Contains("\"not checked\""));
            Assert.That(setup.TwoModels, Is.False, "A provider that cannot say its model forces nothing.");
        }

        [Test]
        public void TheRunnerItBuildsHasTheArmsNamesGuardSwitchesAndOrder()
        {
            var setup = Ready();
            setup.ChosenOrder = EvalArmOrder.Sequential;

            var runner = setup.Build();

            Assert.That(new[] { runner.ArmA.Name, runner.ArmB.Name }, Is.EqualTo(new[] { "control", "guards-on" }));
            Assert.That(new[] { runner.ArmA.CheckTarget, runner.ArmB.CheckTarget }, Is.EqualTo(new[] { false, true }));
            Assert.That(runner.Order, Is.EqualTo(EvalArmOrder.Sequential));
            Assert.That(runner.Set, Is.SameAs(setup.Set));
        }

        [Test]
        public void ASetUpThatCannotRunBuildsNothing()
        {
            var e = Assert.Throws<System.InvalidOperationException>(() => new EvaluationRunSetup().Build());
            Assert.That(e.Message, Does.Contain("prompt set"));
        }

        // A set, a profile, and one Ollama config in both arms: ready to run.
        private EvaluationRunSetup Ready()
        {
            var setup = new EvaluationRunSetup { Set = fixtures.Set(new[] { Bridge }), Profile = Fixtures.VillageGuardProfile() };
            var config = Config("OllamaLocal", "qwen3.5:2b");
            setup.ChangeConfig(0, config);
            setup.ChangeConfig(1, config);
            Assert.That(setup.WhyNotRunnable(), Is.Null, "The fixture is ready to run.");
            return setup;
        }

        private ProviderConfig Config(string name, string model)
        {
            var config = ScriptableObject.CreateInstance<OllamaProviderConfig>();
            config.name = name;
            config.Model = model;
            made.Add(config);
            return config;
        }
    }
}
