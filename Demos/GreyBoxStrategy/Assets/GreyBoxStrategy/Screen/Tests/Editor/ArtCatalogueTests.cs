using System;
using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Realm;
using GreyBoxStrategy.Replay;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Screen.Tests
{
    /// <summary>
    /// The art pack's catalogue (#20): a realm's face is the same every time it is asked for, no two realms of one game
    /// share a face, every listed picture is in the pack, and a missing picture is null so the empty frame stays.
    /// </summary>
    public class ArtCatalogueTests
    {
        private static readonly string[] Ten = { "aldria", "brynor", "caelen", "dornar", "eskis", "faleth", "garran", "halow", "istund", "jarnane" };
        private static string SeatPersona(string id) => ArtCatalogue.Personas[Array.IndexOf(Ten, id) % 5];

        [Test]
        public void The_hash_is_the_same_number_on_every_machine()
        {
            // Pinned from a second implementation of the same arithmetic (Python): string.GetHashCode would differ a run.
            Assert.That(ArtCatalogue.Hash("7:aldria"), Is.EqualTo(970169101u));
            Assert.That(ArtCatalogue.Hash(""), Is.EqualTo(760700053u));
            Assert.That(ArtCatalogue.Hash(null), Is.EqualTo(760700053u));
        }

        [Test]
        public void A_realm_keeps_its_face_however_the_game_is_asked()
        {
            var first = ArtCatalogue.RulerFaces(41, Ten, SeatPersona);
            var again = ArtCatalogue.RulerFaces(41, Ten.Reverse(), SeatPersona);
            Assert.That(again, Is.EquivalentTo(first), "the order the realms are listed in changes nothing");
            // Another game seats other faces: over a few seeds some realm's face must differ, or the seed is not used.
            Assert.That(Enumerable.Range(0, 8).Select(seed => string.Join(",", Ten.Select(id => ArtCatalogue.RulerFaces(seed, Ten, SeatPersona)[id]))).Distinct().Count(),
                Is.GreaterThan(1));
        }

        [Test]
        public void No_two_realms_of_one_game_share_a_face()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                // Ten realms, each persona seated twice, as a huge world seats them; then with you ruling one; then with no personas.
                foreach (var faces in new[]
                         {
                             ArtCatalogue.RulerFaces(seed, Ten, SeatPersona),
                             ArtCatalogue.RulerFaces(seed, Ten, id => id == "caelen" ? null : SeatPersona(id), "caelen", seed),
                             ArtCatalogue.RulerFaces(seed, Ten, _ => null),
                             ArtCatalogue.RulerFaces(seed, Ten.Take(6), _ => "warmonger"),
                         })
                {
                    Assert.That(faces.Values.Distinct().Count(), Is.EqualTo(faces.Count), $"seed {seed}: {string.Join(", ", faces.Select(f => f.Key + "=" + f.Value))}");
                    Assert.That(faces.Values, Is.SubsetOf(ArtCatalogue.AllRulerFaces));
                }
            }
        }

        [Test]
        public void A_personas_realm_wears_that_personas_face_and_yours_wears_the_one_you_picked()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var faces = ArtCatalogue.RulerFaces(seed, Ten, id => id == "eskis" ? null : SeatPersona(id), "eskis", 2);
                Assert.That(faces["eskis"], Is.EqualTo("you-3"));
                foreach (string id in Ten.Where(id => id != "eskis"))
                    Assert.That(faces[id], Does.StartWith(SeatPersona(id) + "-"), $"seed {seed}, {id}");
            }
            Assert.That(ArtCatalogue.RulerFaces(1, Ten, SeatPersona, "eskis", -1)["eskis"], Is.EqualTo("you-4"), "a face number wraps");
        }

        [Test]
        public void Changing_your_face_never_changes_a_rivals()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                // Rivals with personas and without: neither may move when you click to another face.
                Func<string, string> persona = id => Array.IndexOf(Ten, id) % 2 == 0 ? null : SeatPersona(id);
                var before = ArtCatalogue.RulerFaces(seed, Ten, persona, "aldria", 0);
                for (int face = 1; face < ArtCatalogue.PlayerFaces; face++)
                {
                    var after = ArtCatalogue.RulerFaces(seed, Ten, persona, "aldria", face);
                    foreach (string id in Ten.Where(id => id != "aldria")) Assert.That(after[id], Is.EqualTo(before[id]), $"seed {seed}, {id}");
                }
            }
        }

        [Test]
        public void A_realm_with_no_persona_or_an_unknown_one_still_gets_a_face()
        {
            var faces = ArtCatalogue.RulerFaces(5, new[] { "a", "b" }, id => id == "a" ? null : "a persona nobody drew");
            Assert.That(ArtCatalogue.PersonaFaces, Does.Contain(faces["a"]), "the neutral faces are kept for you");
            Assert.That(ArtCatalogue.PersonaFaces, Does.Contain(faces["b"]));
            Assert.That(ArtCatalogue.RulerFaces(5, null, null), Is.Empty);
        }

        [Test]
        public void A_general_keeps_one_face_and_a_womans_name_wears_a_womans()
        {
            Assert.That(ArtCatalogue.WomensNames, Is.SubsetOf(ArmyNames.Generals), "a name here that no general has: the list of generals changed");
            foreach (string name in ArmyNames.Generals)
            {
                string face = ArtCatalogue.GeneralFace(9, name);
                Assert.That(ArtCatalogue.GeneralFaces, Does.Contain(face));
                Assert.That(face.StartsWith("captain-w"), Is.EqualTo(ArtCatalogue.WomensNames.Contains(name)), name);
                string title = char.ToUpperInvariant(name[0]) + name.Substring(1) + " the Wary";
                Assert.That(ArtCatalogue.GeneralFace(9, title), Is.EqualTo(face), "the title and the name are one general");
                Assert.That(ArtCatalogue.GeneralFace(9, name + "_host"), Is.EqualTo(face), "so is the army's id");
            }
            Assert.That(ArmyNames.Generals.Select(n => ArtCatalogue.GeneralFace(9, n)).Distinct().Count(), Is.GreaterThan(8), "the names spread over the faces");
            Assert.That(ArtCatalogue.GeneralFace(9, null), Is.Null);
            Assert.That(ArtCatalogue.GeneralFace(9, "  "), Is.Null);
        }

        [Test]
        public void Every_event_the_storyteller_can_send_has_a_picture()
        {
            foreach (var t in EventRules.AllTemplates)
                Assert.That(ArtCatalogue.Event(t.Id), Is.Not.Null, t.Id);
            foreach (string random in new[] { EventRules.GoodHarvest, EventRules.PoorHarvest, EventRules.Bandits })
                Assert.That(ArtCatalogue.Event(random), Is.Not.Null, random);
        }

        [Test]
        public void Every_picture_the_catalogue_lists_is_in_the_pack()
        {
            foreach (string face in ArtCatalogue.AllRulerFaces.Concat(ArtCatalogue.GeneralFaces))
                foreach (bool small in new[] { true, false })
                {
                    var t = ArtCatalogue.Portrait(face, small);
                    Assert.That(t, Is.Not.Null, face);
                    Assert.That(t.width * 7, Is.EqualTo(t.height * 6), $"{face}: a portrait is 6 by 7");
                    Assert.That(t.height, small ? Is.LessThanOrEqualTo(256) : Is.GreaterThan(256), $"{face}, small {small}");
                }
            foreach (string id in ArtCatalogue.EventPictures)
            {
                var t = ArtCatalogue.Event(id);
                Assert.That(t, Is.Not.Null, id);
                Assert.That(t.width * 9, Is.EqualTo(t.height * 16), $"{id}: an event's picture is 16 by 9");
            }
        }

        [Test]
        public void A_picture_the_pack_does_not_have_is_null_so_the_empty_frame_stays()
        {
            Assert.That(ArtCatalogue.Portrait(null), Is.Null);
            Assert.That(ArtCatalogue.Portrait("nobody-9"), Is.Null);
            Assert.That(ArtCatalogue.Event(null), Is.Null);
            Assert.That(ArtCatalogue.Event("an_event_nobody_drew"), Is.Null);
            Assert.That(ArtCatalogue.EventPicture("begin_a_tale_added_later"), Is.EqualTo("tale_begins"), "a new tale shows the tale's opening picture");
        }

        [Test]
        public void The_screen_hangs_the_packs_portrait_in_a_realms_panel()
        {
            var setup = GameSetup.Writs(3);
            var map = setup.BuildMap();
            var rules = ReplayDirector.WithArmySettings(setup.BuildRules(map), true, true, true, true);
            var g = ReplayBuilder.Build(Recording.Parse(ScriptedWorldGame.Play(setup, map, rules).ToJson()), map, rules);
            var players = g.CountryIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => new RecordedPlayer { Persona = ArtCatalogue.Personas[x.i % 5] });
            var root = new VisualElement();
            var ui = new ScreenUI(root, UnityEngine.ScriptableObject.CreateInstance<StyleSheet>(), null, g.CountryIds, g.ProvinceIds, players, "test") { ArtSeed = g.Seed };
            ui.BuildPanels();

            var expected = ArtCatalogue.RulerFaces(g.Seed, g.CountryIds, id => players[id].Persona);
            foreach (string id in g.CountryIds)
            {
                Assert.That(ui.FaceOf(id), Is.EqualTo(expected[id]));
                ui.ShowCountry(id, g.Initial, Enumerable.Empty<ReplayEvent>(), null, null);
                var hung = root.Query<Image>(className: "portrait-slot").ToList().Select(i => i.image).ToList();
                Assert.That(hung, Does.Contain(ArtCatalogue.Portrait(expected[id])), $"{id}'s panel shows {expected[id]}");
            }
            Assert.That(ui.FaceOf("nobody"), Is.Null);
            Assert.That(ui.EventPicture(EventRules.Plague), Is.SameAs(ArtCatalogue.Event(EventRules.Plague)));
        }
    }
}
