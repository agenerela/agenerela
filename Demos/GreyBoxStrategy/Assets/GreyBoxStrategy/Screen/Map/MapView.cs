using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The painted map: one quad, one material (<c>GreyBoxStrategy/PaintedMap</c>), and the data the
    /// shader reads. Owners, the capture and the focus are numbers written into small textures, so
    /// nothing here touches a pixel of the painting.
    /// </summary>
    public sealed class MapView : MonoBehaviour
    {
        public const int StateColumns = 32;
        public const float PixelsPerUnit = 100f;

        [SerializeField] private Shader shader;
        [SerializeField] private Texture2D painting;
        [SerializeField] private Texture2D provinceIds;
        [SerializeField] private Texture2D provinceEdge;
        [SerializeField] private TextAsset provincePoints;

        private Material material;
        private Texture2D state;
        private Texture2D spread;
        private byte[] ids;
        private int width, height;
        private readonly List<int>[] pixels = new List<int>[25];
        private Vector2[] labelUv = new Vector2[25];
        private readonly Color32[] stateData = new Color32[StateColumns * 4];

        public int Width => width;
        public int Height => height;
        public Vector2 SizeUnits => new Vector2(width / PixelsPerUnit, height / PixelsPerUnit);
        public bool Engraved { set { if (material != null) material.SetFloat("_Engraved", value ? 1 : 0); } }

        public void Build()
        {
            width = provinceIds.width;
            height = provinceIds.height;
            ReadIds();
            ReadPoints();

            material = new Material(shader) { name = "PaintedMap (runtime)" };
            material.SetTexture("_MainTex", painting);
            material.SetTexture("_IdTex", provinceIds);
            material.SetTexture("_EdgeTex", provinceEdge);

            state = new Texture2D(StateColumns, 4, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "ProvinceState" };
            spread = new Texture2D(width, height, TextureFormat.R8, false, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "CaptureSpread" };
            material.SetTexture("_StateTex", state);
            material.SetTexture("_SpreadTex", spread);
            ApplyState();

            var mesh = new Mesh { name = "MapQuad" };
            var s = SizeUnits;
            mesh.vertices = new[] { new Vector3(-s.x / 2, -s.y / 2), new Vector3(s.x / 2, -s.y / 2), new Vector3(-s.x / 2, s.y / 2), new Vector3(s.x / 2, s.y / 2) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void ReadIds()
        {
            // The id map is an 8-bit grey PNG: index * 10 for land, 255 for sea. Read it once.
            var raw = provinceIds.GetPixels32();
            ids = new byte[raw.Length];
            for (int p = 0; p < 25; p++) pixels[p] = new List<int>();
            for (int i = 0; i < raw.Length; i++)
            {
                byte v = raw[i].r;
                byte id = v >= 250 ? (byte)255 : (byte)Mathf.RoundToInt(v / 10f);
                ids[i] = id;
                if (id < 25) pixels[id].Add(i);
            }
        }

        private void ReadPoints()
        {
            var root = Newtonsoft.Json.Linq.JObject.Parse(provincePoints.text);
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

        /// <summary>The province under a world position, or -1 for the sea.</summary>
        public int ProvinceAt(Vector3 world)
        {
            var local = transform.InverseTransformPoint(world);
            var s = SizeUnits;
            int x = Mathf.FloorToInt((local.x / s.x + 0.5f) * width), y = Mathf.FloorToInt((local.y / s.y + 0.5f) * height);
            if (x < 0 || y < 0 || x >= width || y >= height) return -1;
            byte id = ids[y * width + x];
            return id < 25 ? id : -1;
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
            c.a = owner == null ? (byte)0 : (byte)(30 * (slotOf(owner) + 1));
            stateData[row * StateColumns + p] = c;
        }

        public void SetSelected(int province)
        {
            for (int p = 0; p < 25; p++) stateData[2 * StateColumns + p].a = (byte)(p == province ? 255 : 0);
            ApplyState();
        }

        /// <summary>Hatches the provinces in <paramref name="reach"/> and dims the rest, or clears both.</summary>
        public void SetReach(ISet<int> reach, string focusOwner, IReadOnlyList<string> owners)
        {
            for (int p = 0; p < 25; p++)
            {
                bool on = reach != null && reach.Contains(p);
                bool dim = reach != null && !on && owners[p] != focusOwner;
                stateData[3 * StateColumns + p] = new Color32((byte)(on ? 255 : 0), (byte)(dim ? 255 : 0), 0, 0);
            }
            ApplyState();
        }

        private void ApplyState()
        {
            state.SetPixels32(stateData);
            state.Apply(false);
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
            stateData[2 * StateColumns + province] = new Color32(0, 0, 255, stateData[2 * StateColumns + province].a);
            BuildSpread(province, attacker, ownersBefore);
            ApplyState();
        }

        /// <summary>Moves the capture on: <paramref name="fall"/> lifts the old wash, <paramref name="front"/> spreads the new one.</summary>
        public void SetCaptureProgress(int province, float fall, float front)
        {
            var c = stateData[2 * StateColumns + province];
            stateData[2 * StateColumns + province] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(front) * 255), (byte)Mathf.RoundToInt(Mathf.Clamp01(fall) * 255), 255, c.a);
            ApplyState();
        }

        public void EndCapture(int province)
        {
            var c = stateData[2 * StateColumns + province];
            stateData[2 * StateColumns + province] = new Color32(0, 0, 0, c.a);
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
                    byte o = ids[k];
                    if (o < 25 && o != province && owners[o] == attacker) { dist[j] = 0; queue.Enqueue(j); break; }
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
            material.SetVector("_Focus", new Vector4(uv.x, uv.y, 0.09f, amount));
        }
    }
}
