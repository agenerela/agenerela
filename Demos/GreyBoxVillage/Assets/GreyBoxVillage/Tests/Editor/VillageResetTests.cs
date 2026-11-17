using System;
using System.Collections;
using System.Linq;
using Demos.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Demos.GreyBoxVillage.Tests
{
    /// <summary>
    /// What the landing review of #157 found in Play mode, where time passes: after F1 a guard is not
    /// asked about a quiet moment at once, and a player typing to a guard who walks out of earshot is
    /// not left frozen. Each test was seen failing without its fix.
    /// </summary>
    public sealed class VillageResetTests
    {
        private FakeVillageConfig config;
        private VillageCommands commands;

        [UnityTest]
        public IEnumerator AResetRestartsTheQuietTimer() => InPlayMode(QuietTimer);

        [UnityTest]
        public IEnumerator WalkingOutOfEarshotWhileTypingLetsThePlayerMove() => InPlayMode(OutOfEarshot);

        // As VillagePresenterScriptTests: the body is its own enumerator, made once Play has started.
        private IEnumerator InPlayMode(Func<IEnumerator> body)
        {
            EditorSceneManager.OpenScene(DemoLauncher.VillagePath);
            yield return new EnterPlayMode();
            yield return null;
            commands = UnityEngine.Object.FindFirstObjectByType<VillageCommands>();
            Assert.That(commands, Is.Not.Null, "No village in Play mode.");
            config = ScriptableObject.CreateInstance<FakeVillageConfig>();
            config.Fake = new FakeVillageProvider(FakeVillageProvider.Decision("none", "no_target"))
            {
                Script = r => r.Stimulus.Contains("Walk with me")
                    ? FakeVillageProvider.Decision("follow_player", "no_target", "Lead the way.")
                    : FakeVillageProvider.Decision("none", "no_target", "Quiet.")
            };
            commands.UseProvider(config);
            var run = body();
            while (true)
            {
                bool more;
                try { more = run.MoveNext(); }
                catch (Exception e)
                {
                    Debug.Log("[reset test] " + e);
                    throw;
                }
                if (!more) break;
                yield return run.Current;
            }
            yield return new ExitPlayMode();
        }

        // Bram follows the player past his quiet time, so his timer waits; F1 ends the escort, and he
        // must not be asked about a quiet moment until a whole quiet time has passed after it.
        private IEnumerator QuietTimer()
        {
            var bram = commands.Mind("Bram");
            foreach (var mind in commands.Minds)
                if (mind.Senses != null) mind.Senses.QuietTimerEnabled = mind == bram;
            commands.GetComponent<VillageEvents>().enabled = false;
            bram.Senses.QuietSeconds = 1.5f;
            Stand(bram.transform.position + bram.transform.forward * 3f);
            yield return null;
            var walk = bram.HearPlayerAsync("Walk with me.").GetAwaiter();
            yield return Until(() => walk.IsCompleted, 5f);
            Assert.That(((VillageGuard)bram.Body).IsFollowing, Is.True, "Bram did not follow, so he was never busy.");
            yield return Until(() => false, 2.5f);
            Assert.That(Quiet(), Is.False, "Bram was asked about a quiet moment while he was following.");

            Assert.That(commands.Presenter.Press(Key.F1), Is.True);
            yield return Until(Quiet, 1f);
            Assert.That(Quiet(), Is.False, "F1 left Bram's quiet timer where it was, so he was asked at once.");
            // The timer still runs: a whole quiet time after the reset, he is asked.
            yield return Until(Quiet, 3f);
            Assert.That(Quiet(), Is.True, "Bram was never asked about a quiet moment after F1.");
        }

        private bool Quiet() => commands.Recorder.Records.Any(r => r.Agent == "Bram" && r.Source == "quiet");

        // The player starts typing to Bram, then nobody is in earshot (the listener walks off on
        // patrol, or the player is carried off; the panel cannot tell). The panel hides, and the
        // player must be free to move, not typing into a field nobody can see.
        private IEnumerator OutOfEarshot()
        {
            foreach (var mind in commands.Minds)
                if (mind.Senses != null) mind.Senses.enabled = false;
            commands.GetComponent<VillageEvents>().enabled = false;
            var bram = commands.Mind("Bram");
            var input = commands.Input;
            var field = input.GetComponent<UIDocument>().rootVisualElement.Q<TextField>("VillageCommand");
            Stand(bram.transform.position + bram.transform.forward * 3f);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.That(input.IsOpen, Is.True, "The dialogue panel did not open beside Bram.");
            input.StartTyping();
            field.Focus();
            yield return null;
            Assert.That(input.IsTyping, Is.True);

            Stand(bram.transform.position + new Vector3(0, 0, -25f));
            for (int i = 0; i < 3; i++) yield return null;
            Assert.That(input.IsOpen, Is.False, "Nobody is in earshot, and the panel still shows.");
            Assert.That(input.IsTyping, Is.False, "The player is still typing into a panel nobody can see.");
            Assert.That(input.Player.InputBlocked, Is.False, "The player is frozen until Esc.");
        }

        private void Stand(Vector3 at)
        {
            var controller = commands.Player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            commands.Player.position = at;
            if (controller != null) controller.enabled = true;
        }

        private static IEnumerator Until(Func<bool> done, float gameSeconds)
        {
            float until = Time.time + gameSeconds;
            while (!done() && Time.time < until) yield return null;
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
        }
    }
}
