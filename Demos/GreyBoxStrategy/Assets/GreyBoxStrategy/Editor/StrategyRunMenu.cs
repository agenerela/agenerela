using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GreyBoxStrategy.Editor
{
    /// <summary>
    /// The test playground: plays whole games headless and writes them to
    /// <c>Logs/StrategyRuns/&lt;time&gt;-&lt;line-up&gt;/</c> in this project (git ignores <c>Logs/</c>).
    /// </summary>
    /// <remarks>
    /// <para>From the menu: <b>GreyBoxStrategy → Play 200 games</b>.</para>
    /// <para>From the Unity CLI, with the editor open on this project:</para>
    /// <code>
    /// unity command script eval -- --code 'return GreyBoxStrategy.Editor.StrategyRunMenu.Run("mixed", 200, 1);'
    /// </code>
    /// <para>In batch mode, with the project closed:</para>
    /// <code>
    /// Unity.exe -batchmode -nographics -projectPath Demos/GreyBoxStrategy -quit
    ///   -executeMethod GreyBoxStrategy.Editor.StrategyRunMenu.RunFromCommandLine
    ///   -strategyLineup mixed -strategyGames 200 -strategySeed 1
    /// </code>
    /// </remarks>
    public static class StrategyRunMenu
    {
        private const string Menu = "GreyBoxStrategy/";

        [MenuItem(Menu + "Play 200 games (mixed line-up)")]
        private static void PlayMixed() => Reveal(Run("mixed", 200, 1));

        [MenuItem(Menu + "Play 200 games (expansionist vs four random)")]
        private static void PlayExpansionistVsRandom() => Reveal(Run("expansionist-vs-random", 200, 1));

        [MenuItem(Menu + "Play 200 games (cautious vs four random)")]
        private static void PlayCautiousVsRandom() => Reveal(Run("cautious-vs-random", 200, 1));

        [MenuItem(Menu + "Play 20 games (mixed line-up)")]
        private static void PlayQuick() => Reveal(Run("mixed", 20, 1));

        /// <summary>The folder every run is written under.</summary>
        public static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "StrategyRuns"));

        /// <summary>
        /// Plays <paramref name="games"/> games of a named line-up from seed <paramref name="firstSeed"/>,
        /// writes the logs and summary, and returns the folder they are in. Line-ups are listed in
        /// <see cref="Lineups.Named"/>.
        /// </summary>
        public static string Run(string lineup, int games, int firstSeed)
        {
            string folder = StrategyRuns.NewFolder(Root, lineup);
            var started = DateTime.Now;
            var summary = StrategyRuns.Run(folder, lineup, games, firstSeed);
            Debug.Log($"GreyBoxStrategy: played {games} games ({lineup}) in {(DateTime.Now - started).TotalSeconds:0.0} s. " +
                      $"Summary: {Path.Combine(folder, "summary.md")}\n{summary.ToMarkdown()}");
            return folder;
        }

        /// <summary>For <c>-executeMethod</c>: reads <c>-strategyLineup</c>, <c>-strategyGames</c> and <c>-strategySeed</c>.</summary>
        public static void RunFromCommandLine()
        {
            string lineup = Arg("-strategyLineup") ?? "mixed";
            int games = int.TryParse(Arg("-strategyGames"), out int g) ? g : 200;
            int seed = int.TryParse(Arg("-strategySeed"), out int s) ? s : 1;
            try
            {
                Run(lineup, games, seed);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        private static void Reveal(string folder)
        {
            EditorUtility.RevealInFinder(Path.Combine(folder, "summary.md"));
        }
    }
}
