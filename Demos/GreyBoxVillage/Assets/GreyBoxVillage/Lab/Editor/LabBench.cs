using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Agenerela;
using Agenerela.Providers;
using Demos.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Demos.GreyBoxVillage.Lab
{
    /// <summary>
    /// The in-game scenario bench (lab phase L2): plays Lab/scenarios.json in the village, in Play
    /// mode, against two or more variants (Lab/variants.json), and writes what every agent was asked,
    /// sent and answered, scored against each scenario's acceptable answers. Lab/README.md says how to
    /// run it and read its report.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Unity -batchmode -projectPath Demos/GreyBoxVillage -executeMethod
    /// Demos.GreyBoxVillage.Lab.LabBench.RunFromCommandLine -labVariants baseline,baseline -labOut DIR</c>,
    /// with no <c>-quit</c>: it enters Play mode, plays every scenario, and exits the editor itself,
    /// with code 1 if anything failed. Take the Ollama lock first (LAB.md).
    /// </para>
    /// <para>
    /// Each scenario, for each arm: the arm's model is the only one resident, warmed up outside the
    /// measurement (hard rule 3); F1; the variant; the setup; then each turn is previewed
    /// (<see cref="Agent.Preview"/>), asked through the agent's own mind as the game asks it, and
    /// recorded. The village's own events are muted (<see cref="VillageCommands.EventsMuted"/>), so a
    /// bell, a call or an order asks nobody else, and the next F1 puts back whatever the decision did.
    /// </para>
    /// </remarks>
    public static partial class LabBench
    {
        /// <summary>One side of a comparison: a variant, on a provider config, under a name.</summary>
        public sealed class Arm
        {
            public string Name;
            public LabVariant Variant;
            public ProviderConfig Config;
            /// <summary>The Ollama model, for residency; null for a scripted provider.</summary>
            public string Model;
            public int ContextSize;
        }

        public sealed class Settings
        {
            public List<LabScenario> Scenarios = new List<LabScenario>();
            public List<Arm> Arms = new List<Arm>();
            public int Repeats = 1;
            /// <summary>Each scenario in every arm before the next scenario; false runs one arm's whole list after another.</summary>
            public bool Interleaved = true;
            public int Seed = 1;
            /// <summary>Where the report goes; null writes nothing (tests).</summary>
            public string Out;
            /// <summary>The Ollama server, for residency, warm-up and the run's conditions; null for scripted arms.</summary>
            public LabServer Server;
            /// <summary>Throwaway decisions per model switch, outside the measurement.</summary>
            public int WarmUpDecisions = 2;
            public float TimeScale = 1f;
            /// <summary>Real seconds a turn may take before it is recorded as an error.</summary>
            public float TurnTimeoutSeconds = 90f;
            /// <summary>Press F1 once while a request is with the model, and write down what became of it.</summary>
            public bool ResetProbe;
        }

        /// <summary>What a run found, for the report and the tests.</summary>
        public sealed class Run
        {
            public readonly List<LabTurnResult> Results = new List<LabTurnResult>();
            public readonly List<string> Notes = new List<string>();
            /// <summary>The village as it stood after each F1, before its setup; every one should be the first.</summary>
            public readonly List<string> Snapshots = new List<string>();
            public readonly List<string> Leaks = new List<string>();
            public readonly LabRunInfo Info = new LabRunInfo();
            public int Failures;
            public bool Done;
        }

        /// <summary>
        /// Where a run's decision log goes in <paramref name="folder"/>, refusing a folder that holds a
        /// run already: the log appends, and the feed's sequence numbers start again each Play session,
        /// so two runs in one file could not be told apart (Codex, L2).
        /// </summary>
        public static string DecisionsPath(string folder)
        {
            string path = Path.Combine(folder, "decisions.jsonl");
            if (File.Exists(path) || File.Exists(DecisionLog.RolloverPath(path)))
                throw new InvalidOperationException(folder + " already holds a run. Give -labOut a new folder.");
            return path;
        }

        /// <summary>Throwaway lines for the warm-up; a test keeps them off every scenario (hard rule 4).</summary>
        public static readonly string[] WarmUpLines = { "Morning to you.", "Long day ahead, I reckon.", "Mind how you go." };

        /// <summary>The scenario and turn being asked now, for a scripted provider's callback.</summary>
        public static LabScenario CurrentScenario { get; private set; }
        public static LabTurn CurrentTurn { get; private set; }
        public static Arm CurrentArm { get; private set; }

        // ---- The command line ---------------------------------------------------------------------

        public static void RunFromCommandLine()
        {
            var scenarios = LabFiles.LoadScenarios();
            var variants = LabFiles.LoadVariants();
            var problems = LabFiles.Check(scenarios).Concat(LabFiles.Check(variants)).ToList();
            if (problems.Count > 0)
            {
                Debug.LogError("[LabBench] the bench's data is wrong:\n" + string.Join("\n", problems));
                EditorApplication.Exit(1);
                return;
            }

            // "scripted": every arm answers from LabScripted.Oracle, with no model, a dry run of the machinery.
            bool scripted = Arg("-labServer") == "scripted";
            var settings = new Settings
            {
                Repeats = int.Parse(Arg("-labRepeats") ?? "1", CultureInfo.InvariantCulture),
                Interleaved = (Arg("-labOrder") ?? "interleaved") != "sequential",
                Seed = int.Parse(Arg("-labSeed") ?? "1", CultureInfo.InvariantCulture),
                Out = Path.GetFullPath(Arg("-labOut") ?? Path.Combine("Logs", "lab", DateTime.Now.ToString("yyyyMMdd-HHmmss"))),
                Server = scripted ? null : LabServer.Parse(Arg("-labServer")),
                ResetProbe = Environment.GetCommandLineArgs().Contains("-labResetProbe"),
            };
            if (scripted) settings.WarmUpDecisions = 0;
            settings.Scenarios = Filter(scenarios.scenarios, Arg("-labScenarios"));
            var game = AssetDatabase.LoadAssetAtPath<OllamaProviderConfig>("Assets/GreyBoxVillage/Agents/VillageOllama.asset");
            settings.Arms = MakeArms((Arg("-labVariants") ?? "baseline,baseline").Split(','), variants, game, settings.Server ?? LabServer.Local);
            if (scripted)
                foreach (var arm in settings.Arms)
                {
                    var config = ScriptableObject.CreateInstance<ScriptedProviderConfig>();
                    config.name = "Lab oracle " + arm.Name;
                    config.hideFlags = HideFlags.HideAndDontSave;
                    config.Scripted.Callback = LabScripted.Oracle;
                    arm.Config = config;
                    arm.Model = null;
                    arm.ContextSize = 0;
                }
            Start(settings, exit: true);
        }

        /// <summary>
        /// The scenarios a filter picks: empty for all; otherwise comma-separated terms, each an id,
        /// a tag (agent:Bram, kind:bait, difficulty:hard), or first:N, which keeps the first N of
        /// what the others picked, spread over the four agents.
        /// </summary>
        public static List<LabScenario> Filter(List<LabScenario> all, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return all.ToList();
            var terms = filter.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
            var picks = terms.Where(t => !t.StartsWith("first:")).ToList();
            var chosen = picks.Count == 0 ? all.ToList() : all.Where(s => picks.Any(t => t == s.id || s.Tags().Contains(t))).ToList();
            var first = terms.FirstOrDefault(t => t.StartsWith("first:"));
            if (first == null) return chosen;
            int n = int.Parse(first.Substring(6), CultureInfo.InvariantCulture);
            // Round-robin over the agents, and within each over the kinds, so a small slice still asks
            // all four agents every kind of thing.
            var byAgent = chosen.GroupBy(s => s.agent).Select(g => new Queue<LabScenario>(
                g.GroupBy(s => s.kind).SelectMany(k => k.Select((s, i) => (s, i))).OrderBy(x => x.i)
                 .ThenBy(x => Array.IndexOf(LabFiles.Kinds, x.s.kind)).Select(x => x.s))).ToList();
            var slice = new List<LabScenario>();
            while (slice.Count < n && byAgent.Any(q => q.Count > 0))
                foreach (var q in byAgent)
                    if (q.Count > 0 && slice.Count < n) slice.Add(q.Dequeue());
            return slice;
        }

        /// <summary>The arms for these variant names; a name given twice is a second arm of it, "name~2", for the noise run.</summary>
        public static List<Arm> MakeArms(IEnumerable<string> names, LabVariantFile variants, OllamaProviderConfig game, LabServer server)
        {
            var arms = new List<Arm>();
            foreach (var raw in names.Select(n => n.Trim()).Where(n => n.Length > 0))
            {
                var variant = variants.variants.FirstOrDefault(v => v.name == raw)
                    ?? throw new ArgumentException("No variant named " + raw + " in " + LabFiles.VariantsPath);
                int seen = arms.Count(a => a.Variant.name == raw);
                var config = LabTuning.ConfigFor(variant, game, server);
                arms.Add(new Arm
                {
                    Name = seen == 0 ? raw : raw + "~" + (seen + 1),
                    Variant = variant,
                    Config = config,
                    Model = config.Model,
                    ContextSize = config.ContextSize,
                });
            }
            if (arms.Count == 0) throw new ArgumentException("-labVariants names no variant.");
            return arms;
        }

        private static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        // ---- Play mode ------------------------------------------------------------------------------

        private static IEnumerator running;
        private static bool exitWhenDone;
        private static bool savedOptionsEnabled;
        private static EnterPlayModeOptions savedOptions;
        private static Run current;

        /// <summary>Opens the village, enters Play mode and plays the bench there; with <paramref name="exit"/>, quits the editor after.</summary>
        public static Run Start(Settings settings, bool exit)
        {
            if (running != null) throw new InvalidOperationException("The bench is already running.");
            EditorSceneManager.OpenScene(DemoLauncher.VillagePath, OpenSceneMode.Single);
            savedOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            savedOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            exitWhenDone = exit;
            current = new Run();
            running = Wrapped(settings, current);
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
            return current;
        }

        private static IEnumerator Wrapped(Settings settings, Run run)
        {
            while (!EditorApplication.isPlaying) yield return null;
            for (int i = 0; i < 10; i++) yield return null;
            var work = Play(settings, run);
            while (true)
            {
                bool more;
                try { more = work.MoveNext(); }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    run.Failures++;
                    run.Notes.Add("The run stopped: " + e.GetType().Name + ": " + e.Message);
                    break;
                }
                if (!more) break;
                yield return work.Current;
            }
            (work as IDisposable)?.Dispose();
            try { if (settings.Out != null) LabReport.Write(settings, run); }
            catch (Exception e)
            {
                Debug.LogException(e);
                run.Failures++;
            }
            EditorApplication.ExitPlaymode();
            while (EditorApplication.isPlaying) yield return null;
        }

        private static void Tick()
        {
            if (running.MoveNext()) return;
            EditorApplication.update -= Tick;
            running = null;
            EditorSettings.enterPlayModeOptionsEnabled = savedOptionsEnabled;
            EditorSettings.enterPlayModeOptions = savedOptions;
            current.Done = true;
            Debug.Log("[LabBench] done, " + current.Failures + " failure(s)");
            if (exitWhenDone) EditorApplication.Exit(current.Failures == 0 ? 0 : 1);
        }

        /// <summary>
        /// The whole run, as one coroutine in Play mode: the village found, the variants applied in
        /// turn, every scenario played, everything put back. A Play-mode test steps it directly.
        /// </summary>
        public static IEnumerator Play(Settings settings, Run run)
        {
            var commands = Object.FindFirstObjectByType<VillageCommands>();
            if (commands == null) throw new InvalidOperationException("No village in Play mode.");
            var session = new Session(settings, run, commands);
            Exception failed = null;
            try
            {
                foreach (var x in Guarded(Work(session, settings), e => failed = e)) yield return x;
                // Whatever happened, the models the bench loaded are unloaded and the village reset.
                foreach (var x in Guarded(session.Finish(), e => failed = failed ?? e)) yield return x;
            }
            finally
            {
                session.PutBack();
            }
            if (failed != null) throw new InvalidOperationException("The bench stopped: " + failed.Message, failed);
        }

        private static IEnumerable<object> Work(Session session, Settings settings)
        {
            foreach (var x in session.Prepare()) yield return x;
            foreach (var item in session.Schedule())
                foreach (var x in session.Play(item.arm, item.scenario, item.repeat)) yield return x;
            if (settings.ResetProbe)
                foreach (var x in session.ResetProbe()) yield return x;
        }

        // Steps work, handing an exception to onError and ending there, so the caller can still clean up.
        private static IEnumerable<object> Guarded(IEnumerable<object> work, Action<Exception> onError)
        {
            using (var e = work.GetEnumerator())
            {
                while (true)
                {
                    try
                    {
                        if (!e.MoveNext()) yield break;
                    }
                    catch (Exception x)
                    {
                        onError(x);
                        yield break;
                    }
                    yield return e.Current;
                }
            }
        }

        /// <summary>What F1 must put back, as one line per part: the hour, the weather, the purse and pack, quests, the counter, the situations, every agent.</summary>
        public static string Snapshot(VillageCommands commands)
        {
            var lines = new List<string>();
            var inv = commands.Inventory;
            if (commands.Day != null) lines.Add("hour " + commands.Day.TimeOfDay.ToString("0.00", CultureInfo.InvariantCulture) + (commands.Day.IsNight ? " night" : " day"));
            var presenter = commands.Presenter;
            if (presenter.Weather != null) lines.Add("weather " + presenter.Weather.Current);
            lines.Add("coins " + inv.Coins + ", pack " + inv.Describe());
            lines.Add("quests " + string.Join(", ", commands.Quests.Quests.Select(q => q.Id + "=" + q.State)));
            var merchant = (VillageMerchant)commands.Mind("Odile").Body;
            lines.Add("counter " + string.Join(", ", merchant.Stock.Where(s => s != null).Select(s => s.Id + (s.gameObject.activeSelf ? "" : " (gone)"))));
            lines.Add("thief " + merchant.Thief.State + ", fire " + (merchant.Fire.IsBurning ? "burning" : "out") +
                      ", bell " + (((VillageGuard)commands.Mind("Bram").Body).Alarm.IsSounding ? "ringing" : "silent") +
                      ", wolf " + (presenter.Wolf.IsHidden ? "hidden" : "out"));
            var watch = commands.Mind("Wren").GetComponent<NightWatch>();
            lines.Add("watch " + (watch.OnWatch ? "on" : "off") + ", gate " + (watch.Gate.IsOpen ? "open" : "shut"));
            foreach (var mind in commands.Minds)
            {
                var p = mind.transform.position;
                lines.Add(mind.Name + ": " + mind.Body.Activity + (mind.IsDeciding ? ", deciding" : "") + (mind.HasPending ? ", an event waiting" : "") +
                          ", said " + mind.Conversation.Count + ", last " + (mind.Last == null ? "none" : mind.Last.Stimulus) +
                          ", remembers " + mind.Agent.Preview().History.Count +
                          ", at " + Mathf.Round(p.x) + "," + Mathf.Round(p.z) +
                          (mind.Body is VillageGuard g && g.IsFollowing ? ", following" : ""));
            }
            return string.Join("\n", lines);
        }

        /// <summary>The order the run asks in: every scenario shuffled once by the seed, the same order for every arm.</summary>
        public static List<(Arm arm, LabScenario scenario, int repeat)> Order(Settings settings)
        {
            var random = new System.Random(settings.Seed);
            var shuffled = settings.Scenarios.OrderBy(_ => random.Next()).ToList();
            var items = new List<(Arm, LabScenario, int)>();
            if (settings.Interleaved)
            {
                for (int r = 1; r <= settings.Repeats; r++)
                    for (int i = 0; i < shuffled.Count; i++)
                    {
                        // Which arm goes first alternates, so neither always follows the other.
                        var arms = (i + r) % 2 == 0 ? settings.Arms : Enumerable.Reverse(settings.Arms).ToList();
                        foreach (var arm in arms) items.Add((arm, shuffled[i], r));
                    }
            }
            else
            {
                foreach (var arm in settings.Arms)
                    for (int r = 1; r <= settings.Repeats; r++)
                        foreach (var s in shuffled) items.Add((arm, s, r));
            }
            return items;
        }

        // ---- One run ----------------------------------------------------------------------------------

        private sealed partial class Session
        {
            private readonly Settings settings;
            private readonly Run run;
            private readonly VillageCommands commands;
            private readonly VillagePresenter presenter;
            private LabTuning tuning;
            private LabOllama ollama;
            private DecisionLog log;
            // The model and context size resident now, as "model@num_ctx"; null before the first.
            private string loaded;
            private readonly HashSet<string> loadedByUs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<Agent, long> lastSequence = new Dictionary<Agent, long>();
            private readonly Dictionary<string, LabTurn> turns = new Dictionary<string, LabTurn>();
            private float timeScale;
            private bool weatherChanged;
            private string firstSnapshot;

            public Session(Settings settings, Run run, VillageCommands commands)
            {
                this.settings = settings;
                this.run = run;
                this.commands = commands;
                presenter = commands.Presenter;
            }

            public IEnumerable<(Arm arm, LabScenario scenario, int repeat)> Schedule() => Order(settings);

            public IEnumerable<object> Prepare()
            {
                timeScale = Time.timeScale;
                Time.timeScale = settings.TimeScale;
                commands.EventsMuted = true;
                if (presenter.Weather != null)
                {
                    weatherChanged = presenter.Weather.Changes;
                    presenter.Weather.Changes = false;
                }
                foreach (var mind in commands.Minds)
                    if (mind.Senses != null) mind.Senses.QuietTimerEnabled = false;
                // What the game ships with, read before any variant touches it.
                tuning = new LabTuning(commands);
                foreach (var s in settings.Scenarios)
                    for (int i = 0; i < s.turns.Count; i++) turns[s.id + "#" + (i + 1)] = s.turns[i];
                DecisionFeed.Recorded += OnFeed;
                if (settings.Out != null)
                {
                    // One run per folder: the log appends, and the feed's sequence numbers start again
                    // each Play session, so two runs in one file could not be told apart (Codex, L2).
                    string path = DecisionsPath(settings.Out);
                    Directory.CreateDirectory(settings.Out);
                    log = DecisionLog.Start(path, includePrompt: true);
                    // Never rolled over, so no score points at a line the log has dropped.
                    log.MaxFileBytes = long.MaxValue;
                }
                LabRunInfo.Fill(run.Info, settings);

                if (settings.Server != null)
                {
                    ollama = new LabOllama(settings.Server);
                    var version = ollama.VersionAsync();
                    foreach (var x in Wait(version)) yield return x;
                    run.Info.ollamaVersion = version.Result;
                    var digests = ollama.DigestsAsync();
                    foreach (var x in Wait(digests)) yield return x;
                    foreach (var model in settings.Arms.Select(a => a.Model).Distinct())
                    {
                        var show = ollama.ShowAsync(model);
                        foreach (var x in Wait(show)) yield return x;
                        var d = show.Result;
                        string digest = digests.Result.Where(kv => LabOllama.Same(kv.Key, model)).Select(kv => kv.Value).FirstOrDefault() ?? "";
                        run.Info.models.Add(new LabModelInfo
                        {
                            name = model, digest = digest, family = d.family, parameterSize = d.parameter_size, quantization = d.quantization_level
                        });
                    }
                    // Hard rule 3 from the start: nothing resident the bench did not load.
                    var resident = ollama.ResidentAsync();
                    foreach (var x in Wait(resident)) yield return x;
                    Read("at the start", null, resident.Result);
                    if (resident.Result.Count > 0)
                        throw new InvalidOperationException("Hard rule 3: " + string.Join(", ", resident.Result.Select(r => r.name)) +
                            " already resident before the bench loaded anything. If it is another session's, wait; if it is yours, unload it first.");
                }
            }

            private void OnFeed(DecisionFeedEntry entry)
            {
                if (entry.Stage == DecisionStage.Decided && entry.Agent != null) lastSequence[entry.Agent] = entry.Sequence;
            }

            private void Read(string when, string arm, List<LabOllama.Resident> resident)
            {
                var reading = new LabResidency { when = when, arm = arm ?? "" };
                foreach (var r in resident) reading.models.Add(new LabResident { name = r.name, size = r.size, sizeVram = r.size_vram, contextLength = r.context_length });
                run.Info.residency.Add(reading);
            }

            // The arm's model, and only it, resident and warm, before anything of the arm's is timed.
            private IEnumerable<object> Ready(Arm arm, AgentMind asked)
            {
                string key = (arm.Model ?? "scripted") + "@" + arm.ContextSize;
                bool switching = loaded != key;
                if (ollama != null && switching)
                {
                    var others = ollama.UnloadOthersAsync(null);
                    foreach (var x in Wait(others)) yield return x;
                    var load = ollama.LoadAsync(arm.Model, arm.ContextSize, arm.Variant.provider?.keepAlive);
                    foreach (var x in Wait(load)) yield return x;
                    loadedByUs.Add(arm.Model);
                    run.Notes.Add(string.Format(CultureInfo.InvariantCulture, "Loaded {0} (num_ctx {1}) for {2} in {3:0.0} s{4}.",
                        arm.Model, arm.ContextSize, arm.Name, load.Result, others.Result.Count > 0 ? ", after unloading " + string.Join(", ", others.Result) : ""));
                }
                if (switching)
                {
                    // Throwaway decisions through the game's own agent, then F1 forgets them.
                    commands.UseProvider(arm.Config);
                    tuning.Apply(arm.Variant);
                    for (int i = 0; i < settings.WarmUpDecisions; i++)
                    {
                        Stand(asked, "");
                        var warm = asked.HearPlayerAsync(WarmUpLines[i % WarmUpLines.Length]).GetAwaiter();
                        foreach (var x in Until(() => warm.IsCompleted, TimeoutFor(arm))) yield return x;
                        if (!warm.IsCompleted)
                        {
                            foreach (var x in Abandon(asked, arm, warm)) yield return x;
                            throw new InvalidOperationException(arm.Name + "'s warm-up decision did not finish in " + TimeoutFor(arm) + " s.");
                        }
                    }
                    if (ollama != null)
                    {
                        var resident = ollama.ResidentAsync();
                        foreach (var x in Wait(resident)) yield return x;
                        Read("after warm-up", arm.Name, resident.Result);
                        // Hard rule 3 holds, or nothing of this arm is timed (Codex, L2).
                        var mine = resident.Result.FirstOrDefault(r => LabOllama.Same(r.name, arm.Model));
                        if (mine == null)
                            throw new InvalidOperationException("Hard rule 3: " + arm.Model + " was not resident after its warm-up, so its first decisions would time its load.");
                        var others = resident.Result.Where(r => !LabOllama.Same(r.name, arm.Model)).Select(r => r.name).ToList();
                        if (others.Count > 0)
                            throw new InvalidOperationException("Hard rule 3: " + string.Join(", ", others) + " resident beside " + arm.Model + " after " + arm.Name + "'s warm-up. Another session may be using the server.");
                        if (mine.size_vram < mine.size) run.Notes.Add(string.Format(CultureInfo.InvariantCulture,
                            "{0} was not wholly on the GPU after warm-up: {1} of {2} bytes in VRAM, so its latencies are not comparable.", arm.Model, mine.size_vram, mine.size));
                    }
                    loaded = key;
                }
            }

            public IEnumerable<object> Play(Arm arm, LabScenario scenario, int repeat)
            {
                var asked = commands.Mind(scenario.agent);
                foreach (var x in Ready(arm, asked)) yield return x;

                // F1, on this arm's config, then this arm's variant.
                commands.UseProvider(arm.Config);
                presenter.ResetVillage();
                tuning.Apply(arm.Variant);
                CurrentArm = arm;
                CurrentScenario = scenario;
                yield return null;
                yield return null;
                string snapshot = Snapshot();
                run.Snapshots.Add(snapshot);
                if (firstSnapshot == null) firstSnapshot = snapshot;
                else if (snapshot != firstSnapshot)
                    run.Leaks.Add(scenario.id + " (" + arm.Name + ", repeat " + repeat + "): the village after F1 differs from the first reset: " + Difference(firstSnapshot, snapshot));

                foreach (var x in Setup(scenario, asked)) yield return x;

                bool primerMissed = false, stopped = false;
                for (int i = 0; i < scenario.turns.Count; i++)
                {
                    var turn = scenario.turns[i];
                    CurrentTurn = turn;
                    var result = new LabTurnResult
                    {
                        arm = arm.Name, variant = arm.Variant.name, repeat = repeat, scenario = scenario.id, turn = i + 1,
                        primer = turn.primer, agent = scenario.agent, kind = scenario.kind, bait = scenario.bait, difficulty = scenario.difficulty,
                    };
                    if (stopped)
                    {
                        // A turn after one that never finished: what it depends on never happened.
                        result.outcome = "error";
                        result.error = "not asked: an earlier turn of this scenario did not finish";
                        LabScore.Score(result, turn);
                        run.Results.Add(result);
                        continue;
                    }
                    timedOut = null;
                    foreach (var x in Ask(asked, arm, scenario, turn, result)) yield return x;
                    if (timedOut != null)
                    {
                        // Cancelled and settled before anything else is asked, or any model switched.
                        stopped = true;
                        foreach (var x in Abandon(asked, arm, timedOut.Value)) yield return x;
                    }
                    LabScore.Score(result, turn);
                    result.primerMissed = !turn.primer && primerMissed;
                    if (turn.primer && result.verdict != "exact") primerMissed = true;
                    run.Results.Add(result);
                    // Between turns, what the last one started plays on a moment, as in the game.
                    if (i + 1 < scenario.turns.Count) foreach (var x in Frames(10)) yield return x;
                }
                CurrentTurn = null;
                CurrentScenario = null;
            }

            // The awaiter of a turn that did not finish in time; null when it did.
            private Awaitable.Awaiter? timedOut;

            // A provider's own timeout, plus room for the queue and the game: a turn is given at least that.
            private float TimeoutFor(Arm arm) =>
                Mathf.Max(settings.TurnTimeoutSeconds, arm.Config is OllamaProviderConfig o ? o.TimeoutSeconds + 15f : 0f);

            // Resets the agent, which cancels the call it has with the model, and waits for the call to end.
            private IEnumerable<object> Abandon(AgentMind asked, Arm arm, Awaitable.Awaiter call)
            {
                asked.UseProvider(arm.Config);
                foreach (var x in Until(() => call.IsCompleted, 10f)) yield return x;
                run.Notes.Add(arm.Name + ": " + asked.Name + "'s call did not finish in " + TimeoutFor(arm) + " s; the agent was reset, and the call " +
                              (call.IsCompleted ? "ended." : "had still not ended 10 s later."));
            }

            private IEnumerable<object> Ask(AgentMind asked, Arm arm, LabScenario scenario, LabTurn turn, LabTurnResult result)
            {
                string source, label, text;
                DecisionPriority priority;
                if (!turn.IsEvent)
                {
                    source = "player";
                    label = "Player";
                    text = turn.say;
                    priority = DecisionPriority.PlayerFacing;
                    Stand(asked, scenario.setup?.player ?? "");
                    foreach (var x in Frames(3)) yield return x;
                }
                else Event(asked, scenario, turn, out source, out label, out text, out priority);
                result.label = label;
                result.stimulus = text;

                // What this turn would send, without sending it: left-out actions and unavailable targets.
                var preview = asked.Agent.Preview(text, asked.OptionsFor(source, label, priority));
                foreach (var left in preview.LeftOut) result.leftOut.Add(left.Action.Id + ": " + left.Reason);
                foreach (var t in preview.TargetsUnavailable) result.targetsUnavailable.Add(t.ToString());

                var agent = asked.Agent;
                lastSequence.Remove(agent);
                var before = asked.Last;
                float started = Time.realtimeSinceStartup;
                var asking = (turn.IsEvent ? asked.AskAsync(source, label, text, priority) : asked.HearPlayerAsync(text)).GetAwaiter();
                foreach (var x in Until(() => asking.IsCompleted, TimeoutFor(arm))) yield return x;
                result.wallSeconds = Time.realtimeSinceStartup - started;
                var r = asked.Last;
                if (!asking.IsCompleted) timedOut = asking;
                if (!asking.IsCompleted || r == null || r == before || !r.Done)
                {
                    result.outcome = "error";
                    result.error = !asking.IsCompleted ? "no answer within " + TimeoutFor(arm) + " s" : "the agent was not asked";
                    yield break;
                }
                result.offeredActions = r.OfferedActions.ToList();
                result.offeredTargets = r.OfferedTargets.ToList();
                result.modelAction = r.Action ?? "";
                result.modelTarget = r.Target ?? "";
                result.statement = r.Statement ?? "";
                result.outcome = r.Outcome ?? "";
                result.guard = r.Guard ?? "";
                result.guardReason = r.GuardReason ?? "";
                result.said = r.Said ?? "";
                result.error = r.ProviderError ?? "";
                result.model = r.Model ?? "";
                result.latencySeconds = r.LatencySeconds;
                result.queueSeconds = r.QueueWaitSeconds;
                result.promptTokens = r.PromptTokens;
                result.completionTokens = r.CompletionTokens;
                result.sequence = lastSequence.TryGetValue(agent, out long seq) ? seq : -1;
            }

            // A game event in the game's own words, with the source, label and priority the game gives it.
            private void Event(AgentMind asked, LabScenario scenario, LabTurn turn, out string source, out string label, out string text, out DecisionPriority priority)
            {
                label = "Event";
                priority = DecisionPriority.Background;
                var merchant = (VillageMerchant)commands.Mind("Odile").Body;
                float Away(Transform t) => Vector3.Distance(asked.transform.position, t.position);
                switch (turn.@event)
                {
                    case "wolf":
                        source = "wolf";
                        text = GuardSenses.WolfStimulus(asked.VisibleWolfDistance() ?? Away(presenter.Wolf.transform));
                        break;
                    case "thief":
                        source = "thief";
                        text = GuardSenses.ThiefStimulus(merchant.Thief.Loot != null ? merchant.Thief.Loot.Id : "goods",
                            asked.VisibleThiefDistance() ?? Away(merchant.Thief.transform));
                        break;
                    case "snatched":
                        source = "thief";
                        text = VillageThief.SnatchedStimulus(merchant.Thief.Loot != null ? merchant.Thief.Loot.Id : "bread");
                        break;
                    case "fire":
                        source = "fire";
                        text = merchant.Fire.Stimulus;
                        break;
                    case "bell":
                        source = "bell";
                        text = VillageEvents.BellStimulus(string.IsNullOrEmpty(turn.arg) ? null : turn.arg);
                        break;
                    case "zone":
                        source = "zone";
                        priority = DecisionPriority.PlayerFacing;
                        var zone = Zone(scenario.setup?.player);
                        text = GuardSenses.ZoneEnteredStimulus(zone != null ? zone.DisplayName : turn.arg);
                        break;
                    case "quiet":
                        source = "quiet";
                        label = "Time";
                        text = GuardSenses.QuietStimulus;
                        break;
                    case "night":
                        source = "night";
                        text = commands.Mind("Wren").GetComponent<NightWatch>().Arrival;
                        break;
                    case "merchant":
                        source = "merchant";
                        label = "Merchant";
                        text = VillageMerchant.CallStimulus(merchant.Trouble(), turn.arg);
                        break;
                    case "captain":
                        source = "captain";
                        label = "Captain";
                        var parts = (turn.arg ?? "").Split(new[] { '|' }, 2);
                        text = VillageCaptain.OrderStimulus(parts[0], parts.Length > 1 ? parts[1] : "");
                        break;
                    default:
                        throw new InvalidOperationException(scenario.id + ": no event " + turn.@event);
                }
            }

            // ---- The setup ---------------------------------------------------------------------------

            private IEnumerable<object> Setup(LabScenario scenario, AgentMind asked)
            {
                var setup = scenario.setup ?? new LabSetup();
                if (setup.hour == "dusk")
                {
                    presenter.JumpToDusk();
                    var wren = commands.Mind("Wren");
                    var watch = wren.GetComponent<NightWatch>();
                    // Wren hurries to the gate and Bram goes off watch: the scene as night has it.
                    foreach (var x in Until(() => watch.OnWatch && !((VillageGuard)wren.Body).IsMoving && !((VillageGuard)commands.Mind("Bram").Body).IsMoving, 40f * Mathf.Max(1f, 1f / settings.TimeScale))) yield return x;
                }
                if (!string.IsNullOrEmpty(setup.weather) && presenter.Weather != null)
                {
                    presenter.Weather.Set((WeatherKind)Enum.Parse(typeof(WeatherKind), setup.weather, true));
                    presenter.Weather.Snap();
                }
                var merchant = (VillageMerchant)commands.Mind("Odile").Body;
                switch (setup.situation)
                {
                    case "wolf_gate":
                        presenter.Wolf.Approach(presenter.WolfTo.position, presenter.WolfTo.position, 600f);
                        break;
                    case "wolf_road":
                        presenter.Wolf.Approach(presenter.WolfFrom.position, presenter.WolfFrom.position, 600f);
                        break;
                    case "thief":
                        merchant.Thief.Begin();
                        var nav = merchant.Thief.GetComponent<NavMeshAgent>();
                        if (nav != null && nav.isOnNavMesh) nav.Warp(merchant.Thief.Counter.position);
                        merchant.Thief.Grab();
                        break;
                    case "fire":
                        merchant.Fire.Ignite();
                        break;
                    case "bell":
                        ((VillageGuard)commands.Mind("Bram").Body).Alarm.Ring(commands.Mind("Bram").Body);
                        break;
                }
                if (setup.coins >= 0) commands.Inventory.Reset(setup.coins);
                foreach (var item in setup.items ?? new List<LabItem>()) commands.Inventory.Add(item.id, item.count);
                foreach (var q in setup.quests ?? new List<LabQuest>())
                {
                    var quest = commands.Quests.Get(q.id) ?? throw new InvalidOperationException(scenario.id + ": no quest " + q.id);
                    quest.State = q.state == "done" ? QuestState.Done : QuestState.Active;
                }
                Stand(asked, setup.player ?? "");
                // A few frames, so the physics sees the player in a zone and the senses see the wolf.
                foreach (var x in Frames(6)) yield return x;
            }

            private RestrictedZone Zone(string player) =>
                player != null && player.StartsWith("zone:")
                    ? Object.FindObjectsByType<RestrictedZone>(FindObjectsSortMode.None).FirstOrDefault(z => z.Id == player.Substring(5))
                    : null;

            // Where the player stands for a turn: before the asked agent, inside a zone, or far off.
            private void Stand(AgentMind asked, string where)
            {
                var t = asked.transform;
                Vector3 at;
                var zone = Zone(where);
                if (zone != null) at = zone.transform.position;
                else if (where == "far") at = t.position - t.forward * 20f;
                else at = t.position + t.forward * 3f;
                var player = commands.Player;
                var controller = player.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;
                player.position = new Vector3(at.x, player.position.y, at.z);
                var look = t.position - player.position;
                look.y = 0;
                if (look.sqrMagnitude > 0.01f) player.rotation = Quaternion.LookRotation(look);
                if (controller != null) controller.enabled = true;
            }

            // ---- The village's state, for the leak check ------------------------------------------------

            private string Snapshot() => LabBench.Snapshot(commands);

            private static string Difference(string a, string b)
            {
                var left = a.Split('\n');
                var right = b.Split('\n');
                var differ = right.Where((line, i) => i >= left.Length || left[i] != line).ToList();
                return differ.Count == 0 ? "(line count)" : string.Join("; ", differ);
            }

            // ---- The end -------------------------------------------------------------------------------

            public IEnumerable<object> Finish()
            {
                // The last arm's village back to a plain morning, so nothing it did is left running.
                presenter.ResetVillage();
                yield return null;
                if (ollama != null)
                {
                    var resident = ollama.ResidentAsync();
                    foreach (var x in Wait(resident)) yield return x;
                    Read("at the end", null, resident.Result);
                    foreach (var model in loadedByUs.ToList())
                    {
                        var unload = ollama.UnloadAsync(model);
                        foreach (var x in Wait(unload)) yield return x;
                    }
                }
                run.Info.finished = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            }

            /// <summary>Everything the bench changed, put back: run in a finally, so a failed run leaves the game as it shipped.</summary>
            public void PutBack()
            {
                DecisionFeed.Recorded -= OnFeed;
                try { tuning?.Restore(); }
                catch (Exception e) { Debug.LogException(e); }
                if (commands != null)
                {
                    commands.EventsMuted = false;
                    if (presenter != null && presenter.Weather != null) presenter.Weather.Changes = weatherChanged;
                }
                Time.timeScale = timeScale > 0 ? timeScale : 1f;
                log?.Dispose();
                ollama?.Dispose();
                CurrentArm = null;
                CurrentScenario = null;
                CurrentTurn = null;
            }

            // ---- Waiting ---------------------------------------------------------------------------

            private static IEnumerable<object> Wait(Task task)
            {
                while (!task.IsCompleted) yield return null;
                if (task.IsFaulted) throw task.Exception.GetBaseException();
            }

            private static IEnumerable<object> Until(Func<bool> done, float realSeconds)
            {
                float until = Time.realtimeSinceStartup + realSeconds;
                while (!done() && Time.realtimeSinceStartup < until) yield return null;
            }

            private static IEnumerable<object> Frames(int n)
            {
                int until = Time.frameCount + n;
                while (Time.frameCount < until) yield return null;
            }
        }
    }
}
