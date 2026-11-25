using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The world's lore (#20): at New game, behind the loading card and before anything reads a name, the loremaster
    /// (<see cref="WorldLoremaster"/>, a framework agent) names the world, its realms and their rulers and tells its
    /// history in one decision; <see cref="LoreRules"/> keeps what passes its checks, and the rest stays as the seeded
    /// tables made it. Kept per world seed and model beside the court's pictures, so the same world reads the same.
    /// Escape skips it.
    /// </summary>
    public sealed partial class ReplayDirector
    {
        /// <summary>How long New game waits for the loremaster before the world keeps its table names.</summary>
        public const float LoreBudgetSeconds = 45f;

        /// <summary>The lore the world just made took, or null: no model, or nothing kept.</summary>
        private WorldLore worldLore;

        /// <summary>For the Unity CLI and the log: what the last world's lore came to.</summary>
        public static string LastLoreReport { get; private set; } = "no world named yet";

        /// <summary>The persona of each seat as a rule game seats them (<c>TableRunner.PersonaFor</c>): none on yours.</summary>
        private static List<string> SeatPersonas(int countries, int you) =>
            Enumerable.Range(0, countries).Select(i => i == you ? null : Personas.All[i % 5].Id).ToList();

        private IEnumerator NameWorld(LoadingCard loading, GameSetup setup, string model, IReadOnlyList<string> personas)
        {
            worldLore = null;
            if (world == null) yield break;
            int worldSeed = setup.MapSeed ?? setup.Seed;
            string worldKey = LoreRules.WorldKey(world);
            // A saved game carries its world's lore, so Load a replay names the world as it was played, with no model.
            bool saved = setup.Lore != null && setup.LoreFor == worldKey;
            if (!saved) { setup.Lore = null; setup.LoreFor = null; }
            if (!saved && (string.IsNullOrWhiteSpace(model) || model == "scripted"))
            {
                LastLoreReport = "no lore: no model rules this game";
                yield break;
            }
            var cache = new WorldLoreCache(RulerArtServer.CacheFolder(worldSeed));
            string key = saved ? null : WorldLoreCache.Key(world, model);
            var proposal = saved ? setup.Lore : cache.Read(key);
            string from = saved ? "the saved game" : "the cache", why = null;
            float seconds = 0;
            if (proposal == null)
            {
                why = Gateway.WhyNotSet() ?? (DevFaults.ServerDown ? "the server is down (dev)" : null);
                if (why == null)
                {
                    from = "the model";
                    var master = new WorldLoremaster(world, Gateway.Provider(model), personas);
                    float start = Time.realtimeSinceStartup;
                    using (var skip = new CancellationTokenSource())
                    {
                        skip.CancelAfter(TimeSpan.FromSeconds(LoreBudgetSeconds));
                        Task<LoreProposal> asking = master.AskAsync(worldSeed, skip.Token);
                        bool pressed = false;
                        while (!asking.IsCompleted)
                        {
                            var kb = Keyboard.current;
                            if (!skip.IsCancellationRequested && kb != null && kb.escapeKey.wasPressedThisFrame) { pressed = true; skip.Cancel(); }
                            loading.Set(skip.IsCancellationRequested ? "Skipping: the world keeps the names on the old maps..." : "The loremaster names the world and its realms... Esc: skip",
                                0.6f + 0.05f * Mathf.Clamp01((Time.realtimeSinceStartup - start) / 10f));
                            yield return null;
                        }
                        seconds = Time.realtimeSinceStartup - start;
                        if (asking.IsFaulted) Debug.LogWarning("Painted Realm: the loremaster failed: " + asking.Exception.GetBaseException().Message);
                        proposal = asking.IsFaulted ? null : asking.Result;
                        if (proposal == null)
                            why = pressed ? "skipped" : skip.IsCancellationRequested ? $"no answer in {LoreBudgetSeconds:0} s"
                                : master.LastResult?.Verdict != null ? "refused: " + master.LastResult.Verdict.Value.Reason
                                : master.LastResult?.Telemetry?.ErrorKind != null ? "error: " + master.LastResult.Telemetry.ErrorKind : "the model chose not to";
                    }
                    if (proposal != null) cache.Write(key, proposal);
                }
            }
            if (proposal == null)
            {
                LastLoreReport = "no lore: " + why;
                Debug.Log("Painted Realm: the world keeps its table names (" + why + ")");
                yield break;
            }
            setup.Lore = proposal;
            setup.LoreFor = worldKey;
            var lore = LoreRules.Check(world, proposal);
            LoreRules.Apply(world, lore);
            worldLore = lore;
            loading.Tell(lore.WorldName, lore.Backstory);
            LastLoreReport = $"from {from}{(seconds > 0 ? $" in {seconds:0.0} s" : "")}: {lore.Summary(world.Countries.Count)}";
            Debug.Log($"Painted Realm: world lore for seed {worldSeed}, {LastLoreReport} (cache {cache.Folder})");
        }
    }

    /// <summary>
    /// The loremaster's proposals kept on disk, as written, one file per world, model and prompt version: the checks
    /// run again on every read, so a stricter check never trusts an old answer.
    /// </summary>
    public sealed class WorldLoreCache
    {
        public WorldLoreCache(string folder) => Folder = folder;

        public string Folder { get; }

        /// <summary>
        /// The key for <paramref name="world"/> as the generator made it (its table ids, before any rename) and
        /// <paramref name="model"/>: "v1-qwen3.5-4b-1a2b3c4d".
        /// </summary>
        public static string Key(WorldMap world, string model)
        {
            string safe = new string((model ?? "none").Select(c => char.IsLetterOrDigit(c) || c == '.' ? c : '-').ToArray());
            return $"v{WorldLoremaster.Version}-{safe}-{LoreRules.WorldKey(world)}";
        }

        private string PathOf(string key) => Path.Combine(Folder, "lore-" + key + ".json");

        public LoreProposal Read(string key)
        {
            try
            {
                string path = PathOf(key);
                return File.Exists(path) ? Newtonsoft.Json.JsonConvert.DeserializeObject<LoreProposal>(File.ReadAllText(path)) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Painted Realm: the world's lore cache could not be read: " + e.Message);
                return null;
            }
        }

        public void Write(string key, LoreProposal proposal)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(PathOf(key), Newtonsoft.Json.JsonConvert.SerializeObject(proposal, Newtonsoft.Json.Formatting.Indented));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Painted Realm: the world's lore could not be kept: " + e.Message);
            }
        }
    }
}
