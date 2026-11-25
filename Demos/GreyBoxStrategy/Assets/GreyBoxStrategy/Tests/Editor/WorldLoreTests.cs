using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agenerela;
using Agenerela.Providers;
using NUnit.Framework;

namespace GreyBoxStrategy.Tests
{
    // The world's lore (#20): the loremaster proposes, LoreRules decides. Each piece is judged alone and falls back to
    // the seeded tables, a rename reaches every place an id is kept, and the loremaster's one decision goes through the
    // framework with free-text parameters (DR-030).
    public sealed class WorldLoreTests
    {
        private static WorldMap World(int seed = 7)
        {
            var made = MapGenerator.Generate(seed, 5);
            Assert.That(made.Playable, Is.True);
            return made.Map;
        }

        private static LoreProposal Good() => new LoreProposal
        {
            WorldName = "the salt kingdoms",
            RealmNames = new List<string> { "Velmora", "Tidewend", "Korrask", "Ambrel", "Sundholt" },
            RulerLines = new List<string>
            {
                "A young queen who trusts her river captains more than her lords.",
                "An old merchant prince who counts every coin twice.",
                "A scarred warlord who has never lost a winter campaign.",
                "A patient builder of walls and granaries.",
                "A proud king who remembers every slight.",
            },
            Backstory = "When the sea fell back, five crowns rose on the salt flats. Velmora took the rivers and Korrask the passes. Tidewend grew rich, and Ambrel built walls. Sundholt still claims them all.",
        };

        // ---- Kept --------------------------------------------------------------------------------

        [Test]
        public void AGoodProposal_RenamesEveryRealm_AndTheWorldStillPlays()
        {
            var world = World();
            var old = world.Countries.Select(c => c.Id).ToList();
            var capitals = world.Provinces.Where(p => p.Capital).ToDictionary(p => p.Id, p => p.Owner);
            var lore = LoreRules.Check(world, Good());

            Assert.That(lore.Refused, Is.Empty, string.Join("; ", lore.Refused));
            Assert.That(lore.WorldName, Is.EqualTo("The Salt Kingdoms"));
            LoreRules.Apply(world, lore);

            Assert.That(world.Countries.Select(c => c.Id), Is.EqualTo(new[] { "velmora", "tidewend", "korrask", "ambrel", "sundholt" }));
            Assert.That(world.Countries.Select(c => c.Name), Is.EqualTo(new[] { "Velmora", "Tidewend", "Korrask", "Ambrel", "Sundholt" }));
            Assert.That(world.Name, Is.EqualTo("The Salt Kingdoms"));
            Assert.That(world.Backstory, Does.StartWith("When the sea fell back"));
            Assert.That(world.Countries[2].RulerLine, Is.EqualTo("A scarred warlord who has never lost a winter campaign."));
            Assert.That(world.WhyInvalid(), Is.Null);
            Assert.That(world.Provinces.Select(p => p.Owner).Where(o => o != null).Distinct(), Is.SubsetOf(world.Countries.Select(c => c.Id)));
            Assert.That(world.Provinces.Where(p => p.Capital).Select(p => p.Owner),
                Is.EquivalentTo(capitals.Values.Select(o => world.Countries[old.IndexOf(o)].Id)), "Each capital stays its own realm's.");
            Assert.That(world.ToDefinition().Countries.Select(c => c.Id), Is.EqualTo(world.Countries.Select(c => c.Id)));
        }

        [Test]
        public void ARename_ReachesTheShapesRiversRoadsAndSpecialPlaces()
        {
            var world = World();
            var provinceIds = world.Provinces.Select(p => p.Id).ToList();
            string province = provinceIds[3];
            world.Rename(new Dictionary<string, string> { [province] = "quillmere" });

            Assert.That(world.Provinces[3].Id, Is.EqualTo("quillmere"));
            Assert.That(world.Provinces[3].Name, Is.EqualTo("Quillmere"));
            Assert.That(world.Geometry.Provinces.Select(s => s.Id), Does.Contain("quillmere").And.Not.Contain(province));
            Assert.That(world.Borders.SelectMany(b => b), Does.Not.Contain(province));
            Assert.That(world.Regions.SelectMany(r => r.Provinces), Does.Contain("quillmere").And.Not.Contain(province));
            Assert.That(world.Geometry.Rivers.SelectMany(r => r.Provinces).Concat(world.Geometry.Roads.SelectMany(r => r.Provinces)), Does.Not.Contain(province));
            Assert.That(world.Specials.Select(s => s.Province), Does.Not.Contain(province));
            Assert.That(world.WhyInvalid(), Is.Null);
        }

