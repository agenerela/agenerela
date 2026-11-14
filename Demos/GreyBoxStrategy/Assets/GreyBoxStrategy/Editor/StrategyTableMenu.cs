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

        /// <summary>
        /// For <c>-executeMethod</c>: several arms of one table on the same seeds, interleaved in blocks
        /// (<see cref="StrategyTableRuns.RunInterleavedAsync"/>). <c>-strategyArms</c> lists the arms and
        /// <c>-strategyArmNames</c> their folder names, both separated by <c>;</c>; <c>-strategyBlock</c>
        /// is the block size (5), <c>-strategyIndex</c> the series index of the first seed (0). Writes each arm to <c>-strategyOut/&lt;name&gt;</c> and exits when done.
        /// </summary>
        /// <example>
        /// <code>
        /// -executeMethod GreyBoxStrategy.Editor.StrategyTableMenu.RunInterleavedFromCommandLine -strategyTable personas
        ///   -strategyModel qwen3.5:4b -strategyArms "default;default+storyteller" -strategyArmNames "control;storyteller"
        ///   -strategyGames 20 -strategySeed 1 -strategyBlock 5 -strategyOut runs/ab
        /// </code>
        /// </example>
        public static void RunInterleavedFromCommandLine()
        {
            string table = Arg("-strategyTable") ?? "personas";
            string model = Arg("-strategyModel") ?? StrategyModelRunMenu.DefaultModel;
            string model2 = Arg("-strategyModel2");
            string personas = Arg("-strategyPersonas");
            string[] arms = (Arg("-strategyArms") ?? "default").Split(';');
            string[] names = (Arg("-strategyArmNames") ?? string.Join(";", arms)).Split(';');
            int games = int.TryParse(Arg("-strategyGames"), out int g) ? g : 20;
            int seed = int.TryParse(Arg("-strategySeed"), out int s0) ? s0 : 1;
            int block = int.TryParse(Arg("-strategyBlock"), out int b) ? b : 5;
            // The series index of the first seed, when this run is one block of a longer series (seats rotate by it).
            int index = int.TryParse(Arg("-strategyIndex"), out int i0) ? i0 : 0;
            // Rules v2 (R1c): -strategyTurn writs plays the writs turn, with -strategyCap (headless turn cap,
            // default 40), -strategyMap (classic or medium) and -strategyVictory (conquest or domination_75).
            var setup = WritsSetup();
            var defined = new List<(string, TableDefinition)>();
            try
            {
                if (names.Length != arms.Length) throw new ArgumentException("-strategyArmNames needs one name per arm.");
                for (int i = 0; i < arms.Length; i++)
                {
                    var t = Define(table, model, model2, arms[i], personas);
                    t.Setup = setup;
                    defined.Add((names[i], t));
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(2);
                return;
            }
            string folder = Arg("-strategyOut") ?? StrategyRuns.NewFolder(StrategyRunMenu.Root, "interleaved-" + table);
            RunInterleaved(defined, games, seed, block, folder, index);
        }

        private static async void RunInterleaved(List<(string, TableDefinition)> arms, int games, int firstSeed, int block, string folder, int index = 0)
        {
            int code = 0;
            try
            {
                Debug.Log($"GreyBoxStrategy: {arms.Count} arms interleaved in blocks of {block}, {games} games, seeds from {firstSeed}, into {folder}");
                await StrategyTableRuns.RunInterleavedAsync(folder, arms, ProviderCache(), games, firstSeed, block,
                    line => Debug.Log("GreyBoxStrategy: " + line), default, index);
                Debug.Log($"GreyBoxStrategy: done. {folder}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        private static GameSetup WritsSetup()
        {
            if (Arg("-strategyTurn") != "writs") return null;
            string map = Arg("-strategyMap") ?? "classic";
            var setup = GameSetup.Writs(1, map == "medium" ? GameSetup.GeneratedMapId : GameSetup.ClassicMapId, Arg("-strategyVictory") ?? GameSetup.ConquestPreset);
            if (map == "medium")
            {
                setup.Countries = 5; // the tables seat five
                setup.Generator = new MapSettings { Size = MapSize.Medium };
                setup.MapSeed = int.TryParse(Arg("-strategyMapSeed"), out int ms) ? ms : 1;
            }
            int cap = int.TryParse(Arg("-strategyCap"), out int c) ? c : 40;
            setup.TurnCap = cap;
            if (setup.Victory != VictoryMode.Conquest) setup.TurnLimit = cap;
            // -strategyRules "OccupationBecomesOwnership=8;BreakWhenAllOccupied=true": rule settings by field name.
            if (Arg("-strategyRules") is string settings) setup.Numbers = RulesWith(settings);
            return setup;
        }

        /// <summary>Today's rules with the named fields set, from "Field=value;Field=value"; an unknown field throws.</summary>
        public static GameRules RulesWith(string settings)
        {
            var rules = GameRules.Standard;
            foreach (string part in settings.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) throw new ArgumentException($"-strategyRules: \"{part}\" is not Field=value.");
                string name = part.Substring(0, eq).Trim();
                var field = typeof(GameRules).GetField(name) ?? throw new ArgumentException($"-strategyRules: GameRules has no field {name}.");
                field.SetValue(rules, Convert.ChangeType(part.Substring(eq + 1).Trim(), field.FieldType, System.Globalization.CultureInfo.InvariantCulture));
            }
            return rules;
        }

        // One provider per model for the whole run, so every seat and the storyteller share its queue.
        private static Func<string, World, ILLMProvider> ProviderCache()
        {
            var providers = new Dictionary<string, ILLMProvider>(StringComparer.Ordinal);
            return (model, world) =>
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
            };
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
