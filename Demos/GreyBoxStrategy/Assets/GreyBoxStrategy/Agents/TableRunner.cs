using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Agenerela;
using Agenerela.Providers;
using UnityEngine;

namespace GreyBoxStrategy
{
    /// <summary>Who sits in one seat of a table: a model by its name, or a scripted or random player by its kind.</summary>
    public sealed class SeatPlan
    {
        private SeatPlan(string kind, string model)
        {
            Kind = kind;
            Model = model;
        }

        /// <summary><see cref="ModelGameRunner.Model"/> for a model seat, otherwise a <see cref="Lineups"/> kind.</summary>
        public string Kind { get; }
        /// <summary>The model's name for a model seat ("qwen3.5:4b"), otherwise null.</summary>
        public string Model { get; }
        public bool IsModel => Kind == ModelGameRunner.Model;
        /// <summary>What the scorecard groups by: the model's name, or the kind.</summary>
        public string Label => IsModel ? Model : Kind;

        public static SeatPlan ForModel(string model) => new SeatPlan(ModelGameRunner.Model,
            string.IsNullOrWhiteSpace(model) ? throw new ArgumentException("A model seat needs a model name.") : model);

        public static SeatPlan ForKind(string kind)
        {
            Lineups.Make(kind); // throws for an unknown kind
            return new SeatPlan(kind, null);
        }

        /// <summary>A kind (<c>expansionist</c>, <c>cautious</c>, <c>random</c>) or else a model name.</summary>
        public static SeatPlan Parse(string seat)
        {
            seat = seat?.Trim();
            return seat == Lineups.Expansionist || seat == Lineups.Cautious || seat == Lineups.Random ? ForKind(seat) : ForModel(seat);
        }

        public override string ToString() => Label;
    }

    /// <summary>
    /// A table: five seats, each a model, a script or random, and optionally a persona per seat.
    /// Seats rotate as every series here does, seat i playing country <c>(i + game) mod 5</c>'s
    /// turn order, so over five games each seat rules every country once. Personas rotate on their
    /// own, persona <c>(country + 2 × game) mod 5</c>, so on a table of two models each persona is
    /// played by each model, and each persona rules every country, every five games.
    /// </summary>
    public sealed class TableDefinition
    {
        public TableDefinition(string name, IReadOnlyList<SeatPlan> seats, bool personas, ModelSeatOptions options = null, string note = null)
        {
            if (seats == null || seats.Count != 5) throw new ArgumentException("A table has five seats.", nameof(seats));
            Name = name;
            Seats = seats;
            UsesPersonas = personas;
            Options = options ?? ModelSeatOptions.Default;
            Note = note;
        }

        public string Name { get; }
        public IReadOnlyList<SeatPlan> Seats { get; }
        /// <summary>Whether model seats play <see cref="Personas.All"/>; otherwise stage 2's identity.</summary>
        public bool UsesPersonas { get; }
        public ModelSeatOptions Options { get; }
        /// <summary>A line for the scorecard, such as "reference point: no model".</summary>
        public string Note { get; }

        public IEnumerable<string> Models => Seats.Where(s => s.IsModel).Select(s => s.Model).Distinct();

        /// <summary>Who plays country <paramref name="country"/> (its index in the map) in game <paramref name="game"/> of a series.</summary>
        public SeatPlan SeatFor(int country, int game) => Seats[(country + game) % Seats.Count];

        /// <summary>The persona slot of a country in a game: a persona's index, or the placebo slot of a table without them.</summary>
        public static int SlotFor(int country, int game) => (country + 2 * game) % 5;

        /// <summary>The persona of a model seat, or null.</summary>
        public Persona PersonaFor(int country, int game) =>
            UsesPersonas && SeatFor(country, game).IsModel ? Personas.All[SlotFor(country, game)] : null;

        /// <summary>
        /// What the scorecard's character section groups this seat by: its persona; for a model seat
        /// with none, a placebo slot rotating exactly as the personas would ("slot 3"), so a table
        /// without personas shows how much the same seats differ by chance; otherwise the kind.
        /// </summary>
        public string CharacterFor(int country, int game)
        {
            var seat = SeatFor(country, game);
            if (!seat.IsModel) return seat.Kind;
            var persona = PersonaFor(country, game);
            return persona != null ? persona.Id : $"slot {SlotFor(country, game) + 1}";
        }
    }

