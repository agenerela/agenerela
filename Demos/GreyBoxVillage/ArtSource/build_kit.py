"""Builds GreyBoxVillage's low-poly kit and exports it for Unity.

Run headless from the repository root:

    blender --background --factory-startup --python Demos/GreyBoxVillage/ArtSource/build_kit.py

It writes Assets/GreyBoxVillage/Art/Palette.png, PaletteEmission.png and one FBX per model into
Assets/GreyBoxVillage/Art/Models/. Everything is generated, so the script is the source: change it
and run it again rather than editing a model by hand.

An FBX export stamps the time it was made, so every run rewrites all 77 files in a few bytes each.
To change some models only, name them in KIT_ONLY (comma separated); every other file is left alone:

    KIT_ONLY=gate_house,gate_door blender --background --factory-startup --python ...

Layout convention: models are authored in the village's own coordinates, Blender X = Unity X
(east), Blender Y = Unity Z (north), Blender Z = up. FBX import turns that half a turn, which the
scene builder undoes with a 180 degree yaw. A character's front is +Y here, Unity +Z there.
"""
import math
import os
import random
import sys

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import kit  # noqa: E402
from kit import Part  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, "..", "Assets", "GreyBoxVillage", "Art"))
MODELS = os.path.join(ART, "Models")
os.makedirs(MODELS, exist_ok=True)
MADE = []
ONLY = {n.strip() for n in os.environ.get("KIT_ONLY", "").split(",") if n.strip()}


def wanted(name):
    return not ONLY or name in ONLY


def out(name, part):
    if not wanted(name):
        return
    kit.single(name, part, MODELS)
    MADE.append(name)


def out_rig(name, parts):
    if not wanted(name):
        return
    kit.rig(name, parts, MODELS)
    MADE.append(name)


# ---- Small props -------------------------------------------------------------------------------

def barrel(p=None, at=(0, 0, 0), s=1.0):
    p = p or Part()
    x, y, z = at
    p.cyl(0.36 * s, 0.9 * s, "wood", at=(x, y, z), segments=10, radius_top=0.36 * s)
    p.cyl(0.4 * s, 0.25 * s, "wood_light", at=(x, y, z + 0.33 * s), segments=10)
    for h in (0.08, 0.75):
        p.cyl(0.38 * s, 0.07 * s, "iron_dark", at=(x, y, z + h * s), segments=10)
    p.cyl(0.32 * s, 0.03 * s, "wood_dark", at=(x, y, z + 0.9 * s), segments=10)
    return p


def crate(p=None, at=(0, 0, 0), s=1.0, rot=0):
    p = p or Part()
    x, y, z = at
    p.box((0.9 * s, 0.9 * s, 0.9 * s), "plank", at=(x, y, z), rot=(0, 0, rot), bevel=0.04)
    for dz in (0.05, 0.8):
        p.box((0.94 * s, 0.94 * s, 0.07 * s), "wood_dark", at=(x, y, z + dz * s), rot=(0, 0, rot), bevel=0.01)
    p.box((0.95 * s, 0.08 * s, 0.9 * s), "wood_dark", at=(x, y, z), rot=(0, 0, rot + 45), bevel=0.0)
    return p


def sack(p=None, at=(0, 0, 0), s=1.0):
    p = p or Part()
    x, y, z = at
    p.ball(0.3 * s, "cloth_brown", at=(x, y, z + 0.28 * s), scale=(1, 0.9, 1.1), subdiv=1)
    p.cyl(0.1 * s, 0.12 * s, "rope", at=(x, y, z + 0.55 * s), segments=6)
    return p


def rock(p=None, at=(0, 0, 0), s=1.0, seed=1, color="rock"):
    p = p or Part()
    x, y, z = at
    p.ball(0.6 * s, color, at=(x, y, z + 0.25 * s), scale=(1.2, 1.0, 0.75), subdiv=1, jitter=0.18, seed=seed)
    return p


def pine(p=None, at=(0, 0, 0), s=1.0, seed=0):
    p = p or Part()
    x, y, z = at
    rng = random.Random(seed)
    p.cyl(0.18 * s, 1.2 * s, "bark", at=(x, y, z), segments=6, radius_top=0.14 * s)
    h = 0.9 * s
    for i, (r, d) in enumerate(((1.5, 1.6), (1.15, 1.4), (0.8, 1.2), (0.45, 1.0))):
        color = "pine" if i % 2 == 0 else "pine_dark"
        p.cyl(r * s, d * s, color, at=(x, y, z + h), segments=7, radius_top=0.05 * s,
              rot=(0, 0, rng.uniform(0, 50)))
        h += d * 0.62 * s
    return p


def oak(p=None, at=(0, 0, 0), s=1.0, seed=0):
    p = p or Part()
    x, y, z = at
    rng = random.Random(seed)
    p.cyl(0.25 * s, 1.8 * s, "bark", at=(x, y, z), segments=7, radius_top=0.18 * s)
    p.cyl(0.08 * s, 0.9 * s, "bark", at=(x + 0.2 * s, y, z + 1.3 * s), rot=(0, 40, 0), segments=5)
    for i in range(5):
        a = rng.uniform(0, math.tau)
        r = rng.uniform(0.3, 0.9) * s
        color = ("leaf", "leaf_light", "leaf_dark")[i % 3]
        p.ball(rng.uniform(0.85, 1.15) * s, color,
               at=(x + math.cos(a) * r, y + math.sin(a) * r, z + rng.uniform(2.2, 3.0) * s),
               scale=(1, 1, 0.85), subdiv=1, jitter=0.12, seed=seed * 10 + i)
    return p


def bush(p=None, at=(0, 0, 0), s=1.0, seed=0, flowers=None):
    p = p or Part()
    x, y, z = at
    rng = random.Random(seed)
    for i in range(3):
        a = rng.uniform(0, math.tau)
        p.ball(rng.uniform(0.45, 0.6) * s, ("bush", "leaf", "leaf_dark")[i],
               at=(x + math.cos(a) * 0.35 * s, y + math.sin(a) * 0.35 * s, z + 0.35 * s),
               scale=(1, 1, 0.8), subdiv=1, jitter=0.12, seed=seed + i)
    if flowers:
        for i in range(5):
            a = rng.uniform(0, math.tau)
            p.ball(0.08 * s, flowers, at=(x + math.cos(a) * 0.5 * s, y + math.sin(a) * 0.5 * s,
                                          z + rng.uniform(0.5, 0.8) * s), subdiv=0)
    return p


def grass_tuft(p=None, at=(0, 0, 0), s=1.0, seed=0, color="grass_light"):
    p = p or Part()
    x, y, z = at
    rng = random.Random(seed)
    for i in range(5):
        p.cyl(0.05 * s, rng.uniform(0.3, 0.5) * s, color,
              at=(x + rng.uniform(-0.15, 0.15) * s, y + rng.uniform(-0.15, 0.15) * s, z),
              rot=(rng.uniform(-20, 20), rng.uniform(-20, 20), 0), segments=3, radius_top=0.0)
    return p


def lamp_post(p=None, at=(0, 0, 0)):
    p = p or Part()
    x, y, z = at
    p.box((0.4, 0.4, 0.25), "stone_dark", at=(x, y, z), bevel=0.04)
    p.cyl(0.07, 2.6, "timber", at=(x, y, z + 0.2), segments=6)
    p.box((0.08, 0.6, 0.08), "timber", at=(x, y + 0.25, z + 2.6), bevel=0.0)
    lantern(p, at=(x, y + 0.5, z + 2.05), s=0.9)
    return p


def lantern(p=None, at=(0, 0, 0), s=1.0):
    p = p or Part()
    x, y, z = at
    p.box((0.22 * s, 0.22 * s, 0.04 * s), "iron_dark", at=(x, y, z), bevel=0.0)
    p.box((0.17 * s, 0.17 * s, 0.26 * s), "lantern_glow", at=(x, y, z + 0.04 * s), bevel=0.0)
    for dx in (-0.1, 0.1):
        for dy in (-0.1, 0.1):
            p.box((0.025 * s, 0.025 * s, 0.28 * s), "iron_dark", at=(x + dx * s, y + dy * s, z + 0.03 * s), bevel=0.0)
    p.cyl(0.14 * s, 0.1 * s, "iron_dark", at=(x, y, z + 0.3 * s), segments=4, radius_top=0.02 * s, rot=(0, 0, 45))
    p.cyl(0.04 * s, 0.08 * s, "iron_dark", at=(x, y, z + 0.4 * s), segments=6)
    return p


def torch(p=None, at=(0, 0, 0), s=1.0, rot=(0, 0, 0)):
    p = p or Part()
    x, y, z = at
    p.cyl(0.035 * s, 0.6 * s, "wood_dark", at=(x, y, z), segments=5, rot=rot)
    p.cyl(0.07 * s, 0.14 * s, "leather", at=(x, y, z + 0.5 * s), segments=6, rot=rot)
    p.cyl(0.06 * s, 0.18 * s, "fire", at=(x, y, z + 0.62 * s), segments=5, radius_top=0.0, rot=rot)
    return p


