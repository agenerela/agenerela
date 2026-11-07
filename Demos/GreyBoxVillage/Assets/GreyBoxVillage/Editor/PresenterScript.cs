using System.Linq;
using Agenerela.Providers;

namespace Demos.GreyBoxVillage.Editor
{
    /// <summary>
    /// What each agent answers when the README's presenter script is played with no model: one
    /// plausible reply per moment, so the situations chain the way a good run would. The presenter
    /// script's test and the capture tool both answer from here, so the two never drift apart.
    /// </summary>
    /// <remarks>
    /// It reads only what a real model is sent, as <see cref="ScriptedProvider"/> hands it over: who
    /// is asked, the stimulus and its label, and the actions and targets the schema offers. A reply
    /// is never for an action that was not offered, so availability still decides what can happen,
    /// as it does live. It is the scripted provider's <see cref="ScriptedProvider.Callback"/>.
    /// </remarks>
    public static class PresenterScript
    {
        /// <summary>The player's lines in the script, in the order the README says them.</summary>
        public const string BramHello = "Hello. Anything to report?";
        public const string BramWalk = "Walk with me for a bit.";
        public const string BramStay = "Thanks, you can stay here now.";
        /// <summary>The fallback when Bram chases the wolf rather than ringing the bell.</summary>
        public const string RingTheBell = "Ring the bell, Bram! Warn the village!";
        public const string LetMeIn = "Wren, it's me. Let me in, please!";
        public const string ShutIt = "Thank you. Shut it behind me.";
        public const string BramAtNight = "Quiet night, Bram?";

        /// <summary>The scripted reply to one request.</summary>
        public static ScriptedReply Answer(ScriptedRequest request)
        {
            string who = request.Agent ?? "";
            string s = request.Stimulus ?? "";
            string label = request.Label ?? "";
            var actions = request.OfferedActions;
            var targets = request.OfferedTargets;
            bool Can(string action) => actions.Contains(action);

            // Bram at the gate, before anything happens: an escort, begun and ended by the player.
            if (s.Contains(BramWalk) && Can("follow_player"))
                return ScriptedReply.Decide("follow_player", "no_target", "Lead the way.");
            if (s.Contains(BramStay) && Can("stop_following"))
                return ScriptedReply.Decide("stop_following", "no_target", "I'll keep to the gate.");

            // The wolf at the gate: the guard who sees it close rings the bell, the captain sends a guard.
            if (label == "Event" && s.Contains("A wolf has come out") && Can("sound_alarm"))
                return ScriptedReply.Decide("sound_alarm", "no_target", "Wolf at the gate! Ring the bell!");
            if (who == "Hale" && s.Contains("alarm bell is ringing") && Can("send_guard"))
                return ScriptedReply.Decide("send_guard", "village_gate", "To the gate, and drive that wolf off!");
            // A guard sent after the wolf runs at it: Wren chases, Bram (who has no chase) attacks.
            if (label == "Captain" && targets.Contains("wolf") && (Can("chase") || Can("attack_target")))
                return ScriptedReply.Decide(Can("chase") ? "chase" : "attack_target", "wolf", "After it, before it gets in!");

            // The thief: Odile calls Wren, and whoever sees it running goes after it: Wren with
            // chase, Bram with attack_target, which runs after a running thief.
            if (who == "Odile" && Can("call_guard") && s.Contains("snatched"))
                return ScriptedReply.Decide("call_guard", "wren", "Wren! Stop that thief, he's got my bread!");
            if (targets.Contains("thief") && (Can("chase") || Can("attack_target")))
                return ScriptedReply.Decide(Can("chase") ? "chase" : "attack_target", "thief", "Stop, thief!");
            if (label == "Merchant" && s.Contains("thief") && Can("move_to"))
                return ScriptedReply.Decide("move_to", "eastern_road", "Coming, Odile! Which way did he go?");

            // The fire: Odile calls Wren, the only guard she is offered for it, and Wren fetches water.
            if (who == "Odile" && Can("call_guard") && s.Contains("on fire"))
                return ScriptedReply.Decide("call_guard", "wren", "Wren! Water, quick, the stall's alight!");
            if (Can("fetch_water"))
                return ScriptedReply.Decide("fetch_water", "no_target", "Buckets! Get water from the well!");

            // Night at the gate: Wren takes the watch, then lets the player in and shuts it again.
            if (s.Contains("taken the night watch"))
                return ScriptedReply.Decide("none", "no_target", "The gate's shut. I have the watch.");
            if (s.Contains(LetMeIn) && Can("open_gate"))
                return ScriptedReply.Decide("open_gate", "no_target", "In you come, quickly now.");
            if (s.Contains(ShutIt) && Can("close_gate"))
                return ScriptedReply.Decide("close_gate", "no_target", "Barred till morning.");

            return ScriptedReply.Decide("none", "no_target", who == "Odile" ? "Oh, my word." : "Understood.");
        }
    }
}
