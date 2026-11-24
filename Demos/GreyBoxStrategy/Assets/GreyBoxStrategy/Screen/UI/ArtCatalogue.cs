using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The game's pictures (#20): which face a ruler or a general wears and which picture an event shows, from the art
    /// pack shipped in <c>Screen/Art/Resources/ArtPack/</c> (its README has every picture's prompt and seed).
    /// <para>
    /// The choices are plain functions of the game's seed and the ids, with a hash that is the same on every machine,
    /// so a realm keeps its face for a whole game and a saved replay shows the same faces. Nothing here reaches a
    /// model: the pictures are for the screen only.
    /// </para>
    /// <para>A lookup that finds no picture returns null, and the screen draws the empty frame it drew before.</para>
    /// </summary>
    public static class ArtCatalogue
    {
        /// <summary>The pack's folder under a <c>Resources</c> folder.</summary>
        public const string Root = "ArtPack/";

        /// <summary>How many faces the pack holds for each persona: <c>warmonger-1</c> to <c>warmonger-3</c>.</summary>
        public const int FacesPerPersona = 3;

        /// <summary>The personas with faces, as <c>Agents/Personas.cs</c> names them.</summary>
        public static readonly IReadOnlyList<string> Personas = new[] { "warmonger", "builder", "opportunist", "merchant", "proud" };

        /// <summary>The neutral faces a player picks their own ruler from: <c>you-1</c> to <c>you-4</c>.</summary>
        public const int PlayerFaces = 4;

        /// <summary>The generals' faces: six men, then six women.</summary>
        public static readonly IReadOnlyList<string> GeneralFaces = new[]
        {
            "captain-m1", "captain-m2", "captain-m3", "captain-m4", "captain-m5", "captain-m6",
            "captain-w1", "captain-w2", "captain-w3", "captain-w4", "captain-w5", "captain-w6",
        };

        /// <summary>The generals' names (<c>ArmyNames.Generals</c>) that wear a woman's face; every other name wears a man's.</summary>
        public static readonly ISet<string> WomensNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "maren", "edda", "isolde", "kestrel", "lunet", "nessa", "sabine", "tamsin", "vesna", "bree",
            "dora", "fenna", "hester", "juno", "liesel", "nell", "petra", "roswitha", "rowan", "quill",
        };

        /// <summary>The events with a picture of their own, by the storyteller's template id (<c>Sim/Rules/Events.cs</c>).</summary>
        public static readonly IReadOnlyList<string> EventPictures = new[]
        {
            "windfall", "bad_harvest", "plague", "volunteers", "zeal", "unrest", "stronghold", "walls_crumble",
            "good_harvest", "bandits",
            "sellswords", "petitioners", "relic", "muster", "engineers", "fever_camps",
            "pretender", "pretender_rising", "deep_vein", "ration",
            "rich_vein", "flood", "pilgrims", "omen", "end_tale",
            "tale_begins", "long_winter",
        };

        /// <summary>Templates that show another template's picture.</summary>
        private static readonly Dictionary<string, string> EventAlias = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["poor_harvest"] = "bad_harvest",
            ["begin_pretender"] = "pretender",
            ["begin_deep_vein"] = "rich_vein",
            ["begin_fever"] = "plague",
            ["begin_long_winter"] = "long_winter",
        };

        // --- choosing: plain functions, no Unity -------------------------------------------------------------------

        /// <summary>FNV-1a over the text's UTF-16 units: the same number on every machine and run, unlike <see cref="string.GetHashCode()"/>.</summary>
        public static uint Hash(string text)
        {
            uint h = 2166136261;
            foreach (char c in text ?? "")
            {
                h ^= (byte)(c & 0xff); h *= 16777619;
                h ^= (byte)(c >> 8); h *= 16777619;
            }
            // A last stir, so texts that differ in their final character do not land on neighbouring numbers.
            h ^= h >> 15; h *= 2246822519; h ^= h >> 13;
            return h;
        }

        /// <summary>The personas' faces, three each.</summary>
        public static IReadOnlyList<string> PersonaFaces { get; } =
            Personas.SelectMany(p => Enumerable.Range(1, FacesPerPersona).Select(n => $"{p}-{n}")).ToList();

        /// <summary>Every ruler's face in the pack: the personas' first, then the player's.</summary>
        public static IReadOnlyList<string> AllRulerFaces { get; } =
            PersonaFaces.Concat(Enumerable.Range(1, PlayerFaces).Select(n => $"you-{n}")).ToList();

        /// <summary>
        /// The face of every realm in one game, never the same face twice while the pack has faces left
        /// (19, more than the largest world seats).
        /// <list type="bullet">
        /// <item>A realm a persona rules wears one of that persona's faces, begun at a place its id and the seed choose.</item>
        /// <item>Your own realm wears the neutral face you picked (<paramref name="yourFace"/>, 0 to 3).</item>
        /// <item>A realm with no persona wears a persona's face no other realm wears; the four neutral faces are yours alone.</item>
        /// </list>
        /// </summary>
        public static Dictionary<string, string> RulerFaces(int seed, IEnumerable<string> realms, Func<string, string> personaOf,
            string you = null, int yourFace = 0)
        {
            var faces = new Dictionary<string, string>(StringComparer.Ordinal);
            var taken = new HashSet<string>(StringComparer.Ordinal);
            var ids = (realms ?? Enumerable.Empty<string>()).Where(id => id != null).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToList();
            if (you != null && ids.Contains(you))
            {
                string mine = $"you-{((yourFace % PlayerFaces) + PlayerFaces) % PlayerFaces + 1}";
                faces[you] = mine; taken.Add(mine);
            }
            // Personas first, so a realm with no persona never takes a face a persona's realm would have worn.
            foreach (bool withPersona in new[] { true, false })
                foreach (string id in ids)
                {
                    if (faces.ContainsKey(id)) continue;
                    string persona = personaOf?.Invoke(id);
                    bool known = persona != null && Personas.Contains(persona);
                    if (known != withPersona) continue;
                    // A realm with no persona wears any persona's face: the four neutral ones stay yours, so the face
                    // you click to never moves a rival's.
                    var own = known ? Enumerable.Range(1, FacesPerPersona).Select(n => $"{persona}-{n}").ToList() : PersonaFaces;
                    string face = FirstFree(own, taken, seed, id) ?? FirstFree(AllRulerFaces, taken, seed, id)
                                  ?? own[(int)(Hash($"{seed}:{id}") % (uint)own.Count)];
                    faces[id] = face; taken.Add(face);
                }
            return faces;
        }

        private static string FirstFree(IReadOnlyList<string> pool, HashSet<string> taken, int seed, string id)
        {
            int start = (int)(Hash($"{seed}:{id}") % (uint)pool.Count);
            for (int k = 0; k < pool.Count; k++)
            {
                string f = pool[(start + k) % pool.Count];
                if (!taken.Contains(f)) return f;
            }
            return null;
        }

        /// <summary>
        /// A general's face from the name ("Maren", "Maren the Wary" or an army's id, <c>maren_host</c>): the same in
        /// every turn of a game, a woman's face for a woman's name. Null for no general.
        /// </summary>
        public static string GeneralFace(int seed, string general)
        {
            if (string.IsNullOrWhiteSpace(general)) return null;
            string name = new string(general.Trim().TakeWhile(char.IsLetter).ToArray()).ToLowerInvariant();
            if (name.Length == 0) return null;
            int half = GeneralFaces.Count / 2;
            int k = (int)(Hash($"{seed}:{name}") % (uint)half);
            return GeneralFaces[(WomensNames.Contains(name) ? half : 0) + k];
        }

        /// <summary>The picture id for a storyteller's event by its template id, or null when the pack has none for it.</summary>
        public static string EventPicture(string templateId)
        {
            if (string.IsNullOrEmpty(templateId)) return null;
            if (EventAlias.TryGetValue(templateId, out string alias)) return alias;
            if (EventPictures.Contains(templateId)) return templateId;
            return templateId.StartsWith("begin_", StringComparison.Ordinal) ? "tale_begins" : null;
        }

        // --- loading -----------------------------------------------------------------------------------------------

        private static readonly Dictionary<string, Texture2D> Loaded = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        private static Texture2D Load(string path)
        {
            if (Loaded.TryGetValue(path, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>(Root + path);
            if (t != null) Loaded[path] = t;
            return t;
        }

        /// <summary>
        /// A ruler's or a general's portrait by its face id (<c>warmonger-2</c>, <c>you-1</c>, <c>captain-w3</c>), or
        /// null. <paramref name="small"/> is the copy made for frames up to about 70 px high.
        /// </summary>
        public static Texture2D Portrait(string face, bool small = true) =>
            face == null ? null : Load((small ? "portraits-small/" : "portraits/") + face) ?? Load("portraits/" + face);

        /// <summary>The picture of a storyteller's event by its template id (<c>plague</c>, <c>begin_fever</c>), or null.</summary>
        public static Texture2D Event(string templateId) =>
            EventPicture(templateId) is string id ? Load("events/" + id) : null;
    }
}
