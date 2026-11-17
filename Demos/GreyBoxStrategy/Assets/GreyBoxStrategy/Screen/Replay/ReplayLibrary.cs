using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GreyBoxStrategy.Replay
{
    /// <summary>
    /// One finished game kept for "Load a replay": what the list shows (when, how long, who won), and what
    /// plays it again. A game the rules' scripted rulers played is remade from its <see cref="Setup"/>; any
    /// other carries its <see cref="Recording"/>, the decisions as they were made.
    /// </summary>
    public sealed class SavedReplay
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        /// <summary>When the game ended, in UTC.</summary>
        public DateTime Saved;
        /// <summary>"Kesh wins by conquest, the last realm standing", in a player's words.</summary>
        public string Title;
        public int Turns;
        /// <summary>The winner's name as the screen showed it, or null when nobody won.</summary>
        public string Winner;
        /// <summary>"You ruled Kesh", "Watched, live" or "Scripted rulers".</summary>
        public string Played;
        /// <summary>"Classic board" or "Medium continent, seed 101".</summary>
        public string World;
        /// <summary>The game's setup as <see cref="GameSetup.ToJson"/> writes it; null for the Classic board's one-move game.</summary>
        public string Setup;
        /// <summary>New game's four army rules, as the game was played.</summary>
        public bool UnitTypes, Generals, Sieges, Supply;
        /// <summary>The decisions as <see cref="GreyBoxStrategy.Replay.Recording.ToJson"/> writes them; null for a scripted game, remade from its setup.</summary>
        public string Recording;

        /// <summary>The file it was read from; null until saved. Never written into the file.</summary>
        [JsonIgnore] public string Path;
        /// <summary>True for the recording that ships with the game: listed last, never deleted.</summary>
        [JsonIgnore] public bool Bundled;

        /// <summary>The list's second line: "16 Nov 2026, 21:40 · 63 turns · Kesh won".</summary>
        public string Line(Func<DateTime, DateTime> toLocal = null)
        {
            var parts = new List<string>();
            if (Bundled) parts.Add("Ships with the game");
            else parts.Add((toLocal ?? (d => d.ToLocalTime()))(Saved).ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture));
            parts.Add(Turns == 1 ? "1 turn" : $"{Turns} turns");
            parts.Add(Winner != null ? $"{Winner} won" : "nobody won");
            return string.Join(" · ", parts);
        }
    }

    /// <summary>
    /// The saved replays: one JSON file a game in a folder (<c>Replays</c> in the game's data folder). Reading never
    /// throws: a file that is not a replay, or half written, is left out of the list.
    /// </summary>
    public sealed class ReplayLibrary
    {
        /// <summary>The newest games kept; older ones are deleted when a new one is saved.</summary>
        public const int Keep = 40;

        public ReplayLibrary(string folder) { Folder = folder; }

        public string Folder { get; }

        /// <summary>Every saved replay, newest first.</summary>
        public List<SavedReplay> List()
        {
            var found = new List<SavedReplay>();
            if (!Directory.Exists(Folder)) return found;
            foreach (string path in Directory.GetFiles(Folder, "*.json"))
            {
                var r = Read(path, headerOnly: true);
                if (r != null) found.Add(r);
            }
            return found.OrderByDescending(r => r.Saved).ThenBy(r => r.Path, StringComparer.Ordinal).ToList();
        }

        /// <summary>One saved replay whole, with its recording; null when the file is gone or not a replay.</summary>
        public SavedReplay Load(string path) => Read(path, headerOnly: false);

        private static SavedReplay Read(string path, bool headerOnly)
        {
            try
            {
                var o = JObject.Parse(File.ReadAllText(path));
                if ((int?)o["Version"] != SavedReplay.CurrentVersion || o["Title"] == null) return null;
                // The list needs no recording, and a long game's is megabytes.
                if (headerOnly) o.Remove("Recording");
                var r = o.ToObject<SavedReplay>();
                if (r == null) return null;
                r.Saved = DateTime.SpecifyKind(r.Saved, DateTimeKind.Utc);
                r.Path = path;
                return r;
            }
            catch (Exception e) when (e is IOException || e is JsonException || e is UnauthorizedAccessException || e is ArgumentException || e is FormatException)
            {
                return null;
            }
        }

        /// <summary>
        /// Saves a finished game and returns its path, or null when the folder cannot be written (the game goes on
        /// either way). Keeps the newest <see cref="Keep"/>.
        /// </summary>
        public string Save(SavedReplay replay)
        {
            if (replay == null) throw new ArgumentNullException(nameof(replay));
            try
            {
                Directory.CreateDirectory(Folder);
                string name = $"game-{replay.Saved:yyyyMMdd-HHmmss}";
                string path = System.IO.Path.Combine(Folder, name + ".json");
                for (int n = 2; File.Exists(path); n++) path = System.IO.Path.Combine(Folder, $"{name}-{n}.json");
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(replay, Formatting.Indented));
                File.Move(tmp, path);
                replay.Path = path;
                foreach (var old in List().Skip(Keep)) File.Delete(old.Path);
                return path;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
