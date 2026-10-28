using System.Collections.Generic;
using Agenerela.Providers;
using NUnit.Framework;
using static Agenerela.Tests.Tokens;

namespace Agenerela.Tests
{
    // TargetGrounding.Read on its own, over hand-built token probabilities: the share, where it is
    // lowest, what the model wanted, and each reason there is no share. The probe's numbers are in
    // ItMatchesTheProbeNumberForNumber. The guard that acts on the reading is in
    // TargetGroundingGuardTests.
    public sealed class TargetGroundingTests
    {
        private static readonly string[] Village = { "no_target", "training_dummy", "tower", "bridge", "sword", "barrel" };
        private static readonly string[] Fort = { "no_target", "north_gate", "south_gate", "east_tower", "west_tower", "shield", "helmet", "well" };

        [Test]
        public void ASubstitutedTargetReadsLowAndSaysWhatTheModelWanted()
        {
            // "Attack the scarecrow.": the mask wrote training_dummy, but the model wanted "sc".
            var reading = Read(Reply("attack_target",
                T("training", -2.95, ("sc", -0.06), ("str", -3.4), ("training", -2.95), ("the", -4.1), ("dummy", -4.8)),
                T("_dummy", -0.01, ("_dummy", -0.01))), Village);

            Assert.That(reading.Status, Is.EqualTo(GroundingStatus.Scored));
            Assert.That(reading.Share, Is.EqualTo(0.0523).Within(0.0001), "Only training and the unlisted rest; sc, str, the and dummy are off the list.");
            Assert.That(reading.LowestAt, Is.EqualTo(0));
            Assert.That(reading.Wanted, Is.EqualTo("sc"), "The likeliest off-list alternative.");
        }

        [Test]
        public void TheRightTargetReadsHigh()
        {
            // "Attack the training dummy."
            var reading = Read(Reply("attack_target",
                T("training", -0.001, ("training", -0.001), ("dummy", -7.5)),
                T("_dummy", -0.001, ("_dummy", -0.001))), Village);

            Assert.That(reading.Status, Is.EqualTo(GroundingStatus.Scored));
            Assert.That(reading.Share, Is.EqualTo(0.999).Within(0.0005));
            Assert.That(reading.Wanted, Is.EqualTo("dummy"), "Off the list, though too unlikely to matter.");
        }

        [Test]
        public void ALookAlikeIsCaughtPastItsFirstToken()
        {
            // "Pick up the spear." beside sword: "s" could still become sword, so the first token is
            // all on the list. Only the next one shows that the model wanted "pear".
            var reading = Read(Reply("pick_up_item",
                T("s", -0.001, ("s", -0.001), ("sh", -8.0)),
                T("word", -6.0, ("pear", -0.004), ("word", -6.0), ("hield", -7.0))), Village);

            Assert.That(reading.Share, Is.EqualTo(0.0025).Within(0.0001));
            Assert.That(reading.LowestAt, Is.EqualTo(1),
                "A reading of the first token alone would read 0.999 here, and pass the spear as the sword.");
            Assert.That(reading.Wanted, Is.EqualTo("spear"), "Written onto the s before it.");
        }

        [Test]
        public void AnOpenChoiceBetweenTwoListedTargetsStaysOnTheList()
        {
            // "Check one of the gates.": the model was unsure which, but sure it was a listed one.
            var reading = Read(Reply("move_to",
                T("south", -0.598, ("south", -0.598), ("north", -0.844), ("the", -6.0)),
                T("_gate", -0.001, ("_gate", -0.001))), Fort);

            Assert.That(reading.Share, Is.EqualTo(0.98).Within(0.002), "0.55 and 0.43, both on the list.");
        }

        [Test]
        public void ATokenThatClosesTheValueMustCompleteAWholeId()
        {
            // At the second token the written part is "wel". l" completes well, a listed id; a bare
            // closing quote would leave "wel", which is not one, though it starts one.
            var reading = Read(Reply("move_to",
                T("wel", -0.001, ("wel", -0.001)),
                T("l", -0.105, ("l\"", -0.223), ("\"", -1.609), ("l", -2.303))), Fort);

            Assert.That(reading.Share, Is.EqualTo(0.9).Within(0.001), "l\" (0.8) and l (0.1) count; the bare quote (0.2) does not.");
            Assert.That(reading.LowestAt, Is.EqualTo(1));
            Assert.That(reading.Wanted, Is.EqualTo("wel"), "Closing on wel was what it wanted off the list.");
        }

