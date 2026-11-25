using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agenerela;
using Agenerela.Providers;

namespace GreyBoxStrategy
{
    /// <summary>
    /// The loremaster: at New game, a framework <see cref="Agent"/> that names a generated world, its realms and their
    /// rulers and writes how the world came to be, in one decision (#20, world lore). Its one action,
    /// <c>found_world</c>, asks for free-text parameters (DR-030): <c>world_name</c>, then <c>realm_N</c> and
    /// <c>ruler_N</c> for each realm in seat order; the backstory is its free-text field, named <c>backstory</c>.
    /// What it writes is a <see cref="LoreProposal"/>: <see cref="LoreRules.Check"/> decides what the world keeps.
    /// </summary>
    public sealed class WorldLoremaster
    {
        public const string AgentId = "loremaster";
        public const string ActionId = "found_world";
        /// <summary>The free-text field the backstory is written in (<see cref="DecideOptions.StatementFieldName"/>).</summary>
        public const string BackstoryField = "backstory";

        /// <summary>
        /// The prompt's version, in the world cache's key: change it with any change to what the loremaster is asked,
        /// so a cached world written under the old words is asked again.
        /// </summary>
        public const int Version = 2;

        /// <summary>The longest reply in tokens: about 350 for five realms (24 November, qwen3.5:4b), with room for eight.</summary>
        public const int MaxReplyTokens = 900;

        /// <summary>
        /// The one rule for names said in words, first in the knowledge: on 24 November <c>qwen3.5:4b</c> wrote every realm
        /// name as two words ("Gilded Shore") with only the parameter's own description to go on.
        /// </summary>
        public const string NamesRule = "Each realm's new name is one invented word of 4 to 11 letters, never two words and never a place in our world.";

        public const string Stimulus = "A new world has been made. Name it, name its realms and their rulers, and tell how it came to be.";

        private readonly WorldMap world;
        private LoreProposal written;

        /// <param name="world">The world just made, its realms with the seeded tables' names.</param>
        /// <param name="provider">The decision model's provider.</param>
        /// <param name="personas">The persona id of each seat in seat order, null for a seat no persona rules (yours); null for none.</param>
        public WorldLoremaster(WorldMap world, ILLMProvider provider, IReadOnlyList<string> personas = null)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            var identity = new AgentIdentity
            {
                Name = "The Loremaster",
                Role = $"the loremaster who names a newly made world of {world.Countries.Count} realms and tells how it came to be",
                Personality = "A grave old chronicler with an ear for names that sound old and earned.",
                Goals = "Names a player remembers, each realm's sounding like its land; a story that sets the realms against each other.",
                Knowledge = world.Countries.Select((c, i) => Describe(world, i, personas)).Prepend(NamesRule).ToList(),
                StatementRules = "Three or four sentences of the world's history, naming the realms by their new names. No numbers.",
            };
            var found = new ActionDefinition
            {
                Id = ActionId,
                Description = "Name the world, each realm and its ruler, and tell the world's history.",
                TakesNoTarget = true,
                Parameters = Parameters(world.Countries.Count),
            };
            Agent = new Agent(identity, new[] { found }, provider) { Id = AgentId, Memory = null };
            Agent.Bind(ActionId, new DelegateActionHandler(_ => true, (ctx, d) => written = Read(d, world.Countries.Count)));
        }

        /// <summary>The framework agent, for its decision feed and log.</summary>
        public Agent Agent { get; }

        /// <summary>The last decision, for the log; null before one.</summary>
        public DecisionResult LastResult { get; private set; }

        /// <summary>
        /// The parameters <c>found_world</c> asks for, in reply order: the world's name, then each realm's name and its
        /// ruler's line. The caps are the schema's; <see cref="LoreRules"/> is stricter where it matters.
        /// </summary>
        public static ActionParameter[] Parameters(int realms)
        {
            var list = new List<ActionParameter>
            {
                new ActionParameter { Name = "world_name", Description = "the world's name, one to three words", MaxLength = LoreRules.WorldNameLength },
            };
            for (int i = 1; i <= realms; i++)
            {
                list.Add(new ActionParameter { Name = $"realm_{i}", Description = $"realm {i}'s new name, one word of 4 to 11 letters", MaxLength = LoreRules.RealmNameLength });
                list.Add(new ActionParameter { Name = $"ruler_{i}", Description = $"one sentence about realm {i}'s ruler", MaxLength = LoreRules.RulerLineLength });
            }
            return list.ToArray();
        }

