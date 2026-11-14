using System.IO;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The menus and the settings: the main menu is the Watch scene's first state, so the painted
    /// map stays behind it and "back to the main menu" is a reload of the one scene. Settings are a
    /// small JSON file, <c>settings.json</c> in <c>Application.persistentDataPath</c>.
    /// </summary>
    public sealed partial class ReplayDirector
    {
        private SettingsStore settingsStore;
        private GameSettings settings;
        private bool pausedByMenu;

        public GameSettings Settings => settings;
        public string SettingsPath => settingsStore?.Path;

        private void LoadSettings()
        {
            settingsStore = new SettingsStore(Path.Combine(Application.persistentDataPath, "settings.json"));
            settings = settingsStore.Load();
            speed = settings.PlaybackSpeed;
            ApplySettings(settings, null);
        }

        /// <summary>The UI scale now: the settings' fixed step, or automatic from the screen's size.</summary>
        private float CurrentUiScale() => settings != null && settings.UiScale > 0 ? settings.UiScale : UiScale.For(Screen.width, Screen.height);

        private void ApplyScale()
        {
            if (panelSettings == null) return;
            float scale = CurrentUiScale();
            panelSettings.scale = scale;
            ui?.SetCompact(Screen.width / scale < 1600);
            if (cam != null) { if (framedRealm != null) FrameRealm(framedRealm); else FitCamera(); }
        }

        /// <summary>Applies settings at once. The display (a built game only) changes only when it differs from <paramref name="before"/>.</summary>
        private void ApplySettings(GameSettings s, GameSettings before)
        {
            QualitySettings.vSyncCount = s.VSync ? 1 : 0;
            Application.targetFrameRate = s.FrameCap > 0 ? s.FrameCap : -1;
            ApplyScale();
            if (!Application.isEditor && s.DisplayDiffers(before))
            {
                var mode = s.Display == DisplayMode.Windowed ? FullScreenMode.Windowed
                    : s.Display == DisplayMode.Borderless ? FullScreenMode.FullScreenWindow : FullScreenMode.ExclusiveFullScreen;
                int w = s.Width > 0 ? s.Width : Screen.currentResolution.width, h = s.Height > 0 ? s.Height : Screen.currentResolution.height;
                Screen.SetResolution(w, h, mode);
            }
        }

        /// <summary>A change from the settings page: applied and saved at once; a display change waits for Keep, or reverts.</summary>
        private void SettingsChanged(GameSettings next)
        {
            var before = settings;
            settings = next.Normalised();
            ApplySettings(settings, before);
            if (!settings.DisplayDiffers(before) || Application.isEditor)
            {
                settingsStore.Save(settings);
                if (settings.PlaybackSpeed != before.PlaybackSpeed) SetSpeed(settings.PlaybackSpeed);
                return;
            }
            ui.ShowDisplayConfirm(12,
                keep: () => settingsStore.Save(settings),
                revert: () =>
                {
                    var bad = settings;
                    settings = before;
                    ApplySettings(settings, bad);
                    ui.ShowNotice("The display went back to how it was.");
                    OpenSettings(lastSettingsBack);
                });
        }

        private System.Action lastSettingsBack;

        private DisplayInfo Display()
        {
            var info = new DisplayInfo
            {
                InEditor = Application.isEditor,
                Native = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height),
                AutoScale = UiScale.For(Screen.width, Screen.height),
            };
            info.Resolutions = Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).Distinct()
                .Where(r => r.x >= 1280 && r.y >= 720).OrderBy(r => r.x).ThenBy(r => r.y).ToList();
            return info;
        }

        /// <summary>For the Unity CLI and the menus: the settings page, returning to <paramref name="back"/>.</summary>
        public void OpenSettings(System.Action back)
        {
            lastSettingsBack = back;
            ui.ShowSettings(settings, Display(), SettingsChanged, back);
        }

        // ---- the pages ----

        private readonly NewGameChoice newGame = new NewGameChoice();

        /// <summary>The game's first screen.</summary>
        public void ShowMainMenu()
        {
            ui.ShowMainMenu(game.Title, ShowNewGame, StartReplay, () => OpenSettings(ShowMainMenu), Quit);
        }

        public void ShowNewGame()
        {
            if (live != null) { newGame.Model = live.Model; }
            ui.ShowNewGame(newGame, StartNewGame, ShowMainMenu);
        }

        private void StartNewGame(NewGameChoice c)
        {
            string arm = StoryArms.For(c.Storyteller);
            yourWritsUnlimited = c.YourWritsUnlimited;
            switch (c.Route(out string why))
            {
                case NewGameRoute.None: ui.ShowMenuStatus(why); return;
                case NewGameRoute.WorldRule: StartGeneratedRule(c.Size, c.Shape, c.Seed, c.CountriesFor(), c.Slot, c.Model, c.Scripted, c.Victory, arm); return;
                case NewGameRoute.WorldReplay: StartGeneratedWatch(c.Size, c.Shape, c.Seed, c.CountriesFor(), c.Writs, c.Victory); return;
                // Model rulers on the RTX 5080: a court and a marshal for every country.
                case NewGameRoute.WorldLive: StartGeneratedLive(c.Size, c.Seed, c.Model, c.Victory, 0, arm, c.Shape, c.CountriesFor()); return;
                case NewGameRoute.ClassicWritsRule: TryRuleWrits(c.Country, c.Model, c.Seed, c.Victory, arm); return;
                case NewGameRoute.ClassicWritsRuleScripted: ui.HideMenu(); StartRuleScriptedWrits(c.Country, c.Seed, c.Victory, arm); return;
                case NewGameRoute.ClassicWritsReplay: StartWritsWatch(c.Seed, c.Victory); return;
                case NewGameRoute.ClassicWritsLive: TryLiveWrits(c.Model, c.Seed, c.Victory, false, 0, arm); return;
                case NewGameRoute.ClassicRule: TryRule(c.Country, c.Model, c.Seed, arm); return;
                case NewGameRoute.ClassicRuleScripted: ui.HideMenu(); StartRuleScripted(c.Country, c.Seed, 0, arm); return;
                case NewGameRoute.ClassicReplay: StartReplay(); return;
                case NewGameRoute.ClassicLive: TryLive(c.Model, c.Seed, arm); return;
            }
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---- the pause menu: Esc, or Menu in the top bar ----

        /// <summary>Opens the pause menu over a game in progress.</summary>
        public void OpenPause()
        {
            if (!started) return;
            if (!ui.MenuOpen) { pausedByMenu = playing; playing = false; ui.SetPlaying(false, speed); }
            ui.ShowPause(ResumeFromPause, () => OpenSettings(OpenPause), LeaveToMenu, DevTools.Available ? "F1 opens the dev menu (editor and development builds)" : null);
        }

        private void ResumeFromPause()
        {
            ui.HideMenu();
            if (pausedByMenu) { playing = true; ui.SetPlaying(true, speed); }
            pausedByMenu = false;
        }

        /// <summary>
        /// Esc, the same everywhere: it closes what is on top (the display countdown is left alone,
        /// it reverts by itself), then goes back one page, then opens the pause menu in a game.
        /// </summary>
        private void Escape()
        {
            if (ui.DisplayConfirmOpen) return;
            if (ui.MenuOpen) { ui.MenuBack?.Invoke(); return; }
            // The choice card closes only by choosing: Esc opens the pause menu over it.
            if (ui.EventOpen) { ui.CloseEvent(); return; }
            if (ui.BattleOpen) { ui.CloseBattle(); return; }
            if (ui.TalesOpen) { ui.CloseTales(); return; }
            if (ui.InspectorOpen || ui.SideOpen || ui.DiplomacyOpen)
            {
                ui.CloseInspector(); ui.CloseSide(); ui.CloseDiplomacy(); map.SetSelected(-1);
                return;
            }
            OpenPause();
        }
    }
}