        [Test]
        public void ProbabilityTheProviderDidNotReportCountsAgainst()
        {
            // Five alternatives, all on the list, summing to 0.6: the rest is off the list, as the
            // probe counted it when the threshold was calibrated.
            var reading = Read(Reply("move_to",
                T("tower", -1.204, ("tower", -1.204), ("bridge", -2.303), ("barrel", -2.303), ("sword", -2.996), ("training", -2.996))), Village);

            Assert.That(reading.Share, Is.EqualTo(0.6).Within(0.001));
            Assert.That(reading.Wanted, Is.Null, "No alternative was off the list.");
        }

        [Test]
        public void TheReadingIsTheLowestShareNotTheProduct()
        {
            var reading = Read(Reply("attack_target",
                T("training", -0.105, ("training", -0.105), ("sc", -2.303)),
                T("_dummy", -0.105, ("_dummy", -0.105), ("_x", -2.303))), Village);

            Assert.That(reading.Share, Is.EqualTo(0.9).Within(0.001), "0.9 at both tokens; multiplied, it would read 0.81.");
            Assert.That(reading.LowestAt, Is.EqualTo(0), "The first of two equal shares.");
        }

        [Test]
        public void IdsAreComparedIgnoringCase()
        {
            var reading = Read(Reply("move_to", T("Tower", 0, ("Tower", -0.105), ("TOWER", -2.303))), Village);

            Assert.That(reading.Share, Is.EqualTo(1.0).Within(0.001), "The probe lower-cased both sides.");
        }

        [Test]
        public void OnlyTheFirstTokenIsStrippedOfTheSpaceAndQuoteBeforeTheValue()
        {
            // The opening quote merged into the first token: its alternatives carry it too.
            var tokens = new List<TokenProbability>
            {
                T("{\"action\": \"move_to\", \"target\":"),
                T(" \"tow", -0.051, (" \"tow", -0.051), (" \"sc", -3.0)),
                T("er", -0.105, ("er", -0.105), (" er", -2.303)),
                T("\"}"),
            };

            var reading = TargetGrounding.Read(Join(tokens), tokens, Village);

            Assert.That(reading.Status, Is.EqualTo(GroundingStatus.Scored));
            Assert.That(reading.Share, Is.EqualTo(0.9).Within(0.001),
                "\" tow\" starts tower once stripped; \" er\" after tow is off the list, unstripped.");
            Assert.That(reading.LowestAt, Is.EqualTo(1));
        }

        [TestCase("\u001Ctower")]
        [TestCase("\u001F \"tower")]
        [TestCase("　\"tower")]
        public void TheFirstTokenIsStrippedOfWhatPythonCallsWhitespace(string alternative)
        {
            // str.lstrip() also strips U+001C to U+001F, which .NET's char.IsWhiteSpace does not.
            var reading = Read(Reply("move_to", T("tower", -0.051, (alternative, -0.051), ("sc", -3.0))), Village);

            Assert.That(reading.Share, Is.EqualTo(0.95).Within(0.001));
        }

        [Test]
        public void WithoutProbabilitiesThereIsNoShare()
        {
            string text = Join(Reply("move_to", T("tower")));

            foreach (var none in new[] { null, new List<TokenProbability>() })
            {
                var reading = TargetGrounding.Read(text, none, Village);

                Assert.That(reading.Status, Is.EqualTo(GroundingStatus.NoProbabilities));
                Assert.That(reading.Share, Is.EqualTo(0f));
                Assert.That(reading.LowestAt, Is.EqualTo(-1));
                Assert.That(reading.Wanted, Is.Null);
            }
        }

        [Test]
        public void ATargetTokenWithoutAlternativesHasNoShare()
        {
            // A provider that reports each token's probability but none of the alternatives. Read as
            // the probe would, the token's share is 0, and every target would be refused.
            var empty = Reply("attack_target",
                T("training", -2.95, ("sc", -0.06), ("training", -2.95)), T("_dummy", -0.01));
            var missing = Reply("attack_target",
                T("training", -2.95, ("sc", -0.06), ("training", -2.95)), new TokenProbability { Text = "_dummy", Alternatives = null });

            Assert.That(Read(empty, Village).Status, Is.EqualTo(GroundingStatus.NoProbabilities));
            Assert.That(Read(missing, Village).Status, Is.EqualTo(GroundingStatus.NoProbabilities));
            Assert.That(Read(empty, Village).LowestAt, Is.EqualTo(-1));
        }

        [Test]
        public void WithoutATargetFieldThereIsNoShare()
        {
            var tokens = Reply("move_to", T("tower"));

            Assert.That(TargetGrounding.Read(Join(tokens), tokens, null).Status, Is.EqualTo(GroundingStatus.NoTargetField));
            Assert.That(TargetGrounding.Read(Join(tokens), tokens, new string[0]).Status, Is.EqualTo(GroundingStatus.NoTargetField));
        }

