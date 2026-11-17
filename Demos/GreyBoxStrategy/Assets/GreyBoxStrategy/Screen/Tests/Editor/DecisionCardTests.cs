using System.IO;
using System.Linq;
using GreyBoxStrategy.Replay;
using NUnit.Framework;
using UnityEngine;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// The decision inspector shows only what the framework recorded. These check that the card
    /// carries the recorded answer, the game's own menu and refusals, and the telemetry, and that
    /// it says how each decision ended.
    /// </summary>
    public class DecisionCardTests
    {
        private static ReplayGame game;
        private static ReplayGame Game => game ?? (game = ReplayBuilder.Build(Recording.Parse(File.ReadAllText(
            Path.Combine(Application.dataPath, "GreyBoxStrategy/Screen/Replays/seed-0004-personas-4b.json")))));

        private static ReplayEvent First(ReplayEventKind kind, string country = null) =>
            Game.AllEvents.First(e => e.Kind == kind && e.Decision != null && (country == null || e.Country == country));

        [Test]
        public void The_card_shows_the_answer_in_the_order_the_model_writes_it()
        {
            var c = DecisionCard.From(First(ReplayEventKind.Fort, "valen"));
            int a = c.AnswerJson.IndexOf("\"action\""), t = c.AnswerJson.IndexOf("\"target\""), s = c.AnswerJson.IndexOf("\"statement\"");
            Assert.That(a, Is.GreaterThanOrEqualTo(0));
            Assert.That(a < t && t < s, "action, then target, then statement");
            Assert.That(c.AnswerJson, Does.Contain("ashby"));
        }

        [Test]
        public void The_menu_is_the_game_s_own_and_marks_the_choice()
        {
            var c = DecisionCard.From(First(ReplayEventKind.Fort, "valen"));
            Assert.That(c.Offered, Does.Contain("fortify ashby"));
            Assert.That(c.Offered, Does.Contain("none"));
            Assert.That(c.Picked, Is.EqualTo(c.Offered.IndexOf("fortify ashby")));
            Assert.That(c.NotOffered, Is.Not.Empty);
            Assert.That(c.NotOffered.All(n => !string.IsNullOrEmpty(n.why)), "every move not offered says why");
        }

        [Test]
        public void The_card_carries_the_recorded_telemetry_and_facts()
        {
            var e = First(ReplayEventKind.Capture, "kesh");
            var c = DecisionCard.From(e);
            Assert.That(c.Model, Is.EqualTo("qwen3.5:4b"));
            Assert.That(c.LatencySeconds, Is.EqualTo(e.Decision.LatencySeconds));
            Assert.That(c.PromptTokens, Is.GreaterThan(0));
            Assert.That(c.Standing.First(), Does.StartWith("Turn "));
            Assert.That(c.Grounding, Is.Not.Null);
            var grounding = c.Guards.Single(g => g.Name == "TargetGroundingGuard");
            Assert.That(grounding.State, Is.EqualTo("passed"));
            Assert.That(c.Outcome, Is.EqualTo(CardOutcome.Done));
        }

        [Test]
        public void A_move_refused_when_it_landed_says_so_and_why()
        {
            var refused = Game.AllEvents.Where(e => e.Kind == ReplayEventKind.Refused && e.Decision != null).ToList();
            Assert.That(refused, Is.Not.Empty, "seed 4 has moves an earlier move made stale");
            var c = DecisionCard.From(refused[0]);
            Assert.That(c.Outcome, Is.EqualTo(CardOutcome.RefusedAtResolution));
            Assert.That(c.Guards.Last().Detail, Does.Contain(refused[0].Text.Replace("refused: ", "")));
        }
    }
}