def fence(p=None, at=(0, 0, 0), length=2.0, color="wood"):
    p = p or Part()
    x, y, z = at
    for px in (-length / 2 + 0.08, length / 2 - 0.08):
        p.box((0.14, 0.14, 1.0), "wood_dark", at=(x + px, y, z), bevel=0.02)
    for h in (0.35, 0.75):
        p.box((length, 0.08, 0.12), color, at=(x, y, z + h), bevel=0.01)
    return p


def bench(p=None, at=(0, 0, 0), rot=0):
    p = p or Part()
    x, y, z = at
    p.box((1.6, 0.4, 0.08), "plank", at=(x, y, z + 0.42), rot=(0, 0, rot), bevel=0.02)
    for dx in (-0.65, 0.65):
        c, s = math.cos(math.radians(rot)), math.sin(math.radians(rot))
        p.box((0.1, 0.36, 0.42), "wood_dark", at=(x + dx * c, y + dx * s, z), rot=(0, 0, rot), bevel=0.01)
    return p


def hay_bale(p=None, at=(0, 0, 0), rot=0):
    p = p or Part()
    x, y, z = at
    p.box((1.1, 0.6, 0.55), "straw", at=(x, y, z), rot=(0, 0, rot), bevel=0.06)
    for d in (-0.3, 0.3):
        c, s = math.cos(math.radians(rot)), math.sin(math.radians(rot))
        p.box((0.05, 0.64, 0.58), "thatch_dark", at=(x + d * c, y + d * s, z - 0.01), rot=(0, 0, rot), bevel=0.0)
    return p


def woodpile(p=None, at=(0, 0, 0)):
    p = p or Part()
    x, y, z = at
    for row in range(3):
        for i in range(4 - row):
            p.cyl(0.13, 1.2, "bark", at=(x - 0.6, y + (i - (3 - row) / 2) * 0.27, z + 0.13 + row * 0.23),
                  rot=(0, 90, 0), segments=6)
            p.cyl(0.1, 0.01, "wood_light", at=(x + 0.61, y + (i - (3 - row) / 2) * 0.27, z + 0.13 + row * 0.23),
                  rot=(0, 90, 0), segments=6)
    return p


def banner(p=None, at=(0, 0, 0), color="banner_blue", height=3.2):
    p = p or Part()
    x, y, z = at
    p.cyl(0.05, height, "timber", at=(x, y, z), segments=6)
    p.cyl(0.08, 0.1, "gold", at=(x, y, z + height), segments=6)
    p.box((0.7, 0.04, 0.06), "timber", at=(x + 0.35, y, z + height - 0.25), bevel=0.0)
    p.poly([(0, 0, 0), (0.65, 0, 0), (0.65, 0, -1.1), (0.325, 0, -0.85), (0, 0, -1.1)],
           [(0, 1, 2, 3, 4), (4, 3, 2, 1, 0)], color, at=(x + 0.02, y + 0.03, z + height - 0.3))
    p.box((0.2, 0.01, 0.2), "gold", at=(x + 0.33, y + 0.05, z + height - 0.75), rot=(0, 45, 0), bevel=0.0)
    return p


# ---- Buildings ---------------------------------------------------------------------------------

def cottage(name, w, d, roof, door, seed):
    """A timber-framed cottage on a w x d footprint (X by Y), walls 2.8 m, door on 'S', 'N', 'E' or 'W'."""
    rng = random.Random(seed)
    p = Part()
    p.box((w + 0.3, d + 0.3, 0.4), "stone", bevel=0.06)
    p.box((w, d, 2.5), "plaster", at=(0, 0, 0.4), bevel=0.04)
    # Timber frame: corner posts, a beam under the eaves, a middle rail.
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.box((0.2, 0.2, 2.5), "timber", at=(sx * (w / 2 - 0.02), sy * (d / 2 - 0.02), 0.4), bevel=0.02)
    for sy in (-1, 1):
        p.box((w + 0.1, 0.18, 0.18), "timber", at=(0, sy * (d / 2 + 0.01), 2.75), bevel=0.02)
        p.box((w + 0.05, 0.14, 0.12), "timber", at=(0, sy * (d / 2 + 0.01), 1.55), bevel=0.0)
    for sx in (-1, 1):
        p.box((0.18, d + 0.1, 0.18), "timber", at=(sx * (w / 2 + 0.01), 0, 2.75), bevel=0.02)
    # Gable roof, ridge along the longer side.
    along_x = w >= d
    span, length = (d, w) if along_x else (w, d)
    rot = (0, 0, 0) if along_x else (0, 0, 90)
    p.prism(length, span, 1.7, roof, at=(0, 0, 2.85), rot=rot, overhang=0.35)
    for sign in (-1, 1):
        if along_x:
            p.gable_wall(span, 1.65, "plaster_shade", at=(sign * (length / 2 - 0.08), 0, 2.85), rot=(0, 0, 90))
        else:
            p.gable_wall(span, 1.65, "plaster_shade", at=(0, sign * (length / 2 - 0.08), 2.85))
    # Chimney.
    cx = (w / 2 - 0.8) * (1 if rng.random() > 0.5 else -1)
    p.box((0.6, 0.6, 1.9), "stone_dark", at=(cx, (d / 4) * 0.5, 2.6), bevel=0.04)
    p.box((0.7, 0.7, 0.15), "stone_darker", at=(cx, (d / 4) * 0.5, 4.5), bevel=0.02)
    # Door and windows on each wall.
    sides = {"S": (0, -1), "N": (0, 1), "E": (1, 0), "W": (-1, 0)}
    for side, (nx, ny) in sides.items():
        wall_len = w if ny else d
        offset = (d / 2 if ny else w / 2) + 0.06
        def place(u, z, sx, sz, color, depth=0.08):
            if ny:
                p.box((sx, depth, sz), color, at=(u, ny * offset, z), bevel=0.01)
            else:
                p.box((depth, sx, sz), color, at=(nx * offset, u, z), bevel=0.01)
        if side == door:
            place(0, 0.4, 1.1, 1.85, "wood_dark", 0.1)
            place(0, 2.25, 1.3, 0.14, "timber", 0.14)
            place(0.35, 1.2, 0.08, 0.08, "iron", 0.16)
            for u in (-1.3, 1.3):
                if abs(u) + 0.5 < wall_len / 2:
                    place(u, 1.3, 0.7, 0.7, "window_glow")
                    place(u, 1.3, 0.8, 0.08, "timber", 0.12)
                    place(u, 1.3, 0.08, 0.8, "timber", 0.12)
                    place(u + 0.48, 1.3, 0.22, 0.8, "wood", 0.1)
                    place(u - 0.48, 1.3, 0.22, 0.8, "wood", 0.1)
                    place(u, 0.93, 0.85, 0.08, "wood_dark", 0.2)
        else:
            for u in ((-wall_len / 4, wall_len / 4) if wall_len > 4.5 else (0,)):
                place(u, 1.3, 0.7, 0.7, "window_glow")
                place(u, 1.3, 0.8, 0.08, "timber", 0.12)
                place(u, 1.3, 0.08, 0.8, "timber", 0.12)
    # A flower box or a barrel by the door, for life.
    nx, ny = sides[door]
    if ny:
        barrel(p, at=(w / 2 - 0.5, ny * (d / 2 + 0.55), 0), s=0.8)
        bush(p, at=(-w / 2 + 0.6, ny * (d / 2 + 0.6), 0), s=0.6, seed=seed, flowers="flower_pink")
    else:
        barrel(p, at=(nx * (w / 2 + 0.55), d / 2 - 0.5, 0), s=0.8)
        bush(p, at=(nx * (w / 2 + 0.6), -d / 2 + 0.6, 0), s=0.6, seed=seed, flowers="flower_yellow")
    out(name, p)


def stone_wall(name, height=2.0, length=2.0):
    """A 2 m stretch of crenellated village wall, 0.6 m thick, along X."""
    p = Part()
    p.box((length, 0.7, height - 0.35), "stone", bevel=0.05)
    for i, row in enumerate(range(int((height - 0.35) / 0.45))):
        off = 0.25 if i % 2 else 0
        for b in range(3):
            bx = -length / 2 + 0.35 + b * 0.66 + off
            if bx < length / 2 - 0.2:
                p.box((0.6, 0.74, 0.04), "stone_dark", at=(bx, 0, 0.42 + i * 0.45), bevel=0.0)
    p.box((length, 0.8, 0.12), "stone_light", at=(0, 0, height - 0.35), bevel=0.03)
    for mx in (-length / 4, length / 4):
        p.box((0.55, 0.75, 0.3), "stone", at=(mx, 0, height - 0.24), bevel=0.04)
    out(name, p)


def palisade(name, height=1.6, length=2.0):
    """A 2 m stretch of sharpened stakes along X, with a red cloth band: the off-limits fences."""
    p = Part()
    n = 7
    for i in range(n):
        x = -length / 2 + (i + 0.5) * length / n
        h = height - 0.25 + (0.12 if i % 2 else 0)
        p.cyl(0.13, h, "wood", at=(x, 0, 0), segments=6)
        p.cyl(0.13, 0.3, "wood_light", at=(x, 0, h), segments=6, radius_top=0.0)
    p.box((length, 0.3, 0.12), "wood_dark", at=(0, 0.02, 0.45), bevel=0.01)
    p.box((length, 0.32, 0.16), "cloth_red", at=(0, 0.0, height * 0.62), bevel=0.0)
    out(name, p)


