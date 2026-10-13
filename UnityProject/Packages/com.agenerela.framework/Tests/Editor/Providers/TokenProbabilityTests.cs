using System.Linq;
using Agenerela.Providers;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Agenerela.Tests
{
    public class TokenProbabilityTests
    {
        // "Attack the scarecrow." in a scene with a training dummy and no scarecrow: the mask
        // made the model write training_dummy, but at the target's first token it wanted "sc"
        // (DR-016). The numbers are shaped like the probe's, not measured.
        const string Reply = "{\"action\":\"attack\",\"target\":\"training_dummy\",\"statement\":\"Have at you!\"}";

        static TokenProbability Token(string text, float logProbability, params (string text, float logProbability)[] alternatives) =>
            new TokenProbability
            {
                Text = text,
                LogProbability = logProbability,
                Alternatives = alternatives
                    .Select(a => new TokenAlternative { Text = a.text, LogProbability = a.logProbability })
                    .ToList(),
            };

        static ProviderResult HandBuilt() => new ProviderResult
        {
            Text = Reply,
            TokenProbabilities = new[]
            {
                Token("{\"", 0f), Token("action", 0f), Token("\":\"", 0f), Token("attack", -0.02f),
                Token("\",\"", 0f), Token("target", 0f), Token("\":\"", 0f),
                Token("training", -3f, ("sc", -0.06f), ("training", -3f), ("scare", -5.25f), ("Sc", -6.5f), ("straw", -7f)),
                Token("_d", -0.01f), Token("ummy", 0f),
                Token("\",\"", 0f), Token("statement", 0f), Token("\":\"", 0f),
                Token("Have", -1.5f), Token(" at", -0.25f), Token(" you", -0.5f), Token("!\"}", -0.75f),
            },
        };

        // Saved with a run, the probabilities let a later check be scored on the same answers
        // with no new requests: how the probe's second run moved DR-016 to every token.
        static ProviderResult RoundTrip(ProviderResult result) =>
            JsonConvert.DeserializeObject<ProviderResult>(JsonConvert.SerializeObject(result));

        [Test]
        public void NothingIsReportedUntilAProviderSaysSo()
        {
            Assert.That(new ProviderCapabilities().ReportsTokenProbabilities, Is.False);
            Assert.That(new ProviderResult().TokenProbabilities, Is.Null,
                "Null means not reported, which the guard must never read as a reply with no tokens.");
            Assert.That(new TokenProbability().Alternatives, Is.Not.Null.And.Empty);
        }

        [Test]
        public void TheTokensJoinBackIntoTheReply()
        {
            var back = RoundTrip(HandBuilt());

            Assert.That(back.Text, Is.EqualTo(Reply), "Text stays exactly as received.");
            Assert.That(string.Concat(back.TokenProbabilities.Select(t => t.Text)), Is.EqualTo(back.Text));
        }

        [Test]
        public void EachPositionKeepsItsAlternativesInOrder()
        {
            var target = RoundTrip(HandBuilt()).TokenProbabilities[7];

            Assert.That(target.Text, Is.EqualTo("training"));
            Assert.That(target.LogProbability, Is.EqualTo(-3f));
            Assert.That(target.Alternatives.Select(a => a.Text),
                Is.EqualTo(new[] { "sc", "training", "scare", "Sc", "straw" }));
            Assert.That(target.Alternatives.Select(a => a.LogProbability),
                Is.EqualTo(new[] { -0.06f, -3f, -5.25f, -6.5f, -7f }));
        }
    }
}
