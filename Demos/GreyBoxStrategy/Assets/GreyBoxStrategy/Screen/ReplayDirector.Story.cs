using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The storyteller on the screen: its events land as banners where they strike and as lines in
    /// the chronicle, open into the event card and the inspector, gather into tales, and, in rule
    /// mode, a dilemma for you stops the turn behind the choice card.
    /// </summary>
    public sealed partial class ReplayDirector
    {
        private EventChoice lastChoice;

        private void WireStory()
        {
            ui.EventPressed = OpenEvent;
            ui.StoryDecisionPressed = InspectStory;
            ui.TalesSource = () => (game?.Chains ?? new List<Chain>(), played.Count > 0 ? played[played.Count - 1].Turn : 0, played);
        }

        /// <summary>For the Unity CLI and the chronicle: open a storyteller event's card and find it on the map.</summary>
        public void OpenEvent(ReplayEvent e)
        {
            if (e?.Story == null) return;
            if (e.ProvinceIndex >= 0) PanTo(e.ProvinceIndex);
            ui.ShowEvent(e);
        }

        /// <summary>For the Unity CLI: the last storyteller event played, opened.</summary>
        public string OpenLastEvent()
        {
            var e = played.LastOrDefault(x => x.Kind == ReplayEventKind.Story);
            if (e == null) return "no storyteller event played yet";
            OpenEvent(e);
            return $"turn {e.Turn}: {e.Story.Title}";
        }

        /// <summary>For the Unity CLI: the storyteller's decision behind the last event, in the inspector.</summary>
        public string InspectLastStory(bool title = false)
        {
            var e = played.LastOrDefault(x => x.Kind == ReplayEventKind.Story && x.Decision != null);
            if (e == null) return "no storyteller decision played yet";
            InspectStory(title && e.TitleDecision != null ? e.TitleDecision : e.Decision, e);
            return $"turn {e.Turn}: {e.Decision.Action} on {e.Decision.Target}";
        }

        private void InspectStory(RecordedDecision d, ReplayEvent e)
        {
            if (d == null) return;
            ui.ShowCard(DecisionCard.From(new ReplayEvent { Turn = e.Turn, Kind = ReplayEventKind.Story, Decision = d, Text = e.Text }));
        }

        private readonly StoryQueue storyQueue = new StoryQueue();

        // A storyteller event lands. An event with an effect opens the event window in the middle of the screen (the
        // turn waits for it when you rule; watching, it closes by itself), a tale the banner under the top bar; the
        // setting "Event windows" can keep either to your own realm or off, and then it is the chronicler's bubble.
        private IEnumerator LandStory(ReplayEvent e, ChronicleLine line)
        {
            var viewer = ui.Fog.Viewer;
            var card = StoryCards.For(e, viewer, CountryLook.NameOf, Cap, ui.GameOver);
            ui.EventWindowsSetting = settings?.EventWindows ?? EventWindows.All;
            var show = StoryCards.Show(card, ui.EventWindowsSetting);
            if (show == StoryShow.Bubble) { yield return LandStoryBubble(e, line); yield break; }
            // The turn's windows, in the order they land: "1 of 2" when two land in one turn.
            if (storyQueue.Turn != e.Turn || !storyQueue.Has(e))
            {
                var turn = game.Turns.FirstOrDefault(t => t.Turn == e.Turn);
                storyQueue.Load(e.Turn, (turn?.Events ?? new List<ReplayEvent> { e }).Where(x => x.Kind == ReplayEventKind.Story && x.Story != null)
                    .Select(x => StoryCards.For(x, viewer, CountryLook.NameOf, Cap, ui.GameOver))
                    .Where(c => StoryCards.Show(c, ui.EventWindowsSetting) == StoryShow.Window));
            }
            if (e.ProvinceIndex >= 0) map.SetFocus(e.ProvinceIndex, 0.55f);
            Show(e.After, null);
            ui.AddLine(line);
            if (ui.TalesOpen) ui.RenderTales();
            if (show == StoryShow.Banner)
            {
                ui.ShowTaleBanner(card, StoryCards.StaySeconds(card, speed) + 2f);
                yield return Wait(1.2f);
                map.SetFocus(-1, 0);
                yield break;
            }
            bool open = true;
            ui.ShowEvent(e, storyQueue.Place(e), () => open = false);
            bool waits = StoryCards.Waits(viewer, autoOrders || autoplaying);
            float shut = Time.unscaledTime + StoryCards.StaySeconds(card, speed);
            // Skip never waits: a jump ahead (skipUntilTurn) closes the window at once.
            while (open && skipUntilTurn <= e.Turn && (waits || Time.unscaledTime < shut)) yield return null;
            if (open) ui.DismissEvent();
            storyQueue.Pass(e);
            map.SetFocus(-1, 0);
        }

        // The chronicler's bubble where it strikes, the line, the numbers: the event when the setting keeps its window shut.
        private IEnumerator LandStoryBubble(ReplayEvent e, ChronicleLine line)
        {
            if (e.ProvinceIndex >= 0) map.SetFocus(e.ProvinceIndex, 0.55f);
            var banner = ui.Tell(e, () => OpenEvent(e));
            if (settings != null && settings.EventWindows) OpenEvent(e); // Settings: event windows open by themselves
            yield return Wait(0.5f);
            Show(e.After, null);
            ui.AddLine(line);
            if (ui.TalesOpen) ui.RenderTales();
            yield return Wait(Reading(e.Statement) + 0.8f);
            map.SetFocus(-1, 0);
            ui.Unsay(banner);
        }

        /// <summary>For the Unity CLI: your answer to the dilemma on the choice card, as two clicks on it would.</summary>
        public string ChooseOption(string optionId)
        {
            if (rule?.WaitingFor != RuleSession.Waiting.Choice) return "no choice is waiting";
            try { ui.HideChoice(); rule.Choose(optionId); return "chose " + optionId; }
            catch (System.ArgumentException e) { return e.Message; }
        }

        // RuleTick's part for the storyteller: its call, then your choice card. True when it set the status.
        private bool StoryTick(RuleSession.Waiting w, out string status)
        {
            status = null;
            if (w == RuleSession.Waiting.Storyteller)
            {
                ui.OrdersRequest = null;
                status = RulersLine("The chronicler is deciding what happens before the next upkeep");
                ui.SetThinking(true);
                return true;
            }
            if (w == RuleSession.Waiting.Choice && rule.Choice != null)
            {
                ui.OrdersRequest = null;
                if (lastChoice != rule.Choice)
                {
                    lastChoice = rule.Choice;
                    ui.ShowChoice(rule.Choice, id => { ui.HideChoice(); rule.Choose(id); RuleTick(); });
                }
                status = "The chronicler asks you to choose. The turn waits for your choice.";
                ui.SetThinking(rule.ModelsThinking);
                return true;
            }
            return false;
        }
    }
}
