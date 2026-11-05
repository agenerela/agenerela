using System.Collections;
using System.Linq;
using Demos.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Demos.GreyBoxVillage.Tests
{
    public sealed class VillageInputTests
    {
        private FakeVillageConfig config;

        [UnityTest]
        public IEnumerator TypingSubmitAndCancelUseTheRuntimeUI()
        {
            EditorSceneManager.OpenScene(DemoLauncher.VillagePath);
            yield return new EnterPlayMode();
            yield return null;
            var commands = Object.FindFirstObjectByType<VillageCommands>();
            // The agents built their Ollama agents in Awake; replace them before anything can ask.
            config = ScriptableObject.CreateInstance<FakeVillageConfig>();
            config.Fake = new FakeVillageProvider(FakeVillageProvider.Decision("move_to", "north_tower"));
            commands.UseProvider(config);
            foreach (var mind in commands.Minds)
                if (mind.Senses != null) mind.Senses.enabled = false;
            commands.GetComponent<VillageEvents>().enabled = false;
            var input = commands.Input;
            var field = input.GetComponent<UIDocument>().rootVisualElement.Q<TextField>("VillageCommand");
            field.Focus();
            Assert.That(input.Player.InputBlocked, Is.True);
            foreach (char character in "Go to the tower")
                using (var e = KeyDownEvent.GetPooled(character, KeyCode.None, EventModifiers.None)) field.SendEvent(e);
            Assert.That(field.value, Is.EqualTo("Go to the tower"));
            using (var e = KeyDownEvent.GetPooled('\n', KeyCode.Return, EventModifiers.None)) field.SendEvent(e);
            for (int frame = 0; frame < 300 && commands.IsDeciding; frame++) yield return null;
            Assert.That(commands.IsDeciding, Is.False, "The decision never finished.");
            Assert.That(commands.Overlay.ActionId, Is.EqualTo("move_to"));
            Assert.That(commands.Overlay.TargetId, Is.EqualTo("north_tower"));
            Assert.That(input.IsTyping, Is.False);
            Assert.That(input.Player.InputBlocked, Is.False);
            Assert.That(field.value, Is.Empty);
            field.Focus();
            using (var e = KeyDownEvent.GetPooled('\0', KeyCode.Escape, EventModifiers.None)) field.SendEvent(e);
            Assert.That(input.IsTyping, Is.False);
            Assert.That(input.Player.InputBlocked, Is.False);

            // Every panel keeps its own document: the menu must not clear the dialogue, or the reverse.
            Assert.That(input.GetComponent<UIDocument>().rootVisualElement.Q("Dialogue"), Is.Not.Null);
            Assert.That(commands.Menu.GetComponent<UIDocument>().rootVisualElement.Q("Menu"), Is.Not.Null);
            Assert.That(commands.Menu.GetComponent<UIDocument>(), Is.Not.SameAs(input.GetComponent<UIDocument>()));

            // A capped conversation still shows its newest line.
            var bram = commands.Mind("Bram");
            while (bram.Conversation.Count < 30) bram.Conversation.Add(new DialogueLine("You", "filler", DialogueLine.Kinds.Player));
            yield return null;
            bram.Conversation.RemoveAt(0);
            bram.Conversation.Add(new DialogueLine("Bram", "the newest line", DialogueLine.Kinds.Agent));
            yield return null;
            var log = input.GetComponent<UIDocument>().rootVisualElement.Q("DialogueLog");
            var newest = log[log.childCount - 1].Query<Label>().ToList();
            Assert.That(newest.Any(l => l.text == "the newest line"), Is.True, "The log stopped refreshing at its cap.");

            // Behind the open menu, nothing reaches an agent: the dialogue hides and a line goes nowhere.
            commands.Menu.Open();
            yield return null;
            Assert.That(input.IsOpen, Is.False);
            int calls = config.Fake.Calls;
            input.Submit("Ring the bell!");
            for (int frame = 0; frame < 10; frame++) yield return null;
            Assert.That(config.Fake.Calls, Is.EqualTo(calls), "A line was sent to an agent behind the menu.");
            commands.Menu.Close();
            yield return null;
            Assert.That(input.IsOpen, Is.True);
            yield return new ExitPlayMode();
            LeaveTheVillage();
        }

        // The scene tests open the village themselves; one left open here could be loaded twice.
        private static void LeaveTheVillage() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test]
        public void TypingAndTheMenuBlockMovementIndependently()
        {
            var go = new GameObject("player", typeof(CharacterController));
            try
            {
                var player = go.AddComponent<DemoPlayerController>();
                player.Paused = true;
                player.Typing = true;
                player.Typing = false;
                Assert.That(player.InputBlocked, Is.True, "Leaving the text field must not free the player behind the menu.");
                player.Paused = false;
                Assert.That(player.InputBlocked, Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [UnityTearDown]
        public IEnumerator LeavePlayModeAfterFailure()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            LeaveTheVillage();
            if (config != null) Object.DestroyImmediate(config);
        }
    }
}
