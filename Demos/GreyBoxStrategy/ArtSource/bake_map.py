"""Bakes the province map for the strategy screen from the painted sea-chart.

    python bake_map.py            (from Demos/GreyBoxStrategy/ArtSource)

Land mask from the painting, then the rule board's 5 x 5 grid warped into organic borders: four
vertical and four horizontal noise curves, none crossing another of its kind, cut by the coast.
A last pass fixes single pixels where diagonal provinces touch, and the adjacency is checked
against the rules (each province borders only its four orthogonal neighbours); the bake fails if
they differ. Province index = row * 5 + column, the order MapDefinition.Standard() uses.

Writes, into ../Assets/GreyBoxStrategy/Screen/Art/:
  province-ids.png   8-bit: index * 10 for land, 255 for sea (point-sampled)
  province-edge.png  8-bit: distance to the nearest province border or coast, 16 per pixel, capped
  provinces.json     each province's label point (0..1) and area in pixels
  sea-tile.jpg       a seamless tile of open sea, for beyond the painting's edge
"""
import numpy as np, json, base64, io, math
from PIL import Image, ImageFilter
im = np.asarray(Image.open("sea-chart-map.png").convert("RGB")).astype(np.int32)
H, W, _ = im.shape
r, g, b = im[...,0], im[...,1], im[...,2]
lum = (r*3 + g*6 + b) / 10
land = ((r - b) > 10) & (lum > 45)
m = Image.fromarray((land*255).astype(np.uint8))
m = m.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(9)).filter(ImageFilter.MaxFilter(5))  # close then open-ish
land = np.asarray(m) > 127
# keep largest component, fill holes (lakes) via flood fill of sea from the frame edge
def components(mask):
    lab = np.zeros(mask.shape, np.int32); n = 0; sizes = {}
    from collections import deque
    for y0 in range(0, H, 3):
        for x0 in range(0, W, 3):
            if mask[y0, x0] and not lab[y0, x0]:
                n += 1; q = deque([(y0, x0)]); lab[y0, x0] = n; c = 0
                while q:
                    y, x = q.popleft(); c += 1
                    for yy, xx in ((y-1,x),(y+1,x),(y,x-1),(y,x+1)):
                        if 0 <= yy < H and 0 <= xx < W and mask[yy, xx] and not lab[yy, xx]:
                            lab[yy, xx] = n; q.append((yy, xx))
                sizes[n] = c
    return lab, sizes
lab, sizes = components(land)
big = max(sizes, key=sizes.get); land = lab == big
sea = ~land
lab2, s2 = components(sea)
# sea components touching the frame are sea; others are lakes -> land
edge = set(np.unique(np.concatenate([lab2[0], lab2[-1], lab2[:,0], lab2[:,-1]]))) - {0}
land = land | (sea & ~np.isin(lab2, list(edge)) & (lab2 > 0))
ys, xs = np.nonzero(land); x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
yy, xx = np.mgrid[0:H, 0:W].astype(np.float64)
u = (xx - x0) / (x1 - x0); v = (yy - y0) / (y1 - y0)
rng = np.random.default_rng(7)
def noise(u, v, amp):
    out = np.zeros_like(u)
    for f, a in ((2.1, 1.0), (4.3, 0.5), (8.7, 0.25), (17.0, 0.12)):
        p1, p2, th = rng.uniform(0, 6.28, 3)
        out += a * np.sin(f * (u*math.cos(th) + v*math.sin(th)) * 3.14 + p1) * np.cos(f * (v*math.cos(th) - u*math.sin(th)) * 3.14 + p2)
    return out * amp
def curve(t, amp):
    out = np.zeros_like(t)
    for f, a_ in ((1.3, 1.0), (2.9, 0.55), (6.1, 0.3), (13.0, 0.14)):
        out += a_ * np.sin(f * t * 6.283 + rng.uniform(0, 6.283))
    return out * amp
col = np.zeros(u.shape, int); row = np.zeros(u.shape, int)
for k in range(1, 5):
    col += (u > k/5 + curve(v, 0.028)).astype(int)
    row += (v > k/5 + curve(u, 0.028)).astype(int)
ids = np.where(land, row * 5 + col, 255).astype(np.uint8)
# each province: keep its largest piece; stray pieces join the neighbour they touch most
from collections import Counter, deque
for pid in range(25):
    mask = ids == pid
    lb, sz = components(mask)
    if len(sz) <= 1: continue
    keep = max(sz, key=sz.get)
    for k in sz:
        if k == keep: continue
        pts = np.argwhere(lb == k); cnt = Counter()
        for y, x in pts:
            for yy2, xx2 in ((y-1,x),(y+1,x),(y,x-1),(y,x+1)):
                if 0 <= yy2 < H and 0 <= xx2 < W:
                    o = ids[yy2, xx2]
                    if o != pid and o != 255: cnt[o] += 1
        r_, c_ = divmod(pid, 5)
        ortho = {r2*5+c2 for r2, c2 in ((r_-1,c_),(r_+1,c_),(r_,c_-1),(r_,c_+1)) if 0<=r2<5 and 0<=c2<5}
        cnt = Counter({o: n for o, n in cnt.items() if o in ortho})
        print("stray", pid, sz[k], dict(cnt))
        ids[lb == k] = cnt.most_common(1)[0][0] if cnt else 255