def gate_house():
    p = Part()
    for sx in (-1, 1):
        x = sx * 3.8
        p.box((2.1, 2.1, 4.6), "stone", at=(x, 0, 0), bevel=0.07)
        p.box((2.3, 2.3, 0.25), "stone_light", at=(x, 0, 4.6), bevel=0.04)
        for mx in (-0.75, 0.75):
            for my in (-0.75, 0.75):
                p.box((0.45, 0.45, 0.45), "stone", at=(x + mx, my, 4.85), bevel=0.03)
        p.cyl(1.35, 1.6, "roof_red", at=(x, 0, 5.15), segments=4, radius_top=0.0, rot=(0, 0, 45))
        p.box((0.25, 0.12, 0.8), "black", at=(x, -1.06, 2.6), bevel=0.0)
        p.box((0.25, 0.12, 0.8), "black", at=(x, 1.06, 2.6), bevel=0.0)
        torch(p, at=(x - sx * 1.12, -1.15, 2.0), s=1.0, rot=(-20, 0, 0))
        banner(p, at=(x + sx * 1.15, -1.0, 0), color="banner_blue", height=3.6)
    # The walkway over the gate and its posts.
    p.box((9.4, 1.8, 0.35), "wood_dark", at=(0, 0, 3.7), bevel=0.03)
    p.box((9.4, 1.9, 0.12), "plank", at=(0, 0, 4.05), bevel=0.0)
    for sy in (-1, 1):
        p.box((5.6, 0.1, 0.6), "wood", at=(0, sy * 0.9, 4.15), bevel=0.0)
    p.box((5.4, 0.25, 0.9), "wood", at=(0, -0.95, 2.85), bevel=0.02)
    p.box((1.6, 0.06, 0.5), "banner_blue", at=(0, -1.1, 3.05), bevel=0.0)
    p.box((0.3, 0.06, 0.3), "gold", at=(0, -1.14, 3.15), rot=(0, 45, 0), bevel=0.0)
    # The doors are gate_door, two of them hung in the scene, so the night watch can shut them.
    out("gate_house", p)


def gate_door():
    """One leaf of the village gate, hinged at the origin and reaching east (+X) to the middle of the
    5.5 m opening. The scene hangs one at each pier, the west one turned half round."""
    p = Part()
    w, h = 2.72, 2.9
    for k in range(5):
        p.box((w / 5 - 0.03, 0.14, h - 0.08 * (k % 2)), "wood_dark" if k % 2 else "wood", at=((k + 0.5) * w / 5, 0, 0),
              bevel=0.015)
    for z in (0.45, 1.45, 2.45):
        p.box((w - 0.08, 0.2, 0.13), "iron_dark", at=(w / 2, 0, z), bevel=0.0)
    p.box((0.16, 0.24, h), "wood_darker", at=(0.08, 0, 0), bevel=0.02)
    p.box((0.14, 0.12, 0.4), "iron_dark", at=(w - 0.3, -0.14, 1.25), bevel=0.0)
    out("gate_door", p)


def bucket():
    """A wooden pail with an iron rope handle, carried to the fire by whoever fetches water."""
    p = Part()
    p.cyl(0.17, 0.32, "wood", segments=10, radius_top=0.21, bevel=0.01)
    for z in (0.05, 0.24):
        p.cyl(0.19 + z * 0.12, 0.04, "iron_dark", at=(0, 0, z), segments=10)
    p.cyl(0.18, 0.02, "water_deep", at=(0, 0, 0.29), segments=10)
    p.box((0.42, 0.03, 0.03), "iron_dark", at=(0, 0, 0.48), bevel=0.0)
    for sx in (-1, 1):
        p.box((0.03, 0.03, 0.2), "iron_dark", at=(sx * 0.2, 0, 0.3), bevel=0.0)
    out("bucket", p)


def well():
    p = Part()
    p.cyl(1.22, 0.85, "stone", segments=10, bevel=0.04)
    p.cyl(1.3, 0.16, "stone_light", at=(0, 0, 0.85), segments=10)
    p.cyl(0.95, 0.04, "water_deep", at=(0, 0, 0.9), segments=10)
    for sx in (-1, 1):
        p.box((0.22, 0.22, 2.3), "timber", at=(sx * 1.05, 0, 0.9), bevel=0.02)
    p.cyl(0.07, 2.3, "wood", at=(-1.15, 0, 2.4), rot=(0, 90, 0), segments=6)
    p.prism(2.9, 1.9, 0.75, "roof_red", at=(0, 0, 3.05), overhang=0.1)
    p.cyl(0.02, 0.7, "rope", at=(0, 0, 1.75), segments=4)
    p.cyl(0.18, 0.3, "wood_light", at=(0, 0, 1.45), segments=8, radius_top=0.2)
    p.cyl(0.19, 0.04, "iron_dark", at=(0, 0, 1.68), segments=8)
    out("well", p)


def stall(name, awning_a, goods):
    p = Part()
    p.box((3.0, 2.0, 1.0), "wood", bevel=0.04)
    p.box((3.15, 2.15, 0.1), "plank", at=(0, 0, 1.0), bevel=0.02)
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.box((0.14, 0.14, 2.45 if sy < 0 else 2.75), "timber", at=(sx * 1.45, sy * 0.95, 0), bevel=0.02)
    # A striped awning sloping down to the front (-Y, where customers stand).
    stripes = 6
    for i in range(stripes):
        x = -1.6 + (i + 0.5) * 3.2 / stripes
        p.box((3.2 / stripes, 2.6, 0.06), awning_a if i % 2 == 0 else "cloth_white",
              at=(x, -0.1, 2.55), rot=(-9, 0, 0), bevel=0.0)
    for i in range(stripes):
        x = -1.6 + (i + 0.5) * 3.2 / stripes
        p.box((3.2 / stripes, 0.04, 0.25), awning_a if i % 2 == 0 else "cloth_white",
              at=(x, -1.42, 2.2), bevel=0.0)
    for gx, color in goods:
        if color == "bread":
            for k in range(3):
                p.ball(0.15, "bread", at=(gx + (k - 1) * 0.28, -0.4, 1.17), scale=(1.4, 0.8, 0.7), subdiv=1)
        elif color == "apple":
            p.box((0.7, 0.5, 0.18), "wood_light", at=(gx, -0.4, 1.1), bevel=0.02)
            for k in range(5):
                p.ball(0.08, "apple", at=(gx - 0.22 + k * 0.11, -0.45 + (k % 2) * 0.1, 1.3), subdiv=1)
        elif color == "pot":
            for k in range(2):
                p.cyl(0.16, 0.3, "pot", at=(gx + (k - 0.5) * 0.4, -0.35, 1.1), segments=8, radius_top=0.1)
        elif color == "cheese":
            p.cyl(0.25, 0.2, "cheese", at=(gx, -0.4, 1.1), segments=8)
        elif color == "fish":
            for k in range(3):
                p.ball(0.12, "fish", at=(gx + (k - 1) * 0.25, -0.4, 1.15), scale=(1.8, 0.6, 0.5), subdiv=1)
        elif color == "rope":
            p.cyl(0.2, 0.15, "rope", at=(gx, -0.4, 1.1), segments=10)
            p.cyl(0.1, 0.16, "wood_dark", at=(gx, -0.4, 1.1), segments=8)
        elif color == "lantern":
            lantern(p, at=(gx, -0.35, 1.1), s=0.9)
    crate(p, at=(1.9, 0.6, 0), s=0.7, rot=15)
    sack(p, at=(-1.95, 0.5, 0), s=0.9)
    out(name, p)


def campfire():
    p = Part()
    for i in range(9):
        a = i / 9 * math.tau
        rock(p, at=(math.cos(a) * 0.62, math.sin(a) * 0.62, -0.1), s=0.32, seed=i, color="stone_dark")
    p.cyl(0.55, 0.03, "black", segments=9)
    for i in range(4):
        a = i / 4 * math.tau + 0.4
        p.cyl(0.07, 0.95, "bark", at=(math.cos(a) * 0.32, math.sin(a) * 0.32, 0.0),
              segments=5)
    for i in range(4):
        a = i / 4 * math.tau + 0.4
        p.cyl(0.07, 0.9, "bark", at=(math.cos(a) * 0.3, math.sin(a) * 0.3, 0.0),
              rot=(-math.degrees(math.sin(a)) * 0.45, math.degrees(math.cos(a)) * 0.45, 0), segments=5)
    p.ball(0.18, "ember", at=(0, 0, 0.12), scale=(1, 1, 0.5), subdiv=1)
    out("campfire", p)


