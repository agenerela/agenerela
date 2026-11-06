using System.Linq;
using System.Text.RegularExpressions;
using Agenerela.Providers;

namespace Demos.GreyBoxVillage.Editor
{
    /// <summary>
    /// What each agent answers when the README's presenter script is played with no model: one
    /// plausible reply per moment, so the situations chain the way a good run would. The presenter
    /// script's test and the capture tool both answer from here, so the two never drift apart.
    /// </summary>
    /// <remarks>
    /// It reads only what a real model is sent: the system prompt (whose name it is), the stimulus
    /// and its label, and the actions and targets the schema offers. A reply is never for an action
    /// that was not offered, so availability still decides what can happen, as it does live.
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

        /// <summary>The scripted reply to one request, as the model's JSON.</summary>
        public static string Answer(DecisionRequest request)
        {
            string who = Regex.Match(request.SystemPrompt ?? "", @"You are (\w+)").Groups[1].Value;
            string s = request.Stimulus ?? "";
            var actions = Offered(request, "action");
            var targets = Offered(request, "target");
            bool Can(string action) => actions.Contains(action);

            // Bram at the gate, before anything happens: an escort, begun and ended by the player.
            if (s.Contains(BramWalk) && Can("follow_player"))
                return Decision("follow_player", "no_target", "Lead the way.");
            if (s.Contains(BramStay) && Can("stop_following"))
                return Decision("stop_following", "no_target", "I'll keep to the gate.");

            // The wolf at the gate: the guard who sees it close rings the bell, the captain sends a guard.
            if (s.StartsWith("Event:") && s.Contains("A wolf has come out") && Can("sound_alarm"))
                return Decision("sound_alarm", "no_target", "Wolf at the gate! Ring the bell!");
            if (who == "Hale" && s.Contains("alarm bell is ringing") && Can("send_guard"))
                return Decision("send_guard", "village_gate", "To the gate, and drive that wolf off!");
            // A guard sent after the wolf runs at it: Wren chases, Bram (who has no chase) attacks.
            if (s.StartsWith("Captain:") && targets.Contains("wolf") && (Can("chase") || Can("attack_target")))
                return Decision(Can("chase") ? "chase" : "attack_target", "wolf", "After it, before it gets in!");

            // The thief: Odile calls Wren, and whoever sees it running goes after it: Wren with
            // chase, Bram with attack_target, which runs after a running thief.
            if (who == "Odile" && Can("call_guard") && s.Contains("snatched"))
                return Decision("call_guard", "wren", "Wren! Stop that thief, he's got my bread!");
            if (targets.Contains("thief") && (Can("chase") || Can("attack_target")))
                return Decision(Can("chase") ? "chase" : "attack_target", "thief", "Stop, thief!");
            if (s.StartsWith("Merchant:") && s.Contains("thief") && Can("move_to"))
                return Decision("move_to", "eastern_road", "Coming, Odile! Which way did he go?");

            // The fire: Odile calls Wren, the only guard she is offered for it, and Wren fetches water.
            if (who == "Odile" && Can("call_guard") && s.Contains("on fire"))
                return Decision("call_guard", "wren", "Wren! Water, quick, the stall's alight!");
            if (Can("fetch_water"))
                return Decision("fetch_water", "no_target", "Buckets! Get water from the well!");

            // Night at the gate: Wren takes the watch, then lets the player in and shuts it again.
            if (s.Contains("taken the night watch"))
                return Decision("none", "no_target", "The gate's shut. I have the watch.");
            if (s.Contains(LetMeIn) && Can("open_gate"))
                return Decision("open_gate", "no_target", "In you come, quickly now.");
            if (s.Contains(ShutIt) && Can("close_gate"))
                return Decision("close_gate", "no_target", "Barred till morning.");

            return Decision("none", "no_target", who == "Odile" ? "Oh, my word." : "Understood.");
        }

        private static string[] Offered(DecisionRequest request, string field)
        {
            var f = request.Schema?.Fields.FirstOrDefault(x => x.Name == field);
            return f?.AllowedValues?.ToArray() ?? new string[0];
        }

        public static string Decision(string action, string target, string statement) =>
            "{\"action\":\"" + action + "\",\"target\":\"" + target + "\",\"statement\":\"" + statement + "\"}";
    }
}
