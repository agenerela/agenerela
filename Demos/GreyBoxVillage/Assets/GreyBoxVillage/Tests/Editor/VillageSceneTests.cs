using System.Linq;
using Demos.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Demos.GreyBoxVillage.Tests
{
    public sealed class VillageSceneTests
    {
        private Scene scene;
        private bool openedScene;
        private VillageCommands commands;

        [SetUp]
        public void SetUp()
        {
            scene = SceneManager.GetSceneByPath(DemoLauncher.VillagePath);
            openedScene = !scene.isLoaded;
            if (openedScene) scene = EditorSceneManager.OpenScene(DemoLauncher.VillagePath, OpenSceneMode.Additive);
            commands = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<VillageCommands>()).Single();
        }

        [TearDown]
        public void TearDown()
        {
            if (openedScene) EditorSceneManager.CloseScene(scene, true);
        }

        [TestCase("Go to the tower", "move_to", "tower")]
        [TestCase("  GO TO THE BRIDGE. ", "move_to", "bridge")]
        [TestCase("Follow me", "follow_player", "no_target")]
        [TestCase("Attack the training dummy", "attack_target", "training_dummy")]
        [TestCase("Attack training_dummy", "attack_target", "training_dummy")]
        public void PlaceholderReportsActionWithoutMovingGuard(string stimulus, string action, string target)
        {
            var start = commands.Guard.transform.position;
            commands.Submit(stimulus);
            Assert.That(commands.Overlay.ActionId, Is.EqualTo(action));
            Assert.That(commands.Overlay.TargetId, Is.EqualTo(target));
            Assert.That(commands.Overlay.Message, Does.StartWith("Would "));
            Assert.That(commands.Guard.transform.position, Is.EqualTo(start));
        }

        [TestCase("Attack Godzilla")]
        [TestCase("Do not go to the tower")]
        [TestCase("Go to the tower and attack the player")]
        public void UnknownCommandsNeverSubstituteKnownTargets(string stimulus)
        {
            commands.Submit(stimulus);
            Assert.That(commands.Overlay.ActionId, Is.EqualTo("none"));
            Assert.That(commands.Overlay.TargetId, Is.EqualTo("no_target"));
        }

        [Test]
        public void DistantPlayerCannotIssueCommands()
        {
            var start = commands.Player.position;
            try
            {
                commands.Player.position = new Vector3(10, 0, -10);
                commands.Submit("Go to the tower");
                Assert.That(commands.Overlay.ActionId, Is.EqualTo("none"));
                Assert.That(commands.Overlay.Message, Does.Contain("within 5 m"));
            }
            finally { commands.Player.position = start; }
        }

        [Test]
        public void BakedNavMeshConnectsGuardToEveryTargetAcrossBridge()
        {
            var navigation = scene.GetRootGameObjects().Select(g => g.GetComponent<VillageNavigation>()).Single(n => n != null);
            Assert.That(navigation.BakedData, Is.Not.Null);
            var instance = NavMesh.AddNavMeshData(navigation.BakedData);
            try
            {
                foreach (var target in new[] { commands.Tower, commands.Bridge, commands.TrainingDummy })
                {
                    var path = new NavMeshPath();
                    Assert.That(NavMesh.CalculatePath(commands.Guard.transform.position, target.position, NavMesh.AllAreas, path), Is.True, target.name);
                    Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), target.name);
                    if (target == commands.Tower)
                        Assert.That(path.corners.Any(p => Mathf.Abs(p.x) < 3 && p.z > 5 && p.z < 11), Is.True,
                            "The route to the north bank must use the bridge.");
                }
            }
            finally { instance.Remove(); }
        }

        [Test]
        public void SceneWiringAndLauncherRegistrationAreComplete()
        {
            Assert.That(commands.Input.Player.transform, Is.EqualTo(commands.Player));
            Assert.That(commands.Input.Overlay, Is.EqualTo(commands.Overlay));
            Assert.That(commands.Tower.name, Is.EqualTo("tower"));
            Assert.That(commands.Bridge.name, Is.EqualTo("bridge"));
            Assert.That(commands.TrainingDummy.name, Is.EqualTo("training_dummy"));
            Assert.That(commands.Guard.GetComponent<NavMeshAgent>(), Is.Not.Null);
            foreach (var path in new[] { DemoLauncher.VillagePath, DemoLauncher.LauncherPath })
                Assert.That(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == path), Is.True, path);
            Assert.That(typeof(VillageCommands).Assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("Agenerela")), Is.False);
        }
    }
}