    /// <summary>The tables the demo runs, by name.</summary>
    public static class Tables
    {
        /// <summary>
        /// A table by name: <c>models</c> (five seats of model A, stage 2's identity),
        /// <c>personas</c> (five of model A, one persona each), <c>two-models</c> (A, B, A, B, A, with
        /// personas), <c>scripted</c> and <c>random</c> (the two reference points, no model), and
        /// <c>model-vs-scripted</c> (stage 2's line-up: A against two expansionists and two cautious).
        /// Any other name is a comma-separated list of five seats, each a kind or a model name, as in
        /// <c>qwen3.5:4b,qwen3.5:4b,expansionist,cautious,random</c>; <paramref name="personas"/> then
        /// says whether its model seats play personas. Null for an unknown name.
        /// </summary>
        public static TableDefinition Named(string name, string modelA, string modelB = null, string arm = "default", bool? personas = null)
        {
            var options = ModelSeatOptions.Named(arm) ?? throw new ArgumentException($"Unknown arm '{arm}'.");
            SeatPlan A() => SeatPlan.ForModel(modelA);
            SeatPlan B() => SeatPlan.ForModel(modelB ?? throw new ArgumentException("two-models needs a second model."));
            SeatPlan K(string kind) => SeatPlan.ForKind(kind);
            switch (name)
            {
                case "models":
                    return new TableDefinition(Title(name, modelA, null, arm), new[] { A(), A(), A(), A(), A() }, personas ?? false, options);
                case "personas":
                    return new TableDefinition(Title(name, modelA, null, arm), new[] { A(), A(), A(), A(), A() }, personas ?? true, options);
                case "two-models":
                    return new TableDefinition(Title(name, modelA, modelB, arm), new[] { A(), B(), A(), B(), A() }, personas ?? true, options);
                case "model-vs-scripted":
                    return new TableDefinition(Title(name, modelA, null, arm),
                        new[] { A(), K(Lineups.Expansionist), K(Lineups.Cautious), K(Lineups.Expansionist), K(Lineups.Cautious) }, personas ?? false, options);
                case "scripted":
                    return new TableDefinition("scripted", Lineups.Named["scripted"].Select(K).ToArray(), false, options,
                        "**Reference point, no model:** three scripted expansionists and two scripted cautious players.");
                case "random":
                    return new TableDefinition("random", Lineups.Named["random"].Select(K).ToArray(), false, options,
                        "**Reference point, no model:** five random players.");
            }
            if (name == null || !name.Contains(",")) return null;
            var seats = name.Split(',').Select(SeatPlan.Parse).ToArray();
            return new TableDefinition(name + (personas == true ? " (personas)" : "") + (seats.Any(s => s.IsModel) ? $" ({arm})" : ""),
                seats, personas ?? false, options);
        }

        private static string Title(string name, string a, string b, string arm) =>
            $"{name}: {a}{(b != null ? " and " + b : "")} ({arm})";
    }

    /// <summary>One game of a table, with who sat where.</summary>
    public sealed class TableGame
    {
        public TableGame(ModelGameRecord game, IReadOnlyDictionary<string, string> labels, IReadOnlyDictionary<string, string> characters,
            IReadOnlyList<StorytellerDecision> storyteller = null)
        {
            Game = game;
            Labels = labels;
            Characters = characters;
            Storyteller = storyteller ?? Array.Empty<StorytellerDecision>();
        }

        /// <summary>Every call to the game's storyteller, or none when the arm has no storyteller.</summary>
        public IReadOnlyList<StorytellerDecision> Storyteller { get; }

        public ModelGameRecord Game { get; }
        public GameRecord Record => Game.Record;
        /// <summary>Per country: the model's name or the kind.</summary>
        public IReadOnlyDictionary<string, string> Labels { get; }
        /// <summary>Per country: the persona, the placebo slot or the kind.</summary>
        public IReadOnlyDictionary<string, string> Characters { get; }

        public ScoredGame ToScored() => new ScoredGame(Record, Labels, Characters, Game.Decisions.Where(d => d.Moment != Moment.Choice).Select(Info).ToList(),
            Storyteller.Select(s => new StorytellerCallInfo
            {
                Turn = s.Turn,
                Answered = s.ModelAction != null,
                AskedAction = s.ModelAction,
                AskedLegal = s.ModelAnswerLegal,
                Contained = s.Contained,
                Error = s.Error != null,
                None = s.Action == null || s.Action == ActionRegistry.None,
                Queued = s.Queued,
                LatencySeconds = s.LatencySeconds,
                TitleLatencySeconds = s.TitleLatencySeconds,
            }).ToList(), Game.Decisions.Where(d => d.Moment == Moment.Choice).Select(Info).ToList());

