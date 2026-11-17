using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// Realm names on the map, and the one pass each frame that keeps everything on the map clear of everything
    /// else (<see cref="MapDeclutter"/>): pieces, march tags, realm, region, capital and province names.
    /// </summary>
    public sealed partial class ScreenUI
    {
        private readonly List<Label> realmLabels = new List<Label>();

        /// <summary>Realm names, in widely spaced capitals along their land, where the director placed them (panel pixels).</summary>
        public void PlaceRealmLabels(IReadOnlyList<(string text, Vector2 at, float size, float angle)> items)
        {
            while (realmLabels.Count < items.Count)
            {
                var l = new Label { pickingMode = PickingMode.Ignore };
                l.AddToClassList("realmlab");
                labels.Insert(0, l);
                realmLabels.Add(l);
            }
            for (int i = 0; i < realmLabels.Count; i++)
            {
                var l = realmLabels[i];
                if (i >= items.Count) { l.style.display = DisplayStyle.None; continue; }
                l.style.display = DisplayStyle.Flex;
                l.text = items[i].text.ToUpperInvariant();
                l.style.fontSize = items[i].size;
                l.style.left = items[i].at.x; l.style.top = items[i].at.y;
                l.style.rotate = new Rotate(items[i].angle);
            }
        }

        // One entry per box on the map this frame: what it is, and how to show, hide or move it.
        private readonly List<MapItem> mapItems = new List<MapItem>();
        private readonly List<Action<bool, float>> mapApply = new List<Action<bool, float>>();

        /// <summary>Settles every collision on the map for this frame: run once everything is placed.</summary>
        private void Declutter()
        {
            Gather(false);
            var result = MapDeclutter.Resolve(mapItems);
            for (int i = 0; i < result.Length; i++) mapApply[i]?.Invoke(result[i].shown, result[i].dy);
        }

        /// <summary>
        /// For the Unity CLI and the readouts: how many boxes the map shows, how many names stepped down or hid, and
        /// any pair of shown boxes that still overlap, by kind ("none" is the aim).
        /// </summary>
        public string LabelReport()
        {
            Gather(true);
            var counts = mapItems.GroupBy(m => m.Kind).OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key.ToString().ToLowerInvariant()}");
            var hits = MapDeclutter.Collisions(mapItems);
            return $"shown: {string.Join(", ", counts)}; hidden names {hiddenNames}, moved {movedNames}; overlaps: "
                   + (hits.Count == 0 ? "none" : string.Join(", ", hits.Select(kv => $"{kv.Key} {kv.Value}")));
        }

        private int hiddenNames, movedNames;

        // The boxes, from the positions set this frame and the sizes of the last layout. shownOnly: those showing now.
        private void Gather(bool shownOnly)
        {
            mapItems.Clear(); mapApply.Clear();
            if (!shownOnly) { hiddenNames = 0; movedNames = 0; }
            void Add(MapItem m, Action<bool, float> apply) { mapItems.Add(m); mapApply.Add(apply); }
            bool Showing(VisualElement e) => e.style.display != DisplayStyle.None && (!shownOnly || e.style.visibility != Visibility.Hidden);

            // Pieces: each a box over its foot, at its scale.
            if (hostState != null)
                foreach (var a in hostState.Armies)
                {
                    if (!hosts.TryGetValue(a.Id, out var piece) || piece.style.display == DisplayStyle.None || !Measured(piece, out float w, out float h)) continue;
                    var p = PlacedPoint(a);
                    float sc = HostScale * (a.Id == SelectedArmy ? 1.15f : 1f);
                    Add(new MapItem(MapItemKind.Piece, p.x - w * sc / 2, p.y - h * sc, p.x + w * sc / 2, p.y), null);
                }
            // March tags: centred under their point.
            if (marchLines != null)
                foreach (var tag in marchLines.Tags)
                {
                    if (!Showing(tag) || !Measured(tag, out float w, out float h)) continue;
                    float x = tag.style.left.value.value, y = tag.style.top.value.value;
                    var t = tag;
                    Add(new MapItem(MapItemKind.Tag, x - w / 2, y, x + w / 2, y + h), (shown, dy) => { if (dy != 0) t.style.top = y + dy; });
                }
            // Province names: centred on their point; a capital's hangs below it.
            for (int i = 0; i < provinceLabels.Length; i++)
            {
                var l = provinceLabels[i];
                if (l == null) continue;
                if (!Showing(l) || !Measured(l, out float w, out float h)) { if (!shownOnly) Show(l, true); continue; }
                bool capital = l.ClassListContains("clab");
                float x = l.style.left.value.value, y = l.style.top.value.value, y0 = capital ? y : y - h / 2;
                var label = l; var fort = fortMarks[i];
                float fortTop = fort.style.top.value.value;
                Add(new MapItem(capital ? MapItemKind.Capital : MapItemKind.Name, x - w / 2, y0, x + w / 2, y0 + h, l.resolvedStyle.fontSize), (shown, dy) =>
                {
                    Show(label, shown);
                    if (!shown) hiddenNames++;
                    else if (dy != 0) { movedNames++; label.style.top = y + dy; fort.style.top = fortTop + dy; }
                });
            }
            // Region and realm names: centred, a realm's turned along its land (its box holds the turned text).
            foreach (var (list, kind) in new[] { (regionLabels, MapItemKind.Region), (realmLabels, MapItemKind.Realm) })
                foreach (var l in list)
                {
                    if (!Showing(l) || !Measured(l, out float w, out float h)) { if (!shownOnly) Show(l, true); continue; }
                    float a = kind == MapItemKind.Realm ? l.style.rotate.value.angle.ToDegrees() * Mathf.Deg2Rad : 0;
                    float bw = Mathf.Abs(w * Mathf.Cos(a)) + Mathf.Abs(h * Mathf.Sin(a)), bh = Mathf.Abs(w * Mathf.Sin(a)) + Mathf.Abs(h * Mathf.Cos(a));
                    float x = l.style.left.value.value, y = l.style.top.value.value;
                    var label = l;
                    Add(new MapItem(kind, x - bw / 2, y - bh / 2, x + bw / 2, y + bh / 2, l.resolvedStyle.fontSize),
                        (shown, dy) => Show(label, shown));
                }
        }

        // Written only when it changes: setting a style, even to the same value, can repaint the panel every frame.
        private static void Show(VisualElement e, bool shown)
        {
            var want = shown ? Visibility.Visible : Visibility.Hidden;
            if (e.style.visibility.keyword == StyleKeyword.Undefined && e.style.visibility.value == want) return;
            e.style.visibility = want;
        }

        private static bool Measured(VisualElement e, out float w, out float h)
        {
            w = e.resolvedStyle.width; h = e.resolvedStyle.height;
            return !(float.IsNaN(w) || float.IsNaN(h)) && w >= 1 && h >= 1;
        }
    }
}