        [Test]
        public void ASplitCharacterBeforeTheTargetMakesItUnreadable()
        {
            // The reply's "é" was split across two tokens and is missing from both, so every offset
            // after it is off by one. The reading never guesses where the target is.
            var tokens = new List<TokenProbability>
            {
                T("{\"action\": \"caf"),
                T("\", \"target\": \""),
                T("tower", -0.001, ("tower", -0.001)),
                T("\", \"statement\": \"\"}"),
            };
            string reply = "{\"action\": \"café\", \"target\": \"tower\", \"statement\": \"\"}";

            var reading = TargetGrounding.Read(reply, tokens, Village);

            Assert.That(reading.Status, Is.EqualTo(GroundingStatus.Unreadable));
            Assert.That(reading.LowestAt, Is.EqualTo(-1));
        }

        [Test]
        public void ASplitCharacterAfterTheTargetDoesNotMatter()
        {
            var tokens = new List<TokenProbability>
            {
                T("{\"action\": \"move_to\", \"target\": \""),
                T("tower", -0.001, ("tower", -0.001)),
                T("\", \"statement\": \"To the caf"),
                T("\"}"),
            };
            string reply = "{\"action\": \"move_to\", \"target\": \"tower\", \"statement\": \"To the café\"}";

            Assert.That(TargetGrounding.Read(reply, tokens, Village).Status, Is.EqualTo(GroundingStatus.Scored));
        }

        [Test]
        public void ATargetMissingFromTheTokensIsUnreadable()
        {
            var noField = new List<TokenProbability> { T("{\"action\": \"none\", \"statement\": \"Hm.\"}") };
            var empty = Reply("none");

            Assert.That(TargetGrounding.Read(Join(noField), noField, Village).Status, Is.EqualTo(GroundingStatus.Unreadable));
            Assert.That(TargetGrounding.Read(Join(empty), empty, Village).Status, Is.EqualTo(GroundingStatus.Unreadable),
                "An empty value has no token.");
            Assert.That(TargetGrounding.Read(null, Reply("move_to", T("tower")), Village).Status, Is.EqualTo(GroundingStatus.Unreadable),
                "No reply text to check the join against.");
        }

        // Parity with the probe (DR-016). The numbers are the probe's own code run over the same
        // tokens: target_info in target_swap_probe.py locates the target, and signals() and
        // on_list_share in target_swap_score.py read it. Made with this, from the repository root,
        // as python -I parity.py:
        //
        //   import os, sys
        //   sys.path.insert(0, "tools/benchmarks"); sys.argv = ["parity", "--in=" + os.devnull]
        //   import target_swap_probe as probe, target_swap_score as score
        //
        //   def tok(text, lp=0.0, *alts):
        //       return {"token": text, "logprob": lp, "top_logprobs": [{"token": t, "logprob": l} for t, l in alts]}
        //
        //   def head(action):
        //       return [tok('{"'), tok("action"), tok('":'), tok(' "'), tok(action), tok('",'), tok(' "'), tok("target"), tok('":')]
        //
        //   TAIL = [tok(' "'), tok("statement"), tok('":'), tok(' "'), tok("On my way."), tok('"}')]
        //   CASES = {
        //       "scarecrow": ("village", head("attack_target") + [tok(' "'),
        //           tok("training", -2.95, ("sc", -0.06), ("str", -3.4), ("training", -2.95), ("the", -4.1), ("dummy", -4.8)),
        //           tok("_d", -0.02, ("_d", -0.02), ("_", -4.5)), tok("ummy", -0.001, ("ummy", -0.001)), tok('",')] + TAIL),
        //       "south tower": ("fort", head("move_to") + [
        //           tok(' "south', -0.03, (' "south', -0.03), (' "north', -4.0), (' "s', -5.2), (' "the', -6.1)),
        //           tok("_gate", -4.9, ("_tower", -0.01), ("_gate", -4.9), (" tower", -6.0)), tok('",')] + TAIL),
        //       "spear": ("village", head("pick_up_item") + [tok(' "'),
        //           tok("s", -0.01, ("s", -0.01), ("sp", -5.0), ("the", -6.5)),
        //           tok("word", -6.0, ("pear", -0.004), ("word", -6.0), ("hield", -7.0)), tok('",')] + TAIL),
        //       "open gate": ("fort", head("move_to") + [tok(' "'),
        //           tok("south", -0.6, ("south", -0.6), ("north", -0.84), ("east", -5.0), ("the", -6.0)),
        //           tok("_gate", -0.001, ("_gate", -0.001)), tok('",')] + TAIL),
        //       "tower": ("village", head("move_to") + [tok(' "'),
        //           tok("tow", -0.05, ("tow", -0.05), ("watch", -3.2), ("bridge", -4.4)),
        //           tok('er",', -0.01, ('er",', -0.01), ('"', -5.3), ("ers", -6.0))] + TAIL),
        //   }
        //   for name, (scene, lps) in CASES.items():
        //       p, top, value = probe.target_info(lps)
        //       vals = score.legal_values(scene)
        //       prefix, shares = "", []
        //       for k, (text, _, alts) in enumerate(value):
        //           shares.append(score.on_list_share(alts, prefix, vals, k == 0)); prefix += text
        //       path = score.signals({"scene": scene, "top": top, "p_target": p, "value": value})["path"]
        //       print(f"{name:<12} path {path:.6f}  lowest at {shares.index(path)}")
        //
        // which printed:
        //
        //   scarecrow    path 0.052340  lowest at 0
        //   south tower  path 0.007447  lowest at 1
        //   spear        path 0.002479  lowest at 1
        //   open gate    path 0.987260  lowest at 0
        //   tower        path 0.963507  lowest at 0
        [TestCase("scarecrow", 0.052340, 0)]
        [TestCase("south tower", 0.007447, 1)]
        [TestCase("spear", 0.002479, 1)]
        [TestCase("open gate", 0.987260, 0)]
        [TestCase("tower", 0.963507, 0)]
        public void ItMatchesTheProbeNumberForNumber(string name, double path, int lowestAt)
        {
            var (targets, tokens) = ParityCase(name);

            var reading = TargetGrounding.Read(Join(tokens), tokens, targets);

            Assert.That(reading.Status, Is.EqualTo(GroundingStatus.Scored));
            Assert.That(reading.Share, Is.EqualTo(path).Within(0.00005), "The same reading to four decimal places.");
            Assert.That(reading.LowestAt, Is.EqualTo(lowestAt));
        }