        private static SeatDecisionInfo Info(ModelDecision d) => new SeatDecisionInfo
        {
            Seed = d.Seed,
            Turn = d.Turn,
            Country = d.Country,
            Moment = d.Moment,
            Answered = d.ModelAction != null,
            AskedAction = d.ModelAction,
            AskedTarget = d.ModelTarget,
            AskedLegal = d.ModelAnswerLegal,
            Contained = d.Contained,
            Statement = d.Statement,
            OfferedBy = d.OfferedBy,
            StatementRules = d.Persona == null ? null : Personas.Named(d.Persona).StatementRules,
            LatencySeconds = d.LatencySeconds,
        };
    }

    /// <summary>Plays tables: any seat a model, a script or random, every model seat's moves decided in rounds.</summary>
    public static class TableRunner
    {
        /// <summary>
        /// Plays game <paramref name="gameIndex"/> of a series on <paramref name="seed"/>.
        /// <paramref name="providerFor"/> gives the provider for a model's name, asked once per model
        /// seat with the game's world; a live run returns one instance per model, so seats of the
        /// same model share its queue, as agents on one backend should.
        /// </summary>
        public static async Awaitable<TableGame> PlayAsync(TableDefinition table, int seed, int gameIndex,
            Func<string, World, ILLMProvider> providerFor, CancellationToken ct = default)
        {
            var map = MapDefinition.Standard();
            var game = new Game(seed, map);
            var players = new Dictionary<string, IPlayer>(StringComparer.Ordinal);
            var seats = new Dictionary<string, CountryAgent>(StringComparer.Ordinal);
            var shown = new Dictionary<string, string>(StringComparer.Ordinal);
            var labels = new Dictionary<string, string>(StringComparer.Ordinal);
            var characters = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < map.Countries.Count; i++)
            {
                string id = map.Countries[i].Id;
                var plan = table.SeatFor(i, gameIndex);
                labels[id] = plan.Label;
                characters[id] = table.CharacterFor(i, gameIndex);
                if (plan.IsModel)
                {
                    var persona = table.PersonaFor(i, gameIndex);
                    var seat = new CountryAgent(game.World, id, providerFor(plan.Model, game.World), table.Options, persona) { Seed = seed };
                    seats[id] = seat;
                    game.Gates[id] = seat;
                    shown[id] = persona != null ? $"{plan.Model} {persona.Id}" : plan.Model;
                }
                else
                {
                    players[id] = Lineups.Make(plan.Kind);
                    shown[id] = plan.Kind;
                }
            }

            // The storyteller arm: the first model seat's model tells the events, through the same
            // provider and queue, and the random table is off so the two never both fire.
            Storyteller teller = null;
            if (table.Options.Storyteller)
            {
                string model = table.Models.FirstOrDefault() ?? throw new ArgumentException("The storyteller needs a model at the table.");
                game.EventTable = false;
                game.Story = table.Options.StoryRules();
                teller = new Storyteller(game, providerFor(model, game.World), table.Options);
            }

            await ModelGameRunner.PlayToTheEndAsync(game, players, seats, ct, teller);
            var record = new GameRecord(game, shown);
            var decisions = seats.Values.SelectMany(s => s.Decisions).ToList();
            return new TableGame(new ModelGameRecord(record, decisions, seats.Keys.ToList()), labels, characters, teller?.Decisions);
        }

