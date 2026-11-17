using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// The start menu (#20): the title screen's buttons and keys, the model status line's sentences, New game's four
    /// groups, the pause menu's confirmation, and the library of finished games behind Load a replay.
    /// </summary>
    public class StartMenuTests
    {
        private string folder;

        [SetUp] public void SetUp() { folder = Path.Combine(Application.temporaryCachePath, "replays-" + Guid.NewGuid().ToString("N")); }
        [TearDown] public void TearDown() { try { Directory.Delete(folder, true); } catch (IOException) { } }

        private static ScreenUI Ui()
        {
            var ui = new ScreenUI(new VisualElement(), ScriptableObject.CreateInstance<StyleSheet>(), null,
                new[] { "kesh", "varn" }, new[] { "a", "b" }, new Dictionary<string, RecordedPlayer>(), "test");
            ui.BuildPanels();
            return ui;
        }

        // ---- the title screen ----

        [Test]
        public void The_title_lists_its_buttons_in_order_and_Continue_only_when_there_is_a_game_to_go_back_to()
        {
            var plain = StartMenu.Buttons(canContinue: false).Select(i => i.Label).ToList();
            Assert.That(plain, Is.EqualTo(new[] { "New game", "Watch a game", "Load a replay", "Settings", "Credits", "Quit" }));
            var with = StartMenu.Buttons(canContinue: true, "Kesh, turn 12").Select(i => i.Label).ToList();
            Assert.That(with.First(), Is.EqualTo("Continue"));
            Assert.That(with.Skip(1), Is.EqualTo(plain), "Continue comes first and changes nothing else");

            var ui = Ui();
            ui.ShowTitle(StartMenu.Buttons(false), _ => { }, "9.9.9");
            Assert.That(ui.TitleOpen, Is.True);
            Assert.That(ui.MenuItems, Is.EqualTo(plain));
            Assert.That(ui.MenuItems, Has.None.EqualTo("Continue"), "hidden, not greyed out");
            var text = ui.MenuText();
            Assert.That(text, Has.Some.EqualTo(StartMenu.Title.ToUpperInvariant()), "the title, from its one constant");
            Assert.That(text, Has.Some.EqualTo(StartMenu.Subtitle));
            Assert.That(text, Has.Some.Contains("9.9.9"), "the framework's version at the foot");
        }

        [Test]
        public void The_keys_walk_the_buttons_wrap_at_both_ends_and_Enter_presses_the_marked_one()
        {
            var ui = Ui();
            var chosen = new List<string>();
            ui.ShowTitle(StartMenu.Buttons(false), chosen.Add, "1");
            Assert.That(ui.MenuSelected, Is.EqualTo("New game"), "the mark starts on the first button");
            ui.MenuMove(1);
            Assert.That(ui.MenuSelected, Is.EqualTo("Watch a game"));
            ui.MenuMove(-1); ui.MenuMove(-1);
            Assert.That(ui.MenuSelected, Is.EqualTo("Quit"), "up from the first is the last");
            ui.MenuMove(1);
            Assert.That(ui.MenuSelected, Is.EqualTo("New game"), "down from the last is the first");
            ui.MenuMove(1); ui.MenuMove(1);
            Assert.That(ui.MenuPress(), Is.True);
            Assert.That(chosen, Is.EqualTo(new[] { StartMenu.Replays }), "Enter pressed Load a replay, once");
            Assert.That(ui.MenuText(), Has.Some.EqualTo(StartMenu.Buttons(false).Single(i => i.Id == StartMenu.Replays).Note), "the marked button's hint is shown");

            ui.HideMenu();
            Assert.That(ui.MenuPress(), Is.False, "no menu, nothing to press");
            Assert.That(ui.MenuItems, Is.Empty);
        }

        [Test]
        public void The_title_puts_the_games_own_screen_away_and_a_game_brings_it_back()
        {
            var ui = Ui();
            int before = ui.Root.Children().Count(c => c.ClassListContains("hud-off"));
            Assert.That(before, Is.EqualTo(0));
            ui.ShowTitle(StartMenu.Buttons(false), _ => { }, "1");
            Assert.That(ui.HudHidden, Is.True);
            var title = ui.Root.Q("title");
            Assert.That(title.ClassListContains("hud-off"), Is.False, "the title itself stays");
            Assert.That(ui.Root.Children().Where(c => c != title).All(c => c.ClassListContains("hud-off")), Is.True, "the top bar, the panels and the map's labels are put away");
            // A window over the title keeps them away.
            ui.ShowCredits("1", () => { });
            Assert.That(ui.Root.Children().Count(c => c.ClassListContains("hud-off")), Is.GreaterThan(3));
            ui.HideMenu();
            ui.HideHud(false);
            Assert.That(ui.Root.Children().Count(c => c.ClassListContains("hud-off")), Is.EqualTo(0));
        }

        // ---- the model status line ----

        [Test]
        public void The_status_line_says_which_model_and_whether_it_answers_in_a_plain_sentence()
        {
            Assert.That(ModelStatus.Checking("qwen3.5:4b").Line, Is.EqualTo("Ollama · qwen3.5:4b · asking the server…"));

            var ready = ModelStatus.FromModels("qwen3.5:4b", new[] { "gemma4:e4b", "qwen3.5:4b" });
            Assert.That(ready.State, Is.EqualTo(ModelState.Ready));
            Assert.That(ready.Line, Is.EqualTo("Ollama · qwen3.5:4b · the model server answers"));
            Assert.That(ready.Tone, Is.EqualTo("good"));
            Assert.That(ModelStatus.FromModels("mistral", new[] { "mistral:latest" }).State, Is.EqualTo(ModelState.Ready), "a name without a tag is its latest");

            var busy = ModelStatus.CardBusy("qwen3.5:4b", "another session (since 21:40)");
            Assert.That(busy.Line, Does.Contain("in use by another session").And.Contain("was not asked").And.Contain("scripted game or a replay"));
            Assert.That(busy.Detail, Does.Contain("another session (since 21:40)"), "who holds it, in the tooltip");
            Assert.That(busy.Tone, Is.EqualTo("warn"));
            Assert.That(busy.CanPlayModels, Is.False);

            var missing = ModelStatus.FromModels("qwen3.5:4b", new[] { "gemma4:e4b" });
            Assert.That(missing.State, Is.EqualTo(ModelState.ModelMissing));
            Assert.That(missing.CanPlayModels, Is.False);
            Assert.That(missing.Line, Does.Contain("no model named qwen3.5:4b").And.Contain("scripted game or a replay"));

            foreach (var down in new[] { ModelStatus.Down("m", "refused"), ModelStatus.Down("m", "no variable", configured: false) })
            {
                Assert.That(down.Line, Is.EqualTo("No model is running: you can still watch a scripted game or a replay"));
                Assert.That(down.Tone, Is.EqualTo("warn"));
                Assert.That(down.CanPlayModels, Is.False);
            }
            Assert.That(ModelStatus.Down("m", "x", configured: false).State, Is.EqualTo(ModelState.NotConfigured));
        }

        [Test]
        public void The_status_line_is_shown_on_the_title_and_in_New_games_Model_group_and_follows_the_check()
        {
            var ui = Ui();
            ui.SetModelStatus(ModelStatus.Checking("qwen3.5:4b"));
            ui.ShowTitle(StartMenu.Buttons(false), _ => { }, "1");
            Assert.That(ui.MenuText(), Has.Some.EqualTo("Ollama · qwen3.5:4b · asking the server…"), "a status set before the title opened");
            ui.SetModelStatus(ModelStatus.Down("qwen3.5:4b", "refused"));
            Assert.That(ui.MenuText(), Has.Some.EqualTo(ModelStatus.NoModelLine));
            Assert.That(ui.MenuText(), Has.None.Contains("asking the server"));

            ui.ShowNewGame(new NewGameChoice { Scripted = false }, _ => { }, () => { });
            Assert.That(ui.MenuText(), Has.Some.EqualTo(ModelStatus.NoModelLine), "New game's Model group carries it too");
            ui.SetModelStatus(ModelStatus.FromModels("qwen3.5:4b", new[] { "qwen3.5:4b" }));
            Assert.That(ui.MenuText(), Has.Some.EqualTo("Ollama · qwen3.5:4b · the model server answers"));
        }

        // ---- New game ----

        [Test]
        public void New_game_is_one_window_with_four_groups_and_Start_whichever_is_open()
        {
            var ui = Ui();
            NewGameChoice started = null;
            var choice = new NewGameChoice { Rule = false };
            ui.ShowNewGame(choice, c => started = c, () => { }, "WATCH A GAME");
            var (tabs, open) = ui.NewGameTabs();
            Assert.That(tabs, Is.EqualTo(new[] { "World", "Rivals", "Rules", "Model" }));
            Assert.That(open, Is.EqualTo("World"));
            Assert.That(ui.MenuText(), Has.Some.EqualTo("WATCH A GAME"));

            // Each option sits in its own group.
            var groups = ui.Root.Query(className: "ng-group").ToList();
            Assert.That(groups.Count, Is.EqualTo(4));
            List<string> In(int g) => groups[g].Query<Label>().ToList().Select(l => l.text).ToList();
            Assert.That(In(0), Has.Some.EqualTo("Size").And.Some.EqualTo("Shape").And.Some.EqualTo("Countries").And.Some.EqualTo("SEED"));
            Assert.That(In(1), Has.Some.EqualTo("Rulers").And.Some.EqualTo("Advisor").And.Some.EqualTo("THEIR PERSONAS").And.Some.EqualTo("Warmonger"));
            Assert.That(In(1), Has.Some.EqualTo("Your face"));
            Assert.That(groups[1].Query<Button>(className: "face-pick").ToList().Count, Is.EqualTo(ArtCatalogue.PlayerFaces), "the four neutral faces");
            Assert.That(In(2), Has.Some.EqualTo("Victory").And.Some.EqualTo("Sieges").And.Some.EqualTo("Storyteller"));
            Assert.That(In(3), Has.Some.EqualTo("THE MODEL"));
            Assert.That(In(0), Has.None.EqualTo("Victory"));

            Assert.That(ui.ShowNewGameTab("Rules"), Is.True);
            Assert.That(ui.NewGameTabs().open, Is.EqualTo("Rules"));
            Assert.That(groups.Count(g => g.style.display == DisplayStyle.Flex), Is.EqualTo(1), "one group at a time");
            Assert.That(ui.ShowNewGameTab("Weather"), Is.False);

            var start = ui.Root.Query<Button>().ToList().Single(b => b.text == "Start");
            for (var up = start.parent; up != null; up = up.parent) Assert.That(up.ClassListContains("ng-group"), Is.False, "Start is outside the groups");
            ui.HideMenu();
            Assert.That(ui.ShowNewGameTab("World"), Is.False, "no window, no tab");
        }

        // ---- the pause menu ----

        [Test]
        public void The_pause_menu_asks_before_leaving_a_game_under_way_and_not_otherwise()
        {
            var ui = Ui();
            int left = 0, quit = 0, resumed = 0;
            ui.ShowPause(() => resumed++, () => { }, () => left++, null, null, null, () => quit++, underWay: null);
            Assert.That(ui.MenuItems, Is.EqualTo(new[] { "Resume", "Settings", "Back to the start menu", "Quit" }));
            ui.MenuMove(2);
            ui.MenuPress();
            Assert.That(left, Is.EqualTo(1), "a replay loses nothing: no question");

            ui.ShowPause(() => resumed++, () => { }, () => left++, null, null, null, () => quit++, underWay: "This game is not saved.");
            ui.MenuMove(2);
            ui.MenuPress();
            Assert.That(left, Is.EqualTo(1), "a game under way: asked first");
            Assert.That(ui.MenuText(), Has.Some.EqualTo("LEAVE THIS GAME?").And.Some.EqualTo("This game is not saved."));
            Assert.That(ui.MenuSelected, Is.EqualTo("Stay in the game"), "the safe answer is under the keys");
            ui.MenuBack();
            Assert.That(ui.MenuItems, Has.Some.EqualTo("Resume"), "Esc goes back to the pause menu");
            ui.MenuMove(3);
            ui.MenuPress();
            Assert.That(ui.MenuText(), Has.Some.EqualTo("QUIT THE GAME?"));
            ui.MenuMove(1);
            ui.MenuPress();
            Assert.That(quit, Is.EqualTo(1));
            Assert.That(left, Is.EqualTo(1));
        }

        // ---- Load a replay ----

        private static SavedReplay Game(DateTime saved, string title, int turns = 30, string winner = "Kesh") =>
            new SavedReplay { Saved = saved, Title = title, Turns = turns, Winner = winner, World = "Classic board, seed 4", Played = "Watched: scripted rulers", Recording = "{\"seed\":4}" };

        [Test]
        public void Finished_games_are_listed_newest_first_with_date_turns_and_winner()
        {
            var lib = new ReplayLibrary(folder);
            Assert.That(lib.List(), Is.Empty, "no folder yet");
            var day = new DateTime(2026, 11, 16, 20, 0, 0, DateTimeKind.Utc);
            lib.Save(Game(day, "first"));
            lib.Save(Game(day.AddHours(2), "third", 63));
            lib.Save(Game(day.AddHours(1), "second", 1, winner: null));
            var list = lib.List();
            Assert.That(list.Select(r => r.Title), Is.EqualTo(new[] { "third", "second", "first" }));
            Assert.That(list[0].Recording, Is.Null, "the list reads no recording");
            Assert.That(list[0].Line(d => d), Is.EqualTo("16 Nov 2026, 22:00 · 63 turns · Kesh won"));
            Assert.That(list[1].Line(d => d), Is.EqualTo("16 Nov 2026, 21:00 · 1 turn · nobody won"));

            var whole = lib.Load(list[0].Path);
            Assert.That(whole.Recording, Is.EqualTo("{\"seed\":4}"), "loading one reads it whole");
            Assert.That(whole.Saved, Is.EqualTo(day.AddHours(2)));
            Assert.That(Directory.GetFiles(folder, "*.tmp"), Is.Empty);
        }

        [Test]
        public void A_file_that_is_not_a_replay_is_left_out_and_two_games_in_one_second_are_both_kept()
        {
            var lib = new ReplayLibrary(folder);
            var at = new DateTime(2026, 11, 16, 20, 0, 0, DateTimeKind.Utc);
            string a = lib.Save(Game(at, "one")), b = lib.Save(Game(at, "two"));
            Assert.That(a, Is.Not.EqualTo(b));
            File.WriteAllText(Path.Combine(folder, "half.json"), "{ \"Version\": 1, \"Title\": ");
            File.WriteAllText(Path.Combine(folder, "other.json"), "{ \"hello\": 1 }");
            File.WriteAllText(Path.Combine(folder, "old.json"), "{ \"Version\": 0, \"Title\": \"from another version\" }");
            Assert.That(lib.List().Select(r => r.Title), Is.EquivalentTo(new[] { "one", "two" }));
            Assert.That(lib.Load(Path.Combine(folder, "half.json")), Is.Null);
            Assert.That(lib.Load(Path.Combine(folder, "gone.json")), Is.Null);
        }

        [Test]
        public void Only_the_newest_games_are_kept()
        {
            var lib = new ReplayLibrary(folder);
            var at = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < ReplayLibrary.Keep + 3; i++) lib.Save(Game(at.AddMinutes(i), "game " + i));
            var list = lib.List();
            Assert.That(list.Count, Is.EqualTo(ReplayLibrary.Keep));
            Assert.That(list.First().Title, Is.EqualTo("game " + (ReplayLibrary.Keep + 2)));
            Assert.That(list.Last().Title, Is.EqualTo("game 3"), "the three oldest went");
        }

        [Test]
        public void The_replays_window_lists_them_with_the_one_that_ships_and_plays_the_chosen_one()
        {
            var ui = Ui();
            var at = new DateTime(2026, 11, 16, 20, 0, 0, DateTimeKind.Utc);
            var mine = Game(at, "Varn wins by conquest, the last realm standing", 80, "Varn");
            var shipped = new SavedReplay { Bundled = true, Title = "Kesh wins by holding the land the rules ask", Turns = 17, Winner = "Kesh" };
            SavedReplay played = null;
            ui.ShowReplays(new[] { mine, shipped }, r => played = r, () => { });
            Assert.That(ui.MenuItems, Is.EqualTo(new[] { mine.Title, shipped.Title }));
            var text = ui.MenuText();
            Assert.That(text, Has.Some.Contains("80 turns · Varn won"));
            Assert.That(text, Has.Some.EqualTo("Ships with the game · 17 turns · Kesh won"));
            ui.MenuMove(1);
            ui.MenuPress();
            Assert.That(played, Is.SameAs(shipped));

            ui.ShowReplays(new[] { shipped }, _ => { }, () => { });
            Assert.That(ui.MenuText(), Has.Some.StartsWith("No game of yours is saved yet"));
        }

        [Test]
        public void A_finished_scripted_game_saved_as_its_setup_is_the_same_game_when_made_again()
        {
            // What Load a replay does with a scripted game: keep the setup, play it again, get the same ending.
            var setup = GameSetup.Writs(3);
            var map = setup.BuildMap();
            var rules = ReplayDirector.WithArmySettings(setup.BuildRules(map), true, true, true, true);
            var first = ReplayBuilder.Build(ScriptedWorldGame.Play(setup, map, rules), map, rules);

            var lib = new ReplayLibrary(folder);
            string path = lib.Save(new SavedReplay { Saved = DateTime.UtcNow, Title = "t", Turns = first.Result.Turns, Setup = setup.ToJson(indented: false), UnitTypes = true, Generals = true, Sieges = true, Supply = true });
            var back = lib.Load(path);
            var again = GameSetup.FromJson(back.Setup);
            var map2 = again.BuildMap();
            var rules2 = ReplayDirector.WithArmySettings(again.BuildRules(map2), back.UnitTypes, back.Generals, back.Sieges, back.Supply);
            var second = ReplayBuilder.Build(ScriptedWorldGame.Play(again, map2, rules2), map2, rules2);
            Assert.That(second.Result.Winner, Is.EqualTo(first.Result.Winner));
            Assert.That(second.Result.Turns, Is.EqualTo(first.Result.Turns));
            Assert.That(second.Turns.Last().Start.Owners, Is.EqualTo(first.Turns.Last().Start.Owners));
        }

        [Test]
        public void A_saved_recording_replays_to_the_same_ending()
        {
            // What Load a replay does with any other game: its decisions, parsed and built on the same map and rules.
            var setup = GameSetup.Writs(5);
            var map = setup.BuildMap();
            var rules = ReplayDirector.WithArmySettings(setup.BuildRules(map), true, true, true, true);
            var rec = ScriptedWorldGame.Play(setup, map, rules);
            var first = ReplayBuilder.Build(rec, map, rules);
            var lib = new ReplayLibrary(folder);
            string path = lib.Save(new SavedReplay { Saved = DateTime.UtcNow, Title = "t", Turns = first.Result.Turns, Setup = setup.ToJson(indented: false), Recording = rec.ToJson() });
            var second = ReplayBuilder.Build(Recording.Parse(lib.Load(path).Recording), map, rules, partial: true);
            Assert.That(second.Result?.Winner, Is.EqualTo(first.Result.Winner));
            Assert.That(second.Turns.Count, Is.EqualTo(first.Turns.Count));
        }

        // ---- Credits and Settings ----

        [Test]
        public void Credits_name_the_team_the_frameworks_version_and_the_licence()
        {
            var ui = Ui();
            ui.ShowCredits(Agenerela.AgenerelaInfo.Version, () => { });
            var text = ui.MenuText();
            foreach (var (name, _) in StartMenu.Team) Assert.That(text, Has.Some.EqualTo(name));
            Assert.That(StartMenu.Team.Length, Is.EqualTo(6));
            Assert.That(text, Has.Some.Contains(Agenerela.AgenerelaInfo.Version));
            Assert.That(text, Has.Some.Contains("MIT License"));
            Assert.That(ui.MenuBack, Is.Not.Null, "Esc goes back");
        }

        [Test]
        public void Event_windows_is_a_setting_that_is_kept_and_off_until_chosen()
        {
            Assert.That(new GameSettings().EventWindows, Is.False, "as the game was: a banner that waits for a click");
            string path = Path.Combine(folder, "settings.json");
            new SettingsStore(path).Save(new GameSettings { EventWindows = true });
            Assert.That(new SettingsStore(path).Load().EventWindows, Is.True);

            var ui = Ui();
            GameSettings changed = null;
            ui.ShowSettings(new GameSettings(), new GreyBoxStrategy.Realm.DisplayInfo { InEditor = true, Native = new Vector2Int(1920, 1080) }, s => changed = s, () => { });
            Assert.That(ui.MenuText(), Has.Some.EqualTo("Event windows"));
            Assert.That(changed, Is.Null);
        }
    }
}
