using System;
using System.Collections.Generic;
using System.Linq;

namespace GreyBoxStrategy.Replay
{
    /// <summary>Where a sound plays, each with its own volume in the settings.</summary>
    public enum SoundBus
    {
        /// <summary>The ambient loops.</summary>
        Music,
        /// <summary>What happens in the game: marches, battles, sieges, treaties.</summary>
        Effects,
        /// <summary>The menus and panels: clicks, hovers, windows opening and closing.</summary>
        Interface,
        /// <summary>Spoken lines (the text-to-speech work): rulers off by default, the storyteller on.</summary>
        Voice,
    }

    /// <summary>One sound the game can play, by name: which file, which bus, how loud.</summary>
    public sealed class SoundCue
    {
        public string Name;
        public SoundBus Bus;
        /// <summary>The clip under <c>Resources/Sounds/</c>, without its extension; null while the cue has no sound yet (it plays nothing).</summary>
        public string File;
        /// <summary>The clip's own level, 0 to 1, before the bus and the master: how loud it sits beside the others.</summary>
        public float Volume = 0.6f;
        /// <summary>A random change of pitch each time, up or down by this much (0.04 is 4%), so a repeated click never sounds stamped.</summary>
        public float PitchJitter;
        /// <summary>The fewest seconds between two plays of this cue: a hover swept over a list plays once, not ten times.</summary>
        public float MinGap = 0.05f;
        public bool Loop;
        /// <summary>Where the game plays it, in words, for the sound test and the README.</summary>
        public string When;
    }

    /// <summary>
    /// Every sound in the game in one table, so a sound is changed here and nowhere else: swap a file, move a level,
    /// and no code that plays it changes. A cue with no file yet is listed and silent.
    /// </summary>
    public static class SoundCues
    {
        public const string Click = "ui_click", Hover = "ui_hover", PanelOpen = "panel_open", PanelClose = "panel_close";
        public const string EndTurn = "end_turn", TurnResolves = "turn_resolves";
        public const string SelectArmy = "select_army", March = "march", Battle = "battle", SiegeBegins = "siege_begins";
        public const string Captured = "province_captured", WarDeclared = "war_declared", PeaceSigned = "peace_signed";
        public const string StoryEvent = "story_event", Gold = "gold_gained", Refused = "refused", Victory = "victory", Defeat = "defeat";
        public const string MenuAmbience = "ambient_menu", MapAmbience = "ambient_map";

        public static readonly IReadOnlyList<SoundCue> All = new[]
        {
            // The first set (PR 2a): the menus, the panels and the turn.
            new SoundCue { Name = Click, Bus = SoundBus.Interface, File = "ui_click", Volume = 0.45f, PitchJitter = 0.04f, When = "a button pressed" },
            new SoundCue { Name = Hover, Bus = SoundBus.Interface, File = "ui_hover", Volume = 0.18f, PitchJitter = 0.06f, MinGap = 0.08f, When = "the mark moves onto a menu button" },
            new SoundCue { Name = PanelOpen, Bus = SoundBus.Interface, File = "panel_open", Volume = 0.4f, PitchJitter = 0.03f, MinGap = 0.15f, When = "a menu window or a side panel opens" },
            new SoundCue { Name = PanelClose, Bus = SoundBus.Interface, File = "panel_close", Volume = 0.35f, PitchJitter = 0.03f, MinGap = 0.15f, When = "the pause menu is closed" },
            new SoundCue { Name = EndTurn, Bus = SoundBus.Effects, File = "end_turn", Volume = 0.55f, When = "you end your turn" },
            new SoundCue { Name = TurnResolves, Bus = SoundBus.Effects, File = "turn_resolves", Volume = 0.45f, When = "your turn plays out" },
            // Named now so other sessions can hook them; their sounds come in the next set (PR 2b) and are silent until then.
            new SoundCue { Name = SelectArmy, Bus = SoundBus.Effects, Volume = 0.4f, When = "an army chosen" },
            new SoundCue { Name = March, Bus = SoundBus.Effects, Volume = 0.45f, When = "an army ordered to march" },
            new SoundCue { Name = Battle, Bus = SoundBus.Effects, Volume = 0.5f, When = "a battle" },
            new SoundCue { Name = SiegeBegins, Bus = SoundBus.Effects, Volume = 0.45f, When = "a siege begins" },
            new SoundCue { Name = Captured, Bus = SoundBus.Effects, Volume = 0.5f, When = "a province changes hands" },
            new SoundCue { Name = WarDeclared, Bus = SoundBus.Effects, Volume = 0.55f, When = "war declared" },
            new SoundCue { Name = PeaceSigned, Bus = SoundBus.Effects, Volume = 0.5f, When = "peace signed" },
            new SoundCue { Name = StoryEvent, Bus = SoundBus.Effects, Volume = 0.45f, When = "a storyteller's event window opens" },
            new SoundCue { Name = Gold, Bus = SoundBus.Effects, Volume = 0.4f, When = "gold gained" },
            new SoundCue { Name = Refused, Bus = SoundBus.Interface, Volume = 0.35f, When = "an order the rules refuse" },
            new SoundCue { Name = Victory, Bus = SoundBus.Effects, Volume = 0.6f, When = "the end screen, you won" },
            new SoundCue { Name = Defeat, Bus = SoundBus.Effects, Volume = 0.55f, When = "the end screen, you lost" },
            new SoundCue { Name = MenuAmbience, Bus = SoundBus.Music, Volume = 0.5f, Loop = true, When = "behind the start menu" },
            new SoundCue { Name = MapAmbience, Bus = SoundBus.Music, Volume = 0.4f, Loop = true, When = "behind a game" },
        };

        private static readonly Dictionary<string, SoundCue> ByName = All.ToDictionary(c => c.Name, StringComparer.Ordinal);

        /// <summary>The cue by its name, or null.</summary>
        public static SoundCue Named(string name) => name != null && ByName.TryGetValue(name, out var c) ? c : null;

        /// <summary>How loud a cue plays: its own level, its bus's volume and the master volume, multiplied.</summary>
        public static float Gain(SoundCue cue, GameSettings s)
        {
            if (cue == null || s == null) return 0;
            return Clamp01(cue.Volume) * Clamp01(BusVolume(cue.Bus, s)) * Clamp01(s.MasterVolume);
        }

        public static float BusVolume(SoundBus bus, GameSettings s)
        {
            switch (bus)
            {
                case SoundBus.Music: return s.MusicVolume;
                case SoundBus.Effects: return s.EffectsVolume;
                case SoundBus.Interface: return s.InterfaceVolume;
                case SoundBus.Voice: return s.VoiceVolume;
                default: return 0;
            }
        }

        private static float Clamp01(float v) => float.IsNaN(v) ? 0 : Math.Max(0, Math.Min(1, v));
    }
}
