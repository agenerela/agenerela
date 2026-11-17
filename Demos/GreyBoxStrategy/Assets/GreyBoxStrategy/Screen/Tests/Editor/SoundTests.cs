using System.IO;
using System.Linq;
using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using NUnit.Framework;
using UnityEngine;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// The first sounds (#20): one table of cues, four buses with their volumes kept in the settings, levels that never
    /// startle, every named file present, and no sound and no audio device in a headless run.
    /// </summary>
    public class SoundTests
    {
        private static readonly string SoundsFolder = Path.Combine(Application.dataPath, "GreyBoxStrategy", "Screen", "Audio", "Resources", "Sounds");

        [Test]
        public void Every_cue_has_one_name_a_bus_and_a_level_and_every_file_it_names_is_there()
        {
            var names = SoundCues.All.Select(c => c.Name).ToList();
            Assert.That(names, Is.Unique);
            Assert.That(names.Count, Is.GreaterThanOrEqualTo(20), "the whole first list is named, made or not");
            foreach (var c in SoundCues.All)
            {
                Assert.That(c.Volume, Is.InRange(0.05f, 1f), c.Name);
                Assert.That(c.When, Is.Not.Empty, c.Name);
                if (c.File != null) Assert.That(File.Exists(Path.Combine(SoundsFolder, c.File + ".ogg")), Is.True, $"{c.Name} names {c.File}.ogg");
            }
            foreach (string made in new[] { SoundCues.Click, SoundCues.Hover, SoundCues.PanelOpen, SoundCues.PanelClose, SoundCues.EndTurn, SoundCues.TurnResolves })
                Assert.That(SoundCues.Named(made).File, Is.Not.Null, made + " has its sound");
            Assert.That(SoundCues.Named("thunder"), Is.Null);
            Assert.That(SoundCues.All.Where(c => c.Loop).Select(c => c.Bus), Is.All.EqualTo(SoundBus.Music), "loops are the music bus's");
        }

        [Test]
        public void Nothing_startles_the_interface_sits_below_the_effects_and_a_hover_is_barely_there()
        {
            var interfaceMax = SoundCues.All.Where(c => c.Bus == SoundBus.Interface).Max(c => c.Volume);
            var effectsMax = SoundCues.All.Where(c => c.Bus == SoundBus.Effects).Max(c => c.Volume);
            Assert.That(interfaceMax, Is.LessThanOrEqualTo(0.5f));
            Assert.That(effectsMax, Is.LessThanOrEqualTo(0.65f), "effects stay well under full level");
            Assert.That(SoundCues.Named(SoundCues.Hover).Volume, Is.LessThanOrEqualTo(0.2f));
            Assert.That(SoundCues.Named(SoundCues.Hover).MinGap, Is.GreaterThan(0), "a hover swept over a list plays once");
            Assert.That(new GameSettings().InterfaceVolume, Is.LessThan(new GameSettings().EffectsVolume));
        }

        [Test]
        public void A_cue_plays_at_its_level_times_its_bus_times_the_master_and_each_bus_has_its_own_volume()
        {
            var s = new GameSettings { MasterVolume = 0.5f, MusicVolume = 0.2f, EffectsVolume = 0.8f, InterfaceVolume = 0.4f, VoiceVolume = 1f };
            var click = SoundCues.Named(SoundCues.Click);
            Assert.That(SoundCues.Gain(click, s), Is.EqualTo(click.Volume * 0.4f * 0.5f).Within(1e-5));
            var turn = SoundCues.Named(SoundCues.EndTurn);
            Assert.That(SoundCues.Gain(turn, s), Is.EqualTo(turn.Volume * 0.8f * 0.5f).Within(1e-5));
            Assert.That(SoundCues.BusVolume(SoundBus.Music, s), Is.EqualTo(0.2f));
            Assert.That(SoundCues.BusVolume(SoundBus.Voice, s), Is.EqualTo(1f), "the voice bus, for the text-to-speech work");
            s.MasterVolume = 0;
            Assert.That(SoundCues.Gain(turn, s), Is.EqualTo(0), "the master at 0 silences everything");
            Assert.That(SoundCues.Gain(null, s), Is.EqualTo(0));
        }

        [Test]
        public void The_interface_volume_is_kept_with_the_settings_and_brought_into_range()
        {
            string folder = Path.Combine(Application.temporaryCachePath, "sound-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                string path = Path.Combine(folder, "settings.json");
                new SettingsStore(path).Save(new GameSettings { InterfaceVolume = 0.25f });
                Assert.That(new SettingsStore(path).Load().InterfaceVolume, Is.EqualTo(0.25f));
                File.WriteAllText(path, "{ \"InterfaceVolume\": 3.0 }");
                Assert.That(new SettingsStore(path).Load().InterfaceVolume, Is.EqualTo(1f));
            }
            finally { try { Directory.Delete(folder, true); } catch (IOException) { } }
        }

        [Test]
        public void A_headless_run_makes_no_sound_and_needs_no_audio_device()
        {
            SoundManager.Apply(new GameSettings());
            Assert.That(SoundManager.Active, Is.False, "no manager outside Play mode or in batch mode");
            Assert.DoesNotThrow(() => SoundManager.Play(SoundCues.Click));
            Assert.DoesNotThrow(() => SoundManager.Play("no such cue"));
            Assert.DoesNotThrow(() => SoundManager.Music(SoundCues.MenuAmbience));
            Assert.That(Object.FindAnyObjectByType<SoundManager>(), Is.Null);
        }
    }
}