def seat_log():
    p = Part()
    p.cyl(0.24, 2.2, "bark", at=(-1.1, 0, 0.22), rot=(0, 90, 0), segments=7)
    for sx in (-1, 1):
        p.cyl(0.2, 0.02, "wood_light", at=(sx * 1.1 - (0.01 if sx > 0 else 0.01), 0, 0.22), rot=(0, 90, 0), segments=7)
    out("seat_log", p)


def guard_tent():
    p = Part()
    for sx in (-1, 1):
        # Each cloth slope leans from its ground edge to the ridge, 1.9 m up the middle.
        p.box((0.06, 2.5, 2.3), "cloth_white", at=(sx * 1.27, 0, 0.0), rot=(0, sx * -34, 0), bevel=0.0)
    p.cyl(0.04, 2.1, "timber", at=(0, -1.2, 0), segments=5)
    p.cyl(0.04, 2.1, "timber", at=(0, 1.2, 0), segments=5)
    p.box((0.1, 2.6, 0.06), "cloth_blue", at=(0, 0, 1.92), bevel=0.0)
    out("guard_tent", p)


def bell_frame():
    p = Part()
    for sx in (-1, 1):
        p.box((0.22, 0.22, 3.4), "timber", at=(sx * 0.75, 0, 0), bevel=0.02)
        p.box((0.3, 1.1, 0.22), "timber", at=(sx * 0.75, 0, 0), bevel=0.03)
        p.box((0.36, 0.36, 0.3), "stone_dark", at=(sx * 0.75, 0, 0), bevel=0.04)
    p.box((1.8, 0.22, 0.22), "timber", at=(0, 0, 3.15), bevel=0.02)
    p.prism(2.1, 0.9, 0.5, "roof_red", at=(0, 0, 3.4), overhang=0.08, thickness=0.08)
    out("bell_frame", p)


def bell():
    p = Part()
    # Authored hanging from its pivot at the origin: the code swings it about local X.
    p.cyl(0.06, 0.15, "iron_dark", at=(0, 0, -0.15), segments=6)
    p.cyl(0.38, 0.55, "bell", at=(0, 0, -0.7), segments=10, radius_top=0.2)
    p.cyl(0.42, 0.08, "bell", at=(0, 0, -0.72), segments=10)
    p.ball(0.08, "iron_dark", at=(0, 0, -0.78), subdiv=1)
    out("bell", p)


def training_dummy():
    p = Part()
    p.box((1.0, 0.18, 0.14), "wood_dark", bevel=0.01)
    p.box((0.18, 1.0, 0.14), "wood_dark", bevel=0.01)
    p.cyl(0.1, 2.4, "wood", segments=6)
    p.cyl(0.4, 0.85, "straw", at=(0, 0, 1.25), segments=8, radius_top=0.33)
    for h in (1.35, 1.85):
        p.cyl(0.42, 0.07, "rope", at=(0, 0, h), segments=8)
    p.cyl(0.07, 1.9, "wood", at=(-0.95, 0, 1.9), rot=(0, 90, 0), segments=5)
    p.ball(0.27, "cloth_brown", at=(0, 0, 2.35), subdiv=1)
    p.cyl(0.2, 0.03, "cloth_red", at=(0, -0.37, 1.6), rot=(90, 0, 0), segments=10)
    p.cyl(0.1, 0.04, "cloth_white", at=(0, -0.38, 1.6), rot=(90, 0, 0), segments=10)
    out("training_dummy", p)


def shelter():
    """Authored around the shelter's place origin: back wall at +4, side walls at x = +-4."""
    p = Part()
    p.box((8.4, 0.5, 2.9), "stone", at=(0, 4, 0), bevel=0.05)
    for sx in (-1, 1):
        p.box((0.5, 6.4, 2.9), "stone", at=(sx * 4, 1, 0), bevel=0.05)
        p.box((0.32, 0.32, 3.0), "timber", at=(sx * 4, -2.1, 0), bevel=0.02)
    p.box((8.6, 0.25, 0.25), "timber", at=(0, -2.1, 2.85), bevel=0.02)
    p.prism(9.2, 7.0, 1.7, "thatch", at=(0, 1, 2.95), overhang=0.25, thickness=0.18)
    for sign in (-1, 1):
        p.gable_wall(7.0, 1.65, "wood", at=(sign * 4.1, 1, 2.95), rot=(0, 0, 90))
    for bx in (-2.2, 2.2):
        bench(p, at=(bx, 3.3, 0))
    hay_bale(p, at=(-3.0, 1.2, 0), rot=90)
    hay_bale(p, at=(3.0, 0.4, 0), rot=90)
    out("shelter", p)


def bridge():
    """Deck 6 wide (X) and 7.6 long (Y), its top at 0, rails at x = +-2.9."""
    p = Part()
    n = 14
    for i in range(n):
        y = -3.8 + (i + 0.5) * 7.6 / n
        p.box((6.0, 7.6 / n - 0.04, 0.22), "plank" if i % 3 else "wood_light", at=(0, y, -0.22), bevel=0.02)
    for sx in (-1, 1):
        p.box((0.25, 7.8, 0.3), "wood_dark", at=(sx * 2.85, 0, -0.5), bevel=0.02)
        for y in (-3.7, -1.25, 1.25, 3.7):
            p.box((0.26, 0.26, 2.6), "timber", at=(sx * 2.9, y, -1.5), bevel=0.03)
        p.box((0.16, 7.8, 0.16), "wood", at=(sx * 2.9, 0, 1.0), bevel=0.02)
        p.box((0.1, 7.6, 0.1), "rope", at=(sx * 2.9, 0, 0.55), bevel=0.0)
        for y in (-3.7, 3.7):
            p.cyl(0.18, 0.2, "wood_dark", at=(sx * 2.9, y, 1.1), segments=6, radius_top=0.0)
    lantern(p, at=(2.9, -3.7, 1.1), s=0.9)
    out("bridge", p)


def docks():
    """Deck 8 x 4, centred at (0, 0.5), its top at 0."""
    p = Part()
    for i in range(16):
        x = -4 + (i + 0.5) * 0.5
        p.box((0.46, 4.0, 0.2), "plank" if i % 4 else "wood_light", at=(x, 0.5, -0.2), bevel=0.02)
    for x in (-3.9, -1.3, 1.3, 3.9):
        for y in (-1.4, 2.4):
            p.cyl(0.16, 2.6, "timber", at=(x, y, -2.2), segments=6)
    for sx in (-1, 1):
        p.box((0.14, 4.0, 0.14), "wood", at=(sx * 3.9, 0.5, 1.0), bevel=0.01)
    p.box((8.0, 0.14, 0.14), "wood", at=(0, 2.4, 1.0), bevel=0.01)
    for x in (-3.9, 0, 3.9):
        p.cyl(0.13, 1.15, "timber", at=(x, 2.4, 0), segments=6)
    crate(p, at=(2.4, 1.4, 0), s=1.05)
    crate(p, at=(2.5, 1.45, 0.95), s=0.7, rot=20)
    barrel(p, at=(-2.8, 1.6, 0))
    barrel(p, at=(-2.0, 1.9, 0), s=0.85)
    sack(p, at=(1.2, 1.9, 0))
    p.cyl(0.35, 0.25, "rope", at=(-0.6, 1.8, 0), segments=10)
    lamp_post(p, at=(-3.6, -1.1, 0))
    out("docks", p)


def rowboat():
    p = Part()
    p.poly([(-0.7, -1.6, 0.5), (0.7, -1.6, 0.5), (0.45, -1.4, 0), (-0.45, -1.4, 0),
            (-0.8, 1.2, 0.5), (0.8, 1.2, 0.5), (0.5, 1.1, 0), (-0.5, 1.1, 0), (0, 2.0, 0.6), (0, 1.7, 0.1)],
           [(0, 1, 2, 3), (3, 2, 6, 7), (1, 5, 6, 2), (4, 0, 3, 7), (5, 8, 9, 6), (8, 4, 7, 9), (7, 6, 9)],
           "wood", at=(0, 0, 0))
    p.box((1.3, 0.25, 0.06), "plank", at=(0, 0.1, 0.32), bevel=0.0)
    p.box((1.3, 0.25, 0.06), "plank", at=(0, -0.9, 0.32), bevel=0.0)
    p.cyl(0.03, 1.8, "wood_light", at=(0.5, -0.5, 0.4), rot=(0, 80, 20), segments=4)
    out("rowboat", p)