        /// <summary>
        /// One realm as the loremaster reads it: "realm 2: the south, mostly marsh and plains, on the coast, its old names
        /// in the soft river manner; ruled by a merchant." Where it lies, its land, and its ruler's kind, never its table name, so a
        /// new name is not a copy of the old.
        /// </summary>
        public static string Describe(WorldMap world, int seat, IReadOnlyList<string> personas)
        {
            string persona = personas != null && seat < personas.Count ? personas[seat] : null;
            var c = world.Countries[seat];
            var owned = world.Provinces.Where(p => p.Owner == c.Id).ToList();
            string where = Compass(world, owned);
            var terrains = owned.GroupBy(p => p.Terrain).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Take(2).Select(g => g.Key.ToString().ToLowerInvariant()).ToList();
            string land = terrains.Count == 0 ? "" : ", mostly " + string.Join(" and ", terrains);
            string coast = owned.Any(p => p.Coastal) ? ", on the coast" : ", inland";
            var capital = owned.FirstOrDefault(p => p.Capital);
            string style = capital?.Region != null && world.Generated?.Styles != null && world.Generated.Styles.TryGetValue(capital.Region, out var s) ? $", its old names in the {s.Replace('_', ' ')} manner" : "";
            string ruler = personas == null ? "" : persona != null ? $"; ruled by a {persona}" : "; ruled by the player";
            return $"realm {seat + 1}: the {where}{land}{coast}{style}{ruler}.";
        }

        private static string Compass(WorldMap world, List<ProvinceData> owned)
        {
            if (owned.Count == 0 || world.Provinces.Count == 0) return "middle";
            double cx = world.Provinces.Average(p => p.Column), cy = world.Provinces.Average(p => p.Row);
            double w = Math.Max(1, world.Provinces.Max(p => p.Column) - world.Provinces.Min(p => p.Column));
            double h = Math.Max(1, world.Provinces.Max(p => p.Row) - world.Provinces.Min(p => p.Row));
            double x = (owned.Average(p => p.Column) - cx) / w, y = (owned.Average(p => p.Row) - cy) / h;
            string ns = y < -0.15 ? "north" : y > 0.15 ? "south" : "";
            string ew = x < -0.15 ? "west" : x > 0.15 ? "east" : "";
            return ns + ew == "" ? "middle" : ns + ew;
        }

        /// <summary>
        /// Asks once: the proposal it wrote, or null when the model refused, failed or the call was cancelled. Never
        /// throws for a model's fault; cancellation is reported as null too.
        /// </summary>
        public async Task<LoreProposal> AskAsync(int seed, CancellationToken ct)
        {
            written = null;
            var options = new DecideOptions
            {
                StimulusLabel = "Game",
                StatementFieldName = BackstoryField,
                MaxReplyTokens = MaxReplyTokens,
                Seed = seed,
                CheckTarget = false,
            };
            try
            {
                LastResult = await Agent.DecideAsync(Stimulus, options, ct);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            if (LastResult?.Decision == null || LastResult.Decision.ActionId != ActionId) return null;
            Agent.Execute(LastResult);
            return written;
        }

        /// <summary>The proposal in a decision: its arguments and its backstory, as written.</summary>
        public static LoreProposal Read(AgentDecision decision, int realms)
        {
            var p = new LoreProposal { Backstory = decision.Statement };
            p.WorldName = decision.TryGetArgument("world_name", out var name) ? name : null;
            for (int i = 1; i <= realms; i++)
            {
                p.RealmNames.Add(decision.TryGetArgument($"realm_{i}", out var realm) ? realm : null);
                p.RulerLines.Add(decision.TryGetArgument($"ruler_{i}", out var line) ? line : null);
            }
            return p;
        }
    }
}
