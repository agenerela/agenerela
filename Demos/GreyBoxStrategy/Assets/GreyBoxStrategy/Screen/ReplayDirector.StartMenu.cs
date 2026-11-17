using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Agenerela;
using Agenerela.Providers;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The start menu (#20): the title screen over a map that is alive, what each of its buttons does, the finished
    /// games kept for Load a replay, the model status line, and the keys for every menu's big buttons.
    /// </summary>
    /// <remarks>
    /// The background is the Classic board's painting, already loaded when the menu opens, with the camera drifting
    /// slowly over it and the recorded game's borders changing every few seconds. It costs nothing to make (a
    /// generated world would take seconds behind a loading card) and it is the game's best picture. Only the map's
    /// owners are painted: the screen's own state is never touched, so a game starts from exactly where it would.
    /// </remarks>
    public sealed partial class ReplayDirector
    {
        // ---- the title screen ----

        /// <summary>The game's first screen. Also where every menu window goes back to.</summary>
        public void OpenTitle()
        {
            if (started) return;
            // A replay chosen from the library on the Classic board plays at once on the screen just built.
            playingSaved = false;
            // Continue is offered only when there is a game to go back to. Nothing saves a game in progress yet, so
            // there never is; the button is ready for the day one can be.
            ui.ShowTitle(StartMenu.Buttons(canContinue: false), TitleChose, AgenerelaInfo.Version);
            EnterMenus();
        }

        private void TitleChose(string id)
        {
            switch (id)
            {
                case StartMenu.NewGame: OpenNewGame(rule: true); break;
                case StartMenu.Watch: OpenNewGame(rule: false); break;
                case StartMenu.Replays: OpenReplays(); break;
                case StartMenu.Settings: OpenSettings(OpenTitle); break;
                case StartMenu.Credits: ui.ShowCredits(AgenerelaInfo.Version, OpenTitle); break;
                case StartMenu.Quit: Quit(); break;
            }
        }

        /// <summary>
        /// For the menus and the Unity CLI: New game's window, set to ruling a realm or to watching the models play
        /// (null leaves the mode as it was).
        /// </summary>
        public void OpenNewGame(bool? rule)
        {
            if (live != null) newGame.Model = live.Model;
            if (rule != null) newGame.Rule = rule.Value;
            playingSaved = false;
            ui.ShowNewGame(newGame, StartNewGame, OpenTitle, rule == false ? "WATCH A GAME" : "NEW GAME");
            EnterMenus();
        }

        /// <summary>Everything a menu over the title needs: the game's own screen put away, the map alive, the keys, the model asked.</summary>
        private void EnterMenus()
        {
            ui.HideHud(true);
            EnsureMenuKeys();
            if (backdrop == null && !started) backdrop = StartCoroutine(Backdrop());
            if (ui.ModelStatus == null || ui.ModelStatus.Model != newGame.Model) CheckModel();
        }

        // ---- the map behind the menus ----

        private Coroutine backdrop;

        private IEnumerator Backdrop()
        {
            float began = Time.unscaledTime, nextTurn = 5f;
            int turn = 0;
            while (!started)
            {
                float t = Time.unscaledTime - began;
                DriftCamera(t);
                if (t >= nextTurn && game != null && game.Turns.Count > 0)
                {
                    nextTurn = t + 3.5f;
                    turn = (turn + 1) % game.Turns.Count;
                    PaintOwners(game.Turns[turn].Start);
                }
                yield return null;
            }
            // A game began on this screen: its own state back on the map, the camera where the game wants it.
            backdrop = null;
            ui.HideHud(false);
            if (shown != null) PaintOwners(shown);
            if (cam != null) { if (framedRealm != null) FrameRealm(framedRealm); else FitCamera(); }
        }

        private void PaintOwners(ReplayState s) => map.SetOwners(s.Owners, id => CountryLook.Of(id).Colour, id => CountryLook.Of(id).Slot);

        /// <summary>
        /// The camera closer than a game's, moving on a slow loop over the land, kept to the right of the title's
        /// column so the clear part of the veil shows land and not open sea.
        /// </summary>
        private void DriftCamera(float t)
        {
            if (cam == null) return;
            var land = map.LandWorld;
            float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            float half = land.height * (0.33f + 0.012f * Mathf.Sin(t * 2 * Mathf.PI / 47f));
            half = Mathf.Min(half, land.width * 0.5f / aspect);
            float roomX = Mathf.Max(0, land.width / 2 - half * aspect * 0.55f), roomY = Mathf.Max(0, land.height / 2 - half * 0.8f);
            float x = land.center.x + roomX * 0.8f * Mathf.Sin(t * 2 * Mathf.PI / 113f) - half * aspect * 0.28f;
            float y = land.center.y + roomY * 0.8f * Mathf.Sin(t * 2 * Mathf.PI / 71f + 1.3f);
            cam.orthographicSize = half;
            cam.transform.position = new Vector3(x, y, -10);
        }

        // ---- the keys: up, down and Enter on any menu's big buttons ----

        private Coroutine menuKeys;

        private void EnsureMenuKeys()
        {
            if (menuKeys == null) menuKeys = StartCoroutine(MenuKeys());
        }

        private IEnumerator MenuKeys()
        {
            while (true)
            {
                var kb = Keyboard.current;
                if (kb != null && ui != null && ui.MenuOpen && !ui.Typing && !ui.DevOpen)
                {
                    if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) ui.MenuMove(-1);
                    if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) ui.MenuMove(1);
                    if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) ui.MenuPress();
                }
                yield return null;
            }
        }

        // ---- the model status line ----

        private int modelAsked;

        /// <summary>
        /// Asks the model server which models it has, through the framework's provider (<see cref="IModelLifecycle"/>),
        /// and sets the status line when it answers or does not. The menu never waits for it. While another session
        /// holds the card's lock, the server is not asked at all.
        /// </summary>
        public async void CheckModel()
        {
            string model = string.IsNullOrWhiteSpace(newGame.Model) ? "qwen3.5:4b" : newGame.Model.Trim();
            int mine = ++modelAsked;
            ui.SetModelStatus(ModelStatus.Checking(model));
            ModelStatus status;
            try { status = await AskServer(model); }
            catch (Exception e) { status = ModelStatus.Down(model, e.GetType().Name); }
            if (this == null || ui == null || mine != modelAsked) return;
            ui.SetModelStatus(status);
        }

        /// <summary>For the Unity CLI and the frames: the status line as it stands.</summary>
        public string ModelLine => ui?.ModelStatus?.Line;

        private static async Awaitable<ModelStatus> AskServer(string model)
        {
            if (DevFaults.ServerDown) return ModelStatus.Down(model, "The server is down (pretended by the dev menu).");
            string why = Gateway.WhyNotSet();
            if (why != null) return ModelStatus.Down(model, why, configured: false);
            // The card's users take turns, and while one holds it nothing is sent to its server, not even a question.
            string holder = Gateway.ReadLock()?.ToString() ?? (Gateway.Paused ? "paused for a measurement" : null);
            if (holder != null) return ModelStatus.CardBusy(model, holder);
            var provider = new OllamaProvider(new OllamaSettings
            {
                Endpoint = Environment.GetEnvironmentVariable(Gateway.UrlVariable),
                ApiKey = Environment.GetEnvironmentVariable(Gateway.KeyVariable) ?? "",
                Model = model,
                TimeoutSeconds = 8f,
            });
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8)))
            {
                try
                {
                    var models = await provider.ListModelsAsync(cts.Token);
                    var status = ModelStatus.FromModels(model, models.Select(m => m.Name));
                    return status;
                }
                // The kind and the variable's name only: the provider's own message can carry the server's address.
                catch (ProviderException e) { return ModelStatus.Down(model, $"The server in {Gateway.UrlVariable} did not answer ({e.Kind})."); }
                catch (OperationCanceledException) { return ModelStatus.Down(model, $"The server in {Gateway.UrlVariable} did not answer in 8 s."); }
            }
        }

        // ---- Load a replay ----

        private ReplayLibrary library;

        /// <summary>The finished games, in <c>Replays</c> in the game's data folder.</summary>
        public ReplayLibrary Library => library ?? (library = new ReplayLibrary(Path.Combine(Application.persistentDataPath, "Replays")));

        /// <summary>Set before the scene reloads: the saved game's decisions, played instead of the scripted rulers'.</summary>
        private static Recording pendingReplay;

        /// <summary>The saved recording the next loaded game replays, taken once; null for a game the scripted rulers play.</summary>
        internal static Recording TakeReplay() => Interlocked.Exchange(ref pendingReplay, null);

        /// <summary>True while the game on screen came from the library (or ships with the game): it is not saved again.</summary>
        private static bool playingSaved;

        /// <summary>A generated world's setup as it was loaded, kept so the finished game can be saved with it.</summary>
        private GameSetup loadedSetup;

        /// <summary>For the menu and the Unity CLI: the library's window.</summary>
        public void OpenReplays()
        {
            var list = Library.List();
            if (game?.Result != null && recording != null)
            {
                // The game that ships with Painted Realm, the one on the board behind the menu.
                var s = EndSummary.From(game, CountryLook.NameOf);
                list.Add(new SavedReplay
                {
                    Bundled = true, Title = MenuTitle(), Turns = s.Turns, Winner = s.Won ? CountryLook.NameOf(s.Winner) : null,
                    World = $"Classic board, seed {game.Seed}", Played = "Five model rulers, each with a persona",
                });
            }
            ui.ShowReplays(list, PlaySaved, OpenTitle);
            EnterMenus();
        }

        /// <summary>Plays one of the library's games: at once on the Classic board, or after its world is made again.</summary>
        public void PlaySaved(SavedReplay chosen)
        {
            if (started || chosen == null) return;
            if (chosen.Bundled) { playingSaved = true; StartReplay(); return; }
            var full = Library.Load(chosen.Path);
            if (full == null) { ui.ShowMenuStatus("That replay can no longer be read."); return; }
            try
            {
                var rec = full.Recording != null ? Recording.Parse(full.Recording) : null;
                if (full.Setup == null)
                {
                    // The Classic board's one-move game: the board is already here.
                    if (rec == null) { ui.ShowMenuStatus("That replay holds no game."); return; }
                    var replay = ReplayBuilder.Build(rec, partial: true);
                    sourceRecording = rec;
                    game = replay;
                    ui.Players = game.Players;
                    Show(game.Initial, null);
                    playingSaved = true;
                    StartReplay();
                    return;
                }
                var setup = GameSetup.FromJson(full.Setup);
                NextUnitTypes = full.UnitTypes; NextGenerals = full.Generals; NextSieges = full.Sieges; NextSupply = full.Supply;
                pendingReplay = rec;
                pendingLiveModel = null; pendingRule = null;
                if (setup.Map == GameSetup.GeneratedMapId) pendingWorld = setup; else pendingClassic = setup;
                playingSaved = true;
                LeaveToMenu();
            }
            catch (Exception e) when (e is FormatException || e is Newtonsoft.Json.JsonException || e is InvalidOperationException || e is ArgumentException)
            {
                pendingReplay = null;
                ui.ShowMenuStatus("That replay cannot be played: " + e.Message);
            }
        }

        private bool savedThisGame;

        /// <summary>
        /// A game reached its end: kept for Load a replay, once. A scripted game is kept as its setup and made again;
        /// any other is kept with its decisions. Not kept: a replay itself, a game the dev menu touched, and a game
        /// you ruled with unlimited writs (its rules were yours alone, and a replay would not follow them).
        /// </summary>
        private void SaveFinished(EndSummary summary)
        {
            if (savedThisGame || playingSaved || game?.Result == null || summary == null) return;
            savedThisGame = true;
            try
            {
                var saved = FinishedGame(summary);
                if (saved == null) return;
                string path = Library.Save(saved);
                if (path != null) Debug.Log("Painted Realm: the finished game is kept for Load a replay: " + path);
            }
            catch (Exception e)
            {
                // Keeping a replay must never cost the end screen.
                Debug.LogWarning("Painted Realm: the finished game could not be kept as a replay: " + e.Message);
            }
        }

        private SavedReplay FinishedGame(EndSummary summary)
        {
            if (IsDevGame) return null;
            bool ruled = rule != null, watchedLive = live != null && live.PlayLive;
            if (ruled && rule.Writs && yourWritsUnlimited) return null;
            var saved = new SavedReplay
            {
                Saved = DateTime.UtcNow,
                Turns = summary.Turns,
                Winner = summary.Won && summary.Winner != null ? CountryLook.NameOf(summary.Winner) : null,
                Title = summary.Won && summary.Winner != null ? $"{CountryLook.NameOf(summary.Winner)} wins {summary.How}" : $"A game of {summary.Turns} turns, {summary.How}",
                Played = ruled ? $"You ruled {CountryLook.NameOf(rule.You)}" : watchedLive ? "Watched: model rulers" : "Watched: scripted rulers",
                UnitTypes = game.Rules?.UnitTypes ?? false, Generals = game.Rules?.Generals ?? false,
                Sieges = game.Rules?.Sieges ?? false, Supply = game.Rules?.Supply ?? false,
            };
            GameSetup setup = null;
            if (scripted != null && !ruled && !watchedLive) setup = scripted.Value.setup;
            else
            {
                var rec = CurrentRecording();
                if (rec == null) return null;
                saved.Recording = rec.ToJson();
                if (world != null) setup = loadedSetup;
                else if (game.Writs)
                    setup = GameSetup.Writs(game.Seed, GameSetup.ClassicMapId, game.Rules.Victory == VictoryMode.Domination ? GameSetup.DominationPreset : GameSetup.ConquestPreset);
                // A generated world with no setup to make it from again cannot be replayed.
                if (world != null && setup == null) return null;
            }
            saved.Setup = setup?.ToJson(indented: false);
            saved.World = WorldWords(setup);
            return saved;
        }

        private string WorldWords(GameSetup setup)
        {
            if (setup == null || setup.Map != GameSetup.GeneratedMapId) return $"Classic board, seed {game.Seed}";
            var g = setup.Generator ?? new MapSettings();
            string shape = System.Text.RegularExpressions.Regex.Replace(g.Shape.ToString(), "(?<=[a-z])(?=[A-Z])", " ").ToLowerInvariant();
            return $"{g.Size} world, {shape}, seed {setup.MapSeed ?? setup.Seed}";
        }

        // ---- the pause menu's part ----

        /// <summary>What leaving now would lose, as the pause menu's confirmation says it; null when nothing would be.</summary>
        private string LostByLeaving()
        {
            if (game?.Result != null) return null;
            if (rule != null && !rule.Finished)
                return "This game is not saved. Leaving ends it, and your realm with it; only a game played to its end is kept, under Load a replay.";
            if (live != null && live.PlayLive && !live.Finished)
                return "The rulers are still playing, and this game is not saved. Leaving ends it; only a game played to its end is kept, under Load a replay.";
            return null;
        }
    }
}
