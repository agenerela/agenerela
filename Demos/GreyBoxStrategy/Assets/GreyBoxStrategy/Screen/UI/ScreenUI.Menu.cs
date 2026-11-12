using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>The start menu: replay the recorded game, or watch a live one; and a notice line for what goes wrong.</summary>
    public sealed partial class ScreenUI
    {
        private VisualElement menu, notice;
        private Label menuStatus, noticeText;
        private Button liveButton;

        public void ShowMenu(string recordingTitle, string defaultModel, int defaultSeed, Action replay, Action<string, int> live)
        {
            menu = new VisualElement(); menu.AddToClassList("menu-veil");
            var card = new VisualElement(); card.AddToClassList("panel"); card.AddToClassList("menu");
            var title = new Label("PAINTED REALM"); title.AddToClassList("menu-title"); card.Add(title);
            var sub = new Label("Five countries, each ruled by a language model through the Agenerela framework, fight over 25 provinces.");
            sub.AddToClassList("insp-sub"); sub.style.marginBottom = 10; card.Add(sub);

            var a = new VisualElement(); a.AddToClassList("menu-choice");
            a.Add(new Label("Replay the recorded game") { style = { fontSize = 15, unityFontStyleAndWeight = FontStyle.Bold } });
            var at = new Label(recordingTitle + ". Every line is the rulers' own. No model and no server needed."); at.AddToClassList("why-no"); a.Add(at);
            var ab = new Button(() => { HideMenu(); replay(); }) { text = "Replay" }; ab.AddToClassList("menu-button"); a.Add(ab);
            card.Add(a);

            var b = new VisualElement(); b.AddToClassList("menu-choice");
            b.Add(new Label("Watch a live game") { style = { fontSize = 15, unityFontStyleAndWeight = FontStyle.Bold } });
            var bt = new Label($"Five model rulers with personas play a new game now, on the server in {LiveGame.UrlVariable} (the RTX 5080). It takes the card's lock while the model plays, a few minutes."); bt.AddToClassList("why-no"); b.Add(bt);
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 6 } };
            var model = new TextField("Model") { value = defaultModel }; model.AddToClassList("menu-field"); row.Add(model);
            var seed = new IntegerField("Seed") { value = defaultSeed }; seed.AddToClassList("menu-field"); seed.style.width = 140; row.Add(seed);
            b.Add(row);
            liveButton = new Button(() => live(model.value, seed.value)) { text = "Watch live" }; liveButton.AddToClassList("menu-button"); b.Add(liveButton);
            menuStatus = new Label(); menuStatus.AddToClassList("menu-status"); b.Add(menuStatus);
            card.Add(b);

            var keys = new Label("In the game: Space play/pause · N step · + speed · 1–4 map modes · D diplomacy · Esc close · click a province, a crest or a chronicle line · right-drag to pan, wheel to zoom");
            keys.AddToClassList("src"); card.Add(keys);
            menu.Add(card);
            Root.Add(menu);
        }

        public void ShowMenuStatus(string text, bool busy = false)
        {
            if (menuStatus == null) return;
            menuStatus.text = text ?? "";
            menuStatus.EnableInClassList("busy", busy);
            liveButton?.SetEnabled(!busy);
        }

        public void HideMenu() { menu?.RemoveFromHierarchy(); menu = null; }
        public bool MenuOpen => menu != null;

        /// <summary>A line across the top of the map: what went wrong, or what is happening.</summary>
        public void ShowNotice(string text)
        {
            if (notice == null)
            {
                notice = new VisualElement(); notice.AddToClassList("notice");
                noticeText = new Label(); notice.Add(noticeText);
                var close = new Button(() => notice.style.display = DisplayStyle.None) { text = "×" }; close.AddToClassList("tbtn"); notice.Add(close);
                Root.Add(notice);
            }
            noticeText.text = text;
            notice.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
