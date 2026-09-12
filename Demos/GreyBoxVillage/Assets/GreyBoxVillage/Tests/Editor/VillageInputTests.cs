using System.Collections;
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
        [UnityTest]
        public IEnumerator TypingSubmitAndCancelUseTheRuntimeUI()
        {
            EditorSceneManager.OpenScene(DemoLauncher.VillagePath);
            yield return new EnterPlayMode();
            yield return null;
            var commands = Object.FindFirstObjectByType<VillageCommands>();
            var input = commands.Input;
            var field = input.GetComponent<UIDocument>().rootVisualElement.Q<TextField>("VillageCommand");
            field.Focus();
            Assert.That(input.Player.InputBlocked, Is.True);
            foreach (char character in "Go to the tower")
                using (var e = KeyDownEvent.GetPooled(character, KeyCode.None, EventModifiers.None)) field.SendEvent(e);
            Assert.That(field.value, Is.EqualTo("Go to the tower"));
            using (var e = KeyDownEvent.GetPooled('\n', KeyCode.Return, EventModifiers.None)) field.SendEvent(e);
            Assert.That(commands.Overlay.ActionId, Is.EqualTo("move_to"));
            Assert.That(commands.Overlay.TargetId, Is.EqualTo("tower"));
            Assert.That(input.IsTyping, Is.False);
            Assert.That(input.Player.InputBlocked, Is.False);
            Assert.That(field.value, Is.Empty);
            field.Focus();
            using (var e = KeyDownEvent.GetPooled('\0', KeyCode.Escape, EventModifiers.None)) field.SendEvent(e);
            Assert.That(input.IsTyping, Is.False);
            Assert.That(input.Player.InputBlocked, Is.False);
            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator LeavePlayModeAfterFailure()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        }
    }
}
