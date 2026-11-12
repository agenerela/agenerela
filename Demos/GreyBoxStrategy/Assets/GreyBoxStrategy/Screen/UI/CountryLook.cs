using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>The heraldic charge drawn on a country's crest.</summary>
    public enum Charge { Chevron, Sun, Waves, Peaks, Star }

    /// <summary>
    /// How one country looks on screen: name, colour, crest, and its ruler's face. One object, so a
    /// generated identity (research, decision 22) replaces these five without touching the screen.
    /// </summary>
    public sealed class CountryLook
    {
        public string Id;
        public string Name;
        public Color32 Colour;
        public Charge Charge;
        /// <summary>Its slot, 0 to 4: the shader tells owners apart by it.</summary>
        public int Slot;

        public static readonly Dictionary<string, CountryLook> Standard = new Dictionary<string, CountryLook>
        {
            ["valen"] = new CountryLook { Id = "valen", Name = "Valen", Colour = new Color32(176, 46, 36, 255), Charge = Charge.Chevron, Slot = 0 },
            ["orsk"] = new CountryLook { Id = "orsk", Name = "Orsk", Colour = new Color32(214, 152, 40, 255), Charge = Charge.Sun, Slot = 1 },
            ["merrow"] = new CountryLook { Id = "merrow", Name = "Merrow", Colour = new Color32(38, 134, 104, 255), Charge = Charge.Waves, Slot = 2 },
            ["kesh"] = new CountryLook { Id = "kesh", Name = "Kesh", Colour = new Color32(58, 80, 170, 255), Charge = Charge.Peaks, Slot = 3 },
            ["ilvar"] = new CountryLook { Id = "ilvar", Name = "Ilvar", Colour = new Color32(128, 60, 146, 255), Charge = Charge.Star, Slot = 4 },
        };

        public static CountryLook Of(string id) => id != null && Standard.TryGetValue(id, out var l) ? l : null;
        public static string NameOf(string id) => Of(id)?.Name ?? id ?? "the neutrals";
    }

    /// <summary>Which face a persona wears. The persona owns the face (decision 21); a country picks one of its variants.</summary>
    public static class Faces
    {
        /// <summary>The column of each persona in <c>persona-portraits.jpg</c> (5 × 2).</summary>
        public static readonly Dictionary<string, int> Column = new Dictionary<string, int>
        {
            ["warmonger"] = 0, ["builder"] = 1, ["opportunist"] = 2, ["merchant"] = 3, ["proud"] = 4,
        };

        /// <summary>The face's place in the sheet as a texture rect, for <see cref="Image.uv"/>.</summary>
        public static Rect Uv(string persona, int variant)
        {
            int col = persona != null && Column.TryGetValue(persona, out int c) ? c : 0;
            int row = Mathf.Clamp(variant, 0, 1);
            return new Rect(col / 5f, 1 - (row + 1) / 2f, 1 / 5f, 1 / 2f);
        }
    }

    /// <summary>A country's crest, drawn by code: a shield in its colour with a charge, gilt-edged.</summary>
    public sealed class Crest : VisualElement
    {
        private CountryLook look;

        public Crest(CountryLook look, float size = 18)
        {
            this.look = look;
            AddToClassList("crest");
            style.width = size;
            style.height = size * 35f / 30f;
            style.flexShrink = 0;
            generateVisualContent += Draw;
        }

        public void Set(CountryLook l) { look = l; MarkDirtyRepaint(); }

        private void Draw(MeshGenerationContext ctx)
        {
            if (look == null) return;
            var r = contentRect;
            float s = r.width / 30f;
            Vector2 P(float x, float y) => new Vector2(r.x + x * s, r.y + y * s);
            var p = ctx.painter2D;
            p.BeginPath();
            p.MoveTo(P(2, 2)); p.LineTo(P(28, 2)); p.LineTo(P(28, 17));
            p.BezierCurveTo(P(28, 26), P(21, 31), P(15, 33));
            p.BezierCurveTo(P(9, 31), P(2, 26), P(2, 17));
            p.ClosePath();
            p.fillColor = look.Colour;
            p.Fill();
            p.strokeColor = new Color32(210, 173, 98, 255);
            p.lineWidth = 1.6f * s;
            p.Stroke();

            var ink = new Color32(244, 230, 196, 255);
            p.fillColor = ink;
            p.strokeColor = ink;
            switch (look.Charge)
            {
                case Charge.Chevron:
                    p.BeginPath(); p.MoveTo(P(5, 22)); p.LineTo(P(15, 11)); p.LineTo(P(25, 22)); p.LineTo(P(22, 22)); p.LineTo(P(15, 14.5f)); p.LineTo(P(8, 22)); p.ClosePath(); p.Fill();
                    break;
                case Charge.Sun:
                    p.BeginPath(); p.Arc(P(15, 15), 4.2f * s, Angle.Degrees(0), Angle.Degrees(360)); p.Fill();
                    p.lineWidth = 1.6f * s;
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * Mathf.PI / 4;
                        p.BeginPath(); p.MoveTo(P(15 + Mathf.Cos(a) * 6, 15 + Mathf.Sin(a) * 6)); p.LineTo(P(15 + Mathf.Cos(a) * 9, 15 + Mathf.Sin(a) * 9)); p.Stroke();
                    }
                    break;
                case Charge.Waves:
                    p.lineWidth = 1.8f * s;
                    foreach (float y in new[] { 11f, 16f, 21f })
                    {
                        p.BeginPath(); p.MoveTo(P(6, y)); p.BezierCurveTo(P(9, y - 2), P(12, y + 2), P(15, y)); p.BezierCurveTo(P(18, y - 2), P(21, y + 2), P(24, y)); p.Stroke();
                    }
                    break;
                case Charge.Peaks:
                    p.BeginPath(); p.MoveTo(P(5, 23)); p.LineTo(P(11, 13)); p.LineTo(P(14, 17)); p.LineTo(P(18, 9)); p.LineTo(P(25, 23)); p.ClosePath(); p.Fill();
                    break;
                case Charge.Star:
                    p.BeginPath();
                    for (int k = 0; k < 10; k++)
                    {
                        float a = -Mathf.PI / 2 + k * Mathf.PI / 5, rr = k % 2 == 0 ? 9 : 3.8f;
                        var q = P(15 + Mathf.Cos(a) * rr, 15.5f + Mathf.Sin(a) * rr);
                        if (k == 0) p.MoveTo(q); else p.LineTo(q);
                    }
                    p.ClosePath(); p.Fill();
                    break;
            }
        }
    }

    /// <summary>The screen's small icons, drawn by code so they stay sharp at any scale (decision 28, 4K).</summary>
    public enum IconKind { Province, Army, Gold, War, Truce, Fort, Play, Pause, Step, Scroll, Peace, Eye }

    public sealed class Icon : VisualElement
    {
        private readonly IconKind kind;
        public Color Tint = new Color32(194, 179, 147, 255);

        public Icon(IconKind kind, float size = 12)
        {
            this.kind = kind;
            AddToClassList("icon");
            style.width = size; style.height = size; style.flexShrink = 0;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect; float s = r.width / 16f;
            Vector2 P(float x, float y) => new Vector2(r.x + x * s, r.y + y * s);
            var p = ctx.painter2D;
            var c = resolvedStyle.color.a > 0 ? resolvedStyle.color : Tint;
            p.fillColor = c; p.strokeColor = c; p.lineWidth = 1.5f * s;
            void Poly(params float[] xy) { p.BeginPath(); p.MoveTo(P(xy[0], xy[1])); for (int i = 2; i < xy.Length; i += 2) p.LineTo(P(xy[i], xy[i + 1])); p.ClosePath(); p.Fill(); }
            switch (kind)
            {
                case IconKind.Province: Poly(3, 1, 4.5f, 1, 4.5f, 15, 3, 15); Poly(5, 2, 13, 2, 11, 5, 13, 8, 5, 8); break;
                case IconKind.Army: Poly(8, 1, 10, 4, 9, 4, 9, 8, 12, 8, 12, 10, 9, 10, 9, 15, 7, 15, 7, 10, 4, 10, 4, 8, 7, 8, 7, 4, 6, 4); break;
                case IconKind.Gold: p.BeginPath(); p.Arc(P(8, 8), 6.2f * s, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke(); p.BeginPath(); p.Arc(P(8, 8), 2.6f * s, Angle.Degrees(0), Angle.Degrees(360)); p.Fill(); break;
                case IconKind.War: p.BeginPath(); p.MoveTo(P(2, 2)); p.LineTo(P(14, 14)); p.Stroke(); p.BeginPath(); p.MoveTo(P(14, 2)); p.LineTo(P(2, 14)); p.Stroke(); break;
                case IconKind.Truce: Poly(3, 1, 13, 1, 8, 8); Poly(8, 8, 13, 15, 3, 15); break;
                case IconKind.Fort: Poly(2, 15, 2, 5, 4, 5, 4, 7, 6, 7, 6, 5, 7.5f, 5, 7.5f, 2, 8.5f, 2, 8.5f, 5, 10, 5, 10, 7, 12, 7, 12, 5, 14, 5, 14, 15); break;
                case IconKind.Play: Poly(4, 2, 14, 8, 4, 14); break;
                case IconKind.Pause: Poly(4, 3, 7, 3, 7, 13, 4, 13); Poly(9, 3, 12, 3, 12, 13, 9, 13); break;
                case IconKind.Step: Poly(3, 2, 11, 8, 3, 14); Poly(12, 2, 14, 2, 14, 14, 12, 14); break;
                case IconKind.Scroll: Poly(4, 1, 13, 1, 13, 15, 3, 15, 3, 12, 4, 12); break;
                case IconKind.Peace: p.BeginPath(); p.MoveTo(P(2, 14)); p.BezierCurveTo(P(5, 8), P(9, 4), P(14, 2)); p.Stroke(); break;
                case IconKind.Eye: p.BeginPath(); p.Arc(P(8, 8), 2.4f * s, Angle.Degrees(0), Angle.Degrees(360)); p.Fill(); p.BeginPath(); p.MoveTo(P(1, 8)); p.BezierCurveTo(P(5, 2), P(11, 2), P(15, 8)); p.BezierCurveTo(P(11, 14), P(5, 14), P(1, 8)); p.Stroke(); break;
            }
        }
    }
}
