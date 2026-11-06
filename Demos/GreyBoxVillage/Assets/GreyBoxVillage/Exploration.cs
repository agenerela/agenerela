using System;
using System.Collections.Generic;
using Demos.Shared;
using UnityEngine;

namespace Demos.GreyBoxVillage
{
    /// <summary>
    /// The places the player has found north of the river, for the journal ("Places found: 4 of 6")
    /// and a toast as each is found. The presenter's F1 forgets them.
    /// </summary>
    public sealed class Exploration : MonoBehaviour
    {
        public Transform Player;
        [Tooltip("Shows \"Found: ... (2 of 6)\" as each place is found.")]
        public VillageHud Hud;
        public PlaceToFind[] Places = Array.Empty<PlaceToFind>();

        /// <summary>Raised once for each place, the first time the player reaches it.</summary>
        public event Action<PlaceToFind> Found;

        private readonly HashSet<PlaceToFind> found = new HashSet<PlaceToFind>();

        public int Count => found.Count;
        public int Total => Places.Length;
        public bool HasFound(PlaceToFind place) => found.Contains(place);

        private void Update() => Check();

        /// <summary>One look at where the player stands. Public so the tests can step it.</summary>
        public void Check()
        {
            if (Player == null) return;
            foreach (var place in Places)
                if (place != null && !found.Contains(place) && place.Reaches(Player.position))
                {
                    found.Add(place);
                    if (Hud != null) Hud.Toast("Found: " + place.Title + " (" + Count + " of " + Total + ")", 4f);
                    Found?.Invoke(place);
                }
        }

        /// <summary>Forgets every place found. The presenter's reset, and between tests.</summary>
        public void ResetState() => found.Clear();
    }
}