        [Test]
        public void TheGenerator_NamesThroughRename_AndStillGivesTheSameWorld()
        {
            // The table names a seed gave before Rename existed (24 November): renaming through it must not change them.
            var a = World(7);
            var b = World(7);
            Assert.That(a.ToJson(), Is.EqualTo(b.ToJson()));
            Assert.That(a.Countries.All(c => c.Name == NameRules.Display(c.Id)), Is.True);
            Assert.That(a.Generated.Styles.Keys, Is.EquivalentTo(a.Regions.Select(r => r.Id)));
            Assert.That(a.Provinces.All(p => !p.Id.StartsWith("p") || !char.IsDigit(p.Id[1])), Is.True, "Every province named.");
        }

        // ---- Refused, one piece at a time ----------------------------------------------------------

        [Test]
        public void ABadRealmName_KeepsItsTableName_AndTheOthersStillChange()
        {
            var world = World();
            var old = world.Countries.Select(c => c.Id).ToList();
            var p = Good();
            p.RealmNames[0] = "France";             // a real place
            p.RealmNames[1] = "Salt Coast";         // two words
            p.RealmNames[2] = "Korraskmoorland";    // more than 11 letters
            p.RealmNames[3] = "Velmorb";            // one letter from realm 1's... which was refused, so it stands
            p.RealmNames[4] = world.Provinces[0].Name; // a province's name
            var lore = LoreRules.Check(world, p);
            LoreRules.Apply(world, lore);

            Assert.That(world.Countries[0].Id, Is.EqualTo(old[0]));
            Assert.That(world.Countries[1].Id, Is.EqualTo(old[1]));
            Assert.That(world.Countries[2].Id, Is.EqualTo(old[2]));
            Assert.That(world.Countries[3].Id, Is.EqualTo("velmorb"));
            Assert.That(world.Countries[4].Id, Is.EqualTo(old[4]));
            Assert.That(lore.Refused.Count, Is.EqualTo(4), string.Join("; ", lore.Refused));
            Assert.That(world.WhyInvalid(), Is.Null);
        }

        [Test]
        public void TwoRealms_NeverTakeOneName()
        {
            var world = World();
            var p = Good();
            p.RealmNames[1] = "Velmora";
            p.RealmNames[2] = "Velmoria"; // a prefix of the first
            var lore = LoreRules.Check(world, p);
            LoreRules.Apply(world, lore);
            Assert.That(world.Countries.Select(c => c.Id).Distinct().Count(), Is.EqualTo(5));
            Assert.That(world.Countries.Count(c => c.Id.StartsWith("velmor")), Is.EqualTo(1));
        }

        [Test]
        public void ARefusedName_InTheStory_BecomesTheNameTheRealmKeeps()
        {
            var world = World();
            string kept = world.Countries[0].Name;
            var p = Good();
            p.RealmNames[0] = "Paris";
            p.RulerLines[0] = "The queen of Paris trusts her river captains.";
            var lore = LoreRules.Check(world, p);
            Assert.That(lore.Backstory, Does.Not.Contain("Paris"));
            Assert.That(lore.RulerLines[world.Countries[0].Id], Is.EqualTo($"The queen of {kept} trusts her river captains."));
        }

        [Test]
        public void TheWorldName_IsRefusedForARealPlace_ARealmsName_OrOddCharacters()
        {
            var places = new HashSet<string> { "velmora" };
            Assert.That(LoreRules.WorldName("The Old Kingdom of Rome", places, out _), Is.Null);
            Assert.That(LoreRules.WorldName("Velmora", places, out _), Is.Null);
            Assert.That(LoreRules.WorldName("Salt<script>", places, out _), Is.Null);
            Assert.That(LoreRules.WorldName("A Name Of Five Long Words", places, out _), Is.Null);
            Assert.That(LoreRules.WorldName("  \"ashen   reach\" ", places, out _), Is.EqualTo("Ashen Reach"));
        }

        [Test]
        public void Prose_IsCutBackToItsLastWholeSentence_AndOddTextIsRefused()
        {
            string longStory = "The first sentence ends here. " + new string('x', 20) + " and then it runs on and on " + new string('y', 500);
            Assert.That(LoreRules.Prose(longStory, 480, out _), Is.EqualTo("The first sentence ends here."));
            Assert.That(LoreRules.Prose("Cut off in the mid", 140, out string why), Is.Null);
            Assert.That(why, Is.EqualTo("it is not a whole sentence"));
            Assert.That(LoreRules.Prose("Two\nlines “quoted” here.", 140, out _), Is.EqualTo("Two lines \"quoted\" here."));
            Assert.That(LoreRules.Prose("A ruler of bastards.", 140, out _), Is.Null);
            Assert.That(LoreRules.Prose("A ruler of #hashtags.", 140, out _), Is.Null);
            Assert.That(LoreRules.Prose("Death came for the old king.", 140, out _), Is.Not.Null, "A story may tell of death.");
        }

