using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GreyBoxStrategy.Editor
{
    /// <summary>
    /// Writes generated worlds to <c>Logs/Worlds/&lt;time&gt;/</c> as JSON, one per size and land shape,
    /// with a <c>summary.md</c> of how each was made, for <c>ArtSource/world_sheet.py</c> to draw as a
    /// contact sheet (rules v2, slice R2).
    /// </summary>
    /// <remarks>
    /// <para>From the menu: <b>GreyBoxStrategy → Export 20 generated worlds</b>.</para>
    /// <para>In batch mode, with the project closed:</para>
    /// <code>
    /// Unity.exe -batchmode -nographics -projectPath Demos/GreyBoxStrategy -quit
    ///   -executeMethod GreyBoxStrategy.Editor.MapExportMenu.ExportFromCommandLine -worldsFolder &lt;folder&gt; -worldsSeed 100
    /// python ArtSource/world_sheet.py &lt;folder&gt; sheet.png
    /// </code>
    /// </remarks>
    public static class MapExportMenu
    {
        [MenuItem("GreyBoxStrategy/Export 20 generated worlds")]
        private static void ExportMenu()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Worlds", DateTime.Now.ToString("yyyyMMdd-HHmmss")));
            Export(folder, 100);
            EditorUtility.RevealInFinder(Path.Combine(folder, "summary.md"));
        }

        /// <summary>One world per size and shape, seeds <paramref name="firstSeed"/> on; returns the summary.</summary>
        public static string Export(string folder, int firstSeed)
        {
            Directory.CreateDirectory(folder);
            var sb = new StringBuilder();
            sb.AppendLine("# Generated worlds");
            sb.AppendLine();
            sb.AppendLine("| File | Size | Shape | Seed | Countries | Passed | Tries | ms | Neutral | Reach (min–max) | Value (min–max) | Specials |");
            sb.AppendLine("|---|---|---|---:|---:|---|---:|---:|---:|---|---|---:|");
            var inv = CultureInfo.InvariantCulture;
            int k = 0;
            foreach (MapSize size in Enum.GetValues(typeof(MapSize)))
            foreach (LandShape shape in Enum.GetValues(typeof(LandShape)))
            {
                int seed = firstSeed + k;
                int countries = MapSettings.DefaultCountries(size);
                var watch = Stopwatch.StartNew();
                var made = MapGenerator.Generate(seed, countries, new MapSettings { Size = size, Shape = shape });
                watch.Stop();
                string file = $"world-{k:D2}-{size}-{shape}-{seed}.json".ToLowerInvariant();
                if (made.Map != null) File.WriteAllText(Path.Combine(folder, file), made.Map.ToJson());
                var f = made.Fairness;
                string Range(Func<CountryFairness, double> v) => f.Count == 0 ? "" : $"{f.Min(v).ToString("0.#", inv)}–{f.Max(v).ToString("0.#", inv)}";
                sb.AppendLine($"| {file} | {size} | {shape} | {seed} | {countries} | {(made.Passed ? "yes" : "no: " + string.Join("; ", made.Problems.Take(2)))} | " +
                              $"{made.Attempts} | {watch.ElapsedMilliseconds} | {made.Map?.Generated.NeutralPercent}% | {Range(x => x.Reach)} | {Range(x => x.Value)} | {made.Map?.Specials.Count} |");
                k++;
            }
            string summary = sb.ToString();
            File.WriteAllText(Path.Combine(folder, "summary.md"), summary);
            Debug.Log($"GreyBoxStrategy: exported {k} worlds to {folder}\n{summary}");
            return summary;
        }

        /// <summary>For <c>-executeMethod</c>: reads <c>-worldsFolder</c> and <c>-worldsSeed</c>.</summary>
        public static void ExportFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            string Arg(string name)
            {
                for (int i = 0; i + 1 < args.Length; i++)
                    if (args[i] == name) return args[i + 1];
                return null;
            }
            try
            {
                Export(Arg("-worldsFolder") ?? Path.Combine(Application.dataPath, "..", "Logs", "Worlds", "cli"),
                    int.TryParse(Arg("-worldsSeed"), out int s) ? s : 100);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }
    }
}