        // ---- Helpers ------------------------------------------------------------------------

        private static GroundingReading Read(IReadOnlyList<TokenProbability> tokens, string[] targets)
        {
            return TargetGrounding.Read(Join(tokens), tokens, targets);
        }

        // The probe's village and fort target lists, with no_target, as legal_values gives them.
        private static readonly string[] ProbeVillage = { "training_dummy", "tower", "bridge", "sword", "barrel", "no_target" };
        private static readonly string[] ProbeFort = { "north_gate", "south_gate", "east_tower", "west_tower", "shield", "helmet", "no_target" };

        // The parity snippet's CASES, token for token.
        private static (string[] Targets, List<TokenProbability> Tokens) ParityCase(string name)
        {
            switch (name)
            {
                case "scarecrow":
                    return (ProbeVillage, Parity("attack_target", T(" \""),
                        T("training", -2.95, ("sc", -0.06), ("str", -3.4), ("training", -2.95), ("the", -4.1), ("dummy", -4.8)),
                        T("_d", -0.02, ("_d", -0.02), ("_", -4.5)), T("ummy", -0.001, ("ummy", -0.001)), T("\",")));
                case "south tower":
                    return (ProbeFort, Parity("move_to",
                        T(" \"south", -0.03, (" \"south", -0.03), (" \"north", -4.0), (" \"s", -5.2), (" \"the", -6.1)),
                        T("_gate", -4.9, ("_tower", -0.01), ("_gate", -4.9), (" tower", -6.0)), T("\",")));
                case "spear":
                    return (ProbeVillage, Parity("pick_up_item", T(" \""),
                        T("s", -0.01, ("s", -0.01), ("sp", -5.0), ("the", -6.5)),
                        T("word", -6.0, ("pear", -0.004), ("word", -6.0), ("hield", -7.0)), T("\",")));
                case "open gate":
                    return (ProbeFort, Parity("move_to", T(" \""),
                        T("south", -0.6, ("south", -0.6), ("north", -0.84), ("east", -5.0), ("the", -6.0)),
                        T("_gate", -0.001, ("_gate", -0.001)), T("\",")));
                case "tower":
                    return (ProbeVillage, Parity("move_to", T(" \""),
                        T("tow", -0.05, ("tow", -0.05), ("watch", -3.2), ("bridge", -4.4)),
                        T("er\",", -0.01, ("er\",", -0.01), ("\"", -5.3), ("ers", -6.0))));
                default:
                    throw new System.ArgumentException(name);
            }
        }

        // head(action) + the target's tokens + TAIL, as the snippet builds a reply.
        private static List<TokenProbability> Parity(string action, params TokenProbability[] middle)
        {
            var tokens = new List<TokenProbability>
            {
                T("{\""), T("action"), T("\":"), T(" \""), T(action), T("\","), T(" \""), T("target"), T("\":"),
            };
            tokens.AddRange(middle);
            tokens.AddRange(new[] { T(" \""), T("statement"), T("\":"), T(" \""), T(Statement), T("\"}") });
            return tokens;
        }
    }
}
