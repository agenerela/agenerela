using System.Threading.Tasks;
using GreyBoxStrategy.Chart;
using UnityEngine;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// Detail that holds when zoomed in on a drawn chart: once the camera settles on a view where the chart is
    /// magnified, the visible window (and a margin around it) is drawn again at screen scale by the chart's own code,
    /// its glyphs laid out off the main thread. The far view, where the chart is not magnified, is untouched.
    /// </summary>
    public sealed partial class ReplayDirector
    {
        /// <summary>Above this many chart pixels a screen pixel the chart is sharp enough as it is.</summary>
        public const float DetailBelow = 0.8f;

        private Task<ChartAtlas> detailJob;
        private Rect detailJobCells;
        private int detailJobW, detailJobH;
        private Vector3 detailCamPos;
        private float detailCamSize, detailStill;

        /// <summary>For the Unity CLI: the window drawn at screen scale, or why not.</summary>
        public string DetailReport => map == null || !map.Drawn ? "no drawn chart" : map.DetailOn
            ? $"on: {map.DetailCells.width:0.0} × {map.DetailCells.height:0.0} cells at {map.DetailScale:0.0} px a cell"
            : detailJob != null ? "drawing" : "off (the chart is not magnified)";

        private void UpdateDetail()
        {
            if (world == null || map == null || !map.Drawn || cam == null) return;
            var img = ChartGpu.LastImage;
            if (img == null) return;
            if (detailJob != null)
            {
                if (!detailJob.IsCompleted) return;
                if (!detailJob.IsFaulted && ChartGpu.LastImage == img) map.SetDetail(detailJobCells, detailJobW, detailJobH, detailJob.Result);
                detailJob = null;
            }
            if (cam.transform.position != detailCamPos || cam.orthographicSize != detailCamSize)
            {
                detailCamPos = cam.transform.position; detailCamSize = cam.orthographicSize; detailStill = Time.realtimeSinceStartup;
            }

            // The visible rectangle in grid units (y down), from the camera's corners on the map.
            float h = cam.orthographicSize, w = h * cam.aspect;
            var c = cam.transform.position;
            Vector2 Cell(float x, float y)
            {
                var local = map.transform.InverseTransformPoint(new Vector3(x, y, 0));
                return new Vector2((local.x / map.SizeUnits.x + 0.5f) * img.GridWidth, (0.5f - local.y / map.SizeUnits.y) * img.GridHeight);
            }
            var a = Cell(c.x - w, c.y + h); var b = Cell(c.x + w, c.y - h);
            var view = Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            float pxPerCell = Screen.width / Mathf.Max(1e-3f, view.width);
            if (img.Scale / pxPerCell > DetailBelow) { if (map.DetailOn) map.DetailOff(); return; }

            var grid = new Rect(0, 0, img.GridWidth, img.GridHeight);
            var seen = Clip(view, grid);
            bool covers = map.DetailOn && seen.width > 0 && map.DetailCells.Contains(seen.min) && map.DetailCells.Contains(seen.max)
                          && Mathf.Abs(map.DetailScale / pxPerCell - 1) < 0.2f;
            if (covers || Time.realtimeSinceStartup - detailStill < 0.12f || seen.width <= 0) return;

            // A quarter of the view's size on every side, so a small pan stays sharp while the next window is drawn.
            var r = Clip(Rect.MinMaxRect(seen.xMin - view.width * 0.25f, seen.yMin - view.height * 0.25f, seen.xMax + view.width * 0.25f, seen.yMax + view.height * 0.25f), grid);
            float s = Mathf.Min(pxPerCell, 4096 / Mathf.Max(r.width, r.height));
            detailJobCells = r; detailJobW = Mathf.CeilToInt(r.width * s); detailJobH = Mathf.CeilToInt(r.height * s);
            var cells = r;
            detailJob = Task.Run(() => ChartGpu.RegionAtlas(img, cells, s));
        }

        private static Rect Clip(Rect r, Rect to) => Rect.MinMaxRect(Mathf.Max(r.xMin, to.xMin), Mathf.Max(r.yMin, to.yMin), Mathf.Min(r.xMax, to.xMax), Mathf.Min(r.yMax, to.yMax));
    }
}
