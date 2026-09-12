using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Demos.Shared
{
    public sealed class DemoCommandInput : MonoBehaviour
    {
        public DemoPlayerController Player;
        public DecisionDebugOverlay Overlay;
        public event Action<string> Submitted;
        public bool IsTyping { get; private set; }
        private VisualElement root;
        private TextField command;
        private Label decision, guards, stimulus, message, help;
        private int openedTypingFrame = -1;
        private int submittedFrame = -1;

        private void OnEnable()
        {
            root = DemoUI.Create(gameObject);
            var panel = DemoUI.Box(root);
            panel.style.left = 16;
            panel.style.right = 16;
            panel.style.bottom = 16;
            DemoUI.Label(panel, "PLACEHOLDER  |  Provider: none");
            decision = DemoUI.Label(panel, "");
            guards = DemoUI.Label(panel, "");
            stimulus = DemoUI.Label(panel, "");
            message = DemoUI.Label(panel, "");
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 6;
            panel.Add(row);
            command = new TextField { name = "VillageCommand", maxLength = 160 };
            command.style.flexGrow = 1;
            command.style.minWidth = 0;
            command.RegisterCallback<FocusInEvent>(_ => SetTyping(true));
            command.RegisterCallback<FocusOutEvent>(_ => SetTyping(false));
            // Text editing and submission use UI Toolkit's input events.
            command.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    if (Time.frameCount != openedTypingFrame) SendLine();
                    e.StopImmediatePropagation();
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    root.Focus();
                    SetTyping(false);
                    e.StopImmediatePropagation();
                }
            }, TrickleDown.TrickleDown);
            row.Add(command);
            var send = new Button(SendLine) { text = "Send", name = "SendCommand" };
            send.style.width = 80;
            row.Add(send);
            help = DemoUI.Label(panel, "");
            var menu = new Button(() => SceneManager.LoadScene(DemoLauncher.LauncherPath))
                { text = "DemoLauncher", name = "OpenLauncher" };
            menu.style.position = Position.Absolute;
            menu.style.right = 16;
            menu.style.top = 16;
            menu.style.height = 32;
            root.Add(menu);
        }

        private void OnDisable()
        {
            SetTyping(false);
            root?.Clear();
        }

        private void SetTyping(bool value)
        {
            IsTyping = value;
            if (Player != null) Player.InputBlocked = value;
        }

        public void Submit(string text)
        {
            if (!string.IsNullOrWhiteSpace(text)) Submitted?.Invoke(text.Trim());
        }

        private void SendLine()
        {
            submittedFrame = Time.frameCount;
            Submit(command.value);
            command.value = "";
            root.Focus();
            SetTyping(false);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (!IsTyping && Time.frameCount != submittedFrame &&
                    (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                {
                    openedTypingFrame = Time.frameCount;
                    command.Focus();
                    SetTyping(true);
                }
                else if (keyboard.escapeKey.wasPressedThisFrame && IsTyping)
                {
                    root.Focus();
                    SetTyping(false);
                }
            }
            decision.text = "Action: " + Overlay.ActionId + "    Target: " + Overlay.TargetId + "    Latency: " + Overlay.LatencyLabel;
            guards.text = "Guards: " + Overlay.GuardsLabel;
            stimulus.text = "You: " + Overlay.Stimulus;
            message.text = Overlay.Message;
            help.text = IsTyping ? "Typing: Enter sends / Esc returns to movement" :
                "WASD / arrows: walk   |   Enter: type   |   Try: Go to the tower";
        }
    }
}
