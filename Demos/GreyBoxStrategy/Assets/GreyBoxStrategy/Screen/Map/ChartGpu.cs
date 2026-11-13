using System;
using System.Collections.Generic;
using GreyBoxStrategy.Chart;
using UnityEngine;
using UnityEngine.Rendering;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// Draws a generated world's chart on the GPU, from what <see cref="ChartImage.Make"/> prepared in
    /// GPU mode: the ground in one shader pass (<c>GreyBoxStrategy/ChartGround</c>), then rivers and
    /// roads as strips and every glyph as a quad from the atlas (<c>GreyBoxStrategy/ChartMarks</c>),
    /// into one render texture with mipmaps that the map shader reads as its picture.
    /// </summary>
    public static class ChartGpu
    {
        /// <summary>Whether this machine can draw it: render textures, float textures and 32-bit mesh indices.</summary>
        public static bool Supported =>
            SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32) && SystemInfo.SupportsTextureFormat(TextureFormat.RFloat)
            && Shader.Find("GreyBoxStrategy/ChartGround") != null && Shader.Find("GreyBoxStrategy/ChartMarks") != null;

        private static readonly List<UnityEngine.Object> made = new List<UnityEngine.Object>();

        /// <param name="ids">The province map's texture, as the map shader has it.</param>
        public static RenderTexture Render(ChartImage img, Texture2D ids, Texture2D paper, float paperMean, int seed, Shader ground, Shader marks)
        {
            ReleaseInputs();
            int w = img.ChartWidth, h = img.ChartHeight;
            var rt = new RenderTexture(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0)
            {
                sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear,
                useMipMap = true,
                autoGenerateMips = false,
            }) { name = "Chart (drawn on the GPU)", filterMode = FilterMode.Trilinear, anisoLevel = 4, wrapMode = TextureWrapMode.Clamp };
            rt.Create();

            // ---- the ground ----
            var mat = Keep(new Material(ground) { name = "ChartGround (runtime)" });
            mat.SetTexture("_IdTex", ids);
            mat.SetTexture("_SdfTex", Float(img.CoastSdf, img.IdWidth, img.IdHeight, 1, "CoastSdf", FilterMode.Bilinear));
            var lit = new float[img.Fields.Lit.Length / 3 * 4];
            for (int i = 0; i < lit.Length / 4; i++)
            {
                lit[i * 4] = img.Fields.Lit[i * 3] / 255f; lit[i * 4 + 1] = img.Fields.Lit[i * 3 + 1] / 255f; lit[i * 4 + 2] = img.Fields.Lit[i * 3 + 2] / 255f; lit[i * 4 + 3] = 1;
            }
            mat.SetTexture("_LitTex", Float(lit, img.GridWidth, img.GridHeight, 4, "LitTerrain", FilterMode.Bilinear));
            mat.SetTexture("_FarmTex", Float(img.Fields.Farmland, img.GridWidth, img.GridHeight, 1, "Farmland", FilterMode.Bilinear));
            var lut = Keep(new Texture2D(Math.Max(1, img.TerrainOf.Length), 1, TextureFormat.R8, false, true) { filterMode = FilterMode.Point, name = "TerrainLut" });
            lut.SetPixelData(img.TerrainOf, 0); lut.Apply(false, true);
            mat.SetTexture("_TerrainLut", lut);
            var grain = Keep(new Texture2D(256, 256, TextureFormat.RFloat, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "Grain" });
            grain.SetPixelData(Flip(img.Grain, 256, 256, 1), 0); grain.Apply(false, true);
            mat.SetTexture("_GrainTex", grain);
            var sea = Keep(new Texture2D(img.SeaTileSize, img.SeaTileSize, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "SeaTile (values)" });
            sea.SetPixelData(Flip(img.SeaTile, img.SeaTileSize, img.SeaTileSize, 4), 0); sea.Apply(false, true);
            mat.SetTexture("_SeaTex", sea);
            if (paper != null) mat.SetTexture("_PaperTex", paper);
            mat.SetVector("_Grid", new Vector4(img.GridWidth, img.GridHeight, img.Scale, img.IdWidth / (float)img.ChartWidth));
            var rh = img.Scene.Rhumbs;
            mat.SetVector("_Rhumbs", new Vector4(rh[0].u, rh[0].v, rh.Count > 1 ? rh[1].u : rh[0].u, rh.Count > 1 ? rh[1].v : rh[0].v));
            mat.SetVector("_Paper", new Vector4(paper != null ? 1 : 0, Mathf.Max(0.01f, paperMean), 16, 0));
            mat.SetInt("_Seed", seed);
            Graphics.Blit(null, rt, mat);

            // ---- the marks ----
            var marksMat = Keep(new Material(marks) { name = "ChartMarks (runtime)" });
            var atlas = Keep(new Texture2D(img.Atlas.Width, img.Atlas.Height, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Glyph atlas" });
            atlas.SetPixelData(img.Atlas.Rgba, 0); atlas.Apply(false, true);
            marksMat.SetTexture("_MainTex", atlas);
            var cmd = new CommandBuffer { name = "Chart marks" };
            cmd.SetRenderTarget(rt);
            // Chart pixels, y down from the top, onto a texture whose v runs up: measured, the GPU projection
            // for a texture target already accounts for the flip, so it is asked for without it.
            cmd.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(0, w, h, 0, -1, 1), false));
            var strokes = Keep(Strokes(img));
            cmd.DrawMesh(strokes, Matrix4x4.identity, marksMat, 0, 0);
            var quads = Keep(Quads(img.Atlas));
            cmd.DrawMesh(quads, Matrix4x4.identity, marksMat, 0, 1);
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Release();
            rt.GenerateMips();
            return rt;
        }

        /// <summary>The intermediate textures, meshes and materials of the last render; the chart itself stays with its owner.</summary>
        public static void ReleaseInputs()
        {
            foreach (var o in made) if (o != null) UnityEngine.Object.Destroy(o);
            made.Clear();
        }

        private static T Keep<T>(T o) where T : UnityEngine.Object { made.Add(o); return o; }

        private static Texture2D Float(float[] top, int w, int h, int channels, string name, FilterMode filter)
        {
            var t = Keep(new Texture2D(w, h, channels == 1 ? TextureFormat.RFloat : TextureFormat.RGBAFloat, false, true) { filterMode = filter, wrapMode = TextureWrapMode.Clamp, name = name });
            t.SetPixelData(Flip(top, w, h, channels), 0);
            t.Apply(false, true);
            return t;
        }

        private static T[] Flip<T>(T[] top, int w, int h, int channels)
        {
            var o = new T[top.Length];
            int row = w * channels;
            for (int y = 0; y < h; y++) Array.Copy(top, (h - 1 - y) * row, o, y * row, row);
            return o;
        }

        // ---- rivers and roads: two strips each where they have a casing, rivers under roads ----
        private static Mesh Strokes(ChartImage img)
        {
            float s = img.Scale;
            var v = new List<Vector3>(); var uv0 = new List<Vector2>(); var uv1 = new List<Vector2>(); var col = new List<Color>(); var ix = new List<int>();
            void Strip(Stroke st, float halfPx, Color c, bool dashed)
            {
                var p = st.Points;
                if (p.Count < 2) return;
                float along = 0;
                int first = v.Count;
                for (int k = 0; k < p.Count; k++)
                {
                    Vector2 a = new Vector2(p[Math.Max(0, k - 1)].u, p[Math.Max(0, k - 1)].v), b = new Vector2(p[Math.Min(p.Count - 1, k + 1)].u, p[Math.Min(p.Count - 1, k + 1)].v);
                    Vector2 dir = (b - a).sqrMagnitude > 1e-8f ? (b - a).normalized : Vector2.right;
                    Vector2 n = new Vector2(-dir.y, dir.x);
                    if (k > 0) along += Vector2.Distance(new Vector2(p[k - 1].u, p[k - 1].v), new Vector2(p[k].u, p[k].v));
                    Vector2 at = new Vector2(p[k].u, p[k].v) * s;
                    float r = halfPx + 1;
                    v.Add(at + n * r); v.Add(at - n * r);
                    uv0.Add(new Vector2(along, 1)); uv0.Add(new Vector2(along, -1));
                    uv1.Add(new Vector2(halfPx, dashed ? 1 : 0)); uv1.Add(new Vector2(halfPx, dashed ? 1 : 0));
                    col.Add(c); col.Add(c);
                    if (k > 0)
                    {
                        int i0 = first + (k - 1) * 2;
                        ix.Add(i0); ix.Add(i0 + 2); ix.Add(i0 + 1);
                        ix.Add(i0 + 1); ix.Add(i0 + 2); ix.Add(i0 + 3);
                    }
                }
            }
            Color C(Ink i, float a = 1) => new Color(i.R / 255f, i.G / 255f, i.B / 255f, a);
            foreach (var st in img.Scene.Strokes)
                if (st.Kind == StrokeKind.River) Strip(st, (st.Width * s + Mathf.Max(1.2f, 0.07f * s)) / 2, C(new Ink(38, 28, 18), 0.75f), false);
            foreach (var st in img.Scene.Strokes)
                if (st.Kind == StrokeKind.River) Strip(st, st.Width * s / 2, C(ChartPainter.RiverTone), false);
            foreach (var st in img.Scene.Strokes)
                if (st.Kind == StrokeKind.Highway) Strip(st, (st.Width * s + Mathf.Max(1.4f, 0.09f * s)) / 2, C(new Ink(38, 28, 18), 0.8f), false);
            foreach (var st in img.Scene.Strokes)
                if (st.Kind == StrokeKind.Highway) Strip(st, st.Width * s / 2, C(new Ink(214, 192, 142)), false);
                else if (st.Kind == StrokeKind.Track) Strip(st, st.Width * s / 2, C(new Ink(58, 40, 24), 0.9f), true);
            var mesh = new Mesh { name = "Chart strokes", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(v); mesh.SetUVs(0, uv0); mesh.SetUVs(1, uv1); mesh.SetColors(col); mesh.SetTriangles(ix, 0);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
            return mesh;
        }

        // ---- glyphs: one quad each, in the scene's order, back to front ----
        private static Mesh Quads(ChartAtlas atlas)
        {
            int n = atlas.Quads.Count;
            var v = new Vector3[n * 4]; var uv = new Vector2[n * 4]; var ix = new int[n * 6];
            for (int k = 0; k < n; k++)
            {
                var q = atlas.Quads[k];
                v[k * 4] = new Vector3(q.X0, q.Y0); v[k * 4 + 1] = new Vector3(q.X1, q.Y0); v[k * 4 + 2] = new Vector3(q.X1, q.Y1); v[k * 4 + 3] = new Vector3(q.X0, q.Y1);
                // The atlas is uploaded top row first, as drawn, so its v runs down the sheet like the quads' y.
                uv[k * 4] = new Vector2(q.U0, q.V0); uv[k * 4 + 1] = new Vector2(q.U1, q.V0); uv[k * 4 + 2] = new Vector2(q.U1, q.V1); uv[k * 4 + 3] = new Vector2(q.U0, q.V1);
                ix[k * 6] = k * 4; ix[k * 6 + 1] = k * 4 + 1; ix[k * 6 + 2] = k * 4 + 2; ix[k * 6 + 3] = k * 4; ix[k * 6 + 4] = k * 4 + 2; ix[k * 6 + 5] = k * 4 + 3;
            }
            var mesh = new Mesh { name = "Chart glyphs", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = v; mesh.uv = uv; mesh.triangles = ix;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f);
            return mesh;
        }
    }
}