        [Test]
        public void NoProposal_ChangesNothing()
        {
            var world = World();
            string json = world.ToJson();
            var lore = LoreRules.Check(world, null);
            LoreRules.Apply(world, lore);
            Assert.That(lore.Any, Is.False);
            Assert.That(world.ToJson(), Is.EqualTo(json));
        }

        // ---- The loremaster, through the framework ------------------------------------------------

        private static string Reply(WorldMap world) =>
            "{\"action\":\"found_world\",\"world_name\":\"The Salt Kingdoms\"," +
            string.Join(",", Enumerable.Range(1, world.Countries.Count).Select(i => $"\"realm_{i}\":\"{Good().RealmNames[i - 1]}\",\"ruler_{i}\":\"{Good().RulerLines[i - 1]}\"")) +
            ",\"backstory\":\"" + Good().Backstory + "\"}";

        [Test]
        public async Task TheLoremaster_AsksOnceWithFreeTextParameters_AndReadsTheProposal()
        {
            var world = World();
            var provider = new ScriptedProvider { Callback = _ => ScriptedReply.Raw(Reply(world)) };
            var master = new WorldLoremaster(world, provider, new[] { null, "builder", "opportunist", "merchant", "proud" });

            var proposal = await master.AskAsync(7, CancellationToken.None);

            Assert.That(proposal, Is.Not.Null, "Not refused, failed or cancelled.");
            Assert.That(provider.RequestCount, Is.EqualTo(1));
            var request = provider.LastRequest.Request;
            var branch = request.Schema.Pairs.Single(b => b.Actions.Contains(WorldLoremaster.ActionId));
            Assert.That(branch.Parameters.Select(f => f.Name).Take(3), Is.EqualTo(new[] { "world_name", "realm_1", "ruler_1" }));
            Assert.That(branch.Parameters.All(f => f.AllowedValues == null && f.MaxLength > 0), Is.True, "Every one a free text.");
            Assert.That(request.Schema.StatementName, Is.EqualTo("backstory"));
            Assert.That(request.SystemPrompt, Does.Contain("realm_1 (realm 1's new name, one word of 4 to 11 letters): free text, at most 20 characters."));
            Assert.That(request.SystemPrompt, Does.Contain("realm 1: the ").And.Contain("ruled by the player").And.Contain("ruled by a builder"));
            Assert.That(request.SystemPrompt, Does.Not.Contain(world.Countries[0].Id), "The table names are not shown, so the new ones are not copies.");
            Assert.That(request.Seed, Is.EqualTo(7));

            Assert.That(proposal.WorldName, Is.EqualTo("The Salt Kingdoms"));
            Assert.That(proposal.RealmNames, Is.EqualTo(Good().RealmNames));
            Assert.That(proposal.RulerLines[3], Is.EqualTo(Good().RulerLines[3]));
            Assert.That(proposal.Backstory, Is.EqualTo(Good().Backstory));
        }

        [Test]
        public async Task TheLoremaster_GivesNoProposal_WhenTheModelChoosesNone()
        {
            var world = World();
            var provider = new ScriptedProvider { Callback = _ => ScriptedReply.Decide(ActionRegistry.None) };
            var proposal = await new WorldLoremaster(world, provider).AskAsync(7, CancellationToken.None);
            Assert.That(proposal, Is.Null);
        }

        // ---- The rulers' arm -------------------------------------------------------------------------

        [Test]
        public void WorldLoreArm_IsOffByDefault_AndAddsTheStoryAndTheRulersLine()
        {
            Assert.That(ModelSeatOptions.Default.WorldLore, Is.False, "No A/B has measured it (hard rule 2).");
            Assert.That(ModelSeatOptions.Named("default+world-lore").WorldLore, Is.True);

            var world = World();
            LoreRules.Apply(world, LoreRules.Check(world, Good()));
            var identity = new AgentIdentity();
            WritRuler.AddWorldLore(identity, world, "korrask");
            Assert.That(identity.Knowledge, Is.EqualTo(new[]
            {
                "The world is The Salt Kingdoms. Its history: " + Good().Backstory,
                "Of you it is said: A scarred warlord who has never lost a winter campaign.",
            }));
        }
    }
}
