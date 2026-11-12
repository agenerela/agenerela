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
        private Button liveButton, ruleButton;
        private Label ruleStatus;

        public void ShowMenu(string recordingTitle, string defaultModel, int defaultSeed, Action replay, Action<string, int> live, Action<string, string, int> rule = null)
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

            if (rule != null)
            {
                var c = new VisualElement(); c.AddToClassList("menu-choice");
                c.Add(new Label("Rule a country") { style = { fontSize = 15, unityFontStyleAndWeight = FontStyle.Bold } });
                var ct = new Label("You rule one country against four model rulers with personas, on the same server. You see what your court knows; they decide while you choose. The card's lock is taken only while they decide."); ct.AddToClassList("why-no"); c.Add(ct);
                string pickedCountry = "kesh";
                var crests = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
                foreach (var id in CountryIds)
                {
                    var look = CountryLook.Of(id);
                    var cb = new Button { tooltip = $"Rule {look.Name}" }; cb.AddToClassList("menu-crest");
                    cb.Add(new Crest(look, 22)); cb.Add(new Label(look.Name));
                    cb.clicked += () => { pickedCountry = id; foreach (var x in crests.Children()) x.RemoveFromClassList("pick"); cb.AddToClassList("pick"); };
                    if (id == pickedCountry) cb.AddToClassList("pick");
                    crests.Add(cb);
                }
                c.Add(crests);
                var row2 = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 6 } };
                var model2 = new TextField("Model") { value = defaultModel }; model2.AddToClassList("menu-field"); row2.Add(model2);
                var seed2 = new IntegerField("Seed") { value = defaultSeed }; seed2.AddToClassList("menu-field"); seed2.style.width = 140; row2.Add(seed2);
                c.Add(row2);
                ruleButton = new Button(() => rule(pickedCountry, model2.value, seed2.value)) { text = "Rule" }; ruleButton.AddToClassList("menu-button"); c.Add(ruleButton);
                ruleStatus = new Label(); ruleStatus.AddToClassList("menu-status"); c.Add(ruleStatus);
                card.Add(c);
            }

            var keys = new Label("In the game: Enter ends your turn · Space play/pause · N step · + speed · 1–4 map modes · D diplomacy · Esc close · click a province, a crest or a chronicle line · right-drag to pan, wheel to zoom");
            keys.AddToClassList("src"); card.Add(keys);
            menu.Add(card);
            Root.Add(menu);
        }

        /// <summary>The rule choice's status line: why it cannot start, or that it is starting.</summary>
        public void ShowRuleStatus(string text, bool busy = false)
        {
            if (ruleStatus == null) return;
            ruleStatus.text = text ?? "";
            ruleStatus.EnableInClassList("busy", busy);
            ruleButton?.SetEnabled(!busy);
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
