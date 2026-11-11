using System;
using System.IO;
using Agenerela;
using Agenerela.Providers;
using UnityEditor;
using UnityEngine;

namespace GreyBoxStrategy.Editor
{
    /// <summary>
    /// Stage 2's playground: one seat played by a model through a framework agent, against four
    /// scripted countries, written to <c>Logs/StrategyRuns/</c> as <see cref="StrategyRunMenu"/> does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The model is reached through Ollama's HTTP API at <c>AGENERELA_OLLAMA_URL</c> (default
    /// <c>http://127.0.0.1:11434</c>), with the key in <c>AGENERELA_OLLAMA_API_KEY</c> when the server
    /// wants one (the RTX 5080's gateway, <c>tools/ollama-server/</c>). Both are read from the
    /// environment, never from a file here, and the key goes in a header only.
    /// </para>
    /// <para>From the Unity CLI, with the editor open on this project (the run goes on in the background):</para>
    /// <code>
    /// unity command script eval -- --code 'return GreyBoxStrategy.Editor.StrategyModelRunMenu.Start("model", "qwen3.5:4b", "default", 10, 1);'
    /// </code>
    /// <para>In batch mode, with the project closed. No <c>-quit</c>: the editor exits when the run ends.</para>
    /// <code>
    /// Unity.exe -batchmode -nographics -projectPath Demos/GreyBoxStrategy
    ///   -executeMethod GreyBoxStrategy.Editor.StrategyModelRunMenu.RunFromCommandLine
    ///   -strategySeat model -strategyModel qwen3.5:4b -strategyArm default -strategyGames 10 -strategySeed 1
    /// </code>
    /// <para>
    /// <c>-strategySeat</c> is <c>model</c>, or a baseline kind (<c>expansionist</c>, <c>cautious</c>,
    /// <c>random</c>) in the same seat against the same four opponents and seeds. Arms are
    /// <see cref="ModelSeatOptions.Named"/>'s.
    /// </para>
    /// </remarks>
    public static class StrategyModelRunMenu
    {
        private const string Menu = "GreyBoxStrategy/";
        public const string DefaultModel = "qwen3.5:4b";

        [MenuItem(Menu + "Model seat: play 5 games (qwen3.5:4b vs scripted)")]
        private static void PlayFive() => Start(ModelGameRunner.Model, DefaultModel, "default", 5, 1);

        [MenuItem(Menu + "Model seat: play 1 game (qwen3.5:4b vs scripted)")]
        private static void PlayOne() => Start(ModelGameRunner.Model, DefaultModel, "default", 1, 1);

        /// <summary>Starts a run and returns its folder at once; the run logs its progress to the console.</summary>
        public static string Start(string seat, string model, string arm, int games, int firstSeed)
        {
            string folder = FolderFor(seat, model, arm);
            Run(seat, model, arm, games, firstSeed, folder, exitWhenDone: false);
            return folder;
        }

        /// <summary>For <c>-executeMethod</c>: reads the <c>-strategy*</c> arguments and exits the editor when done.</summary>
        public static void RunFromCommandLine()
        {
            string seat = Arg("-strategySeat") ?? ModelGameRunner.Model;
            string model = Arg("-strategyModel") ?? DefaultModel;
            string arm = Arg("-strategyArm") ?? "default";
            int games = int.TryParse(Arg("-strategyGames"), out int g) ? g : 10;
            int seed = int.TryParse(Arg("-strategySeed"), out int s) ? s : 1;
            string folder = Arg("-strategyOut") ?? FolderFor(seat, model, arm);
            Run(seat, model, arm, games, seed, folder, exitWhenDone: true);
        }

        private static string FolderFor(string seat, string model, string arm)
        {
            string name = seat == ModelGameRunner.Model ? $"{model.Replace(':', '-')}-{arm}" : $"seat-{seat}";
            return StrategyRuns.NewFolder(StrategyRunMenu.Root, name);
        }

        private static async void Run(string seat, string model, string arm, int games, int firstSeed, string folder, bool exitWhenDone)
        {
            int code = 0;
            try
            {
                var options = ModelSeatOptions.Named(arm) ?? throw new ArgumentException($"Unknown arm '{arm}'.");
                ILLMProvider provider = null;
                string kind = seat;
                if (seat == ModelGameRunner.Model)
                {
                    provider = new OllamaProvider(new OllamaSettings
                    {
                        Endpoint = Environment.GetEnvironmentVariable("AGENERELA_OLLAMA_URL") is string url && url.Length > 0 ? url : "http://127.0.0.1:11434",
                        ApiKey = Environment.GetEnvironmentVariable("AGENERELA_OLLAMA_API_KEY") ?? "",
                        Model = model,
                        TimeoutSeconds = 90f,
                    });
                    kind = $"{model} ({arm})";
                }

                Debug.Log($"GreyBoxStrategy: {games} games, seat {kind}, seeds from {firstSeed}, into {folder}");
                var (summary, report) = await StrategyModelRuns.RunAsync(folder, seat, provider, kind, options, games, firstSeed,
                    line => Debug.Log("GreyBoxStrategy: " + line));
                Debug.Log($"GreyBoxStrategy: done. {Path.Combine(folder, "summary.md")}\n{summary.ToMarkdown()}\n{report.ToMarkdown()}");
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
