using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agenerela;
using Agenerela.Providers;
using UnityEditor;
using UnityEngine;

namespace GreyBoxStrategy.Realm.Editor
{
    /// <summary>
    /// The live check of free orders (the turn rework, slice 1, part 3): rule mode played headless against model rulers,
    /// a stand-in player who plans for a set number of seconds each turn and then gives a plain plan (a recruit when one
    /// is offered, a march for every army that can march). It records, turn by turn, how many rulers were still
    /// deciding when the player pressed End turn, how long End turn waited and how much of that was treaty terms, and the
    /// rulers' calls and model seconds, read from the session's own decision log.
    /// </summary>
    /// <remarks>
    /// <c>Unity -batchmode -executeMethod GreyBoxStrategy.Realm.Editor.FreeOrdersLiveCheck.RunFromCommandLine
    /// -planSeconds 20 -turns 12 -strategySeed 1 -strategyOut &lt;folder&gt; -strategyArmNames &lt;name&gt;</c>. It reads
    /// <c>AGENERELA_OLLAMA_URL</c> and <c>AGENERELA_OLLAMA_API_KEY</c> from the environment. It writes
    /// <c>&lt;folder&gt;/&lt;name&gt;/turns.csv</c> and <c>summary.md</c>.
    /// <para>Turn 1 (the wait a game opens with): <c>-coldStart 1</c> unloads the model first, as the end of the last rule
    /// game leaves it; <c>-warm 1</c> then loads it through the framework's <c>IModelLifecycle.WarmUpAsync</c> before the
    /// game begins, as New game does; <c>-headStartSeconds S</c> begins the session S seconds before the player sees turn
    /// 1's council, as a generated world's rule game begins behind the loading card. Today's game is
    /// <c>-coldStart 1</c> alone.</para>
    /// </remarks>
    public static class FreeOrdersLiveCheck
    {
        public const string Model = "qwen3.5:4b";