def north_tower():
    """Authored around the tower's place origin: the tower stands at y = 3.5, its door to -Y."""
    p = Part()
    c = (0, 3.5)
    p.cyl(2.5, 0.5, "stone_dark", at=(c[0], c[1], 0), segments=8, rot=(0, 0, 22.5))
    p.cyl(2.3, 7.0, "stone", at=(c[0], c[1], 0.4), segments=8, radius_top=2.1, rot=(0, 0, 22.5))
    for h in (2.3, 4.6):
        p.cyl(2.32 - h * 0.03, 0.12, "stone_light", at=(c[0], c[1], h), segments=8, rot=(0, 0, 22.5))
    p.cyl(2.55, 0.35, "wood_dark", at=(c[0], c[1], 7.3), segments=8, rot=(0, 0, 22.5))
    for i in range(8):
        a = i / 8 * math.tau
        p.box((0.55, 0.4, 0.55), "stone", at=(c[0] + math.cos(a) * 2.3, c[1] + math.sin(a) * 2.3, 7.6),
              rot=(0, 0, math.degrees(a)), bevel=0.03)
    p.cyl(2.75, 2.2, "roof_blue", at=(c[0], c[1], 8.3), segments=8, radius_top=0.0, rot=(0, 0, 22.5))
    p.cyl(0.05, 1.3, "timber", at=(c[0], c[1], 10.4), segments=5)
    p.poly([(0, 0, 0), (0.9, 0, -0.2), (0, 0, -0.5)], [(0, 1, 2), (2, 1, 0)], "banner_red",
           at=(c[0], c[1], 11.65))
    # Door to the south, windows round about.
    p.box((1.3, 0.3, 2.1), "wood_dark", at=(c[0], c[1] - 2.2, 0.4), bevel=0.02)
    p.box((1.5, 0.35, 0.18), "stone_light", at=(c[0], c[1] - 2.22, 2.5), bevel=0.02)
    for i, h in enumerate((3.4, 5.6, 3.4, 5.6)):
        a = math.radians(45 + i * 90)
        p.box((0.25, 0.25, 0.75), "window_glow", at=(c[0] + math.cos(a) * 2.18, c[1] + math.sin(a) * 2.18, h),
              rot=(0, 0, math.degrees(a)), bevel=0.0)
    banner(p, at=(c[0] + 1.6, c[1] - 2.4, 0), color="banner_blue", height=2.8)
    out("north_tower", p)


def armory():
    """5 (X) x 7 (Y) stone hall, door to +X, 3.5 m walls."""
    p = Part()
    p.box((5.3, 7.3, 0.4), "stone_dark", bevel=0.05)
    p.box((5.0, 7.0, 3.1), "stone", at=(0, 0, 0.4), bevel=0.05)
    for sy in (-1, 0, 1):
        p.box((5.2, 0.35, 3.3), "stone_light", at=(0, sy * 3.35, 0.2), bevel=0.04)
    p.prism(7.4, 5.2, 1.6, "slate", at=(0, 0, 3.5), rot=(0, 0, 90), overhang=0.3)
    for sign in (-1, 1):
        p.gable_wall(5.2, 1.55, "stone", at=(0, sign * 3.45, 3.5))
    p.box((0.2, 1.5, 2.3), "wood_dark", at=(2.55, 0, 0.4), bevel=0.02)
    for h in (0.9, 2.1):
        p.box((0.24, 1.55, 0.12), "iron_dark", at=(2.57, 0, h), bevel=0.0)
    p.box((0.2, 2.0, 0.25), "stone_light", at=(2.6, 0, 2.75), bevel=0.02)
    # A shield crest over the door and a weapon rack beside it.
    p.box((0.12, 0.7, 0.8), "banner_red", at=(2.62, 0, 3.0), bevel=0.04)
    p.box((0.13, 0.25, 0.25), "gold", at=(2.66, 0, 3.27), rot=(45, 0, 0), bevel=0.0)
    for y in (-2.0, 2.2):
        p.box((0.12, 1.2, 0.1), "wood_dark", at=(2.85, y, 1.4), bevel=0.0)
        for k in range(4):
            p.cyl(0.03, 1.9, "wood", at=(2.95, y - 0.45 + k * 0.3, 0), rot=(0, -8, 0), segments=4)
            p.cyl(0.05, 0.25, "iron", at=(2.95 + 0.25, y - 0.45 + k * 0.3, 1.9), segments=4, radius_top=0.0)
    out("armory", p)


def keep():
    """8 (X) x 4 (Y) great hall, 6 m walls, door to -Y, two corner turrets."""
    p = Part()
    p.box((8.4, 4.4, 0.5), "stone_dark", bevel=0.05)
    p.box((8.0, 4.0, 5.5), "stone", at=(0, 0, 0.5), bevel=0.06)
    p.box((8.2, 4.2, 0.2), "stone_light", at=(0, 0, 6.0), bevel=0.03)
    for i in range(7):
        x = -3.6 + i * 1.2
        for sy in (-1, 1):
            p.box((0.55, 0.4, 0.5), "stone", at=(x, sy * 1.9, 6.2), bevel=0.03)
    p.prism(7.0, 3.2, 1.5, "roof_red", at=(0, 0, 6.2), overhang=0.0)
    for sx in (-1, 1):
        x = sx * 4.1
        p.cyl(1.2, 8.0, "stone", at=(x, -1.6, 0), segments=8, rot=(0, 0, 22.5))
        p.cyl(1.35, 0.25, "stone_light", at=(x, -1.6, 8.0), segments=8, rot=(0, 0, 22.5))
        p.cyl(1.45, 2.0, "roof_red", at=(x, -1.6, 8.2), segments=8, radius_top=0.0, rot=(0, 0, 22.5))
        p.box((0.25, 0.3, 0.8), "window_glow", at=(x, -2.8, 5.0), bevel=0.0)
        banner(p, at=(x, -1.6, 10.0), color="banner_red", height=1.6)
    p.box((1.8, 0.3, 2.8), "wood_dark", at=(0, -2.05, 0.5), bevel=0.03)
    p.box((2.2, 0.35, 0.3), "stone_light", at=(0, -2.08, 3.3), bevel=0.02)
    for x in (-2.4, 2.4):
        p.box((0.6, 0.15, 1.1), "window_glow", at=(x, -2.05, 3.2), bevel=0.0)
        p.box((0.8, 0.2, 0.15), "stone_light", at=(x, -2.07, 4.35), bevel=0.0)
        p.box((0.5, 0.06, 1.8), "banner_red", at=(x, -2.12, 1.0), bevel=0.0)
    out("keep", p)


def cave():
    """Rocks round the locked cave, authored around (-40, 45): the mouth faces -Y."""
    p = Part()
    rng = random.Random(7)
    big = [((0, 2.8, 0), 3.4, (1.9, 0.9, 1.2)), ((-5, -1, 0), 2.6, (0.8, 1.6, 1.2)),
           ((5, -1, 0), 2.6, (0.8, 1.6, 1.2)), ((-3.2, 2.2, 0.5), 2.2, (1, 1, 1.1)),
           ((3.2, 2.2, 0.5), 2.2, (1, 1, 1.1)), ((0, 2.5, 3.2), 2.4, (1.6, 1, 0.7))]
    for i, (at, r, sc) in enumerate(big):
        p.ball(r, ("rock", "rock_dark", "stone_dark")[i % 3], at=at, scale=sc, subdiv=1, jitter=0.15, seed=i)
    p.box((6.4, 1.2, 3.2), "black", at=(0, 0.9, 0), bevel=0.2)
    # The barred gate, chained and locked.
    p.box((7.0, 0.3, 0.25), "wood_dark", at=(0, 0.3, 2.85), bevel=0.02)
    for k in range(9):
        x = -3.2 + k * 0.8
        p.box((0.18, 0.18, 3.0), "iron_dark", at=(x, 0.3, 0), bevel=0.0)
    for h in (0.6, 1.8):
        p.box((7.0, 0.22, 0.16), "wood_dark", at=(0, 0.25, h), bevel=0.01)
    p.box((0.5, 0.25, 0.6), "gold", at=(0, 0.1, 1.2), bevel=0.05)
    p.cyl(0.12, 0.2, "iron", at=(0, 0.1, 1.75), rot=(90, 0, 0), segments=6)
    for i in range(8):
        a = rng.uniform(0, math.tau)
        rock(p, at=(rng.uniform(-6, 6), rng.uniform(-3.5, -1.5), 0), s=rng.uniform(0.3, 0.6), seed=20 + i)
    for i in range(6):
        grass_tuft(p, at=(rng.uniform(-6, 6), rng.uniform(-3, -1), 0), seed=40 + i, color="moss")
    out("cave", p)


def signpost():
    p = Part()
    p.cyl(0.08, 2.4, "timber", segments=6)
    p.box((1.3, 0.08, 0.3), "plank", at=(0.45, 0, 1.9), bevel=0.02)
    p.poly([(0, 0, 0), (0.25, 0, 0.15), (0, 0, 0.3)], [(0, 1, 2), (2, 1, 0)], "plank", at=(1.1, -0.0, 1.9))
    p.box((1.1, 0.08, 0.28), "wood_light", at=(-0.35, 0, 1.45), rot=(0, 0, 0), bevel=0.02)
    for i in range(4):
        rock(p, at=(math.cos(i * 1.6) * 0.35, math.sin(i * 1.6) * 0.35, -0.05), s=0.22, seed=60 + i)
    out("signpost", p)


