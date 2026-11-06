using UnityEngine;
using UnityEngine.UI;

namespace LivingVale.UI
{
    /// <summary>
    /// G8a: the one HUD layout, every slot in 1280 × 720 reference units. Every HUD canvas scales
    /// with the screen (match 0.5), so at 1920 × 1080 the same slots scale by 1.5 and still hold.
    /// <code>
    ///  ┌──────────────┐   ┌── compass 340 ──┐    ┌─── journal 380 ───┐
    ///  │ inventory (I)│     goal readout          │                   │
    ///  │ 448 × 450    │   place name              └───────────────────┘
    ///  │              │   notices (3, queued)
    ///  └──────────────┘                          ┌─ trade / quest ─┐  (chat only)
    ///   driver badge                             │                 │
    ///  ┌─ controls ───┐                          │   chat 560      │
    ///  └──────────────┘    combat status         │                 │
    ///   vitals              talk prompt          └─────────────────┘
    /// </code>
    /// While a conversation is open the chat column takes the right of the screen from x 702, so the
    /// goal readout hides and the notice area moves left of it.
    /// </summary>
    public static class HudLayout
    {
        public const float ReferenceWidth = 1280f;
        public const float ReferenceHeight = 720f;

        // Top centre.
        public const float CompassWidth = 340f;
        public const float CompassHeight = 28f;
        public const float CompassTop = 4f;
        public const float GoalTop = 34f;
        public const float NoticeTop = 56f;
        public const float NoticeWidth = 340f;
        public const float NoticeLine = 22f;

        /// <summary>
        /// The notice area's centre offset from the screen centre while chatting: 220 wide around
        /// x 580 spans 470–690, clear of the inventory (to 462) and the chat column (from 702).
        /// </summary>
        public const float NoticeChatOffset = -60f;
        public const float NoticeChatWidth = 220f;

        // Left column.
        public const float InventoryTop = 14f;
        public const float HelpBottom = 118f;
        public const float BadgeBottom = 226f;

        // Right column.
        public const float JournalTop = 14f;
        public const float JournalWidth = 380f;

        // Bottom centre.
        public const float CombatStatusBottom = 66f;

        /// <summary>The scaler every HUD canvas uses, so all slots scale together.</summary>
        public static void Scale(GameObject canvasObject)
        {
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = canvasObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0.5f;
        }
    }
}
