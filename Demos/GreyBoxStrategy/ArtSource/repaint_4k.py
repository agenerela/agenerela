"""Paints real 4K detail onto the Classic sea-chart, keeping every coast where the province map has it.

    python repaint_4k.py            (from Demos/GreyBoxStrategy/ArtSource; needs tools/comfyui/.env)

The 4096 x 2304 upscaled master (sea-chart-map-upscale.png) only has the 1672 px original's detail.
This cuts it into fifteen overlapping 1024 px tiles and runs each through Z-Image Turbo img2img at a
low denoise (0.22) on the asset server, so the model sharpens what is there (trees, fields, roofs,
cliffs, the sea's ripples) without moving it. Then:

- the tiles are stitched with linear ramps across their 128 px overlaps, so no seam shows;
- the result's low frequencies (a Gaussian of sigma 6 px) are swapped for the master's, so colour and
  coarse shapes are the master's and only the fine detail is new: no tile drifts in tone, and a
  coast cannot move by more than the blur;
- the land mask the bake uses is taken from the new painting and compared with province-ids.png.
  It fails if the overlap (IoU) falls more than 0.01 below the master's own.

Writes sea-chart-map.png (the 4K master) and ../Assets/GreyBoxStrategy/Screen/Art/sea-chart-map.jpg.
The province maps are not re-baked: bake_map.py reads the upscaled master, which they came from.

Measured on 25 November, degenerator's RTX 4070 SUPER: about 9 s a tile, two and a half minutes in
all. IoU with the province map 0.9629 against the master's 0.9663, and a phase-correlation shift
of 0 px at four places across the island. Denoise 0.35 and above redrew villages and moved rivers.
"""
import os, re, subprocess, sys, tempfile
from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter

HERE = Path(__file__).resolve().parent
# A worktree has no tools/comfyui/.env of its own: point REPAINT_COMFY_PY at a checkout's comfy.py that has one.
COMFY = Path(os.environ.get("REPAINT_COMFY_PY", HERE.parents[2] / "tools" / "comfyui" / "comfy.py"))
MASTER = HERE / "sea-chart-map-upscale.png"
IDS = HERE.parent / "Assets/GreyBoxStrategy/Screen/Art/province-ids.png"
W, H, T, F = 4096, 2304, 1024, 128
XS, YS = [0, 896, 1792, 2688, 3072], [0, 640, 1280]
DENOISE, SEED, SIGMA = 0.22, 4242, 6.0
PROMPT = ("Antique hand-painted fantasy world map seen from high above, fine oil painting detail, tiny pine and oak "
          "forests, small rocky mountains, patchwork farm fields, small villages, winding blue-grey river, dirt roads, "
          "rocky cliffs and dark sea, muted olive green, ochre and umber palette, aged varnish, sharp fine detail, no text")


def paint_tiles(work):
    src = Image.open(MASTER).convert("RGB")
    tiles = {}
    for y in YS:
        for x in XS:
            inp = work / f"in_{x}_{y}.png"
            src.crop((x, y, x + T, y + T)).save(inp)
            r = subprocess.run([sys.executable, str(COMFY), "run", str(HERE / "repaint_4k_workflow.json"),
                                "--set", f"image={inp}", "--set", f"prompt={PROMPT}", "--set", f"seed={SEED}",
                                "--set", f"denoise={DENOISE}", "--out", str(work / "out")],
                               capture_output=True, text=True)
            if r.returncode:
                sys.exit(r.stderr[-800:])
            tiles[(x, y)] = np.asarray(Image.open(r.stdout.strip().splitlines()[-1]).convert("RGB"), float)
            print("tile", x, y, flush=True)
    return src, tiles


def blur(a, s):
    r = int(3 * s); k = np.exp(-np.arange(-r, r + 1) ** 2 / (2 * s * s)); k /= k.sum()
    p = np.pad(a, ((r, r), (r, r), (0, 0)), mode="reflect")
    p = sum(k[i] * p[:, i:i + a.shape[1]] for i in range(2 * r + 1))
    return sum(k[i] * p[i:i + a.shape[0]] for i in range(2 * r + 1))


def stitch(src, tiles):
    acc = np.zeros((H, W, 3)); wsum = np.zeros((H, W, 1))
    ramp = np.ones(T); ramp[:F] = np.linspace(0.02, 1, F); ramp[-F:] = np.linspace(1, 0.02, F)
    for (x, y), a in tiles.items():
        rx, ry = ramp.copy(), ramp.copy()
        if x == 0: rx[:F] = 1
        if x + T == W: rx[-F:] = 1
        if y == 0: ry[:F] = 1
        if y + T == H: ry[-F:] = 1
        w = np.outer(ry, rx)[..., None]
        acc[y:y + T, x:x + T] += a * w; wsum[y:y + T, x:x + T] += w
    gen = acc / wsum
    out = gen - blur(gen, SIGMA) + blur(np.asarray(src, float), SIGMA)
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))


def land_iou(img):
    """The bake's land mask (bake_map.py) against the province map, inside the island's box."""
    im = np.asarray(img.convert("RGB").resize((2048, 1152), Image.LANCZOS)).astype(np.int32)
    r, g, b = im[..., 0], im[..., 1], im[..., 2]
    land = ((r - b) > 10) & ((r * 3 + g * 6 + b) / 10 > 45)
    m = Image.fromarray((land * 255).astype(np.uint8))
    m = np.asarray(m.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(9)).filter(ImageFilter.MaxFilter(5))) > 127
    ids = np.asarray(Image.open(IDS)) != 255
    ys, xs = np.nonzero(ids); box = (slice(ys.min() - 20, ys.max() + 20), slice(xs.min() - 20, xs.max() + 20))
    a, b = m[box], ids[box]
    return (a & b).sum() / (a | b).sum()


if __name__ == "__main__":
    with tempfile.TemporaryDirectory() as tmp:
        src, tiles = paint_tiles(Path(tmp))
    out = stitch(src, tiles)
    before, after = land_iou(src), land_iou(out)
    print(f"land IoU with the province map: master {before:.4f}, new {after:.4f}")
    if after < before - 0.01:
        sys.exit("the coasts moved; not written")
    out.save(HERE / "sea-chart-map.png", optimize=True)
    out.save(HERE.parent / "Assets/GreyBoxStrategy/Screen/Art/sea-chart-map.jpg", quality=92, subsampling=0)
    print("written")