for it in range(60):
    n = 0
    for dy, dx in ((0,1),(1,0)):
        a_ = ids[:H-dy, :W-dx].astype(int); b_ = ids[dy:, dx:].astype(int)
        diag = (a_ != 255) & (b_ != 255) & (np.abs(a_//5 - b_//5) == 1) & (np.abs(a_%5 - b_%5) == 1)
        ys_, xs_ = np.nonzero(diag)
        for y, x in zip(ys_, xs_):
            p, q = int(ids[y, x]), int(ids[y+dy, x+dx])
            ids[y, x] = ((p//5)*5 + (q%5)) if (it//3) % 2 == 0 else ((q//5)*5 + (p%5)); n += 1
    if n == 0: break
print("pixel fixes, rounds:", it)
# adjacency check against the rule graph (4-neighbours of the 5x5 grid)
adj = set()
a = ids[:, :-1]; bb = ids[:, 1:]; mk = (a != bb) & (a != 255) & (bb != 255); adj |= set(zip(a[mk].tolist(), bb[mk].tolist()))
a = ids[:-1, :]; bb = ids[1:, :]; mk = (a != bb) & (a != 255) & (bb != 255); adj |= set(zip(a[mk].tolist(), bb[mk].tolist()))
adj = {tuple(sorted(p)) for p in adj}
rule = {tuple(sorted((r_*5+c, r_*5+c+1))) for r_ in range(5) for c in range(4)} | {tuple(sorted((r_*5+c, (r_+1)*5+c))) for r_ in range(4) for c in range(5)}
print("extra (diagonal) adjacencies:", sorted(adj - rule)); print("missing:", sorted(rule - adj))
# label points: the pixel farthest from its province's edge (coarse)
pts = {}
for pid in range(25):
    mask = ids == pid
    d = mask.astype(np.int32)
    m2 = Image.fromarray((mask*255).astype(np.uint8))
    for k in range(1, 60):
        m2 = m2.filter(ImageFilter.MinFilter(3)); arr = np.asarray(m2) > 127
        if not arr.any(): break
        last = arr
    py, px = np.argwhere(last).mean(axis=0)
    pts[pid] = [round(px / W, 4), round(py / H, 4), int(mask.sum())]
assert not (adj - rule) and not (rule - adj), "the province map does not match the rule graph"
OUT = "../Assets/GreyBoxStrategy/Screen/Art/"
import os
os.makedirs(OUT, exist_ok=True)
Image.fromarray(np.where(ids == 255, 255, ids * 10).astype(np.uint8)).save(OUT + "province-ids.png", optimize=True)
# distance to a border: grow the border mask one pixel at a time (Chebyshev), up to 15 steps
border = np.zeros_like(ids, dtype=bool)
border[:, 1:] |= ids[:, 1:] != ids[:, :-1]; border[:, :-1] |= ids[:, :-1] != ids[:, 1:]
border[1:, :] |= ids[1:, :] != ids[:-1, :]; border[:-1, :] |= ids[:-1, :] != ids[1:, :]
dist = np.full(ids.shape, 15, np.uint8)
grown = Image.fromarray((border * 255).astype(np.uint8))
for k in range(15):
    arr = np.asarray(grown) > 127
    dist[(dist == 15) & arr] = k
    grown = grown.filter(ImageFilter.MaxFilter(3))
edge = np.where(ids == 255, 0, np.minimum(255, dist.astype(np.int32) * 16 + 8)).astype(np.uint8)
Image.fromarray(edge).save(OUT + "province-edge.png", optimize=True)
json.dump({"width": W, "height": H, "provinces": [{"index": int(k), "x": v[0], "y": v[1], "area": v[2]} for k, v in sorted(pts.items())]},
          open(OUT + "provinces.json", "w"), indent=1)
print("baked", OUT)

# The open sea beyond the painting: the calmest square of pure sea (away from the compass rose),
# mirrored into a seamless tile. The screen tiles it behind the painting, so the map fills any
# window edge to edge and the land can sit in the space between the panels.
img = Image.open("sea-chart-map.png").convert("RGB")
grey = np.asarray(img.convert("L")).astype(float)
S = 128
cands = []
for y in range(0, H - S, 8):
    for x in range(0, W - S, 8):
        if x + S > W * 0.78 and y + S > H * 0.5:
            continue
        if (ids[y:y + S, x:x + S] == 255).all():
            cands.append((grey[y:y + S, x:x + S].std(), x, y))
cands.sort()
_, x, y = cands[0]
patch = img.crop((x, y, x + S, y + S)).resize((200, 200), Image.LANCZOS)
tile = Image.new("RGB", (400, 400))
tile.paste(patch, (0, 0)); tile.paste(patch.transpose(Image.FLIP_LEFT_RIGHT), (200, 0))
tile.paste(patch.transpose(Image.FLIP_TOP_BOTTOM), (0, 200)); tile.paste(patch.transpose(Image.ROTATE_180), (200, 200))
tile.save(OUT + "sea-tile.jpg", quality=92)
print("sea tile from", x, y)
