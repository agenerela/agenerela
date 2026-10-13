using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Demos.Shared
{
    /// <summary>Start screen for this standalone game; it does not load other projects.</summary>
    public sealed class DemoLauncher : MonoBehaviour
    {
        public const string VillagePath = "Assets/GreyBoxVillage/GreyBoxVillage.unity";
        public const string LauncherPath = "Assets/GreyBoxVillage/LocalShared/DemoLauncher.unity";

        public void OpenVillage() => SceneManager.LoadScene(VillagePath);

        private void OnEnable()
        {
            var root = DemoUI.Create(gameObject);
            var panel = DemoUI.Box(root);
            panel.style.left = Length.Percent(25);
            panel.style.right = Length.Percent(25);
            panel.style.top = Length.Percent(30);
            DemoUI.Label(panel, "AGENERELA / DEMO LAUNCHER").style.fontSize = 22;
            DemoUI.Label(panel, "GreyBoxVillage: village guard playground, no AI yet.");
            DemoUI.Label(panel, "Walk, type commands, inspect placeholder output.");
            var button = new Button(OpenVillage) { text = "Open GreyBoxVillage", name = "OpenVillage" };
            button.style.height = 44;
            panel.Add(button);
        }
    }
}