        public static void RunFromCommandLine()
        {
            double plan = double.TryParse(Arg("-planSeconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out double p) ? p : 0;
            int turns = int.TryParse(Arg("-turns"), out int t) ? t : 12;
            int seed = int.TryParse(Arg("-strategySeed"), out int s) ? s : 1;
            string name = Arg("-strategyArmNames") ?? $"plan{plan:0}";
            string folder = Path.Combine(Arg("-strategyOut") ?? Path.Combine(Application.temporaryCachePath, "free-orders-live"), name);
            var start = new Start
            {
                Cold = Arg("-coldStart") == "1",
                Warm = Arg("-warm") == "1",
                HeadStart = double.TryParse(Arg("-headStartSeconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out double h) ? h : 0,
            };
            Run(plan, turns, seed, folder, start);
        }

        /// <summary>How the game is begun: the model unloaded first, warmed before, the session's head start on turn 1.</summary>
        public sealed class Start
        {
            public bool Cold, Warm;
            public double HeadStart;
            public override string ToString() => $"cold {(Cold ? 1 : 0)}, warm {(Warm ? 1 : 0)}, head start {HeadStart.ToString("0.##", CultureInfo.InvariantCulture)} s";
        }

        private static async void Run(double planSeconds, int turns, int seed, string folder, Start start)
        {
            int code = 0;
            try
            {
                Directory.CreateDirectory(folder);
                await Play(planSeconds, turns, seed, folder, start ?? new Start());
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            EditorApplication.Exit(code);
        }

        private static async Task Play(double planSeconds, int turns, int seed, string folder, Start start)
        {
            var setup = GameSetup.Writs(seed);
            var map = setup.BuildMap();
            var rules = setup.BuildRules(map);
            rules.FreeOrders = true;
            string you = map.Countries[0].Id;
            var providers = new Dictionary<string, ILLMProvider>(StringComparer.Ordinal);
            ILLMProvider ProviderFor(string model, World w)
            {
                if (!providers.TryGetValue(model, out var pr))
                    providers[model] = pr = new OllamaProvider(ThinkSwitch.Apply(new OllamaSettings
                    {
                        Endpoint = Environment.GetEnvironmentVariable("AGENERELA_OLLAMA_URL") is string url && url.Length > 0 ? url : "http://127.0.0.1:11434",
                        ApiKey = Environment.GetEnvironmentVariable("AGENERELA_OLLAMA_API_KEY") ?? "",
                        Model = model,
                        TimeoutSeconds = 90f,
                    }));
                return pr;
            }
            // Before the game: the model as the last game left it (unloaded) or as New game leaves it (warmed).
            var lifecycle = (IModelLifecycle)ProviderFor(Model, null);
            double unloadS = 0, warmS = 0;
            using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(120)))
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                if (start.Cold && !await lifecycle.UnloadAsync(cts.Token)) Debug.LogWarning("GreyBoxStrategy free-orders check: the server still listed the model after unloading it");
                unloadS = watch.Elapsed.TotalSeconds; watch.Restart();
                if (start.Warm) await lifecycle.WarmUpAsync(cts.Token);
                warmS = watch.Elapsed.TotalSeconds;
            }
            var session = new RuleSession(you, Model, "default", seed, Path.Combine(folder, "decisions.jsonl"), ProviderFor, null, map, rules) { AdvisorOn = false };
            var csv = new StringBuilder("turn,plan_s,deciding_at_open,deciding_at_end_turn,end_turn_wait_s,treaty_wait_s,calls,model_s,courts,armies,envoys,your_court_orders,errors,head_start_s,warm_s\n");
            Debug.Log($"GreyBoxStrategy free-orders check: you are {you}, seed {seed}, {turns} turns, planning {planSeconds} s a turn, {start} (unloaded in {unloadS:0.00} s, warmed in {warmS:0.00} s), into {folder}");
            var began = DateTime.UtcNow;
            var run = session.RunAsync();
            int seen = 0;
            for (int turn = 1; turn <= turns && !session.Finished; turn++)
            {
                await Until(() => session.WaitingFor == RuleSession.Waiting.Orders && session.Orders != null || session.Finished || session.Error != null, 600, $"turn {turn}'s council", session);
                if (session.Finished) break;
                // Turn 1 begun early: the player sees the council only once the head start has passed.
                double headStart = 0;
                if (turn == 1 && start.HeadStart > 0)
                {
                    double left = start.HeadStart - (DateTime.UtcNow - began).TotalSeconds;
                    if (left > 0) await Task.Delay(TimeSpan.FromSeconds(left));
                    headStart = (DateTime.UtcNow - began).TotalSeconds;
                }
                int atOpen = session.RulersDeciding;
                if (planSeconds > 0) await Task.Delay(TimeSpan.FromSeconds(planSeconds));
                int atEnd = session.RulersDeciding;
                var court = session.Orders;
                var plan = new RuleSession.WritsPlan();
                var recruit = court.Legal.FirstOrDefault(m => m.Action == Actions.Recruit || Actions.RecruitTypes.Contains(m.Action));
                if (recruit != null) plan.Courts.Add(recruit);
                foreach (var a in court.World.ArmiesOf(you).Where(a => !a.Marching).OrderBy(a => a.Id))
                    if (WritLegality.ArmyMoves(court.World, you, a.Id).FirstOrDefault(m => m.Action == Actions.March) is Move march) plan.Armies[a.Id] = march;
                session.Plan(plan);
                // Treaty terms put to you are refused at once: the wait they add is the rulers', not a person's.
                await Until(() =>
                {
                    if (session.WaitingFor == RuleSession.Waiting.Envoy) session.Answer(false);
                    return session.Resolved >= turn || session.Finished || session.Error != null;
                }, 600, $"turn {turn} to resolve", session);

                // The rulers' calls this turn, from the session's log: every Decided record of a model seat.
                var lines = session.Lines().Where(l => l.Contains("\"stage\":\"Decided\"")).ToList();
                var mine = lines.Skip(seen).ToList();
                seen = lines.Count;
                double modelSeconds = mine.Sum(l => Number(l, "\"latencySeconds\":"));
                int courts = mine.Count(l => l.Contains("\"label\":\"Court\""));
                int armies = mine.Count(l => l.Contains("\"label\":\"Army\""));
                int envoys = mine.Count(l => l.Contains("\"label\":\"Treaty\""));
                // A call that failed (the server unreachable, a timeout): the turn is not a measure of the model.
                int errors = mine.Count(l => l.Contains("\"error\":{"));
                var report = session.Game.Reports.Last();
                int yours = report.Outcomes.Count(o => o.Request != null && o.Request.CountryId == you && o.Request.Moment == Moment.Council && o.Choice.Action != Actions.None);
                csv.Append(string.Join(",", turn, planSeconds.ToString(CultureInfo.InvariantCulture), atOpen, atEnd,
                    session.EndTurnWaitSeconds.ToString("0.000", CultureInfo.InvariantCulture), session.TreatyWaitSeconds.ToString("0.000", CultureInfo.InvariantCulture),
                    mine.Count, modelSeconds.ToString("0.000", CultureInfo.InvariantCulture), courts, armies, envoys, yours, errors,
                    headStart.ToString("0.000", CultureInfo.InvariantCulture), (turn == 1 ? warmS : 0).ToString("0.000", CultureInfo.InvariantCulture))).Append('\n');
                File.WriteAllText(Path.Combine(folder, "turns.csv"), csv.ToString());
                Debug.Log($"GreyBoxStrategy free-orders check: turn {turn}: deciding {atOpen} at open, {atEnd} at End turn; waited {session.EndTurnWaitSeconds:0.00} s ({session.TreatyWaitSeconds:0.00} treaties); {mine.Count} calls, {modelSeconds:0.0} model s");
            }
            File.WriteAllText(Path.Combine(folder, "summary.md"), $"# free orders, planning {planSeconds} s, seed {seed}\n\n{start}; unloaded in {unloadS:0.00} s, warmed in {warmS:0.00} s\n\n{session.Resolved} turns resolved; error: {session.Error ?? "none"}\n");
            session.Stop();
            try { await run; } catch (OperationCanceledException) { }
            if (session.Error != null) throw new InvalidOperationException(session.Error);
        }

        private static double Number(string line, string key)
        {
            int i = line.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return 0;
            i += key.Length;
            int j = i;
            while (j < line.Length && (char.IsDigit(line[j]) || line[j] == '.' || line[j] == '-' || line[j] == 'E' || line[j] == 'e')) j++;
            return double.TryParse(line.Substring(i, j - i), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0;
        }

        private static async Task Until(Func<bool> condition, double seconds, string what, RuleSession session)
        {
            var until = DateTime.UtcNow.AddSeconds(seconds);
            while (!condition())
            {
                if (session.Error != null) throw new InvalidOperationException(session.Error);
                if (DateTime.UtcNow > until) throw new TimeoutException("timed out waiting for " + what);
                await Task.Delay(50);
            }
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
