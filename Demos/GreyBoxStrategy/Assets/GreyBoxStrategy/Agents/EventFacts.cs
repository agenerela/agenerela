using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Agenerela;

namespace GreyBoxStrategy
{
    /// <summary>
    /// <c>+event-facts</c>: the storyteller's events in the framework's <see cref="FactLedger"/> (#295), so a
    /// ruler reads what happened for <see cref="StoryRules.EventFactTurns"/> turns instead of only the turn
    /// after. One ledger a game; each event is one fact, keyed by its turn and template, with the news every
    /// country reads and its own line worded for each country it struck. The game no longer puts the event
    /// among the turn's facts (<see cref="StoryRules.EventFacts"/>), so nothing is read twice.
    /// </summary>
    /// <remarks>
    /// The <see cref="Storyteller"/> attaches the game (<see cref="Attach"/>); the seats read through
    /// <see cref="Source"/>, which finds the game's ledger by the seat's <see cref="World"/> when it is
    /// asked, so seats made before the storyteller read it too. Each read first brings the ledger up to
    /// the game: one <see cref="FactLedger.Tick"/> a turn passed, then every event landed since.
    /// </remarks>
    public sealed class EventFacts
    {
        /// <summary>The most event lines a ruler reads, the newest.</summary>
        public const int MaxLines = 3;

        private static readonly ConditionalWeakTable<World, EventFacts> ByWorld = new ConditionalWeakTable<World, EventFacts>();

        private readonly Game game;
        private int turn;
        private int seen;

        private EventFacts(Game game)
        {
            this.game = game;
            turn = game.World.Turn;
        }

        /// <summary>The ledger the rulers read.</summary>
        public FactLedger Ledger { get; } = new FactLedger { MaxLines = MaxLines };

        /// <summary>The game's event facts, made on the first call; null for a game without <see cref="StoryRules.EventFacts"/>.</summary>
        public static EventFacts Attach(Game game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (!game.Story.EventFacts) return null;
            return ByWorld.GetValue(game.World, w => new EventFacts(game));
        }

        /// <summary>The event facts attached for this world, or null.</summary>
        public static EventFacts Of(World world) => world != null && ByWorld.TryGetValue(world, out var f) ? f : null;

        /// <summary>The fact's key for an event: <c>event_7_windfall</c>.</summary>
        public static string Key(EventRecord e) => $"event_{e.Turn}_{e.Template}";

        /// <summary>Brings the ledger up to the game: a tick for every turn passed, then the events landed since.</summary>
        public void Sync()
        {
            while (turn < game.World.Turn)
            {
                Ledger.Tick();
                turn++;
            }
            var told = game.Events.Events;
            for (; seen < told.Count; seen++)
            {
                var e = told[seen];
                if (e.Source != EventRecord.FromStoryteller || e.News == null) continue;
                string key = Key(e);
                Ledger.Set(key, $"Turn {e.Turn}: {e.News}", StoryRules.EventFactTurns);
                foreach (var hit in e.HitLines) Ledger.Word(key, hit.Key, $"Turn {e.Turn}: {hit.Value}");
            }
        }

        /// <summary>The lines <paramref name="countryId"/> reads now.</summary>
        public IEnumerable<string> For(string countryId, AgentContext ctx)
        {
            Sync();
            return Ledger.For(countryId).Observe(ctx).ToList();
        }

        /// <summary>A seat's observation source: the event facts of its world, worded for it; nothing until a storyteller attaches.</summary>
        public static IObservationSource Source(World world, string countryId) => new Reader(world, countryId);

        private sealed class Reader : IObservationSource
        {
            private readonly World world;
            private readonly string countryId;

            public Reader(World world, string countryId)
            {
                this.world = world;
                this.countryId = countryId;
            }

            public IEnumerable<string> Observe(AgentContext ctx) => Of(world)?.For(countryId, ctx) ?? Enumerable.Empty<string>();
        }
    }
}
