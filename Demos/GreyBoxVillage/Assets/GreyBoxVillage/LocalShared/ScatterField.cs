using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Demos.Shared
{
    /// <summary>
    /// Many copies of a few small models (trees, grass, rocks, reeds), drawn with GPU instancing
    /// rather than kept as one GameObject each. The scene stores only where each copy stands, its
    /// turn and its size, so the scene file stays small, and every camera, including the Scene view
    /// and the capture tool's, draws each mesh in a handful of instanced calls.
    /// </summary>
    /// <remarks>
    /// Copies are grouped into square cells, each with its own bounds, so a camera skips the cells it
    /// cannot see. The copies have no colliders and no GameObjects, so they cannot change where
    /// anyone walks or what anyone sees. The material must have GPU instancing on.
    /// </remarks>
    [ExecuteAlways]
    public sealed class ScatterField : MonoBehaviour
    {
        /// <summary>One mesh of a model, where it sits inside the model.</summary>
        [Serializable]
        public sealed class Part
        {
            public Mesh Mesh;
            public int SubMesh;
            public Matrix4x4 Local = Matrix4x4.identity;
        }

        /// <summary>One model and every copy of it: x, y and z are the position, w the turn about Y in degrees.</summary>
        [Serializable]
        public sealed class Kind
        {
            public string Model;
            public Part[] Parts = Array.Empty<Part>();
            public Vector4[] Placements = Array.Empty<Vector4>();
            public float[] Scales = Array.Empty<float>();
        }

        [Tooltip("The material every copy is drawn with; it must have GPU instancing on.")]
        public Material Material;
        public Kind[] Kinds = Array.Empty<Kind>();
        [Tooltip("The side of the square cells copies are grouped into for culling, in metres.")]
        public float CellSize = 120f;
        public bool CastShadows = true;

        // One instanced draw: a mesh, the copies of it in one cell, and their bounds.
        private sealed class Batch
        {
            public Mesh Mesh;
            public int SubMesh;
            public Matrix4x4[] Matrices;
            public Bounds Bounds;
        }

        private readonly List<Batch> batches = new List<Batch>();
        private bool built;
        private Matrix4x4 builtFor;

        /// <summary>How many copies are drawn, over every kind.</summary>
        public int Count
        {
            get
            {
                int n = 0;
                foreach (var k in Kinds) n += k.Placements.Length;
                return n;
            }
        }

        /// <summary>The instanced draws each camera makes, after culling none: one per mesh per cell.</summary>
        public int BatchCount
        {
            get
            {
                Build();
                return batches.Count;
            }
        }

        /// <summary>The world bounds of every copy, as the cameras cull them; empty when there are none.</summary>
        public Bounds Bounds
        {
            get
            {
                Build();
                if (batches.Count == 0) return new Bounds(transform.position, Vector3.zero);
                var b = batches[0].Bounds;
                foreach (var batch in batches) b.Encapsulate(batch.Bounds);
                return b;
            }
        }

        /// <summary>The world matrix of copy <paramref name="index"/> of <paramref name="kind"/>, before its parts' own.</summary>
        public Matrix4x4 Placement(Kind kind, int index)
        {
            var p = kind.Placements[index];
            float scale = index < kind.Scales.Length ? kind.Scales[index] : 1f;
            return transform.localToWorldMatrix *
                   Matrix4x4.TRS(new Vector3(p.x, p.y, p.z), Quaternion.Euler(0, p.w, 0), Vector3.one * scale);
        }

        private void OnEnable()
        {
            built = false;
            RenderPipelineManager.beginCameraRendering += Draw;
        }

        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= Draw;

        private void OnValidate() => built = false;

        /// <summary>Drops the batches, so the next frame builds them again from <see cref="Kinds"/>.</summary>
        public void Rebuild() => built = false;

        // Rebuilt when the copies change (OnValidate, Rebuild) or the field or a parent moves.
        private void Build()
        {
            var matrix = transform.localToWorldMatrix;
            if (built && matrix == builtFor) return;
            built = true;
            builtFor = matrix;
            batches.Clear();
            float cell = Mathf.Max(1f, CellSize);
            foreach (var kind in Kinds)
            {
                if (kind == null || kind.Parts == null) continue;
                foreach (var part in kind.Parts)
                {
                    if (part == null || part.Mesh == null) continue;
                    var cells = new Dictionary<Vector2Int, List<Matrix4x4>>();
                    for (int i = 0; i < kind.Placements.Length; i++)
                    {
                        var m = Placement(kind, i) * part.Local;
                        var at = m.GetColumn(3);
                        var key = new Vector2Int(Mathf.FloorToInt(at.x / cell), Mathf.FloorToInt(at.z / cell));
                        if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<Matrix4x4>();
                        list.Add(m);
                    }
                    foreach (var list in cells.Values)
                        // An instanced call takes at most 1023 copies.
                        for (int start = 0; start < list.Count; start += 1023)
                        {
                            var matrices = list.GetRange(start, Mathf.Min(1023, list.Count - start)).ToArray();
                            batches.Add(new Batch { Mesh = part.Mesh, SubMesh = part.SubMesh, Matrices = matrices, Bounds = BoundsOf(part.Mesh, matrices) });
                        }
                }
            }
        }

        private static Bounds BoundsOf(Mesh mesh, Matrix4x4[] matrices)
        {
            var local = mesh.bounds;
            var b = new Bounds(matrices[0].MultiplyPoint3x4(local.center), Vector3.zero);
            foreach (var m in matrices)
                for (int corner = 0; corner < 8; corner++)
                {
                    var c = local.center + Vector3.Scale(local.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    b.Encapsulate(m.MultiplyPoint3x4(c));
                }
            return b;
        }

        private void Draw(ScriptableRenderContext context, Camera camera)
        {
            if (Material == null || !isActiveAndEnabled) return;
            if (camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection) return;
            if ((camera.cullingMask & (1 << gameObject.layer)) == 0) return;
            Build();
            // Unity culls each draw by its bounds, for the camera and for the shadow maps.
            foreach (var batch in batches)
            {
                var rp = new RenderParams(Material)
                {
                    camera = camera,
                    layer = gameObject.layer,
                    worldBounds = batch.Bounds,
                    shadowCastingMode = CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                    receiveShadows = true,
                    lightProbeUsage = LightProbeUsage.Off
                };
                Graphics.RenderMeshInstanced(rp, batch.Mesh, batch.SubMesh, batch.Matrices);
            }
        }
    }
}
