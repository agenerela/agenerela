using System;
using System.Collections.Generic;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The dev menu: a plain, dense tool panel, not part of the look. Every button runs a command
    /// line through <see cref="DevCommands"/>, the same lines <c>DevTools.Run</c> takes from code;
    /// the edit buttons fill the command line for you to finish.
    /// </summary>
    public sealed partial class ScreenUI
    {
        private VisualElement devPanel;
        private Label devReadouts, devOutput, devMark;
        private TextField devLine;

        public bool DevOpen => devPanel != null;

        /// <summary>True while a text field has the keyboard, so the game's keys do not fire as you type.</summary>
        public bool Typing
        {
            get
            {
                var f = Root.panel?.focusController?.focusedElement as VisualElement;
                return f != null && (f is TextField || f is IntegerField || f.GetFirstAncestorOfType<TextField>() != null || f.GetFirstAncestorOfType<IntegerField>() != null);
            }
        }

        public void CloseDev() { devPanel?.RemoveFromHierarchy(); devPanel = null; }

        public void ShowDev(DevCommands commands, string you, string rival, IReadOnlyList<string> events)
        {
            CloseDev();
            devPanel = new VisualElement(); devPanel.AddToClassList("dev");
            var head = new VisualElement(); head.AddToClassList("dev-head");
            head.Add(new Label("DEV MENU · F1") { style = { flexGrow = 1, unityFontStyleAndWeight = FontStyle.Bold } });
            var close = new Button(CloseDev) { text = "×" }; close.AddToClassList("dev-b"); head.Add(close);
            devPanel.Add(head);
            var scroll = new ScrollView(); scroll.style.flexShrink = 1; devPanel.Add(scroll);
            var body = scroll.contentContainer;

            devReadouts = new Label(); devReadouts.AddToClassList("dev-mono"); body.Add(devReadouts);

            string Run(string line) { string r = commands.Run(line); devOutput.text = "> " + line + "\n" + r; return r; }
            void Buttons(string title, params (string label, string command)[] items)
            {
                body.Add(new Label(title) { pickingMode = PickingMode.Ignore }.WithClass("dev-sec"));
                var row = new VisualElement(); row.AddToClassList("dev-row");
                foreach (var (label, command) in items)
                {
                    var b = new Button(() => Run(command)) { text = label, tooltip = command }; b.AddToClassList("dev-b"); row.Add(b);
                }
                body.Add(row);
            }
            void Fill(string title, params (string label, string template)[] items)
            {
                body.Add(new Label(title) { pickingMode = PickingMode.Ignore }.WithClass("dev-sec"));
                var row = new VisualElement(); row.AddToClassList("dev-row");
                foreach (var (label, template) in items)
                {
                    var b = new Button(() => { devLine.value = template; devLine.Focus(); }) { text = label, tooltip = "Fill the command line: " + template };
                    b.AddToClassList("dev-b"); b.AddToClassList("fill"); row.Add(b);
                }
                body.Add(row);
            }

            var lineRow = new VisualElement(); lineRow.AddToClassList("dev-row");
            devLine = new TextField { value = "help" }; devLine.AddToClassList("dev-line");
            devLine.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { Run(devLine.value); e.StopPropagation(); } }, TrickleDown.TrickleDown);
            var go = new Button(() => Run(devLine.value)) { text = "Run" }; go.AddToClassList("dev-b");
            lineRow.Add(devLine); lineRow.Add(go);
            body.Add(lineRow);
            devOutput = new Label("Type a command, or press a button. help lists them all."); devOutput.AddToClassList("dev-mono"); devOutput.AddToClassList("dev-out");
            body.Add(devOutput);

            Buttons("PLAY WITHOUT THE CARD", ($"Rule {you}, stand-ins", $"scripted-rule {you} 7"), ("Watch, stand-ins", "scripted-watch 7"));
            Buttons("GENERATED WORLD (scripted rulers, drawn chart)", ("Small", "generated small continent 7"), ("Medium", "generated medium continent 7"),
                ("Large", "generated large continent 7"), ("Huge", "generated huge continent 7"), ("Two lands", "generated medium two_lands 7"),
                ("Inland sea", "generated medium inland_sea 7"), ("Peninsula", "generated medium peninsula 7"), ("Long coast", "generated medium long_coast 7"));
            Buttons("STORYTELLER (a stand-in one, no server)", ($"Rule {you}, saga", $"scripted-rule {you} 7 saga"), ("Watch, saga", "scripted-watch 7 saga"),
                ("Dilemma on you", $"event sellswords {you}"), ("Pretender tale", $"event begin_pretender {you}"), ("Omen", $"event omen {you}"),
                ("Open last event", "story-open"), ("Its decision", "story-inspect"), ("Title call", "story-inspect title"), ("Tales", "tales"));
            Buttons("TIME", ("Step", "step"), ("Play", "play"), ("Pause", "pause"), ("1×", "speed 1"), ("4×", "speed 4"), ("16×", "speed 16"),
                ("Turn 5", "turn 5"), ("Turn 15", "turn 15"), ("Autoplay", "autoplay on"), ("Stop autoplay", "autoplay off"));
            Fill("STATE (rule games; at the start of the next turn)",
                ("Gold", $"gold {you} +100"), ("Soldiers", $"army {you} +20"), ("Stability", $"stability {you} =5"), ("Fort", "forts harrow +1"),
                ("Owner", $"owner harrow {you}"), ("War", $"war {you} {rival}"), ("Peace", $"peace {you} {rival}"), ("Eliminate", $"eliminate {rival}"));
            Buttons("FOG", ("Reveal", "fog off"), ("Restore", "fog on"));
            var ev = new List<(string, string)>();
            foreach (string id in events) ev.Add((id, $"event {id} {you}"));
            Fill("EVENTS (the storyteller's templates)", ev.ToArray());
            Buttons("FAULTS (pretended; the real lock and server are never touched)",
                ("Card held", "fault lock another session"), ("Server down", "fault server on"), ("Round times out", "fault timeout on"),
                ("Unreadable replies", "fault unreadable on"), ("All off", "faults off"));
            Buttons("CAPTURE", ("Screenshot", "screenshot"), ("Save recording", "save-recording"));
            Buttons("WINDOW", ("1366×768", "size 1366x768"), ("1920×1080", "size 1920x1080"), ("2560×1440", "size 2560x1440"), ("3840×2160", "size 3840x2160"), ("1920×1200", "size 1920x1200"), ("3840×2400", "size 3840x2400"));
            Buttons("HELP", ("All commands", "help"), ("Event list", "events"), ("Readouts", "readouts"));
            Root.Add(devPanel);
        }

        public void SetDevReadouts(string text) { if (devReadouts != null) devReadouts.text = text; }

        /// <summary>A red DEV mark in the top bar while the game on screen is a dev game.</summary>
        public void SetDevMark(bool on)
        {
            if (devMark == null)
            {
                devMark = new Label("DEV") { tooltip = "Changed through the dev menu, played with stand-in rulers, or under a pretended fault: not a measured game." };
                devMark.AddToClassList("dev-mark");
                turnLabel.parent.Insert(0, devMark);
            }
            devMark.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    internal static class VisualElementClassExtensions
    {
        public static T WithClass<T>(this T e, string cls) where T : VisualElement { e.AddToClassList(cls); return e; }
    }
}
