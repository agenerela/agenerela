using System;
using System.Linq;
using System.Threading.Tasks;
using Agenerela;
using LivingVale.Agenerela;
using LivingVale.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LivingVale.Tests
{
    public sealed class ScriptedNpcDecisionDriverTests
    {
        private static readonly NpcDescriptor Mara = new NpcDescriptor("npc_mara", "Mara", "General trader");

        private ScriptedNpcDecisionDriver script;
        private AgenerelaNpcDecisionDriver driver;
        private ConversationCoordinator coordinator;

        [SetUp]
        public void SetUp()
        {
            script = new ScriptedNpcDecisionDriver(seed: 1) { NormalDelay = TimeSpan.FromMilliseconds(5), SlowDelay = TimeSpan.FromMilliseconds(300) };
            driver = new ScriptedReplies(script).Driver();
            coordinator = new ConversationCoordinator(driver, new ConversationSettings { TurnTimeout = TimeSpan.FromMilliseconds(150) });
            coordinator.Open(Mara);
        }

        private async Task<TurnFailure> Talk(string line)
        {
            var failure = TurnFailure.None;
            coordinator.Changed += Record;
            try
            {
                Assert.That(coordinator.Submit(line), Is.True);
                await Run.NotCancelled(coordinator.LastTurn);
            }
            finally
            {
                coordinator.Changed -= Record;
            }

            return failure;

            void Record(ConversationEvent e)
            {
                if (e.Kind == ConversationEventKind.TurnFailed)
                {
                    failure = e.Failure;
                }
            }
        }

        [Test]
        public async Task KeywordPicksTheScriptedLine_AsANoneDecision()
        {
            Assert.That(await Talk("Do you sell a POTION?"), Is.EqualTo(TurnFailure.None));

            var last = coordinator.HistoryFor(Mara.Id).Last();
            Assert.That(last.Speaker, Is.EqualTo(ChatSpeaker.Npc));
            StringAssert.Contains("ten gold", last.Text);
            Assert.That(driver.ExecutedCount, Is.EqualTo(1));
        }

        [Test]
        public async Task UnknownNpc_StillAnswers()
        {
            coordinator.Open(new NpcDescriptor("npc_nobody", "Nobody", ""));
            Assert.That(await Talk("hello"), Is.EqualTo(TurnFailure.None));
        }

        [Test]
        public void Label_SaysScripted_AndNamesTheFixture()
        {
            StringAssert.Contains("Scripted", driver.Label);
            script.Fixture = ScriptedFixture.Slow;
            StringAssert.Contains("Slow", driver.Label);
        }

        [Test]
        public async Task NeverAnswers_EndsInTimeout()
        {
            script.Fixture = ScriptedFixture.NeverAnswers;
            Assert.That(await Talk("hello"), Is.EqualTo(TurnFailure.Timeout));
        }

        [Test]
        public async Task Slow_PastTheTimeout_IsATimeout_AndNeverExecutes()
        {
            script.Fixture = ScriptedFixture.Slow;
            Assert.That(await Talk("hello"), Is.EqualTo(TurnFailure.Timeout));
            Assert.That(driver.ExecutedCount, Is.Zero);
        }

        [Test]
        public async Task Malformed_IsMalformed()
        {
            script.Fixture = ScriptedFixture.Malformed;
            Assert.That(await Talk("hello"), Is.EqualTo(TurnFailure.Malformed));
        }

        [Test]
        public async Task Throws_IsAProviderError()
        {
            script.Fixture = ScriptedFixture.Throws;
            Assert.That(await Talk("hello"), Is.EqualTo(TurnFailure.ProviderError));
        }
    }
}
