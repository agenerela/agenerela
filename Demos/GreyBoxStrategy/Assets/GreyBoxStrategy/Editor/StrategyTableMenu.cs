using System;
using System.Collections.Generic;
using System.IO;
using Agenerela;
using Agenerela.Providers;
using UnityEditor;
using UnityEngine;

namespace GreyBoxStrategy.Editor
{
    /// <summary>
    /// Models at one table: any seat a model, a script or random (<see cref="Tables.Named"/>), each
    /// written to <c>Logs/StrategyRuns/</c> with its scorecard.
    /// </summary>
    /// <remarks>
    /// <para>The address and the key are read from the environment, as for the model seat (<see cref="StrategyModelRunMenu"/>).</para>
    /// <para>From the Unity CLI, with the editor open (the run goes on in the background):</para>
    /// <code>
    /// unity command script eval -- --code 'return GreyBoxStrategy.Editor.StrategyTableMenu.Start("personas", "qwen3.5:4b", null, "default", 5, 1);'
    /// </code>
    /// <para>In batch mode, with the project closed. No <c>-quit</c>: the editor exits when the run ends.</para>
    /// <code>
    /// Unity.exe -batchmode -nographics -projectPath Demos/GreyBoxStrategy
    ///   -executeMethod GreyBoxStrategy.Editor.StrategyTableMenu.RunFromCommandLine
    ///   -strategyTable personas -strategyModel qwen3.5:4b -strategyArm default -strategyGames 20 -strategySeed 1
    /// </code>
    /// <para>
    /// <c>-strategyModel2</c> is the second model of <c>two-models</c>; <c>-strategyPersonas on|off</c>
    /// overrides a table's own choice. The reference tables <c>scripted</c> and <c>random</c> need no model.
    /// </para>
    /// </remarks>
    public static class StrategyTableMenu
    {
        private const string Menu = "GreyBoxStrategy/";

        [MenuItem(Menu + "Table: score the two reference tables (scripted, random)")]
        private static void References()
        {
            Start("scripted", null, null, "default", 30, 1);
            Start("random", null, null, "default", 30, 1);
        }

        [MenuItem(Menu + "Table: five qwen3.5:4b seats with personas, 1 game")]
        private static void PersonasOne() => Start("personas", StrategyModelRunMenu.DefaultModel, null, "default", 1, 1);

        /// <summary>Starts a run and returns its folder at once; the run logs its progress to the console.</summary>
        public static string Start(string table, string model, string model2, string arm, int games, int firstSeed, string personas = null)
        {
            var definition = Define(table, model, model2, arm, personas);
            string folder = StrategyRuns.NewFolder(StrategyRunMenu.Root, Slug(table, model, model2, arm, personas));
            Run(definition, games, firstSeed, folder, exitWhenDone: false);
            return folder;
        }

        /// <summary>For <c>-executeMethod</c>: reads the <c>-strategy*</c> arguments and exits the editor when done.</summary>
        public static void RunFromCommandLine()
        {
            string table = Arg("-strategyTable") ?? "personas";
            string model = Arg("-strategyModel") ?? StrategyModelRunMenu.DefaultModel;
            string model2 = Arg("-strategyModel2");
            string arm = Arg("-strategyArm") ?? "default";
            string personas = Arg("-strategyPersonas");
            int games = int.TryParse(Arg("-strategyGames"), out int g) ? g : 20;
            int seed = int.TryParse(Arg("-strategySeed"), out int s) ? s : 1;
            TableDefinition definition;
            try
            {
                definition = Define(table, model, model2, arm, personas);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(2);
                return;
            }
            string folder = Arg("-strategyOut") ?? StrategyRuns.NewFolder(StrategyRunMenu.Root, Slug(table, model, model2, arm, personas));
            Run(definition, games, seed, folder, exitWhenDone: true);
        }

        private static TableDefinition Define(string table, string model, string model2, string arm, string personas)
        {
            bool? withPersonas = personas == "on" ? true : personas == "off" ? false : (bool?)null;
            return Tables.Named(table, model, model2, arm ?? "default", withPersonas)
                   ?? throw new ArgumentException($"Unknown table '{table}'.");
        }

        private static string Slug(string table, string model, string model2, string arm, string personas)
        {
            if (table == "scripted" || table == "random") return "table-" + table;
            string s = $"table-{table}-{model}{(model2 != null ? "-" + model2 : "")}-{arm}{(personas != null ? "-personas-" + personas : "")}";
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '-');
            return s.Replace(':', '-').Replace(',', '_');
        }

        private static async void Run(TableDefinition table, int games, int firstSeed, string folder, bool exitWhenDone)
        {
            int code = 0;
            try
            {
                var providers = new Dictionary<string, ILLMProvider>(StringComparer.Ordinal);
                ILLMProvider ProviderFor(string model, World world)
                {
                    if (!providers.TryGetValue(model, out var p))
                        providers[model] = p = new OllamaProvider(new OllamaSettings
                        {
                            Endpoint = Environment.GetEnvironmentVariable("AGENERELA_OLLAMA_URL") is string url && url.Length > 0 ? url : "http://127.0.0.1:11434",
                            ApiKey = Environment.GetEnvironmentVariable("AGENERELA_OLLAMA_API_KEY") ?? "",
                            Model = model,
                            TimeoutSeconds = 90f,
                        });
                    return p;
                }

                Debug.Log($"GreyBoxStrategy: table {table.Name}, {games} games, seeds from {firstSeed}, into {folder}");
                var scorecard = await StrategyTableRuns.RunAsync(folder, table, ProviderFor, games, firstSeed,
                    line => Debug.Log("GreyBoxStrategy: " + line));
                Debug.Log($"GreyBoxStrategy: done. {Path.Combine(folder, "scorecard.md")}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            if (exitWhenDone) EditorApplication.Exit(code);
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
