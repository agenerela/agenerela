using System;
using System.Collections.Generic;

namespace GreyBoxStrategy.Replay
{
    /// <summary>
    /// The start menu's words and choices as plain data, so the screen draws them and the tests read them: the
    /// title, the buttons in order, the credits, and the model status line.
    /// </summary>
    public static class StartMenu
    {
        /// <summary>The game's name. A working title: change it here and every screen follows.</summary>
        public const string Title = "Painted Realm";

        public const string Subtitle = "Realms ruled by language models, built on the Agenerela framework";

        public const string Continue = "continue", NewGame = "new", Watch = "watch", Replays = "replays", Settings = "settings", Credits = "credits", Quit = "quit";

        public sealed class Item
        {
            public string Id, Label, Note;
        }

        /// <summary>
        /// The title screen's buttons, top to bottom. Continue is listed only when there is a game to go back to;
        /// <paramref name="continueNote"/> says which.
        /// </summary>
        public static List<Item> Buttons(bool canContinue, string continueNote = null)
        {
            var items = new List<Item>();
            if (canContinue) items.Add(new Item { Id = Continue, Label = "Continue", Note = continueNote ?? "Go back to the game you left" });
            items.Add(new Item { Id = NewGame, Label = "New game", Note = "Rule a realm against model rulers: choose the world, your rivals, the rules and the model" });
            items.Add(new Item { Id = Watch, Label = "Watch a game", Note = "Model rulers play each other; you follow, pause and read every decision" });
            items.Add(new Item { Id = Replays, Label = "Load a replay", Note = "Finished games, newest first; they play without a model" });
            items.Add(new Item { Id = Settings, Label = "Settings", Note = "Window, UI scale, sound, event windows" });
            items.Add(new Item { Id = Credits, Label = "Credits", Note = "The team, the framework and the licence" });
            items.Add(new Item { Id = Quit, Label = "Quit", Note = null });
            return items;
        }

        /// <summary>The team, from the build plan's section 6.6: each person's research area, then the feature they built first.</summary>
        public static readonly (string Name, string Part)[] Team =
        {
            ("Yevhen Mishchenko", "Team lead · the framework core and the demo games"),
            ("Hero Jaiyen", "Memory · targets and target grounding"),
            ("Maxim Goloubitsky", "Targets · decision telemetry"),
            ("Timothy Brustinov", "Evaluation · the action vocabulary"),
            ("Hunter Hudson", "Observations · agents and decision requests"),
            ("Tigran Kolsuzyan", "Guards · action availability and the decision schema"),
        };

        public const string Course = "A senior design project, CSUN COMP 490 / 491";
        public const string Licence = "MIT License · Copyright (c) 2026 the Agenerela contributors";

        /// <summary>The line under the title on the credits page, with the framework's version.</summary>
        public static string FrameworkLine(string version) => $"Agenerela framework {version}";
    }

    /// <summary>What the start menu found when it asked the model server, without ever holding the menu up.</summary>
    public enum ModelState
    {
        /// <summary>Still asking.</summary>
        Checking,
        /// <summary>The server answers and has the model.</summary>
        Ready,
        /// <summary>The server answers, but the model is not installed on it.</summary>
        ModelMissing,
        /// <summary>The server did not answer, or refused.</summary>
        NoServer,
        /// <summary>No server is set in the environment the game was started from.</summary>
        NotConfigured,
        /// <summary>The card the server runs on is in use by someone else, so the server was not asked at all.</summary>
        CardBusy,
    }

    /// <summary>The model status line at the foot of the start menu.</summary>
    public sealed class ModelStatus
    {
        /// <summary>What a player can still do with no model, said the same way everywhere.</summary>
        public const string NoModelLine = "No model is running: you can still watch a scripted game or a replay";

        public ModelState State;
        /// <summary>"Ollama", the provider's own name.</summary>
        public string Provider = "Ollama";
        public string Model;
        /// <summary>Why, in the provider's words, for the tooltip. Never a key or an address.</summary>
        public string Detail;
        /// <summary>Who holds the card, for <see cref="ModelState.CardBusy"/>.</summary>
        public string Busy;

        public bool CanPlayModels => State == ModelState.Ready;

        /// <summary>One plain sentence.</summary>
        public string Line
        {
            get
            {
                switch (State)
                {
                    case ModelState.Checking: return $"{Provider} · {Model} · asking the server…";
                    case ModelState.Ready: return $"{Provider} · {Model} · the model server answers";
                    case ModelState.CardBusy: return $"{Provider} · {Model} · its card is in use by another session, so the server was not asked. You can still watch a scripted game or a replay";
                    case ModelState.ModelMissing: return $"{Provider} answers, but it has no model named {Model}: choose another in New game. You can still watch a scripted game or a replay";
                    default: return NoModelLine;
                }
            }
        }

        /// <summary>"good", "warn" or "muted": the status dot's colour class.</summary>
        public string Tone => State == ModelState.Ready ? "good" : State == ModelState.Checking ? "muted" : "warn";

        public static ModelStatus Checking(string model) => new ModelStatus { State = ModelState.Checking, Model = model };

        /// <summary>From the server's list of installed models: ready when it has this one (with or without a tag's default).</summary>
        public static ModelStatus FromModels(string model, IEnumerable<string> installed)
        {
            bool has = false;
            foreach (string name in installed ?? Array.Empty<string>())
                if (string.Equals(name, model, StringComparison.OrdinalIgnoreCase) || string.Equals(name, model + ":latest", StringComparison.OrdinalIgnoreCase)) has = true;
            return new ModelStatus { State = has ? ModelState.Ready : ModelState.ModelMissing, Model = model };
        }

        /// <summary>The card is someone else's for now: nothing is sent to the server while it is.</summary>
        public static ModelStatus CardBusy(string model, string holder) => new ModelStatus { State = ModelState.CardBusy, Model = model, Busy = holder, Detail = "The RTX 5080's lock is held: " + holder };

        public static ModelStatus Down(string model, string why, bool configured = true) =>
            new ModelStatus { State = configured ? ModelState.NoServer : ModelState.NotConfigured, Model = model, Detail = why };
    }
}