def cart():
    p = Part()
    p.box((1.6, 2.4, 0.2), "plank", at=(0, 0, 0.6), bevel=0.02)
    for sx in (-1, 1):
        p.box((0.08, 2.4, 0.45), "wood", at=(sx * 0.78, 0, 0.8), bevel=0.0)
        p.cyl(0.5, 0.12, "wood_dark", at=(sx * 0.9, -0.3, 0.5), rot=(0, 90, 0), segments=10)
        p.cyl(0.12, 0.16, "iron_dark", at=(sx * 0.91, -0.3, 0.5), rot=(0, 90, 0), segments=6)
    p.box((0.08, 1.8, 0.08), "wood", at=(0.3, 2.0, 0.55), rot=(10, 0, 0), bevel=0.0)
    p.box((0.08, 1.8, 0.08), "wood", at=(-0.3, 2.0, 0.55), rot=(10, 0, 0), bevel=0.0)
    hay_bale(p, at=(0, -0.4, 0.8), rot=90)
    sack(p, at=(0.2, 0.6, 0.8), s=0.8)
    out("cart", p)


def weapon_rack():
    p = Part()
    for sx in (-1, 1):
        p.box((0.12, 0.3, 1.5), "wood_dark", at=(sx * 0.7, 0, 0), bevel=0.01)
    p.box((1.6, 0.12, 0.12), "wood", at=(0, 0, 1.2), bevel=0.0)
    p.box((1.6, 0.3, 0.1), "wood", at=(0, 0, 0.25), bevel=0.0)
    for k in range(4):
        x = -0.5 + k * 0.33
        p.cyl(0.03, 1.9, "wood", at=(x, 0.05, 0.3), rot=(-6, 0, 0), segments=4)
        p.cyl(0.05, 0.25, "iron", at=(x, 0.25, 2.2), segments=4, radius_top=0.0)
    p.cyl(0.35, 0.06, "banner_blue", at=(0.95, 0.0, 0.7), rot=(90, 0, 0), segments=8)
    out("weapon_rack", p)


def grass_patch(name, seed):
    p = Part()
    rng = random.Random(seed)
    for i in range(4):
        grass_tuft(p, at=(rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6), 0), seed=seed * 7 + i,
                   color=("grass_light", "grass", "reed")[i % 3])
    out(name, p)


def flowers(name, color, seed):
    p = Part()
    rng = random.Random(seed)
    for i in range(7):
        x, y = rng.uniform(-0.5, 0.5), rng.uniform(-0.5, 0.5)
        p.cyl(0.02, 0.35, "herb_stem", at=(x, y, 0), segments=3)
        p.ball(0.07, color, at=(x, y, 0.37), subdiv=0)
    out(name, p)


def reeds():
    p = Part()
    rng = random.Random(3)
    for i in range(9):
        x, y = rng.uniform(-0.5, 0.5), rng.uniform(-0.3, 0.3)
        h = rng.uniform(0.8, 1.3)
        p.cyl(0.03, h, "reed", at=(x, y, 0), rot=(rng.uniform(-10, 10), rng.uniform(-10, 10), 0), segments=3)
        if i % 3 == 0:
            p.cyl(0.05, 0.2, "wood_dark", at=(x, y, h), segments=4)
    out("reeds", p)


def stump():
    p = Part()
    p.cyl(0.4, 0.45, "bark", segments=7, radius_top=0.35)
    p.cyl(0.33, 0.02, "wood_light", at=(0, 0, 0.45), segments=7)
    p.ball(0.08, "cloth_red", at=(0.3, 0.1, 0.45), scale=(1, 1, 0.5), subdiv=0)
    out("stump", p)


# ---- Items (pickup scale, centred, resting on z = 0) ------------------------------------------

def items():
    p = Part()
    p.ball(0.16, "bread", at=(0, 0, 0.1), scale=(1.5, 0.85, 0.65), subdiv=1)
    for k in range(3):
        p.box((0.04, 0.2, 0.02), "wood_light", at=(-0.12 + k * 0.12, 0, 0.19), rot=(0, 0, 20), bevel=0.0)
    out("item_bread", p)

    p = Part()
    p.cyl(0.2, 0.16, "rope", segments=12)
    p.cyl(0.1, 0.17, "wood_darker", segments=10)
    p.cyl(0.05, 0.25, "rope", at=(0.2, 0, 0.1), rot=(0, 70, 0), segments=4)
    out("item_rope", p)

    out("item_torch", torch(None, at=(0, 0, 0), s=1.0))
    out("item_lantern", lantern(None, at=(0, 0, 0), s=1.2))

    p = Part()
    p.cyl(0.025, 0.4, "herb_stem", segments=4)
    for i in range(6):
        a = i / 6 * math.tau
        p.ball(0.07, "herb_glow", at=(math.cos(a) * 0.08, math.sin(a) * 0.08, 0.42), scale=(1.4, 0.6, 0.35), subdiv=1)
    p.ball(0.04, "flower_yellow", at=(0, 0, 0.44), subdiv=0)
    for i in range(3):
        a = i / 3 * math.tau
        p.ball(0.06, "leaf_light", at=(math.cos(a) * 0.08, math.sin(a) * 0.08, 0.12), scale=(1.6, 0.6, 0.3), subdiv=0)
    out("item_moonpetal", p)

    p = Part()
    p.poly([(-0.45, -0.3, 0), (0.45, -0.32, 0), (0.55, 0.0, 0), (0.42, 0.33, 0), (-0.4, 0.3, 0), (-0.55, 0, 0)],
           [(0, 1, 2, 3, 4, 5), (5, 4, 3, 2, 1, 0)], "fur", at=(0, 0, 0.02))
    p.ball(0.14, "fur_dark", at=(0.55, 0, 0.06), scale=(1.4, 1, 0.5), subdiv=1)
    p.cyl(0.05, 0.4, "fur_light", at=(-0.55, 0, 0.04), rot=(0, -80, 0), segments=4, radius_top=0.0)
    out("item_pelt", p)

    p = Part()
    p.ball(0.15, "leather", at=(0, 0, 0.13), scale=(1, 1, 0.9), subdiv=1)
    p.cyl(0.06, 0.08, "rope", at=(0, 0, 0.25), segments=6)
    for k in range(3):
        p.cyl(0.06, 0.02, "gold", at=(0.17 + k * 0.03, 0.05 * k, k * 0.022), segments=8)
    out("item_coins", p)

    p = Part()
    p.ball(0.09, "apple", at=(0, 0, 0.09), subdiv=1)
    p.cyl(0.01, 0.06, "wood_dark", at=(0, 0, 0.17), segments=3)
    out("item_apple", p)


# ---- Characters --------------------------------------------------------------------------------
# A segmented figure, 1.85 m tall, front +Y. Parts hang from their joints so code can swing them:
#   torso (pivot at the hips), head (neck), arm_l / arm_r (shoulders), leg_l / leg_r (hips).

