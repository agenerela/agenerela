using System;
using System.Collections.Generic;
using GreyBoxStrategy.Chart;
using Unity.Collections;
using UnityEngine;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The map: one quad, one material (<c>GreyBoxStrategy/PaintedMap</c>), and the data the
    /// shader reads. Owners, the capture and the focus are numbers written into small textures, so
    /// nothing here touches a pixel of the picture under them.
    /// </summary>
    /// <remarks>
    /// Two pictures: the Classic board's painting with its baked province map (<see cref="Build"/>),
    /// or a generated world's chart, drawn by code at the screen's size (<see cref="BuildChart"/>,
    /// rules v2 section 2.6). The shader, the wash, the borders and the capture are the same for both.
    /// </remarks>
    public sealed class MapView : MonoBehaviour
    {
        /// <summary>The state texture's width on the Classic board; a larger world has one column per province.</summary>
        public const int StateColumns = 32;
        public const float PixelsPerUnit = 100f;
        private const ushort NoProvince = 0xFFFF;

        [SerializeField] private Shader shader;
        [SerializeField] private Texture2D painting;
        [SerializeField] private Texture2D provinceIds;
        [SerializeField] private Texture2D provinceEdge;
        [SerializeField] private TextAsset provincePoints;
        [SerializeField] private Shader seaShader;
        [SerializeField] private Texture2D seaTile;

        private Material material;
        private Texture2D state;
        private Texture2D spread;
        private ushort[] ids;          // bottom row first, as the textures
        private int width, height;
        private int count = 25;
        private int columns = StateColumns;
        private List<int>[] pixels;
        private Vector2[] labelUv;
        private Color32[] stateData;
        private Vector2 sizeUnits;

        public int Width => width;
        public int Height => height;
        public int Count => count;
        public Vector2 SizeUnits => sizeUnits;
        /// <summary>True when the map is a generated world's drawn chart rather than the Classic painting.</summary>
        public bool Drawn { get; private set; }
        /// <summary>For a drawn chart: how it was made, for the readouts.</summary>
        public string ChartReport { get; private set; }
        public bool Engraved { set { if (material != null) material.SetFloat("_Engraved", value ? 1 : 0); } }

        /// <summary>The Classic board: the painting and its baked province map.</summary>
        public void Build()
        {
            width = provinceIds.width;
            height = provinceIds.height;
            count = 25;
            Allocate();
            sizeUnits = new Vector2(width / PixelsPerUnit, height / PixelsPerUnit);
            ReadIds();
            ReadPoints();
            Assemble(painting, provinceIds, provinceEdge, false);
            if (seaShader != null && seaTile != null) AddSea(seaTile, sizeUnits.y * 0.42f, null);
        }

        /// <summary>
        /// A generated world: the chart drawn by <see cref="ChartImage"/>, its province map with
        /// 16-bit ids, and its own sea tile behind it. Label points come from the geometry.
        /// </summary>
        public void BuildChart(ChartImage chart, WorldMap world, int seed = 0)
        {
            Drawn = true;
            width = chart.IdWidth;
            height = chart.IdHeight;
            count = world.Provinces.Count;
            Allocate();
            sizeUnits = new Vector2(width / PixelsPerUnit, height / PixelsPerUnit);

            // Province ids, flipped to bottom-first, into an RG16 texture (red the low byte).
            ids = new ushort[width * height];
            var idBytes = new byte[width * height * 2];
            var edgeBytes = new byte[width * height];
            for (int y = 0; y < height; y++)
            {
                int src = (height - 1 - y) * width, dst = y * width;
                for (int x = 0; x < width; x++)
                {
                    ushort v = chart.Ids[src + x];
                    ushort id = ChartRaster.IsWater(v) ? NoProvince : v;
                    ids[dst + x] = id;
                    idBytes[(dst + x) * 2] = (byte)(v & 0xFF);
                    idBytes[(dst + x) * 2 + 1] = (byte)(v >> 8);
                    edgeBytes[dst + x] = chart.Edge[src + x];
                    if (id != NoProvince) pixels[id].Add(dst + x);
                }
            }
            var idTex = new Texture2D(width, height, TextureFormat.RG16, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "ProvinceIds (drawn)" };
            idTex.SetPixelData(idBytes, 0); idTex.Apply(false, true);
            var edgeTex = new Texture2D(width, height, TextureFormat.R8, false, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "ProvinceEdge (drawn)" };
            edgeTex.SetPixelData(edgeBytes, 0); edgeTex.Apply(false, true);
            Texture chartTex;
            var drawWatch = System.Diagnostics.Stopwatch.StartNew();
            if (chart.Gpu)
            {
                // The ground in a shader, the glyphs as quads from their atlas: the chart is drawn on the GPU.
                gpuChart = ChartGpu.Render(chart, idTex, Paper, PaperMean, seed, Shader.Find("GreyBoxStrategy/ChartGround"), Shader.Find("GreyBoxStrategy/ChartMarks"));
                chartTex = gpuChart;
                // Wait for the GPU, so the time measured is the draw's, not only its submission.
                UnityEngine.Rendering.AsyncGPUReadback.Request(gpuChart, 0, 0, 1, 0, 1, 0, 1).WaitForCompletion();
            }
            else
            {
                var t = new Texture2D(chart.ChartWidth, chart.ChartHeight, TextureFormat.RGBA32, true, false) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 4, name = "Chart (drawn)" };
                t.SetPixelData(Flip(chart.Chart, chart.ChartWidth, chart.ChartHeight, 4), 0); t.Apply(true, true);
                chartTex = t;
            }
            chart.Milliseconds["upload and draw"] = drawWatch.ElapsedMilliseconds;
            ChartWidth = chart.ChartWidth;

            // Label points and the land's box, from the geometry.
            var geo = world.Geometry;
            for (int p = 0; p < count; p++)
            {
                var l = geo.Provinces[p].Label;
                labelUv[p] = new Vector2((l[0] + 0.5f) / geo.Width, 1 - (l[1] + 0.5f) / geo.Height);
            }
            var cells = geo.DecodeCells();
            int minX = geo.Width, minY = geo.Height, maxX = 0, maxY = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] < 0) continue;
                int x = i % geo.Width, y = i / geo.Width;
                if (x < minX) minX = x; if (x > maxX) maxX = x; if (y < minY) minY = y; if (y > maxY) maxY = y;
            }
            var s = sizeUnits;
            LandWorld = Rect.MinMaxRect((minX / (float)geo.Width - 0.5f) * s.x, (0.5f - (maxY + 1) / (float)geo.Height) * s.y,
                ((maxX + 1) / (float)geo.Width - 0.5f) * s.x, (0.5f - minY / (float)geo.Height) * s.y);

            Assemble(chartTex, idTex, edgeTex, true);
            material.SetFloat("_EdgeFade", 0.03f);

            var tile = new Texture2D(chart.SeaTileSize, chart.SeaTileSize, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, name = "Sea tile (drawn)" };
            tile.SetPixelData(Flip(chart.SeaTile, chart.SeaTileSize, chart.SeaTileSize, 4), 0); tile.Apply(true, true);
            if (seaShader != null) AddSea(tile, ChartPainter.SeaPeriodCells * s.x / geo.Width, 0.0f);

            long total = 0; foreach (var v in chart.Milliseconds.Values) total += v;
            ChartReport = $"chart {chart.ChartWidth}×{chart.ChartHeight}, province map {width}×{height}, {count} provinces, {chart.Glyphs} glyphs, drawn in {total} ms ({string.Join(", ", chart.Milliseconds)})";
        }

        private RenderTexture gpuChart;

        /// <summary>The width the chart was last drawn at, in pixels.</summary>
        public int ChartWidth { get; private set; }

        /// <summary>
        /// Draws the chart again at another width (the window changed size), on the GPU, and swaps it in:
        /// the province map, the state and the camera stay as they are. False where the GPU cannot draw it.
        /// </summary>
        public bool RedrawChart(ChartImage chart, int seed)
        {
            if (!chart.Gpu || material == null) return false;
            var idBytes = new byte[chart.IdWidth * chart.IdHeight * 2];
            for (int y = 0; y < chart.IdHeight; y++)
            {
                int src = (chart.IdHeight - 1 - y) * chart.IdWidth, dst = y * chart.IdWidth;
                for (int x = 0; x < chart.IdWidth; x++)
                {
                    ushort v = chart.Ids[src + x];
                    idBytes[(dst + x) * 2] = (byte)(v & 0xFF);
                    idBytes[(dst + x) * 2 + 1] = (byte)(v >> 8);
                }
            }
            var idTex = new Texture2D(chart.IdWidth, chart.IdHeight, TextureFormat.RG16, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "ProvinceIds (redraw)" };
            idTex.SetPixelData(idBytes, 0); idTex.Apply(false, true);
            var drawn = ChartGpu.Render(chart, idTex, Paper, PaperMean, seed, Shader.Find("GreyBoxStrategy/ChartGround"), Shader.Find("GreyBoxStrategy/ChartMarks"));
            Destroy(idTex);
            material.SetTexture("_MainTex", drawn);
            if (gpuChart != null) { gpuChart.Release(); Destroy(gpuChart); }
            gpuChart = drawn;
            ChartWidth = chart.ChartWidth;
            return true;
        }

        /// <summary>The paper tile laid over a drawn chart's land (<c>Screen/Art/Resources/chart-paper.png</c>), generated art; null when missing.</summary>
        public static Texture2D Paper => paper != null ? paper : paper = Resources.Load<Texture2D>("chart-paper");
        private static Texture2D paper;
        /// <summary>The paper tile's mean brightness, 0 to 1: the shader multiplies by the paper relative to it, so the land keeps its tone.</summary>
        public const float PaperMean = 0.673f;

        private void OnDestroy()
        {
            ChartGpu.ReleaseInputs();
            if (gpuChart != null) { gpuChart.Release(); Destroy(gpuChart); }
        }

        private static byte[] Flip(byte[] top, int w, int h, int bpp)
        {
            var o = new byte[top.Length];
            int row = w * bpp;
            for (int y = 0; y < h; y++) Buffer.BlockCopy(top, (h - 1 - y) * row, o, y * row, row);
            return o;
        }

        private void Allocate()
        {
            columns = Math.Max(StateColumns, count);
            pixels = new List<int>[count];
            for (int p = 0; p < count; p++) pixels[p] = new List<int>();
            labelUv = new Vector2[count];
            stateData = new Color32[columns * Rows];
        }

        private void Assemble(Texture main, Texture idTex, Texture edgeTex, bool wide)
        {
            material = new Material(shader) { name = "PaintedMap (runtime)" };
            material.SetTexture("_MainTex", main);
            material.SetTexture("_IdTex", idTex);
            material.SetTexture("_EdgeTex", edgeTex);
            material.SetFloat("_IdWide", wide ? 1 : 0);

            state = new Texture2D(columns, Rows, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "ProvinceState" };
            spread = new Texture2D(width, height, TextureFormat.R8, false, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "CaptureSpread" };
            material.SetTexture("_StateTex", state);
            material.SetTexture("_SpreadTex", spread);
            ApplyState();

            var mesh = Quad();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private Mesh Quad()
        {
            var mesh = new Mesh { name = "MapQuad" };
            var s = sizeUnits;
            mesh.vertices = new[] { new Vector3(-s.x / 2, -s.y / 2), new Vector3(s.x / 2, -s.y / 2), new Vector3(-s.x / 2, s.y / 2), new Vector3(s.x / 2, s.y / 2) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The open sea behind the map, far wider than any window, so the map fills the screen edge to edge.</summary>
        private void AddSea(Texture2D tile, float tileUnits, float? flatten)
        {
            var sea = new GameObject("OpenSea");
            sea.transform.SetParent(transform, false);
            sea.transform.localPosition = new Vector3(0, 0, 1);
            sea.transform.localScale = new Vector3(8, 8, 1);
            tile.wrapMode = TextureWrapMode.Repeat;
            sea.AddComponent<MeshFilter>().sharedMesh = GetComponent<MeshFilter>().sharedMesh;
            var seaMat = new Material(seaShader) { name = "SeaBackdrop (runtime)" };
            seaMat.SetTexture("_MainTex", tile);
            seaMat.SetFloat("_TileUnits", tileUnits);
            if (flatten.HasValue) { seaMat.SetFloat("_Flatten", flatten.Value); seaMat.SetFloat("_Shade", 1f); }
            var r = sea.AddComponent<MeshRenderer>(); r.sharedMaterial = seaMat; r.sortingOrder = -10;
            GetComponent<MeshRenderer>().sortingOrder = 0;
        }

        private void ReadIds()
        {
            // The id map is an 8-bit grey PNG: index * 10 for land, 255 for sea. Read it once.
            var raw = provinceIds.GetPixels32();
            ids = new ushort[raw.Length];
            for (int i = 0; i < raw.Length; i++)
            {
                byte v = raw[i].r;
                ushort id = v >= 250 ? NoProvince : (ushort)Mathf.RoundToInt(v / 10f);
                ids[i] = id;
                if (id < count) pixels[id].Add(i);
            }
        }

        private void ReadPoints()
        {
            var root = Newtonsoft.Json.Linq.JObject.Parse(provincePoints.text);
            var s = SizeUnits;
            if (root["land"] is Newtonsoft.Json.Linq.JArray land)
            {
                float x0 = (float)land[0], y0 = (float)land[1], x1 = (float)land[2], y1 = (float)land[3];
                LandWorld = Rect.MinMaxRect((x0 - 0.5f) * s.x, (0.5f - y1) * s.y, (x1 - 0.5f) * s.x, (0.5f - y0) * s.y);
            }
            else LandWorld = new Rect(-s.x * 0.45f, -s.y * 0.45f, s.x * 0.9f, s.y * 0.9f);
            foreach (var p in (Newtonsoft.Json.Linq.JArray)root["provinces"])
                labelUv[(int)p["index"]] = new Vector2((float)p["x"], 1 - (float)p["y"]);
        }

        /// <summary>The world position of a province's label point.</summary>
        public Vector3 ProvinceWorld(int index)
        {
            var uv = labelUv[index];
            var s = SizeUnits;
            return transform.TransformPoint(new Vector3((uv.x - 0.5f) * s.x, (uv.y - 0.5f) * s.y, 0));
        }

        /// <summary>A point given in the world's grid units (a drawn chart only), in world space.</summary>
        public Vector3 GridWorld(float u, float v, int gridWidth, int gridHeight)
        {
            var s = SizeUnits;
            return transform.TransformPoint(new Vector3((u / gridWidth - 0.5f) * s.x, (0.5f - v / gridHeight) * s.y, 0));
        }

        /// <summary>The province under a world position, or -1 for the sea.</summary>
        public int ProvinceAt(Vector3 world)
        {
            var local = transform.InverseTransformPoint(world);
            var s = SizeUnits;
            int x = Mathf.FloorToInt((local.x / s.x + 0.5f) * width), y = Mathf.FloorToInt((local.y / s.y + 0.5f) * height);
            if (x < 0 || y < 0 || x >= width || y >= height) return -1;
            ushort id = ids[y * width + x];
            return id < count ? id : -1;
        }

        // ---- state ----

        /// <summary>Paints every province in its owner's colour; null owners are neutral.</summary>
        public void SetOwners(IReadOnlyList<string> owners, Func<string, Color32> colourOf, Func<string, int> slotOf)
        {
            for (int p = 0; p < owners.Count; p++) WriteOwner(p, 0, owners[p], colourOf, slotOf);
            ApplyState();
        }

        private void WriteOwner(int p, int row, string owner, Func<string, Color32> colourOf, Func<string, int> slotOf)
        {
            var c = owner == null ? new Color32(0, 0, 0, 0) : colourOf(owner);
            // The shader only compares slots, so any distinct values do; 20 apart leaves room for ten.
            c.a = owner == null ? (byte)0 : (byte)(20 * (slotOf(owner) + 1));
            stateData[row * columns + p] = c;
        }

        public void SetSelected(int province)
        {
            for (int p = 0; p < count; p++) stateData[2 * columns + p].a = (byte)(p == province ? 255 : 0);
            ApplyState();
        }

        /// <summary>
        /// A map mode's overlay: <paramref name="strength"/> scales each province's wash (null for
        /// full), <paramref name="reach"/> hatches provinces, <paramref name="dim"/> fades the rest.
        /// </summary>
        public void SetOverlay(IReadOnlyList<float> strength, ISet<int> reach, ISet<int> dim)
        {
            for (int p = 0; p < count; p++)
            {
                float k = strength == null ? 1 : Mathf.Clamp01(strength[p]);
                stateData[3 * columns + p] = new Color32((byte)(reach != null && reach.Contains(p) ? 255 : 0),
                    (byte)(dim != null && dim.Contains(p) ? 255 : 0), (byte)Mathf.RoundToInt((1 - k) * 255), 0);
            }
            ApplyState();
        }

        /// <summary>The land's bounding box in world units.</summary>
        public Rect LandWorld { get; private set; }

        private void ApplyState()
        {
            state.SetPixels32(stateData);
            state.Apply(false);
        }

        /// <summary>For tests and the readouts: the state texture's size.</summary>
        public Vector2Int StateSize => new Vector2Int(columns, Rows);

        /// <summary>The state texture's rows: owner, before, capture, overlay, occupier, hatch sweep.</summary>
        public const int Rows = 6;

        // ---- occupation (the writs turn) ----

        /// <summary>
        /// The occupier of each province, as inked diagonal hatching in its colour over the owner's wash
        /// (rules v2 5.1). Null where nobody occupies; a province with no occupier given keeps none.
        /// </summary>
        public void SetOccupiers(IReadOnlyList<string> occupiers, Func<string, Color32> colourOf)
        {
            for (int p = 0; p < count; p++)
            {
                string o = occupiers == null || p >= occupiers.Count ? null : occupiers[p];
                var c = o == null ? new Color32(0, 0, 0, 0) : colourOf(o);
                c.a = (byte)(o == null ? 0 : 255);
                stateData[4 * columns + p] = c;
                stateData[5 * columns + p] = new Color32(0, 0, 0, 0);
                var extra = stateData[3 * columns + p];
                stateData[3 * columns + p] = new Color32(extra.r, extra.g, extra.b, 0);
            }
            ApplyState();
        }

        /// <summary>
        /// One province's hatching on its way in or out. <paramref name="amount"/> 0 to 1; with
        /// <paramref name="from"/> set it is drawn in stroke by stroke from that side (a province index),
        /// else it lifts from the edges inward as the amount falls. <paramref name="thicken"/> widens the
        /// strokes until they flow together, as a treaty turns it into the new owner's wash.
        /// </summary>
        public void SetHatch(int province, string occupier, Func<string, Color32> colourOf, float amount, int from = -1, float thicken = 0)
        {
            if (province < 0 || province >= count) return;
            var c = occupier == null ? new Color32(0, 0, 0, 0) : colourOf(occupier);
            c.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(amount) * 255);
            stateData[4 * columns + province] = c;
            var uv = labelUv[province];
            byte angle = 0, sweep = 0;
            if (from >= 0 && from < count)
            {
                var d = labelUv[from] - uv;
                angle = (byte)Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(d.y, d.x) / (2 * Mathf.PI), 1) * 255);
                sweep = 255;
            }
            // The sweep's centre and its reach: a province's share of the land, as the focus has it.
            stateData[5 * columns + province] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(uv.x) * 255), (byte)Mathf.RoundToInt(Mathf.Clamp01(uv.y) * 255), angle, sweep);
            var extra = stateData[3 * columns + province];
            stateData[3 * columns + province] = new Color32(extra.r, extra.g, extra.b, (byte)Mathf.RoundToInt(Mathf.Clamp01(thicken) * 255));
            material.SetFloat("_HatchReach", Drawn ? 0.09f * Mathf.Sqrt(25f / count) : 0.09f);
            ApplyState();
        }

        // ---- the capture ----

        /// <summary>
        /// Starts a capture: <paramref name="province"/> keeps its old colour in row 1 and gets the
        /// attacker's in row 0, and the spread field is built from the attacker's border inward.
        /// </summary>
        public void BeginCapture(int province, string attacker, string from, Func<string, Color32> colourOf, Func<string, int> slotOf,
            IReadOnlyList<string> ownersBefore)
        {
            WriteOwner(province, 1, from, colourOf, slotOf);
            WriteOwner(province, 0, attacker, colourOf, slotOf);
            stateData[2 * columns + province] = new Color32(0, 0, 255, stateData[2 * columns + province].a);
            BuildSpread(province, attacker, ownersBefore);
            ApplyState();
        }

        /// <summary>Moves the capture on: <paramref name="fall"/> lifts the old wash, <paramref name="front"/> spreads the new one.</summary>
        public void SetCaptureProgress(int province, float fall, float front)
        {
            var c = stateData[2 * columns + province];
            stateData[2 * columns + province] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(front) * 255), (byte)Mathf.RoundToInt(Mathf.Clamp01(fall) * 255), 255, c.a);
            ApplyState();
        }

        public void EndCapture(int province)
        {
            var c = stateData[2 * columns + province];
            stateData[2 * columns + province] = new Color32(0, 0, 0, c.a);
            ApplyState();
        }

        /// <summary>
        /// Distance inside the province from its border with the attacker's land, 0 there and 1 at
        /// the far side: the front sweeps along it. Breadth-first over the province's pixels.
        /// </summary>
        private void BuildSpread(int province, string attacker, IReadOnlyList<string> owners)
        {
            var px = pixels[province];
            var index = new Dictionary<int, int>(px.Count);
            for (int j = 0; j < px.Count; j++) index[px[j]] = j;
            var dist = new int[px.Count];
            for (int j = 0; j < dist.Length; j++) dist[j] = int.MaxValue;
            var queue = new Queue<int>();
            for (int j = 0; j < px.Count; j++)
            {
                int i = px[j];
                foreach (int k in Neighbours(i))
                {
                    ushort o = ids[k];
                    if (o < count && o != province && owners[o] == attacker) { dist[j] = 0; queue.Enqueue(j); break; }
                }
            }
            if (queue.Count == 0 && px.Count > 0) { dist[0] = 0; queue.Enqueue(0); }
            int max = 1;
            while (queue.Count > 0)
            {
                int j = queue.Dequeue();
                foreach (int k in Neighbours(px[j]))
                    if (index.TryGetValue(k, out int jj) && dist[jj] > dist[j] + 1)
                    {
                        dist[jj] = dist[j] + 1;
                        if (dist[jj] > max) max = dist[jj];
                        queue.Enqueue(jj);
                    }
            }
            NativeArray<byte> data = spread.GetRawTextureData<byte>();
            for (int j = 0; j < px.Count; j++) data[px[j]] = (byte)Mathf.RoundToInt(255f * Mathf.Min(1f, dist[j] / (float)max));
            spread.Apply(false);
        }

        private IEnumerable<int> Neighbours(int i)
        {
            int x = i % width;
            if (x > 0) yield return i - 1;
            if (x < width - 1) yield return i + 1;
            if (i >= width) yield return i - width;
            if (i < ids.Length - width) yield return i + width;
        }

        // ---- the focus ----

        public void SetFocus(int province, float amount)
        {
            if (province < 0) { material.SetVector("_Focus", Vector4.zero); return; }
            var uv = labelUv[province];
            // The same share of the land on any world: the Classic board's 0.09, scaled to a smaller province.
            float radius = Drawn ? 0.09f * Mathf.Sqrt(25f / count) : 0.09f;
            material.SetVector("_Focus", new Vector4(uv.x, uv.y, radius, amount));
        }
    }
}
