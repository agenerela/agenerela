using System.Collections.Generic;
using System.Linq;
using GreyBoxStrategy.Replay;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// Armies on the map at all times (the lead, 12 November): each country's army as a piece with
    /// its size, at its capital, in every map mode. The rules have one army per country today, so
    /// one piece each; it is the stand-in until armies become pieces in the rules.
    /// </summary>
    public sealed partial class ScreenUI
    {
        private readonly Dictionary<string, VisualElement> armyPieces = new Dictionary<string, VisualElement>();
        private readonly Dictionary<string, int> armyAt = new Dictionary<string, int>();

        /// <summary>Where a country's army stands: its capital while it holds it, else the province of its own nearest the capital.</summary>
        public int ArmyProvince(ReplayState s, string country)
        {
            int capital = CapitalIndex(country);
            if (capital >= 0 && s.Owners[capital] == country) return capital;
            var own = Enumerable.Range(0, s.Owners.Length).Where(q => s.Owners[q] == country).ToList();
            if (own.Count == 0) return -1;
            if (capital < 0 || ProvinceToPanel == null) return own[0];
            var c = ProvinceToPanel(capital);
            return own.OrderBy(q => (ProvinceToPanel(q) - c).sqrMagnitude).First();
        }

        private void ShowArmies(ReplayState s)
        {
            if (Writs) { ShowHosts(s); return; }
            foreach (var id in CountryIds)
            {
                if (!armyPieces.TryGetValue(id, out var piece))
                {
                    piece = new VisualElement { pickingMode = PickingMode.Position }; piece.AddToClassList("army-piece");
                    piece.style.backgroundColor = (Color)Darker(CountryLook.Of(id).Colour);
                    piece.Add(new Icon(IconKind.Army, 11) { Tint = new Color32(243, 231, 200, 255) });
                    var n = new Label(); n.AddToClassList("army-piece-n"); piece.Add(n);
                    string country = id;
                    piece.RegisterCallback<ClickEvent>(_ => CountryPressed?.Invoke(country));
                    labels.Add(piece);
                    armyPieces[id] = piece;
                }
                var c = s.Countries[id];
                int at = c.Alive ? ArmyProvince(s, id) : -1;
                armyAt[id] = at;
                piece.style.display = at >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
                piece.Q<Label>(className: "army-piece-n").text = c.Army.ToString();
                piece.tooltip = $"{CountryLook.NameOf(id)}'s army: {c.Army} soldiers, at {Cap(at >= 0 ? ProvinceIds[at] : "")}. Every country has one army today; it defends with 70% of it and attacks with all of it.";
            }
        }

        private void PlaceArmies()
        {
            if (Writs) { PlaceHosts(); return; }
            foreach (var kv in armyPieces)
            {
                if (!armyAt.TryGetValue(kv.Key, out int at) || at < 0) continue;
                var p = ProvinceToPanel(at);
                bool capital = at == CapitalIndex(kv.Key);
                // Above the capital's name; on another province, above its name.
                kv.Value.style.left = p.x;
                kv.Value.style.top = p.y - (capital ? 16 : 22);
            }
        }

        private static Color32 Darker(Color32 c) => new Color32((byte)(c.r * 0.55f), (byte)(c.g * 0.55f), (byte)(c.b * 0.55f), 235);
    }
}
