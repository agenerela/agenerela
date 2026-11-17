using System.Collections.Generic;
using LivingVale.Game;
using LivingVale.World;
using UnityEngine;

namespace LivingVale.Unity
{
    /// <summary>One speaking NPC as the map and the name tags see it.</summary>
    public readonly struct NpcMarker
    {
        public NpcSpeaker Speaker { get; }
        public string HomePlaceId { get; }

        public NpcMarker(NpcSpeaker speaker, string homePlaceId)
        {
            Speaker = speaker;
            HomePlaceId = homePlaceId;
        }
    }

    /// <summary>
    /// G8a: turns the guide's goal and the NPCs into positions, from the scene as it is now, so a
    /// marker follows an NPC along its routine rather than pointing at where it was placed. The
    /// framework has no "where is this agent" query (framework gaps, #15), so this reads the
    /// <see cref="NpcSpeaker"/> Transforms directly.
    /// </summary>
    public sealed class NavigationTargets : System.IDisposable
    {
        private readonly ValeBootstrap bootstrap;
        private readonly List<NpcMarker> npcs = new List<NpcMarker>();
        private readonly Dictionary<string, NpcSpeaker> npcById = new Dictionary<string, NpcSpeaker>();
        private readonly Dictionary<string, Transform> pickups = new Dictionary<string, Transform>();

        private QuestGoal goal;
        private bool goalDirty = true;

        public NavigationTargets(ValeBootstrap owner)
        {
            bootstrap = owner;
            // The goal changes only with quest or pack state; work it out again then, not every frame.
            bootstrap.Quests.Changed += MarkDirty;
            bootstrap.Routes.Changed += MarkDirty;
            bootstrap.Inventory.Changed += MarkDirty;
            foreach (var speaker in Object.FindObjectsByType<NpcSpeaker>(FindObjectsSortMode.InstanceID))
            {
                string id = speaker.Descriptor.Id;
                if (string.IsNullOrEmpty(id) || npcById.ContainsKey(id))
                {
                    continue;
                }

                npcById[id] = speaker;
                npcs.Add(new NpcMarker(speaker, HomeOf(id)));
            }

            foreach (var point in Object.FindObjectsByType<QuestPickupPoint>(FindObjectsSortMode.InstanceID))
            {
                if (!string.IsNullOrEmpty(point.PickupId) && !pickups.ContainsKey(point.PickupId))
                {
                    pickups[point.PickupId] = point.transform;
                }
            }
        }

        public IReadOnlyList<NpcMarker> Npcs => npcs;

        /// <summary>The goal for the current quest state, or null.</summary>
        public QuestGoal Goal
        {
            get
            {
                if (goalDirty)
                {
                    goal = QuestGuide.For(bootstrap.Routes);
                    goalDirty = false;
                }

                return goal;
            }
        }

        private void MarkDirty() => goalDirty = true;

        /// <summary>Stops listening; the owner calls it when the HUD goes away.</summary>
        public void Dispose()
        {
            bootstrap.Quests.Changed -= MarkDirty;
            bootstrap.Routes.Changed -= MarkDirty;
            bootstrap.Inventory.Changed -= MarkDirty;
        }

        /// <summary>Whether the map may show this NPC: once its home place is discovered.</summary>
        public bool IsKnown(NpcMarker npc) =>
            npc.Speaker != null && bootstrap.Discovery.IsDiscovered(npc.HomePlaceId);

        /// <summary>Where the goal is now: the NPC or the object if the scene has it, else its place's centre.</summary>
        public bool TryLocate(QuestGoal goal, out Vector3 position)
        {
            position = default;
            if (goal == null)
            {
                return false;
            }

            if (goal.Kind == QuestGoalKind.Npc && npcById.TryGetValue(goal.TargetId, out NpcSpeaker speaker) && speaker != null)
            {
                position = speaker.transform.position;
                return true;
            }

            if (goal.Kind == QuestGoalKind.Pickup && pickups.TryGetValue(goal.TargetId, out Transform pickup) && pickup != null)
            {
                position = pickup.position;
                return true;
            }

            string placeId = goal.Kind == QuestGoalKind.Place ? goal.TargetId : goal.PlaceId;
            foreach (var place in ValePlaces.All)
            {
                if (place.Id == placeId)
                {
                    position = new Vector3(place.X, 0f, place.Z);
                    return true;
                }
            }

            return false;
        }

        private static string HomeOf(string npcId)
        {
            foreach (var npc in RegionNpcs.All)
            {
                if (npc.Id == npcId)
                {
                    return npc.PlaceId;
                }
            }

            // Iven and Mara, and any later Glen speaker.
            return ValePlaces.BriarGlen;
        }
    }
}