        /// <summary>One game per seed, the seats and personas rotating game by game.</summary>
        /// <param name="firstIndex">The series index of the first seed, so a block of a longer series (seeds 6 to 10 of 1 to 20) seats everyone as the whole series would.</param>
        public static async Awaitable<List<TableGame>> PlaySeriesAsync(TableDefinition table, IReadOnlyList<int> seeds,
            Func<string, World, ILLMProvider> providerFor, Action<int, TableGame> progress = null, CancellationToken ct = default, int firstIndex = 0)
        {
            var games = new List<TableGame>(seeds.Count);
            for (int g = 0; g < seeds.Count; g++)
            {
                var game = await PlayAsync(table, seeds[g], firstIndex + g, providerFor, ct);
                games.Add(game);
                progress?.Invoke(g, game);
            }
            return games;
        }
    }

    /// <summary>
    /// Plays a table's series and writes it to a folder: everything a model-seat run writes
    /// (<c>summary.md</c>, <c>games.csv</c>, <c>model.csv</c>, <c>decisions.jsonl</c>, one
    /// <c>game-&lt;seed&gt;.md</c> a game), plus <c>scorecard.md</c> and <c>scorecard.csv</c>, and
    /// <c>events.csv</c> and <c>events.md</c> (every event), and with a storyteller <c>storyteller.csv</c>.
    /// </summary>
    public static class StrategyTableRuns
    {
        public static async Awaitable<Scorecard> RunAsync(string folder, TableDefinition table, Func<string, World, ILLMProvider> providerFor,
            int games, int firstSeed, Action<string> progress = null, CancellationToken ct = default)
        {
            Directory.CreateDirectory(folder);
            var seeds = GameRunner.Seeds(games, firstSeed);
            var started = DateTime.Now;
            List<TableGame> played;
            using (var log = table.Models.Any() ? DecisionLog.Start(Path.Combine(folder, "decisions.jsonl"), includePrompt: true) : null)
            {
                played = await TableRunner.PlaySeriesAsync(table, seeds, providerFor,
                    (i, g) => progress?.Invoke(Line(i, games, g, started)), ct);
            }
            return Write(folder, table, played, $"{games} games, seeds {firstSeed} to {firstSeed + games - 1}", (DateTime.Now - started).TotalSeconds);
        }

        /// <summary>
        /// Plays several arms of one table on the same seeds, interleaved in blocks (hard rule 2): seeds
        /// 1 to 5 in every arm, then 6 to 10, the arms' order rotating each block, so drift over the
        /// session reaches every arm alike. Each arm is written to <c>folder/&lt;name&gt;</c> as
        /// <see cref="RunAsync"/> writes one, seated exactly as a single run of the whole series would be.
        /// <paramref name="firstIndex"/> is the series index of <paramref name="firstSeed"/>, for a run that is
        /// one block of a longer series (seeds 6 to 10 of 1 to 20: 5).
        /// </summary>
        public static async Awaitable<List<Scorecard>> RunInterleavedAsync(string folder, IReadOnlyList<(string name, TableDefinition table)> arms,
            Func<string, World, ILLMProvider> providerFor, int games, int firstSeed, int block, Action<string> progress = null, CancellationToken ct = default,
            int firstIndex = 0)
        {
            if (arms == null || arms.Count == 0) throw new ArgumentException("No arms to run.", nameof(arms));
            if (block < 1) block = games;
            // Two arms in one folder would append to one decision log and overwrite each other's files.
            var clash = arms.GroupBy(a => a.name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
            if (clash != null) throw new ArgumentException($"Two arms are named '{clash.Key}': give each its own name.", nameof(arms));
            var seeds = GameRunner.Seeds(games, firstSeed);
            var played = arms.Select(_ => new List<TableGame>()).ToList();
            var started = DateTime.Now;
            for (int b = 0, k = 0; b < games; b += block, k++)
            {
                var some = seeds.Skip(b).Take(block).ToList();
                for (int j = 0; j < arms.Count; j++)
                {
                    int a = (j + k) % arms.Count;
                    var (name, table) = arms[a];
                    string sub = Path.Combine(folder, name);
                    Directory.CreateDirectory(sub);
                    progress?.Invoke($"block {k + 1}: arm {name}, seeds {some[0]} to {some[some.Count - 1]}");
                    int offset = b;
                    using (var log = table.Models.Any() ? DecisionLog.Start(Path.Combine(sub, "decisions.jsonl"), includePrompt: true) : null)
                    {
                        var blockGames = await TableRunner.PlaySeriesAsync(table, some, providerFor,
                            (i, g) => progress?.Invoke($"{name}: " + Line(offset + i, games, g, started)), ct, firstIndex: firstIndex + offset);
                        played[a].AddRange(blockGames);
                    }
                }
            }
            double wall = (DateTime.Now - started).TotalSeconds;
            var cards = new List<Scorecard>();
            for (int a = 0; a < arms.Count; a++)
                cards.Add(Write(Path.Combine(folder, arms[a].name), arms[a].table, played[a].OrderBy(g => g.Record.Seed).ToList(),
                    $"{games} games, seeds {firstSeed} to {firstSeed + games - 1}, interleaved in blocks of {block} with {string.Join(", ", arms.Select(x => x.name))}",
                    wall));
            return cards;
        }

        private static string Line(int i, int games, TableGame g, DateTime started) =>
            $"game {i + 1}/{games} (seed {g.Record.Seed}): {g.Record.Result.Winner} ({g.Record.Kinds[g.Record.Result.Winner]}) won on turn {g.Record.Result.Turns}, "
            + $"{g.Game.Decisions.Count} model decisions, {g.Record.Reports.Sum(r => r.Events.Count)} events; {(DateTime.Now - started).TotalSeconds:0} s so far";

        private static Scorecard Write(string folder, TableDefinition table, List<TableGame> played, string what, double wall)
        {
            Directory.CreateDirectory(folder);
            bool anyModel = table.Models.Any();
            var scorecard = new Scorecard(table.Name, played.Select(g => g.ToScored()).ToList(),
                (table.Note ?? "") + $"\n\n{what}, seats and personas rotating. Wall time {wall:0} s.");
            var summary = new SeriesSummary(table.Name, played.Select(g => g.Record).ToList());
            var sb = new StringBuilder(summary.ToMarkdown());
            foreach (string model in table.Models)
            {
                var mine = played.Select(g => Only(g.Game, model, g.Labels)).ToList();
                sb.AppendLine().Append(new ModelReport(model, mine).ToMarkdown());
            }
            sb.AppendLine().AppendLine($"Wall time: {wall:0} s. Arm: {table.Options.Arm}. Personas: {(table.UsesPersonas ? "on" : "off")}.");
            File.WriteAllText(Path.Combine(folder, "summary.md"), sb.ToString());
            File.WriteAllText(Path.Combine(folder, "scorecard.md"), scorecard.ToMarkdown());
            File.WriteAllText(Path.Combine(folder, "scorecard.csv"), scorecard.ToCsv());
            File.WriteAllText(Path.Combine(folder, "scorecard-games.csv"), scorecard.PerGameCsv());
            File.WriteAllText(Path.Combine(folder, "games.csv"), summary.ToCsv());
            if (anyModel) File.WriteAllText(Path.Combine(folder, "model.csv"), new ModelReport("all", played.Select(g => g.Game).ToList()).ToCsv());
            var records = played.Select(g => g.Record).ToList();
            File.WriteAllText(Path.Combine(folder, "events.csv"), EventLog.Csv(records));
            File.WriteAllText(Path.Combine(folder, "events.md"), EventLog.Markdown(table.Name, records));
            if (table.Options.Storyteller) File.WriteAllText(Path.Combine(folder, "storyteller.csv"), StorytellerCsv(played));
            if (table.Options.Storyteller) File.WriteAllText(Path.Combine(folder, "chains.csv"), EventLog.ChainsCsv(records));
            if (table.Options.Choices) File.WriteAllText(Path.Combine(folder, "choices.csv"), ChoicesCsv(played));
            foreach (var g in played)
                File.WriteAllText(Path.Combine(folder, $"game-{g.Record.Seed:D4}.md"), GameLog.Write(g.Record));
            return scorecard;
        }

        private static string StorytellerCsv(IEnumerable<TableGame> played)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new StringBuilder("seed,turn,open,model_action,model_target,action,target,model_answer_legal,latency_s,prompt_tokens,guard,verdict,error,execution,queued,statement,title_latency_s,title_statement,skipped\n");
            foreach (var d in played.SelectMany(g => g.Storyteller))
                sb.AppendLine(string.Join(",", d.Seed, d.Turn, d.Open, d.ModelAction, d.ModelTarget, d.Action, d.Target, d.ModelAnswerLegal,
                    d.LatencySeconds.ToString("0.000", inv), d.PromptTokens, d.Guard, d.Verdict, d.Error, d.Execution, d.Queued, EventLog.Q(d.Statement),
                    d.TitleLatencySeconds.ToString("0.000", inv), EventLog.Q(d.TitleStatement), d.Skipped));
            return sb.ToString();
        }

        // Every ruler's answer to a dilemma, with its persona and what the game took.
        private static string ChoicesCsv(IEnumerable<TableGame> played)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new StringBuilder("seed,turn,country,character,template,model_action,option,chosen_by,lean,latency_s,statement\n");
            foreach (var g in played)
                foreach (var e in g.Record.Reports.SelectMany(r => r.Events).Where(e => e.Choices.Count > 0))
                    foreach (var kv in e.Choices)
                    {
                        var d = g.Game.Decisions.FirstOrDefault(x => x.Moment == Moment.Choice && x.Country == kv.Key && x.Turn == e.Turn);
                        string lean = EventRules.Template(e.Template)?.Options?.FirstOrDefault(o => o.Id == kv.Value)?.Lean;
                        sb.AppendLine(string.Join(",", g.Record.Seed, e.Turn, kv.Key, g.Characters[kv.Key], e.Template, d?.ModelAction, kv.Value,
                            e.ChosenBy[kv.Key], lean, (d?.LatencySeconds ?? 0).ToString("0.000", inv), EventLog.Q(d?.Statement)));
                    }
            return sb.ToString();
        }

        // One model's seats of a game, for its own report.
        private static ModelGameRecord Only(ModelGameRecord game, string model, IReadOnlyDictionary<string, string> labels)
        {
            var countries = game.ModelCountries.Where(c => labels[c] == model).ToList();
            return new ModelGameRecord(game.Record, game.Decisions.Where(d => countries.Contains(d.Country)).ToList(), countries);
        }
    }
}