def humanoid(name, look):
    skin, hair = look.get("skin", "skin"), look.get("hair", "hair_brown")
    shirt, trousers, boots = look["shirt"], look.get("trousers", "cloth_brown"), look.get("boots", "leather")
    parts = []

    torso = Part()
    torso.box((0.46, 0.28, 0.55), shirt, at=(0, 0, 0.02), bevel=0.05, taper=1.08)
    torso.box((0.48, 0.3, 0.09), look.get("belt", "leather"), at=(0, 0, 0.02), bevel=0.02)
    torso.box((0.08, 0.04, 0.06), "gold", at=(0, 0.16, 0.035), bevel=0.0)
    if look.get("tabard"):
        torso.box((0.3, 0.04, 0.5), look["tabard"], at=(0, 0.15, -0.12), bevel=0.0)
        torso.box((0.12, 0.05, 0.12), "gold", at=(0, 0.17, 0.3), rot=(0, 45, 0), bevel=0.0)
    if look.get("apron"):
        torso.box((0.36, 0.04, 0.6), look["apron"], at=(0, 0.15, -0.32), bevel=0.0)
    if look.get("cape"):
        torso.box((0.52, 0.05, 0.95), look["cape"], at=(0, -0.17, -0.42), rot=(-6, 0, 0), bevel=0.0)
        torso.box((0.56, 0.32, 0.08), look["cape"], at=(0, 0, 0.55), bevel=0.02)
    if look.get("skirt"):
        torso.box((0.5, 0.34, 0.45), look["skirt"], at=(0, 0, -0.42), bevel=0.04, taper=0.82)
    if look.get("pack"):
        torso.box((0.36, 0.18, 0.4), look["pack"], at=(0, -0.22, 0.1), bevel=0.05)
        torso.box((0.3, 0.2, 0.08), "leather", at=(0, -0.24, 0.5), bevel=0.02)
    if look.get("pauldrons"):
        for sx in (-1, 1):
            torso.ball(0.13, look["pauldrons"], at=(sx * 0.27, 0, 0.52), scale=(1, 1.1, 0.7), subdiv=1)
    parts.append(("torso", torso, None, (0, 0, 0.84)))

    head = Part()
    head.box((0.12, 0.12, 0.08), skin, at=(0, 0, 0.0), bevel=0.0)
    head.box((0.36, 0.34, 0.36), skin, at=(0, 0, 0.06), bevel=0.06)
    for sx in (-1, 1):
        head.box((0.05, 0.03, 0.07), "eye", at=(sx * 0.08, 0.17, 0.22), bevel=0.0)
    head.box((0.06, 0.06, 0.07), skin, at=(0, 0.18, 0.15), bevel=0.01)
    if look.get("beard"):
        head.box((0.3, 0.1, 0.16), look["beard"], at=(0, 0.14, 0.04), bevel=0.03)
    style = look.get("hair_style", "short")
    if style == "short":
        head.box((0.38, 0.36, 0.12), hair, at=(0, -0.01, 0.34), bevel=0.04)
        head.box((0.38, 0.12, 0.2), hair, at=(0, -0.13, 0.18), bevel=0.03)
    elif style == "long":
        head.box((0.39, 0.37, 0.13), hair, at=(0, -0.01, 0.34), bevel=0.04)
        head.box((0.4, 0.14, 0.42), hair, at=(0, -0.13, -0.02), bevel=0.04)
    elif style == "bun":
        head.box((0.38, 0.36, 0.12), hair, at=(0, -0.01, 0.34), bevel=0.04)
        head.ball(0.12, hair, at=(0, -0.14, 0.42), subdiv=1)
    hat = look.get("hat")
    if hat == "helmet":
        head.cyl(0.24, 0.24, look.get("helmet", "silver"), at=(0, 0, 0.3), segments=8, radius_top=0.2)
        head.cyl(0.2, 0.08, look.get("helmet", "silver"), at=(0, 0, 0.54), segments=8, radius_top=0.05)
        head.box((0.04, 0.05, 0.2), look.get("helmet", "silver"), at=(0, 0.22, 0.12), bevel=0.0)
        if look.get("plume"):
            head.box((0.06, 0.34, 0.22), look["plume"], at=(0, -0.05, 0.6), rot=(10, 0, 0), bevel=0.02)
    elif hat == "hood":
        head.box((0.42, 0.4, 0.42), look.get("hood", "cloth_green"), at=(0, -0.03, 0.08), bevel=0.08)
        head.box((0.3, 0.06, 0.3), skin, at=(0, 0.19, 0.06), bevel=0.02)
        for sx in (-1, 1):
            head.box((0.05, 0.03, 0.07), "eye", at=(sx * 0.08, 0.225, 0.22), bevel=0.0)
        if look.get("mask"):
            head.box((0.31, 0.05, 0.12), look["mask"], at=(0, 0.22, 0.06), bevel=0.0)
    elif hat == "straw":
        head.cyl(0.38, 0.05, "straw", at=(0, 0, 0.36), segments=10)
        head.cyl(0.2, 0.16, "straw", at=(0, 0, 0.38), segments=10, radius_top=0.17)
        head.cyl(0.205, 0.04, "cloth_red", at=(0, 0, 0.4), segments=10)
    elif hat == "scarf":
        head.box((0.4, 0.38, 0.16), look.get("scarf", "cloth_purple"), at=(0, -0.01, 0.3), bevel=0.05)
        head.box((0.12, 0.08, 0.3), look.get("scarf", "cloth_purple"), at=(0.15, -0.18, 0.05), rot=(0, 15, 0), bevel=0.02)
    elif hat == "cap":
        head.box((0.39, 0.37, 0.12), look.get("capc", "cloth_blue"), at=(0, 0, 0.35), bevel=0.04)
        head.box((0.3, 0.15, 0.03), look.get("capc", "cloth_blue"), at=(0, 0.2, 0.37), bevel=0.0)
    parts.append(("head", head, "torso", (0, 0, 1.42)))

    for side, sx in (("arm_l", -1), ("arm_r", 1)):
        arm = Part()
        arm.box((0.14, 0.15, 0.36), look.get("sleeve", shirt), at=(0, 0, -0.36), bevel=0.04)
        arm.box((0.12, 0.13, 0.22), look.get("forearm", look.get("sleeve", shirt)), at=(0, 0, -0.56), bevel=0.03)
        arm.box((0.11, 0.12, 0.11), skin, at=(0, 0.0, -0.67), bevel=0.03)
        held = look.get("right") if sx > 0 else look.get("left")
        if held == "spear":
            arm.cyl(0.025, 2.1, "wood", at=(0, 0.06, -1.25), segments=5)
            arm.cyl(0.05, 0.25, "silver", at=(0, 0.06, 0.85), segments=4, radius_top=0.0)
            arm.box((0.12, 0.03, 0.04), "cloth_red", at=(0, 0.06, 0.78), bevel=0.0)
        elif held == "shield":
            arm.cyl(0.3, 0.06, look.get("shield", "banner_blue"), at=(-0.1, 0.03, -0.45), rot=(0, 90, 0), segments=8)
            arm.cyl(0.08, 0.08, "gold", at=(-0.14, 0.03, -0.45), rot=(0, 90, 0), segments=6)
        elif held == "sword":
            arm.box((0.05, 0.05, 0.75), "silver", at=(0, 0.1, -0.66), rot=(-80, 0, 0), bevel=0.0)
            arm.box((0.2, 0.05, 0.04), "gold", at=(0, 0.12, -0.67), bevel=0.0)
        elif held == "basket":
            arm.cyl(0.18, 0.18, "wood_light", at=(0, 0.05, -0.85), segments=8, radius_top=0.21)
            arm.ball(0.06, "apple", at=(0.05, 0.05, -0.65), subdiv=0)
            arm.ball(0.06, "bread", at=(-0.05, 0.08, -0.66), subdiv=0)
        elif held == "staff":
            arm.cyl(0.03, 1.8, "wood_dark", at=(0, 0.06, -1.25), segments=5)
        elif held == "ledger":
            arm.box((0.2, 0.05, 0.26), "leather", at=(0, 0.1, -0.7), bevel=0.01)
        elif held == "torch":
            torch(arm, at=(0, 0.06, -0.85), s=1.0)
        parts.append((side, arm, "torso", (sx * 0.3, 0, 1.36)))

    for side, sx in (("leg_l", -1), ("leg_r", 1)):
        leg = Part()
        leg.box((0.17, 0.19, 0.6), trousers, at=(0, 0, -0.62), bevel=0.04)
        leg.box((0.19, 0.25, 0.22), boots, at=(0, 0.03, -0.84), bevel=0.04)
        parts.append((side, leg, "torso", (sx * 0.12, 0, 0.84)))

    out_rig(name, parts)


def wolf():
    parts = []
    body = Part()
    body.box((0.42, 1.0, 0.42), "fur", at=(0, 0, -0.18), bevel=0.08, taper=0.9)
    body.box((0.46, 0.45, 0.48), "fur_dark", at=(0, 0.3, -0.2), bevel=0.08)
    body.box((0.32, 0.6, 0.12), "fur_light", at=(0, 0.05, -0.2), bevel=0.04)
    body.cyl(0.08, 0.55, "fur_dark", at=(0, -0.5, 0.0), rot=(-120, 0, 0), segments=5, radius_top=0.03)
    parts.append(("torso", body, None, (0, 0, 0.62)))
    head = Part()
    head.box((0.3, 0.32, 0.3), "fur", at=(0, 0.1, -0.12), bevel=0.06)
    head.box((0.18, 0.28, 0.16), "fur_light", at=(0, 0.35, -0.12), bevel=0.04, taper=0.85)
    head.box((0.07, 0.05, 0.05), "black", at=(0, 0.5, -0.0), bevel=0.0)
    for sx in (-1, 1):
        head.cyl(0.07, 0.17, "fur_dark", at=(sx * 0.1, 0.05, 0.16), segments=4, radius_top=0.0)
        head.box((0.05, 0.03, 0.04), "flower_yellow", at=(sx * 0.08, 0.26, 0.05), bevel=0.0)
    parts.append(("head", head, "torso", (0, 0.55, 0.72)))
    for name, x, y in (("leg_fl", -0.14, 0.32), ("leg_fr", 0.14, 0.32), ("leg_bl", -0.14, -0.35), ("leg_br", 0.14, -0.35)):
        leg = Part()
        leg.box((0.12, 0.13, 0.5), "fur_dark", at=(0, 0, -0.5), bevel=0.03)
        leg.box((0.13, 0.16, 0.07), "fur_dark", at=(0, 0.02, -0.55), bevel=0.02)
        parts.append((name, leg, "torso", (x, y, 0.55)))
    out_rig("wolf", parts)


