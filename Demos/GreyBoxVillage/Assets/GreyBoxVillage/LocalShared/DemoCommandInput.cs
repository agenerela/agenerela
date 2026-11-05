using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Demos.Shared
{
    /// <summary>One line of a conversation, as the dialogue panel shows it.</summary>
    public sealed class DialogueLine
    {
        public enum Kinds { Player, Agent, Refusal, Note }

        public string Speaker = "", Text = "";
        public Kinds Kind;

        public DialogueLine(string speaker, string text, Kinds kind)
        {
            Speaker = speaker;
            Text = text;
            Kind = kind;
        }
    }

    /// <summary>
    /// The dialogue panel: who the player is talking to, the conversation so far, a text field and
    /// suggestion chips. It shows whenever someone is in earshot; Enter (or E on them) starts typing,
    /// Enter sends, Esc returns to walking. Framework-free: the game passes strings.
    /// </summary>
    public sealed class DemoCommandInput : MonoBehaviour
    {
        public DemoPlayerController Player;
        public DecisionDebugOverlay Overlay;
        public event Action<string> Submitted;
        public bool IsTyping { get; private set; }
        /// <summary>Whether the panel is showing, which it does while someone is in earshot.</summary>
        public bool IsOpen { get; private set; }

        private VisualElement root, panel, log, chips;
        private Label who, role, status, hint;
        private TextField command;
        private int openedTypingFrame = -1, submittedFrame = -1, shownLines = -1;
        private string shownFor;
        private IReadOnlyList<string> shownChips;

        private void OnEnable()
        {
            root = DemoUI.Create(gameObject, 10);
            panel = DemoUI.Panel(root);
            panel.name = "Dialogue";
            panel.style.position = Position.Absolute;
            panel.style.left = Length.Percent(27);
            panel.style.right = Length.Percent(27);
            panel.style.bottom = 14;
            panel.pickingMode = PickingMode.Position;

            var head = DemoUI.Row(panel);
            who = DemoUI.Label(head, "", "vui-title");
            role = DemoUI.Label(head, "", "vui-subtitle");
            role.style.marginLeft = 8;
            role.style.flexGrow = 1;
            status = DemoUI.Label(head, "", "vui-chip");

            log = new VisualElement { name = "DialogueLog" };
            log.pickingMode = PickingMode.Ignore;
            log.style.marginTop = 4;
            log.style.marginBottom = 6;
            panel.Add(log);

            chips = DemoUI.Row(panel);
            chips.style.flexWrap = Wrap.Wrap;
            chips.style.marginBottom = 4;

            var row = DemoUI.Row(panel);
            command = new TextField { name = "VillageCommand", maxLength = 160 };
            command.AddToClassList("vui-field");
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
            DemoUI.Button(row, "Send", SendLine, "SendCommand");
            hint = DemoUI.Label(panel, "", "vui-muted");
            hint.style.marginTop = 4;
            DemoUI.Show(panel, false);
        }

        private void OnDisable()
        {
            SetTyping(false);
            root?.Clear();
        }

        /// <summary>
        /// Shows the conversation with <paramref name="who"/>, or hides the panel when null. Cheap
        /// to call every frame: the log is rebuilt only when it grows.
        /// </summary>
        public void Converse(string who, string whoRole, string state, IReadOnlyList<DialogueLine> lines,
            IReadOnlyList<string> suggestions)
        {
            if (panel == null) return;
            IsOpen = who != null;
            DemoUI.Show(panel, IsOpen);
            if (!IsOpen)
            {
                shownFor = null;
                return;
            }
            this.who.text = who;
            role.text = whoRole ?? "";
            status.text = state ?? "";
            DemoUI.Show(status, !string.IsNullOrEmpty(state));
            int count = lines?.Count ?? 0;
            if (who != shownFor || count != shownLines)
            {
                shownFor = who;
                shownLines = count;
                log.Clear();
                for (int i = Mathf.Max(0, count - 5); i < count; i++) log.Add(Line(lines[i]));
                if (count == 0) DemoUI.Label(log, "Say anything. " + who + " decides what to do, from what the game allows.", "vui-muted");
            }
            if (!ReferenceEquals(suggestions, shownChips))
            {
                shownChips = suggestions;
                chips.Clear();
                if (suggestions != null)
                    foreach (var s in suggestions)
                    {
                        string text = s;
                        var chip = new Button(() => Submit(text)) { text = text };
                        chip.AddToClassList("vui-chip");
                        chips.Add(chip);
                    }
            }
            hint.text = IsTyping ? "Enter sends  ·  Esc goes back to walking" : "Enter to type  ·  or pick a suggestion";
        }

        private static VisualElement Line(DialogueLine line)
        {
            var row = new VisualElement();
            row.pickingMode = PickingMode.Ignore;
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 2;
            var who = new Label(line.Speaker + ":");
            who.AddToClassList("vui-text");
            who.style.unityFontStyleAndWeight = FontStyle.Bold;
            who.style.marginRight = 6;
            who.style.color = line.Kind == DialogueLine.Kinds.Player ? new Color(0.62f, 0.8f, 1f) : new Color(0.96f, 0.86f, 0.6f);
            var text = new Label(line.Text);
            text.AddToClassList(line.Kind == DialogueLine.Kinds.Note ? "vui-muted" : "vui-text");
            text.style.flexShrink = 1;
            if (line.Kind == DialogueLine.Kinds.Refusal) text.style.color = new Color(1f, 0.74f, 0.32f);
            if (line.Kind == DialogueLine.Kinds.Note) text.style.unityFontStyleAndWeight = FontStyle.Italic;
            row.Add(who);
            row.Add(text);
            return row;
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

        /// <summary>Puts the cursor in the text field, as Enter does.</summary>
        public void StartTyping()
        {
            if (command == null || !IsOpen) return;
            openedTypingFrame = Time.frameCount;
            command.Focus();
            SetTyping(true);
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
            if (keyboard == null) return;
            if (!IsTyping && IsOpen && Time.frameCount != submittedFrame &&
                (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                StartTyping();
            else if (keyboard.escapeKey.wasPressedThisFrame && IsTyping)
            {
                root.Focus();
                SetTyping(false);
            }
        }
    }
}
