using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Demos.Shared
{
    /// <summary>The title screen for this standalone game; it does not load other projects.</summary>
    public sealed class DemoLauncher : MonoBehaviour
    {
        public const string VillagePath = "Assets/GreyBoxVillage/GreyBoxVillage.unity";
        public const string LauncherPath = "Assets/GreyBoxVillage/LocalShared/DemoLauncher.unity";

        public void OpenVillage() => SceneManager.LoadScene(VillagePath);

        private void OnEnable()
        {
            var root = DemoUI.Create(gameObject);
            var backdrop = new VisualElement { pickingMode = PickingMode.Ignore };
            backdrop.style.position = Position.Absolute;
            backdrop.style.left = backdrop.style.right = backdrop.style.top = backdrop.style.bottom = 0;
            var art = Resources.Load<Texture2D>("TitleBackdrop");
            if (art != null)
            {
                backdrop.style.backgroundImage = new StyleBackground(art);
                // Fills the screen whatever its shape, cropping rather than stretching.
                backdrop.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                backdrop.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                backdrop.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            }
            backdrop.style.backgroundColor = new Color(0.1f, 0.13f, 0.12f);
            root.Add(backdrop);

            var panel = DemoUI.Panel(root);
            panel.style.position = Position.Absolute;
            panel.style.left = Length.Percent(30);
            panel.style.right = Length.Percent(30);
            panel.style.top = Length.Percent(26);
            panel.style.paddingTop = 20;
            panel.style.paddingBottom = 18;
            var title = DemoUI.Label(panel, "ASHFORD", "vui-title");
            title.style.fontSize = 34;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            var sub = DemoUI.Label(panel, "A village run by language models", "vui-subtitle");
            sub.style.fontSize = 14;
            sub.style.unityTextAlign = TextAnchor.MiddleCenter;
            var blurb = DemoUI.Label(panel,
                "Two guards keep the gate and the bridge, Odile keeps the market and Captain Hale gives the orders. " +
                "Talk to them in your own words: each one chooses only from the actions the game allows it, and " +
                "game rules stop the choices they must not make. Press Tab in the village to watch the framework decide.", "vui-text");
            blurb.style.marginTop = 10;
            blurb.style.marginBottom = 12;
            blurb.style.unityTextAlign = TextAnchor.MiddleCenter;
            var enter = DemoUI.Button(panel, "Enter the village", OpenVillage, "OpenVillage");
            enter.style.height = 40;
            enter.style.fontSize = 16;
            var made = DemoUI.Label(panel, "An Agenerela demo  ·  needs Ollama with qwen3.5:2b at 127.0.0.1:11434", "vui-muted");
            made.style.marginTop = 8;
            made.style.unityTextAlign = TextAnchor.MiddleCenter;
        }
    }
}
