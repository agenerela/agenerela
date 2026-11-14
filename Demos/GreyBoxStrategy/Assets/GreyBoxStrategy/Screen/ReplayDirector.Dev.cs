using System;
using System.IO;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The dev menu (F1, in the editor and development builds only) and its commands. Everything
    /// it does goes through <see cref="DevTools.Run"/>, so a session can drive it with
    /// <c>unity command script eval</c>. A game it touches is marked DEV on screen and in its
    /// recording; the table runner and the scorecard never go through the screen, so they never
    /// meet it.
    /// </summary>
    public sealed partial class ReplayDirector
    {
        private DevCommands dev;
        private bool autoOrders;
        private float fps, devTick;
        private Recording sourceRecording;

        /// <summary>True when the game on screen is a dev game: changed through the dev menu, or played with stand-in rulers.</summary>
        public bool IsDevGame => (game != null && game.Dev) || (rule != null && rule.Dev) || (live != null && live.Dev) || DevFaults.Any;

        public DevCommands Dev => dev ?? (dev = BuildDevCommands());

        private string You => rule?.You ?? followed ?? "kesh";

        private DevCommands BuildDevCommands()
        {
            int N(string[] a, int i, int fallback) => a.Length > i ? int.Parse(a[i]) : fallback;
            string Need(bool ok, string why) => ok ? null : throw new ArgumentException(why);
            var c = new DevCommands();
            c.Add("help", "help", "every command", _ => dev.Help());

            // ---- play without the card ----
            c.Add("scripted-rule", "scripted-rule <country> [seed] [storyteller preset]", "rule a country against four stand-in rulers, and a stand-in storyteller with a preset (even_hand, saga...); no server, no card",
                a => { Need(a.Length >= 1, "Name a country."); if (started) return "A game is running: Esc, then Main menu, first."; string why = Preset(a, 2); if (why != null) return why;
                       ui.HideMenu(); StartRuleScripted(a[0], N(a, 1, 7), 0, StoryArms.For(a.Length > 2 ? a[2] : null)); return $"rule game, {a[0]}, stand-in rulers{(a.Length > 2 ? ", storyteller " + a[2] : "")}"; });
            c.Add("scripted-watch", "scripted-watch [seed] [storyteller preset]", "watch five stand-in rulers, and a stand-in storyteller with a preset; no server, no card",
                a => { if (started || live == null) return started ? "A game is running: Esc, then Main menu, first." : "No live component in the scene."; string why = Preset(a, 1); if (why != null) return why;
                       StartScriptedWatch(N(a, 0, 7), StoryArms.For(a.Length > 1 ? a[1] : null)); return "watch game, stand-in rulers"; });

            // ---- the storyteller ----
            c.Add("story-open", "story-open", "open the last storyteller event played", _ => OpenLastEvent());
            c.Add("story-inspect", "story-inspect [title]", "the storyteller's decision behind the last event in the inspector; with title, the title call's", a => InspectLastStory(a.Length > 0));
            c.Add("tales", "tales", "open or close the tales panel", _ => { ui.ToggleTales(); return ui.TalesOpen ? "tales open" : "tales closed"; });
            c.Add("choose", "choose <option>", "answer the dilemma on the choice card (rule mode)", a => { Need(a.Length == 1, "Name an option."); return ChooseOption(a[0]); });

            c.Add("generated", "generated <small|medium|large|huge> [shape] [seed] [countries]",
                "watch scripted rulers play a generated world, drawn as a chart; shapes continent, two_lands, inland_sea, peninsula, long_coast",
                a =>
                {
                    Need(a.Length >= 1, "Give a size.");
                    var size = (MapSize)Enum.Parse(typeof(MapSize), a[0], true);
                    var shape = a.Length > 1 ? (LandShape)Enum.Parse(typeof(LandShape), a[1].Replace("_", ""), true) : LandShape.Continent;
                    int seed = N(a, 2, 7), countries = N(a, 3, MapSettings.DefaultCountries(size));
                    string why = new MapSettings { Size = size, Shape = shape }.WhyInvalid(countries);
                    if (why != null) return why;
                    StartGeneratedWatch(size, shape, seed, countries);
                    return $"loading a {size} {shape} world, seed {seed}, {countries} countries";
                });

            // ---- the writs turn ----
            c.Add("writs", "writs classic|<small|medium|large|huge> [seed] [conquest|domination_75]", "watch stand-in rulers play the writs turn: armies as pieces, occupation, treaties",
                a =>
                {
                    Need(a.Length >= 1, "classic, or a world size.");
                    int seed = N(a, 1, 3);
                    string victory = a.Length > 2 ? a[2] : GameSetup.ConquestPreset;
                    Need(GameSetup.VictoryPresets.Contains(victory), "conquest or domination_75.");
                    if (a[0] == "classic") { StartWritsWatch(seed, victory); return $"the writs turn on the Classic board, seed {seed}"; }
                    var size = (MapSize)Enum.Parse(typeof(MapSize), a[0], true);
                    StartGeneratedWatch(size, LandShape.Continent, seed, 0, true, victory);
                    return $"the writs turn on a {size} world, seed {seed}";
                });
            c.Add("writs-rule", "writs-rule <country> [seed] [storyteller preset]", "rule a country on the writs turn against four stand-in rulers through the real agents, and a stand-in storyteller with a preset; no server, no card",
                a => { if (a.Length < 1) return "Name a country."; if (started) return "A game is running: Esc, then Main menu, first."; string why = Preset(a, 2); if (why != null) return why;
                       ui.HideMenu(); StartRuleScriptedWrits(a[0], a.Length > 1 ? int.Parse(a[1]) : 3, GameSetup.ConquestPreset, StoryArms.For(a.Length > 2 ? a[2] : null)); return $"writs rule game, {a[0]}, stand-in rulers{(a.Length > 2 ? ", storyteller " + a[2] : "")}"; });
            c.Add("writs-rule-world", "writs-rule-world <small|medium|large|huge> [seed] [seat] [countries] [model]",
                "rule the realm in a seat (0 is the first) of a generated world on the writs turn: stand-in rulers through the real agents, or a model on the RTX 5080 when one is named",
                a =>
                {
                    Need(a.Length >= 1, "Give a size.");
                    var size = (MapSize)Enum.Parse(typeof(MapSize), a[0], true);
                    int seed = N(a, 1, 7), seat = N(a, 2, 0), countries = N(a, 3, MapSettings.DefaultCountries(size));
                    string why = new MapSettings { Size = size, Shape = LandShape.Continent }.WhyInvalid(countries);
                    if (why != null) return why;
                    Need(seat >= 0 && seat < countries, $"A seat from 0 to {countries - 1}.");
                    bool scripted = a.Length < 5;
                    StartGeneratedRule(size, LandShape.Continent, seed, countries, seat, scripted ? null : a[4], scripted);
                    return $"a {size} world, seed {seed}, {countries} countries: you rule seat {seat}, {(scripted ? "stand-in rulers" : a[4])}";
                });
            c.Add("plan", "plan court <move> | court2 <move> | <army> <march province|hold> | end", "the writs turn's orders, as the panel and the map give them (rule mode)", a => WritsPlanCommand(a));
            c.Add("writs-live", "writs-live classic|<size> [seed] [turns] [model]", "model rulers play the writs turn live on the RTX 5080, a court and a marshal each (the lock, its pause file and the server are checked first)",
                a => LiveWritsCommand(a, null));
            c.Add("writs-agents", "writs-agents classic|<size> [seed] [turns]", "stand-in rulers play the writs turn through the real agents, live: the inspector has their prompts; no server, no card",
                a => LiveWritsCommand(a, "scripted"));
            c.Add("until", "until <event kind>", "play on and hold before the next event of a kind: Battle, March, Occupy, Treaty, TermOffered...", a => { Need(a.Length == 1, "Name a kind."); RunUntil(a[0]); return "playing until " + a[0]; });
            c.Add("battle-report", "battle-report", "open the report of the last battle played", _ =>
            {
                var e = played.LastOrDefault(x => x.Kind == ReplayEventKind.Battle);
                if (e == null) return "No battle played yet.";
                ui.ShowBattle(e);
                return $"turn {e.Turn}: {e.Text}";
            });

            // ---- time ----
            c.Add("turn", "turn <n>", "jump to turn n without animating the turns before it", a => { SkipToTurn(N(a, 0, 1)); return $"jumping to turn {N(a, 0, 1)}"; });
            c.Add("step", "step", "land the next event", _ => { playing = false; stepOnce = true; ui.SetPlaying(false, speed); return Position; });
            c.Add("speed", "speed <x>", "playback speed, 0.25 to 32", a => { Need(a.Length == 1, "Give a speed."); speed = Mathf.Clamp(float.Parse(a[0], System.Globalization.CultureInfo.InvariantCulture), 0.25f, 32f); ui.SetPlaying(playing, speed); return $"speed {speed}×"; });
            c.Add("play", "play", "play", _ => { playing = true; ui.SetPlaying(true, speed); return "playing"; });
            c.Add("pause", "pause", "pause", _ => { Pause(); return "paused"; });
            c.Add("autoplay", "autoplay on|off", "play to the end at 8×; in rule mode, hold every turn, refuse every envoy and take every dilemma's default",
                a => { bool on = a.Length == 0 || a[0] == "on"; autoOrders = on && rule != null; if (on) { speed = Math.Max(speed, 8); playing = true; ui.SetPlaying(true, speed); } return on ? "autoplay on" : "autoplay off"; });

            // ---- state (rule games: they own the turn loop) ----
            foreach (string name in DevEdits.Names)
                c.Add(name, EditUsage(name), "dev edit, applied at the start of the next turn (rule games)", a =>
                {
                    if (rule == null && game != null && game.Writs && scripted != null) return WritsEdit(name + " " + string.Join(" ", a));
                    if (rule == null) return "State edits need a rule game (scripted-rule kesh) or a writs game with stand-in rulers: a replay cannot change, and a watch game runs on the table runner with no hook between turns.";
                    string command = name + " " + string.Join(" ", a);
                    string why = rule.QueueDev(command);
                    return why ?? $"queued for turn {rule.Game.NextTurn}: {command}";
                });
            c.Add("events", "events", "every event template: the random table's, the storyteller's, its dilemmas and the tales it can begin", _ =>
                string.Join("\n", EventRules.AllTemplates.Select(t => $"{t.Id} ({t.Takes}{(t.IsChoice ? ", a dilemma: " + string.Join(" / ", t.Options.Select(o => o.Id)) : "")}{(t.Skeleton != null ? ", begins a tale" : "")}): {t.Description}")));
            c.Add("fog", "fog off|on", "reveal everything, or restore what your court knows (rule mode)", a =>
            {
                Need(a.Length == 1 && (a[0] == "on" || a[0] == "off"), "on or off.");
                if (rule == null) return "Fog is a rule-mode rule.";
                ui.Fog = new Fog(a[0] == "on" ? rule.You : null, queries);
                if (shown != null) Show(shown, null);
                return a[0] == "on" ? "fog restored" : "fog off: everything shown";
            });

            // ---- faults: none touches the real lock or server ----
            c.Add("fault", "fault lock <holder…>|lock off|server on|off|timeout on|off|unreadable on|off", "pretend a fault", a =>
            {
                Need(a.Length >= 2, "Say which fault and on or off.");
                bool on = a[1] != "off";
                switch (a[0])
                {
                    case "lock": DevFaults.FakeLockHolder = on ? string.Join(" ", a.Skip(1)) : null; break;
                    case "server": DevFaults.ServerDown = on; break;
                    case "timeout":
                        DevFaults.Hang = on;
                        if (rule != null) rule.RoundTimeout = on ? TimeSpan.FromSeconds(8) : TimeSpan.FromSeconds(150);
                        break;
                    case "unreadable": DevFaults.Unreadable = on; break;
                    default: throw new ArgumentException($"No fault '{a[0]}'.");
                }
                return "faults: " + DevFaults.Describe();
            });
            c.Add("faults", "faults off", "clear every pretended fault", _ => { DevFaults.Clear(); if (rule != null) rule.RoundTimeout = TimeSpan.FromSeconds(150); return "faults: none"; });

            // ---- readouts, capture, window ----
            c.Add("readouts", "readouts", "frames per second, scale, resolution, the last request, the card", _ => Readouts());
            c.Add("screenshot", "screenshot [name]", "save a screenshot in Logs/Dev", a =>
            {
                string path = DevPath(a.Length > 0 ? a[0] : $"shot-{DateTime.Now:yyyyMMdd-HHmmss}", ".png");
                ScreenCapture.CaptureScreenshot(path);
                return path;
            });
            c.Add("save-recording", "save-recording [name]", "save the game as a recording the replay can load", a =>
            {
                var rec = CurrentRecording();
                if (rec == null) return "No game to save.";
                string path = DevPath(a.Length > 0 ? a[0] : $"game-{DateTime.Now:yyyyMMdd-HHmmss}", ".json");
                File.WriteAllText(path, rec.ToJson());
                return $"{path} ({rec.Decisions.Count} decisions{(rec.Dev ? ", dev game" : "")})";
            });
            c.Add("load-recording", "load-recording <path>", "replay a saved recording", a =>
            {
                Need(a.Length >= 1, "Give a path.");
                if (started) return "A game is running: Esc, then Main menu, first.";
                var rec = Recording.Parse(File.ReadAllText(string.Join(" ", a)));
                sourceRecording = rec;
                game = ReplayBuilder.Build(rec);
                ui.Players = game.Players;
                Show(game.Initial, null);
                StartReplay();
                return $"replaying {rec.Title}, {game.Turns.Count} turns{(game.Dev ? ", dev game" : "")}";
            });
            c.Add("size", "size <w>x<h>", "set the window or Game view size: 1366x768, 1920x1080, 2560x1440, 3840x2160, or 16:10 1920x1200, 3840x2400", a =>
            {
                Need(a.Length == 1 && a[0].Contains("x"), "Give a size like 1920x1080.");
                var wh = a[0].Split('x'); int w = int.Parse(wh[0]), h = int.Parse(wh[1]);
#if UNITY_EDITOR
                UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)w, (uint)h, $"dev {w}x{h}");
                return $"Game view {w} × {h}";
#else
                Screen.SetResolution(w, h, Screen.fullScreenMode);
                return $"window {w} × {h}";
#endif
            });
            c.Add("menu", "menu", "show or hide the dev menu", _ => { ToggleDevMenu(); return ui.DevOpen ? "dev menu open" : "dev menu closed"; });
            return c;
        }

        private string WritsPlanCommand(string[] a)
        {
            if (rule == null || !rule.Writs || ui.OrdersRequest == null) return "Not waiting for your writs orders.";
            if (a.Length == 0) return ui.WritsKey();
            if (a[0] == "end") { EndTurn(); return "orders sent"; }
            var req = ui.OrdersRequest;
            Move Find(System.Collections.Generic.IEnumerable<Move> legal, string action, string target) =>
                legal.FirstOrDefault(m => m.Action == action && m.Target == target) ?? throw new ArgumentException($"{action} {target} is not offered.");
            if (a[0] == "court" || a[0] == "court2")
            {
                var m = Find(req.Legal, a[1], a.Length > 2 ? a[2] : null);
                if (a[0] == "court") ui.Plan.Court = m; else ui.Plan.Court2 = m;
            }
            else
            {
                var moves = WritLegality.ArmyMoves(req.World, rule.You, a[0]);
                ui.Plan.Armies[a[0]] = a.Length > 1 && a[1] == "march" ? Find(moves, Actions.March, a[2]) : Find(moves, Actions.None, null);
            }
            lastOrdersStatus = null; RuleTick();
            return ui.WritsKey();
        }

        private string LiveWritsCommand(string[] a, string model)
        {
            if (a.Length < 1) return "classic, or a world size.";
            if (started) return "A game is running: Esc, then Main menu, first.";
            int seed = a.Length > 1 ? int.Parse(a[1]) : 3, turns = a.Length > 2 ? int.Parse(a[2]) : 0;
            model = model ?? (a.Length > 3 ? a[3] : live?.Model ?? "qwen3.5:4b");
            ui.HideMenu();
            if (a[0] == "classic") { TryLiveWrits(model, seed, GameSetup.ConquestPreset, model == "scripted", turns); return $"live writs game on the Classic board, {model}, seed {seed}{(turns > 0 ? $", {turns} turns" : "")}"; }
            StartGeneratedLive((MapSize)Enum.Parse(typeof(MapSize), a[0], true), seed, model, GameSetup.ConquestPreset, turns);
            return $"live writs game on a {a[0]} world, {model}, seed {seed}";
        }

        private static string EditUsage(string name)
        {
            switch (name)
            {
                case "gold": case "army": case "stability": return name + " <country> <+n|-n|=n>";
                case "forts": return "forts <province> <+n|-n|=n>";
                case "owner": return "owner <province> <country|neutral>";
                case "war": case "peace": return name + " <country> <country>";
                case "eliminate": return "eliminate <country>";
                case "spawn": return "spawn <country> <province> <soldiers>";
                case "march": return "march <army> <province>";
                case "battle": return "battle <army> <enemy army>";
                case "occupy": return "occupy <province> <country>";
                case "free": return "free <province>";
                case "warscore": return "warscore <country> <country> =<-20..20>";
                case "treaty": return "treaty <country> <offer_white_peace|demand_occupied|cede_occupied> <country>";
                default: return "event <template> <target>";
            }
        }

        private static string DevPath(string name, string ext)
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Dev"));
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, Path.GetFileNameWithoutExtension(name) + ext);
        }

        /// <summary>The game on screen as a recording: a rule game's, a live game's log, or the replay's own.</summary>
        private Recording CurrentRecording()
        {
            if (rule != null) return rule.BuildRecording();
            if (live != null && live.PlayLive)
            {
                var r = live.Recording();
                r.Dev = live.Dev;
                return r;
            }
            return sourceRecording ?? (recording != null ? Recording.Parse(recording.text) : null);
        }

        private static string Preset(string[] a, int i) =>
            a.Length <= i || a[i] == "off" || StorytellerSettings.Named(a[i]) != null ? null
                : $"No storyteller preset '{a[i]}': off, {string.Join(", ", StorytellerSettings.Presets.Select(p => p.Name))}.";

        private void StartScriptedWatch(int seed, string arm = null)
        {
            started = true;
            live.BeginScripted(seed, arm);
            ui.Players = live.Players;
            game = live.Replay();
            ui.HideMenu();
            Show(game.Initial, null);
            StartCoroutine(Play());
        }

        public string Readouts()
        {
            var last = game?.AllEvents.LastOrDefault(e => e.Decision != null && e.Decision.ModelName != RuleSession.YourModel)?.Decision;
            string holder = Gateway.LockHolder();
            return string.Join("\n", new[]
            {
                $"{fps:0} fps · UI scale {CurrentUiScale():0.##} · {Screen.width} × {Screen.height}{(Application.isEditor ? " (Game view)" : "")}",
                $"game: {(rule != null ? "rule, you are " + rule.You : live != null && live.PlayLive ? "watch live" : started ? "replay" : "menu")} · {Position}{(IsDevGame ? " · DEV" : "")}",
                $"last reply: {DevProvider.LastLatency:0.00} s, {DevProvider.LastPromptTokens} in / {DevProvider.LastCompletionTokens} out · requests {DevProvider.Requests}, in flight {DevProvider.InFlight}",
                last != null ? $"last decision played: {last.AgentId} turn {last.Turn}, {last.LatencySeconds:0.00} s, queue wait {last.QueueWaitSeconds:0.00} s, {last.PromptTokens} tokens in" : "last decision played: none",
                $"card: {(holder == null ? "free" : holder)}{(Gateway.HoldsLock ? " · this game holds it" : "")} · faults: {DevFaults.Describe()}",
                rule != null && rule.DevQueued.Count > 0 ? "next turn: " + string.Join("; ", rule.DevQueued) : null,
                MapReadout(),
            }.Where(x => x != null));
        }

        // ---- the panel ----

        public void ToggleDevMenu()
        {
            if (!DevTools.Available) return;
            if (ui.DevOpen) { ui.CloseDev(); return; }
            ui.ShowDev(Dev, You, rule != null ? rule.Game.World.CountryList.Select(x => x.Id).Where(x => x != rule.You).FirstOrDefault() : "valen",
                EventRules.Templates.Select(t => t.Id).ToList());
            ui.SetDevReadouts(Readouts());
        }

        /// <summary>Every frame: the readouts' clock, the DEV mark, and rule mode's automatic orders.</summary>
        private void DevUpdate()
        {
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.05f);
            ui.SetDevMark(IsDevGame);
            if ((devTick -= Time.unscaledDeltaTime) <= 0) { devTick = 0.5f; if (ui.DevOpen) ui.SetDevReadouts(Readouts()); }
            if (autoOrders && rule != null && !rule.Finished)
            {
                if (rule.WaitingFor == RuleSession.Waiting.Orders && ui.OrdersRequest != null) { ui.Chosen = Move.Hold; EndTurn(); }
                else if (rule.WaitingFor == RuleSession.Waiting.Envoy) AnswerOffer(false);
                else if (rule.WaitingFor == RuleSession.Waiting.Choice && rule.Choice != null) ChooseOption(rule.Choice.DefaultOption);
            }
        }
    }
}
