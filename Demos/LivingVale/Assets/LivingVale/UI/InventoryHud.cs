using System.Text;
using LivingVale.Game;
using LivingVale.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace LivingVale.UI
{
    /// <summary>A read-only view of the player's holdings, on the chat overlay's canvas.</summary>
    public sealed class InventoryHud : MonoBehaviour
    {
        private ValeBootstrap bootstrap;
        private Font font;
        private GameObject panel;
        private Text gold;
        private Text weapon;
        private Text armor;
        private readonly Text[] quickUse = new Text[PlayerInventory.QuickUseCount];
        private readonly Text[] slots = new Text[PlayerInventory.SlotCount];
        private Text questItems;
        private ScrollRect questScroll;

        public bool IsVisible => panel != null && panel.activeSelf;

        public void Initialize(ValeBootstrap owner)
        {
            bootstrap = owner;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildInterface();
            panel.SetActive(false);
            bootstrap.Inventory.Changed += Render;
        }

        public void Toggle()
        {
            bootstrap.BlockWorldInputForFrame();
            panel.SetActive(!IsVisible);
            Render();
        }

        private void BuildInterface()
        {
            // This component shares ChatOverlay's GameObject, so transform is its canvas.
            // Ends 610 px down the 720 px reference, just above the combat vitals panel in the
            // bottom-left corner (18 px margin, 92 px tall), so the two never overlap.
            RectTransform inventory = Box("Inventory", transform, new Vector2(14f, -158f),
                new Vector2(448f, 450f), new Color(0.045f, 0.06f, 0.08f, 0.96f));
            panel = inventory.gameObject;
            Label("Title", inventory, "Inventory", new Vector2(14f, -10f), new Vector2(420f, 28f), 21);
            gold = Label("Gold", inventory, "", new Vector2(14f, -42f), new Vector2(420f, 24f), 18);
            gold.color = new Color(1f, 0.86f, 0.35f);
            weapon = Label("Weapon", inventory, "", new Vector2(14f, -70f), new Vector2(420f, 24f), 17);
            armor = Label("Armor", inventory, "", new Vector2(14f, -96f), new Vector2(420f, 24f), 17);
            Label("Quick-use heading", inventory, "Quick-use", new Vector2(14f, -126f),
                new Vector2(420f, 22f), 17);
            for (int i = 0; i < quickUse.Length; i++)
            {
                quickUse[i] = Label($"Quick-use {i + 1}", inventory, "", new Vector2(14f + i * 140f, -152f),
                    new Vector2(132f, 26f), 16);
            }

            Label("Pack heading", inventory, "Pack · 16 slots", new Vector2(14f, -180f),
                new Vector2(420f, 22f), 17);
            for (int i = 0; i < slots.Length; i++)
            {
                RectTransform cell = Box($"Slot {i + 1}", inventory,
                    new Vector2(14f + (i % 4) * 105f, -206f - (i / 4) * 46f),
                    new Vector2(98f, 42f), new Color(0.12f, 0.17f, 0.2f));
                slots[i] = Label("Item", cell, "", new Vector2(5f, -2f), new Vector2(88f, 38f), 15);
                slots[i].alignment = TextAnchor.MiddleCenter;
            }

            Label("Quest heading", inventory, "Quest items · protected storage", new Vector2(14f, -394f),
                new Vector2(420f, 22f), 17);
            RectTransform viewport = Box("Quest viewport", inventory, new Vector2(14f, -418f),
                new Vector2(420f, 26f), new Color(0.02f, 0.03f, 0.04f, 0.6f));
            viewport.gameObject.AddComponent<RectMask2D>();
            questItems = Label("Quest items", viewport, "", new Vector2(6f, -2f), new Vector2(408f, 24f), 16);
            var content = questItems.rectTransform;
            questItems.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            questScroll = viewport.gameObject.AddComponent<ScrollRect>();
            questScroll.viewport = viewport;
            questScroll.content = content;
            questScroll.horizontal = false;
            questScroll.movementType = ScrollRect.MovementType.Clamped;
            questScroll.scrollSensitivity = 24f;
            Label("Footer", inventory, "I closes", new Vector2(340f, -14f), new Vector2(94f, 22f), 14)
                .color = new Color(0.75f, 0.8f, 0.85f);
        }

        private void Render()
        {
            if (!IsVisible)
            {
                return;
            }

            PlayerInventory inventory = bootstrap.Inventory;
            gold.text = $"Gold: {inventory.Gold}";
            weapon.text = $"Weapon: {ItemName(inventory.Weapon)}";
            armor.text = $"Armor: {ItemName(inventory.Armor)}";
            for (int i = 0; i < quickUse.Length; i++)
            {
                quickUse[i].text = $"{i + 1}: {ItemName(inventory.QuickUse[i])}";
            }

            for (int i = 0; i < slots.Length; i++)
            {
                ItemStack stack = inventory.Slots[i];
                slots[i].text = stack.IsEmpty ? "" : StackText(stack.ItemId, stack.Quantity);
            }

            var text = new StringBuilder();
            foreach (var item in inventory.QuestItems)
            {
                if (text.Length > 0)
                {
                    text.Append('\n');
                }

                text.Append(StackText(item.Key, item.Value));
            }

            questItems.text = text.Length == 0 ? "—" : text.ToString();
            questScroll.verticalNormalizedPosition = 1f;
        }

        private string ItemName(string itemId) => itemId == null ? "—" : bootstrap.Inventory.Catalog.Get(itemId).DisplayName;

        private string StackText(string itemId, int quantity) => quantity > 1
            ? $"{ItemName(itemId)} ×{quantity}" : ItemName(itemId);

        private static RectTransform Rect(string objectName, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform Box(string objectName, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = Rect(objectName, parent, position, size);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        private Text Label(string objectName, Transform parent, string text, Vector2 position, Vector2 size, int fontSize)
        {
            RectTransform rect = Rect(objectName, parent, position, size);
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.text = text;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private void OnDestroy()
        {
            if (bootstrap != null && bootstrap.Inventory != null)
            {
                bootstrap.Inventory.Changed -= Render;
            }
        }
    }
}
