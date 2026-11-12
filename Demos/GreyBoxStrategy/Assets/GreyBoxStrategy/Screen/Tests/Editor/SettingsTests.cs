using System;
using System.IO;
using GreyBoxStrategy.Replay;
using NUnit.Framework;
using UnityEngine;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>The settings file: kept between runs, and never able to break the game however it was edited.</summary>
    public class SettingsTests
    {
        private string folder, path;

        [SetUp] public void SetUp() { folder = Path.Combine(Application.temporaryCachePath, "settings-" + Guid.NewGuid().ToString("N")); path = Path.Combine(folder, "settings.json"); }
        [TearDown] public void TearDown() { try { Directory.Delete(folder, true); } catch (IOException) { } }

        [Test]
        public void Settings_survive_a_restart()
        {
            var s = new GameSettings { Display = DisplayMode.Windowed, Width = 2560, Height = 1440, UiScale = 1.5f, VSync = false, FrameCap = 144, PlaybackSpeed = 4, MusicVolume = 0.25f };
            new SettingsStore(path).Save(s);
            var back = new SettingsStore(path).Load();
            Assert.That(back.Display, Is.EqualTo(DisplayMode.Windowed));
            Assert.That((back.Width, back.Height), Is.EqualTo((2560, 1440)));
            Assert.That(back.UiScale, Is.EqualTo(1.5f));
            Assert.That(back.VSync, Is.False);
            Assert.That(back.FrameCap, Is.EqualTo(144));
            Assert.That(back.PlaybackSpeed, Is.EqualTo(4f));
            Assert.That(back.MusicVolume, Is.EqualTo(0.25f));
            Assert.That(File.ReadAllText(path), Does.Contain("\"Windowed\""), "readable by a person: modes by name");

            s.FrameCap = 60;
            new SettingsStore(path).Save(s); // over an existing file
            Assert.That(new SettingsStore(path).Load().FrameCap, Is.EqualTo(60));
            Assert.That(File.Exists(path + ".tmp"), Is.False, "no temporary file left behind");
        }

        [Test]
        public void No_file_or_a_broken_one_gives_the_defaults()
        {
            var defaults = new GameSettings();
            Assert.That(new SettingsStore(path).Load().Display, Is.EqualTo(defaults.Display), "no file yet");
            Directory.CreateDirectory(folder);
            File.WriteAllText(path, "{ \"Display\": \"Windowed\", \"Width\": ");
            var s = new SettingsStore(path).Load();
            Assert.That(s.Display, Is.EqualTo(defaults.Display), "half a file");
            Assert.That(s.UiScale, Is.EqualTo(0f));
        }

        [Test]
        public void Values_out_of_range_are_brought_back_into_it()
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(path, "{ \"Display\": 7, \"Width\": 12, \"Height\": 99999, \"UiScale\": 9.0, \"FrameCap\": 61, \"PlaybackSpeed\": 3.1, \"MasterVolume\": 4.0, \"VoiceVolume\": -1 }");
            var s = new SettingsStore(path).Load();
            Assert.That(s.Display, Is.EqualTo(DisplayMode.Borderless), "an unknown mode");
            Assert.That((s.Width, s.Height), Is.EqualTo((0, 0)), "a size no display has means the display's own");
            Assert.That(s.UiScale, Is.EqualTo(2f), "the nearest step");
            Assert.That(s.FrameCap, Is.EqualTo(60));
            Assert.That(s.PlaybackSpeed, Is.EqualTo(4f));
            Assert.That(s.MasterVolume, Is.EqualTo(1f));
            Assert.That(s.VoiceVolume, Is.EqualTo(0f));
            Assert.That(s.MusicVolume, Is.EqualTo(new GameSettings().MusicVolume), "a missing value keeps its default");
        }

        [Test]
        public void Only_a_change_of_mode_or_resolution_needs_the_countdown()
        {
            var a = new GameSettings { Width = 1920, Height = 1080 };
            var b = a.Clone(); b.UiScale = 1.5f; b.MusicVolume = 0.1f;
            Assert.That(b.DisplayDiffers(a), Is.False);
            b.Display = DisplayMode.Windowed;
            Assert.That(b.DisplayDiffers(a), Is.True);
            var c = a.Clone(); c.Width = 2560;
            Assert.That(c.DisplayDiffers(a), Is.True);
        }
    }
}
