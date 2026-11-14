using System.Collections.Generic;

namespace GreyBoxStrategy.Replay
{
    /// <summary>One note of the first-turn help: what it explains, and where on the screen it points.</summary>
    public sealed class HelpCallout
    {
        /// <summary>What it points at: <c>writs</c>, <c>army</c>, <c>hatching</c>, <c>edge</c>, <c>advisor</c> or <c>endturn</c>.</summary>
        public string Key;
        public string Title;
        public string Text;
    }

    /// <summary>
    /// The first-turn help for rule mode on the writs turn: no more than six notes, in the screen's own words, each
    /// pinned to what it explains. Plain C#, so what it says is tested without a screen.
    /// </summary>
    public static class HelpCallouts
    {
        public const int Max = 6;

        /// <param name="advisor">The advisor is on: its note replaces the edge markers' on a small world, where they rarely show.</param>
        /// <param name="bigWorld">A world large enough that battles and the capital go off screen, where the edge markers matter.</param>
        public static List<HelpCallout> ForRule(bool advisor, bool bigWorld)
        {
            var list = new List<HelpCallout>
            {
                new HelpCallout { Key = "writs", Title = "Writs: your orders a turn",
                    Text = "Each order your court gives, and each army you command, takes a writ. You get a few a turn, more as your realm grows; unused ones are lost." },
                new HelpCallout { Key = "army", Title = "Ordering an army",
                    Text = "Click an army awaiting orders, then a lit province on the map to march there, or Hold. N jumps to the next army that waits." },
                new HelpCallout { Key = "hatching", Title = "Stripes on the map",
                    Text = "Diagonal stripes in a realm's colour mark land it occupies. The land stays its owner's until a treaty hands it over." },
            };
            if (bigWorld) list.Add(new HelpCallout { Key = "edge", Title = "Markers at the edge",
                Text = "When a battle or your capital is off screen, a marker at the map's edge points to it. Click it to go there." });
            if (advisor) list.Add(new HelpCallout { Key = "advisor", Title = "Your advisor",
                Text = "A model on your own seat proposes the turn's orders. Take them all, take one, change any, or ignore them." });
            list.Add(new HelpCallout { Key = "endturn", Title = "End turn",
                Text = "Nothing happens until you end the turn. Then every ruler's orders land together, in the rules' order." });
            if (list.Count > Max) list.RemoveRange(Max, list.Count - Max);
            return list;
        }
    }
}
