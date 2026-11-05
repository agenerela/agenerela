using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LivingVale.Combat
{
    /// <summary>
    /// Player health and stamina, an overhead health bar per enemy and a status line for combat
    /// (chat unavailable, defeated). Built from code with uGUI like the chat overlay. It has no
    /// raycaster and no raycast targets, so it never sits between a click and the chat.
    /// </summary>
    [DefaultExecutionOrder(400)]
    public sealed class CombatHud : MonoBehaviour
    {
        private static readonly Color HealthColor = new Color(0.82f, 0.22f, 0.2f);
        private static readonly Color StaminaColor = new Color(0.86f, 0.75f, 0.28f);
        private static readonly Color Backing = new Color(0.04f, 0.06f, 0.08f, 0.85f);

        [SerializeField] private CombatDirector director;

        private readonly List<(EnemyController enemy, RectTransform bar, RectTransform fill)> overhead =
            new List<(EnemyController, RectTransform, RectTransform)>();
        private Font font;
        private RectTransform root;
        private RectTransform healthFill;
        private RectTransform staminaFill;
        private Text healthText;
        private Text status;
        private int shownHealth = -1;

        public void Configure(CombatDirector sceneDirector)
        {
            director = sceneDirector;
        }

        private void Start()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildInterface();
            foreach (var enemy in director.Enemies)
            {
                RectTransform bar = Box("Overhead " + enemy.name, root, new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(70f, 8f), Backing);
                overhead.Add((enemy, bar, Fill(bar, HealthColor)));
            }
        }

        private void LateUpdate()
        {
            Combatant player = director.Player.Combatant;
            SetFraction(healthFill, player.Health / player.MaxHealth);
            SetFraction(staminaFill, player.Stamina / player.MaxStamina);
            int health = Mathf.CeilToInt(player.Health);
            if (health != shownHealth)
            {
                shownHealth = health;
                healthText.text = $"{health} / {Mathf.RoundToInt(player.MaxHealth)}";
            }

            status.text = director.PlayerDefeated
                ? $"Defeated. Returning to the village in {director.SecondsUntilRespawn:0.0} s"
                : director.AnyEngaged ? "In combat: nobody will talk until the fight ends" : "";

            Camera view = Camera.main;
            foreach (var (enemy, bar, fill) in overhead)
            {
                Combatant foe = enemy != null ? enemy.Body.Combatant : null;
                bool show = view != null && foe != null && !foe.IsDefeated &&
                    (enemy.IsEngaged || foe.Health < foe.MaxHealth);
                Vector3 screen = show ? view.WorldToScreenPoint(enemy.Body.OverheadPoint) : Vector3.zero;
                show &= screen.z > 0f;
                if (show && RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local))
                {
                    bar.anchoredPosition = local;
                    SetFraction(fill, foe.Health / foe.MaxHealth);
                }
                else
                {
                    show = false;
                }

                if (bar.gameObject.activeSelf != show)
                {
                    bar.gameObject.SetActive(show);
                }
            }
        }

        private void BuildInterface()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -10; // Under the chat overlay.
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            root = (RectTransform)transform;

            RectTransform panel = Box("Player vitals", root, Vector2.zero, new Vector2(18f, 18f),
                new Vector2(300f, 92f), Backing);
            RectTransform health = Box("Health", panel, new Vector2(0f, 1f), new Vector2(12f, -12f),
                new Vector2(276f, 18f), new Color(0.2f, 0.08f, 0.08f));
            healthFill = Fill(health, HealthColor);
            healthText = Label("Health value", health, "", Vector2.zero, new Vector2(276f, 18f), 14);
            healthText.alignment = TextAnchor.MiddleCenter;
            RectTransform stamina = Box("Stamina", panel, new Vector2(0f, 1f), new Vector2(12f, -36f),
                new Vector2(276f, 10f), new Color(0.2f, 0.18f, 0.06f));
            staminaFill = Fill(stamina, StaminaColor);
            Label("Combat help", panel, "Left mouse attack · Right mouse block · Space dodge",
                new Vector2(12f, -54f), new Vector2(276f, 30f), 13);

            // Top right: the chat overlay owns the top left (help) and the bottom right (chat).
            RectTransform statusRect = Box("Combat status", root, Vector2.one, new Vector2(-18f, -16f),
                new Vector2(480f, 32f), Color.clear);
            status = Label("Status", statusRect, "", Vector2.zero, new Vector2(480f, 32f), 19);
            status.alignment = TextAnchor.MiddleRight;
            status.color = new Color(1f, 0.8f, 0.5f);
        }

        private static void SetFraction(RectTransform fill, float fraction)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        }

        private static RectTransform Fill(RectTransform bar, Color color)
        {
            RectTransform fill = Rect("Fill", bar);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            Image image = fill.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return fill;
        }

        private static RectTransform Rect(string objectName, Transform parent)
        {
            var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static RectTransform Box(string objectName, Transform parent, Vector2 anchor,
            Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = Rect(objectName, parent);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private Text Label(string objectName, Transform parent, string text, Vector2 position, Vector2 size, int fontSize)
        {
            RectTransform rect = Rect(objectName, parent);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.text = text;
            label.supportRichText = false;
            label.raycastTarget = false;
            return label;
        }
    }
}
