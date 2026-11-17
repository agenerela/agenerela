using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Shared;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// A day in Ashford for its scripted villagers: where each is meant to be at each hour of
    /// <see cref="DayNightCycle"/>'s day, the points of each place, and the line every agent is told
    /// about who is where, so the model sees the village change through the day.
    /// </summary>
    /// <remarks>
    /// The villagers are not agents and nothing here talks to the framework. Only the agents'
    /// observations change with the routine: no action or target is offered by the hour, and the
    /// villagers stay targets wherever they are, indoors included, so Bram's schema never moves.
    /// </remarks>
    public sealed class VillageRoutine : MonoBehaviour
    {
        public const string Home = "home", Well = "well", Market = "market", Fields = "fields",
            Square = "square", Campfire = "campfire", Shelter = "shelter";

        /// <summary>A place of the routine: its points, and how the agents are told about it.</summary>
        [Serializable]
        public sealed class Spot
        {
            public string Id;
            [Tooltip("The place in a sentence, such as \"in the fields outside the gate\".")]
            public string Words;
            public Transform[] Points = Array.Empty<Transform>();
        }

        public DayNightCycle Day;
        public Villager[] Villagers = Array.Empty<Villager>();
        public Spot[] Spots = Array.Empty<Spot>();
        [Tooltip("A villager this close to one of a spot's points is at that spot.")]
        public float AtSpotDistance = 6f;

        /// <summary>The hour now, 0 to 24.</summary>
        public float Hour => Day != null ? Day.TimeOfDay * 24f : 12f;

        /// <summary>
        /// Where a villager who works at <paramref name="job"/> goes at <paramref name="hour"/>: home
        /// until a quarter to six, the well at dawn, work, the square from eleven, work again from
        /// two, the campfire from five, home from half past seven.
        /// </summary>
        public static string PlaceAt(float hour, string job)
        {
            hour = Mathf.Repeat(hour, 24f);
            if (hour < 5.75f || hour >= 19.5f) return Home;
            if (hour < 7f) return Well;
            if (hour < 11f) return job;
            if (hour < 14f) return Square;
            if (hour < 17f) return job;
            return Campfire;
        }

        /// <summary>Where this villager should be now.</summary>
        public string PlaceFor(Villager villager) => PlaceAt(Hour, villager.Job);

        public Spot Find(string id) => Spots.FirstOrDefault(s => s.Id == id);

        /// <summary>The points of a place, or none when the routine has no such place.</summary>
        public Transform[] PointsOf(string id) => Find(id)?.Points ?? Array.Empty<Transform>();

        /// <summary>Whether another villager is at or making for this point, so a villager picks another.</summary>
        public bool Taken(Vector3 point, Villager asking)
        {
            foreach (var v in Villagers)
            {
                if (v == null || v == asking || v.IsHome) continue;
                var d = v.Goal - point;
                d.y = 0;
                if (d.magnitude < 2f) return true;
            }
            return false;
        }

        /// <summary>Every villager straight to where the hour has it: after the presenter jumps the clock.</summary>
        public void CatchUp()
        {
            foreach (var v in Villagers)
                if (v != null) v.CatchUp();
        }

        /// <summary>The spot a villager stands at, read from where it is; null on the way between two.</summary>
        public Spot SpotOf(Villager villager)
        {
            if (villager.IsHome) return Find(Home);
            var at = villager.transform.position;
            Spot best = null;
            float bestDistance = AtSpotDistance;
            foreach (var spot in Spots)
            {
                if (spot.Id == Home) continue;
                foreach (var p in spot.Points)
                {
                    if (p == null) continue;
                    var d = p.position - at;
                    d.y = 0;
                    if (d.magnitude < bestDistance)
                    {
                        best = spot;
                        bestDistance = d.magnitude;
                    }
                }
            }
            return best;
        }

        /// <summary>
        /// One line for the agents' observations: who is where, read from where the villagers stand.
        /// The market and the square are always named, busy or empty; the other places only when
        /// someone is there. Villagers are named by their target ids.
        /// </summary>
        public string Describe()
        {
            var live = Villagers.Where(v => v != null).ToList();
            if (live.Count == 0) return null;
            if (live.All(v => v.InCover)) return "In the village: the villagers are taking cover in the shelter.";
            if (live.All(v => v.IsHome)) return "In the village: everyone is at home for the night, and the streets are empty.";

            var at = new Dictionary<string, List<string>>();
            foreach (var v in live)
            {
                string place = v.InCover ? Shelter : SpotOf(v)?.Id;
                if (place == null) continue;
                if (!at.TryGetValue(place, out var names)) at[place] = names = new List<string>();
                names.Add(IdOf(v));
            }

            var parts = new List<string>();
            foreach (var place in new[] { Market, Square })
                parts.Add(at.TryGetValue(place, out var here)
                    ? "the " + place + " is busy (" + string.Join(", ", here) + ")"
                    : "the " + place + " is empty");
            foreach (var spot in Spots)
            {
                if (spot.Id == Market || spot.Id == Square || !at.TryGetValue(spot.Id, out var here)) continue;
                parts.Add(And(here) + (here.Count == 1 ? " is " : " are ") + spot.Words);
            }
            if (at.TryGetValue(Shelter, out var hiding))
                parts.Add(And(hiding) + (hiding.Count == 1 ? " is" : " are") + " taking cover in the shelter");
            return "In the village: " + string.Join(", ", parts) + ".";
        }

        private static string IdOf(Villager v)
        {
            var t = v.GetComponent<Agenerela.Targetable>();
            return t != null && !string.IsNullOrEmpty(t.Id) ? t.Id : v.name;
        }

        private static string And(List<string> names) =>
            names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1];
    }
}