def characters():
    humanoid("char_bram", dict(shirt="cloth_blue", tabard="gold", sleeve="iron", trousers="cloth_dark",
                               hat="helmet", helmet="silver", right="spear", left="shield", shield="banner_blue",
                               pauldrons="silver", beard="hair_brown"))
    humanoid("char_wren", dict(shirt="cloth_teal", tabard="silver", sleeve="iron", trousers="cloth_dark",
                               hat="helmet", helmet="iron", right="spear", left="shield", shield="cloth_teal",
                               hair="hair_red", hair_style="long", pauldrons="iron"))
    humanoid("char_hale", dict(shirt="cloth_red", tabard="gold", sleeve="iron", trousers="cloth_dark",
                               hat="helmet", helmet="gold", plume="cloth_red", cape="banner_red", right="sword",
                               pauldrons="gold", beard="hair_grey", hair="hair_grey", skin="skin_tan"))
    humanoid("char_odile", dict(shirt="cloth_green", apron="cloth_white", skirt="cloth_purple", trousers="cloth_purple",
                                hat="scarf", scarf="cloth_purple", hair="hair_black", hair_style="bun",
                                right="ledger", skin="skin_dark"))
    humanoid("char_player", dict(shirt="cloth_brown", sleeve="cloth_brown", trousers="cloth_dark", boots="leather",
                                 hat="hood", hood="cloth_green", cape="cloth_green", pack="leather", right="staff"))
    humanoid("char_thief", dict(shirt="cloth_dark", sleeve="cloth_dark", trousers="black", hat="hood",
                                hood="cloth_dark", mask="black", right="sword"))
    looks = [
        ("char_villager_tomas", dict(shirt="cloth_orange", trousers="cloth_brown", hat="straw", beard="hair_brown",
                                     right="basket")),
        ("char_villager_ada", dict(shirt="cloth_yellow", skirt="cloth_blue", trousers="cloth_blue", hair="hair_blond",
                                   hair_style="long", left="basket")),
        ("char_villager_pell", dict(shirt="cloth_grey", trousers="cloth_dark", hat="cap", capc="cloth_red",
                                    hair="hair_black", skin="skin_dark")),
        ("char_villager_mira", dict(shirt="cloth_purple", skirt="cloth_white", trousers="cloth_white",
                                    hair="hair_red", hair_style="bun", apron="cloth_white", skin="skin_tan")),
    ]
    for name, look in looks:
        humanoid(name, look)
    wolf()


# ---- Terrain -----------------------------------------------------------------------------------

PATHS = [  # (x, z) polylines the dirt roads follow, in village coordinates
    [(0, -40), (0, -15), (0, -2.4), (16, -0.5), (33, 0), (60, 0)],
    [(0, -2.4), (0, 23), (0, 30), (-20, 33), (-40, 38)],
    [(0, 30), (18, 32.6)],
    [(0, -2.4), (-12.6, -8)],
    [(0, -2.4), (-18, 8)],
    [(0, -6), (14, -8.2), (21, -12)],
    [(16, -0.5), (30, 18.5)],
]


def _seg_dist(px, pz, a, b):
    ax, az = a
    bx, bz = b
    dx, dz = bx - ax, bz - az
    t = max(0.0, min(1.0, ((px - ax) * dx + (pz - az) * dz) / (dx * dx + dz * dz or 1)))
    cx, cz = ax + t * dx, az + t * dz
    return math.hypot(px - cx, pz - cz)


def path_distance(x, z):
    return min(_seg_dist(x, z, line[i], line[i + 1]) for line in PATHS for i in range(len(line) - 1))


def _hill_height(x, z):
    """Height outside the play area. Keep TerrainHeight in VillageSceneBuilder.Art.cs in step."""
    out_x = max(0.0, abs(x) - 50)
    out_z = max(0.0, -30 - z, z - 50)
    d = math.hypot(out_x, out_z)
    if d <= 0:
        return 0.0
    return min(26.0, d * 0.34) + math.sin(x * 0.11) * math.cos(z * 0.09) * min(4.0, d * 0.12)


def _patch(x, z):
    """Smooth 0..1 noise for grass colour patches."""
    n = (math.sin(x * 0.11 + z * 0.05) + math.sin(z * 0.13 - x * 0.07 + 1.3) +
         math.sin((x + z) * 0.21 + 2.1) * 0.5 + math.sin(x * 0.37 - z * 0.29) * 0.25)
    return n / 5.5 + 0.5


def _axis(lo, hi, inner_lo, inner_hi):
    """Grid lines: 1.5 m apart inside the play area, then 3 m, then 6 m out to the edge."""
    vals = []
    v = lo
    while v <= hi + 1e-6:
        vals.append(v)
        if inner_lo - 4 <= v < inner_hi + 4:
            v += 1.5
        elif inner_lo - 24 <= v < inner_hi + 24:
            v += 3.0
        else:
            v += 6.0
    return vals


def terrain():
    """The ground: flat at 0 wherever anyone walks, the stream bed through z 20..26, hills beyond."""
    p = Part()
    p.recalc_normals = False
    rng = random.Random(11)
    xs = _axis(-150, 150, -52, 52)
    zs = _axis(-130, 170, -32, 52)

    def height(x, z):
        h = 0.0
        if 19.4 < z < 26.6:
            d = min(z - 19.4, 26.6 - z)
            h = -min(1.25, d * 1.4)
        return max(h, _hill_height(x, z)) if not (-50 <= x <= 50 and -30 <= z <= 50) else h

    grid = {}
    for i, x in enumerate(xs):
        for j, z in enumerate(zs):
            inside = -51 <= x <= 51 and -31 <= z <= 51
            jx = 0.0 if inside else rng.uniform(-0.6, 0.6)
            jz = 0.0 if inside else rng.uniform(-0.6, 0.6)
            grid[(i, j)] = (x + jx, z + jz, height(x, z))
    for i in range(len(xs) - 1):
        for j in range(len(zs) - 1):
            a, b, c, d = grid[(i, j)], grid[(i + 1, j)], grid[(i + 1, j + 1)], grid[(i, j + 1)]
            for tri in ((a, b, c), (a, c, d)) if (i + j) % 2 else ((a, b, d), (b, c, d)):
                cx = sum(v[0] for v in tri) / 3
                cz = sum(v[1] for v in tri) / 3
                ch = sum(v[2] for v in tri) / 3
                n = _patch(cx, cz)
                if ch < -0.6:
                    color = "mud"
                elif ch < -0.05:
                    color = "dirt_dark"
                elif ch > 2.0:
                    color = "grass_dark" if n < 0.45 else ("moss" if n < 0.6 else "pine_dark" if ch > 14 else "grass_dark")
                else:
                    pd = path_distance(cx, cz)
                    if pd < 1.5:
                        color = "path"
                    elif pd < 2.1:
                        color = "dirt"
                    else:
                        color = "grass_light" if n > 0.64 else ("grass_dark" if n < 0.3 else "grass")
                p.poly([(v[0], v[1], v[2]) for v in tri], [(0, 1, 2)], color)
    out("terrain", p)


def water():
    """The stream's surface and the well's: flat, finely divided so the shader can ripple it."""
    p = Part()
    p.recalc_normals = False
    step = 2.0
    for i in range(int(-80 / step), int(80 / step)):
        for j in range(3):
            x0, x1 = i * step, (i + 1) * step
            z0, z1 = 19.5 + j * 2.4, 19.5 + (j + 1) * 2.4
            p.poly([(x0, z0, 0), (x1, z0, 0), (x1, z1, 0), (x0, z1, 0)], [(0, 1, 2, 3)], "water")
    out("water", p)


# ---- Run ---------------------------------------------------------------------------------------

def main():
    kit.write_palettes(ART)
    gate_house()
    gate_door()
    bucket()
    stone_wall("wall_stone")
    palisade("wall_palisade")
    well()
    stall("stall_bread", "cloth_red", [(-0.8, "bread"), (0.6, "cheese")])
    stall("stall_goods", "cloth_blue", [(-0.8, "rope"), (0.6, "lantern")])
    stall("stall_fruit", "cloth_green", [(-0.8, "apple"), (0.6, "pot")])
    campfire()
    seat_log()
    guard_tent()
    bell_frame()
    bell()
    training_dummy()
    shelter()
    bridge()
    docks()
    rowboat()
    north_tower()
    armory()
    keep()
    cave()
    signpost()
    cart()
    weapon_rack()
    cottage("cottage_a", 5, 4, "roof_red", "S", 1)
    cottage("cottage_b", 5, 4, "thatch", "S", 2)
    cottage("cottage_c", 6, 5, "roof_blue", "E", 3)
    cottage("cottage_d", 5, 5, "roof_green", "W", 4)
    cottage("cottage_e", 4, 4, "thatch", "N", 5)
    cottage("cottage_f", 5, 4, "roof_red", "N", 6)
    for i in range(3):
        out("pine_%d" % i, pine(None, s=1.0 + i * 0.15, seed=i))
    for i in range(2):
        out("oak_%d" % i, oak(None, s=1.0 + i * 0.2, seed=10 + i))
    for i in range(2):
        out("bush_%d" % i, bush(None, s=1.0, seed=20 + i, flowers=("flower_pink", None)[i]))
    for i in range(3):
        out("rock_%d" % i, rock(None, s=1.0 + i * 0.5, seed=30 + i, color=("rock", "rock_dark", "stone")[i]))
    grass_patch("grass_0", 1)
    grass_patch("grass_1", 2)
    flowers("flowers_0", "flower_yellow", 3)
    flowers("flowers_1", "flower_blue", 4)
    reeds()
    stump()
    out("barrel", barrel())
    out("crate", crate())
    out("sack", sack())
    out("hay_bale", hay_bale())
    out("woodpile", woodpile())
    out("lamp_post", lamp_post())
    out("fence", fence())
    out("bench", bench())
    out("banner_blue", banner(None, color="banner_blue"))
    out("banner_red", banner(None, color="banner_red"))
    items()
    characters()
    terrain()
    water()
    print("KIT: %d models into %s" % (len(MADE), MODELS))


main()
