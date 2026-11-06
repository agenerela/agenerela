"""Every model in Living Vale's art kit, as code. Front faces -Y, Z up, metres, pivot at the base centre.

Each function takes a `Kit` and adds parts to it. `ASSETS` at the bottom is the export list; its
ids must match `ArtKit.Entries` in the game (a Unity test checks every id resolves).
"""
import math

from kitlib import Kit

# ----------------------------------------------------------------------------------------------
# Buildings


def gable_roof(k, w, d, z_eave, z_ridge, overhang, colour, ridge_colour, thick=0.22):
    """Two roof slabs meeting at a ridge along X, eaves at y = +-(d/2 + overhang)."""
    hw = w / 2 + overhang
    e = d / 2 + overhang
    drop = (z_ridge - z_eave) / (d / 2) * overhang   # keep the pitch through the overhang
    for side in (-1, 1):
        k.slab(colour, -hw, hw, [
            (side * e, z_eave - drop), (side * e, z_eave - drop + thick),
            (0, z_ridge + thick), (0, z_ridge),
        ])
    k.box(ridge_colour, (0, 0, z_ridge + thick * 0.9), (w + overhang * 2 + 0.1, 0.3, 0.2))


def gable_wall(k, x, d, z0, z1, colour, thick=0.2):
    k.hull(colour, [(x + dx, y, z) for dx in (-thick / 2, thick / 2)
                    for (y, z) in ((-d / 2, z0), (d / 2, z0), (0, z1))])


def window(k, center, facing, w=0.8, h=0.7):
    """A shuttered window on a wall: facing is 'y' (front/back wall) or 'x' (side wall)."""
    x, y, z = center
    if facing == "y":
        k.box("timber_dark", (x, y, z), (w + 0.2, 0.08, h + 0.2))
        k.box("glass", (x, y - 0.03 * (1 if y < 0 else -1), z), (w, 0.06, h))
        k.box("timber_dark", (x, y - 0.06 * (1 if y < 0 else -1), z), (0.06, 0.04, h))
        for s in (-1, 1):
            k.box("timber", (x + s * (w / 2 + 0.24), y - 0.04 * (1 if y < 0 else -1), z), (0.3, 0.05, h + 0.1))
    else:
        k.box("timber_dark", (x, y, z), (0.08, w + 0.2, h + 0.2))
        k.box("glass", (x + 0.03 * (1 if x > 0 else -1), y, z), (0.06, w, h))


def cottage_a(k):
    """Whitewashed timber-framed cottage on a stone plinth, thatched roof, chimney: Briar Glen's homes."""
    W, D, plinth, wall = 5.0, 4.0, 0.5, 2.2
    top = plinth + wall
    k.box_on("stone_dark", (0, 0, 0), (W + 0.25, D + 0.25, plinth))
    k.box_on("plaster", (0, 0, plinth), (W, D, wall))
    # timber frame: corner posts, sill and top beams, braces on the front
    for sx in (-1, 1):
        for sy in (-1, 1):
            k.box_on("timber_dark", (sx * (W / 2 - 0.06), sy * (D / 2 - 0.06), plinth), (0.22, 0.22, wall))
    for sy in (-1, 1):
        k.box("timber_dark", (0, sy * (D / 2 + 0.02), top - 0.1), (W, 0.12, 0.2))
        k.box("timber_dark", (0, sy * (D / 2 + 0.02), plinth + 0.08), (W, 0.12, 0.16))
    for sx in (-1, 1):
        k.box("timber_dark", (sx * (W / 2 + 0.02), 0, top - 0.1), (0.12, D, 0.2))
        k.box("timber_dark", (sx * 1.7, -D / 2 - 0.02, plinth + wall / 2), (0.14, 0.1, wall))
    k.box("timber_dark", (-1.9, -D / 2 - 0.03, plinth + 0.75), (0.12, 0.08, 1.3), r=(0, 40, 0))
    k.box("timber_dark", (1.9, -D / 2 - 0.03, plinth + 0.75), (0.12, 0.08, 1.3), r=(0, -40, 0))
    # gables and roof
    for sx in (-1, 1):
        gable_wall(k, sx * W / 2, D, top, top + 1.8, "plaster")
        k.box("timber_dark", (sx * (W / 2 + 0.02), 0, top + 0.8), (0.12, 0.14, 1.6))
    gable_roof(k, W, D, top - 0.05, top + 2.0, 0.45, "thatch", "thatch_dark", thick=0.3)
    # door, step, windows
    k.box("timber_dark", (0.6, -D / 2 - 0.02, plinth + 1.0), (1.1, 0.08, 2.05))
    k.box("timber", (0.6, -D / 2 - 0.06, plinth + 0.95), (0.9, 0.08, 1.9))
    k.box("iron", (0.95, -D / 2 - 0.11, plinth + 0.95), (0.08, 0.04, 0.08))
    k.box_on("stone", (0.6, -D / 2 - 0.45, 0), (1.3, 0.7, 0.25))
    window(k, (-1.0, -D / 2 - 0.02, plinth + 1.35), "y")
    window(k, (-1.0, D / 2 + 0.02, plinth + 1.35), "y")
    window(k, (W / 2 + 0.02, 0.0, plinth + 1.35), "x")
    # chimney on the back slope
    k.box_on("stone", (1.5, 0.9, plinth + wall - 0.2), (0.7, 0.7, 3.2))
    k.box_on("stone_dark", (1.5, 0.9, plinth + wall + 2.9), (0.85, 0.85, 0.15))


def cottage_b(k):
    """Longer stone cottage, gable to the front, slate roof and a woodshed lean-to."""
    W, D, wall = 4.4, 6.0, 2.5
    k.box_on("stone_light", (0, 0, 0), (W, D, wall))
    k.box_on("stone_dark", (0, 0, 0), (W + 0.12, D + 0.12, 0.35))
    for sx in (-1, 1):            # quoins
        for sy in (-1, 1):
            for i in range(4):
                k.box_on("stone", (sx * (W / 2 - 0.12), sy * (D / 2 - 0.12), 0.35 + i * 0.55),
                         (0.34 + 0.08 * (i % 2), 0.34 + 0.08 * ((i + 1) % 2), 0.3))
    with k.group(r=(0, 0, 90)):   # ridge along Y: build along X, turn a quarter
        for sx in (-1, 1):
            gable_wall(k, sx * D / 2, W, wall, wall + 2.2, "stone_light", thick=0.3)
        gable_roof(k, D, W, wall - 0.05, wall + 2.35, 0.4, "slate", "stone_dark", thick=0.2)
    # door in the front gable, timber lintel, step
    k.box("timber_dark", (0, -D / 2 - 0.02, 1.1), (1.2, 0.1, 2.2))
    k.box("timber", (0, -D / 2 - 0.06, 1.0), (0.95, 0.08, 2.0))
    k.box("timber_dark", (0, -D / 2 - 0.08, 2.3), (1.5, 0.14, 0.2))
    k.box_on("stone", (0, -D / 2 - 0.4, 0), (1.4, 0.6, 0.2))
    window(k, (0, -D / 2 - 0.02, 3.45), "y", w=0.6, h=0.5)
    for y in (-1.4, 1.4):
        window(k, (-W / 2 - 0.02, y, 1.5), "x")
        window(k, (W / 2 + 0.02, y, 1.5), "x")
    # woodshed lean-to on the +X side, with stacked logs
    for y in (-1.2, 1.2):
        k.box_on("timber_dark", (W / 2 + 1.3, y, 0), (0.15, 0.15, 1.6))
    k.hull("thatch_dark", [(x, y, z) for y in (-1.5, 1.5) for (x, z) in
                           ((W / 2, 2.3), (W / 2, 2.45), (W / 2 + 1.6, 1.55), (W / 2 + 1.6, 1.7))])
    for row in range(3):
        for i in range(4):
            k.cyl("bark", (W / 2 + 0.25 + i * 0.26 + (row % 2) * 0.13, -1.0, 0.13 + row * 0.24), 0.12, 2.0,
                  seg=6, r=(-90, 0, 0))
    k.box_on("stone_dark", (-0.9, 1.6, wall + 0.6), (0.6, 0.6, 2.0))


def market_stall(k):
    """Mara's stall: timber counter, sloping Vale-blue striped canopy, goods on the board."""
    k.box_on("timber", (0, 0.1, 0), (3.6, 0.9, 1.0))
    k.box_on("timber_light", (0, 0.05, 1.0), (3.8, 1.1, 0.08))
    for i in range(6):
        k.box("timber_dark", (-1.5 + i * 0.6, -0.37, 0.5), (0.06, 0.04, 0.9))
    for sx in (-1, 1):
        k.box_on("timber_dark", (sx * 1.85, -0.5, 0), (0.14, 0.14, 2.45))
        k.box_on("timber_dark", (sx * 1.85, 1.0, 0), (0.14, 0.14, 2.85))
    stripes = 6
    for i in range(stripes):
        x0 = -2.05 + i * (4.1 / stripes)
        x1 = x0 + 4.1 / stripes
        col = "vale_blue" if i % 2 == 0 else "cloth_cream"
        k.hull(col, [(x, y, z) for x in (x0, x1) for (y, z) in
                     ((1.15, 2.85), (1.15, 2.92), (-0.95, 2.42), (-0.95, 2.49))])
        k.hull(col, [(x0, -0.95, 2.42), (x1, -0.95, 2.42), ((x0 + x1) / 2, -0.97, 2.18),
                     (x0, -0.93, 2.45), (x1, -0.93, 2.45)])
    # goods: apples, loaves, jars, a small crate
    for i in range(5):
        k.ico("cloth_red", (-1.4 + i * 0.14, -0.05, 1.13), 0.06, subdiv=1)
    for i in range(3):
        k.limb("thatch", (-0.4 + i * 0.3, -0.1, 1.14), 0.07, (-0.4 + i * 0.3, 0.15, 1.14), 0.06, seg=6, flat=0.7)
    for i in range(3):
        k.cyl("glass", (0.6 + i * 0.2, 0.0, 1.08), 0.06, 0.18, seg=6)
        k.cyl("leather", (0.6 + i * 0.2, 0.0, 1.26), 0.04, 0.03, seg=6)
    k.box_on("timber_light", (1.4, 0.15, 1.08), (0.4, 0.35, 0.3))
    k.box_on("herb", (1.4, 0.15, 1.38), (0.34, 0.3, 0.06))


def well(k):
    """Stone well with a little thatched roof, crank and bucket."""
    n = 11
    for layer in range(3):
        for i in range(n):
            a = 2 * math.pi * (i + 0.5 * (layer % 2)) / n
            k.box_on("stone" if (i + layer) % 3 else "stone_light",
                     (0.85 * math.cos(a), 0.85 * math.sin(a), layer * 0.27),
                     (0.36, 0.5, 0.27), r=(0, 0, math.degrees(a)))
    k.cyl("water", (0, 0, 0.3), 0.72, 0.1, seg=10)
    for sx in (-1, 1):
        k.box_on("timber_dark", (sx * 1.0, 0, 0), (0.16, 0.16, 2.15))
    k.cyl("timber", (-1.1, 0, 1.6), 0.07, 2.2, seg=6, r=(0, 90, 0))
    k.box("timber_dark", (1.2, -0.12, 1.6), (0.06, 0.06, 0.35))
    k.box("rope", (0, 0, 1.25), (0.03, 0.03, 0.7))
    k.cyl("timber_light", (0, 0, 0.72), 0.13, 0.2, seg=7, top=0.16)
    k.cyl("iron", (0, 0, 0.88), 0.165, 0.03, seg=7)
    with k.group(t=(0, 0, 0)):
        gable_roof(k, 2.0, 1.0, 2.05, 2.45, 0.25, "thatch", "thatch_dark", thick=0.12)


# ----------------------------------------------------------------------------------------------
# Props


def fence_segment(k):
    """A 2.5 m run of split rails with the post at its -X end. Chain segments every 2.5 m along X
    and finish the run with a fence_post at the +X end."""
    # Blender +X: the export's half turn puts it at Unity -X, the end the docs and builders expect.
    k.box_on("timber_dark", (1.25, 0, 0), (0.16, 0.16, 1.1))
    k.box("timber", (0, 0, 0.5), (2.5, 0.08, 0.13), r=(0, 1.2, 0))
    k.box("timber_light", (0, 0, 0.92), (2.5, 0.09, 0.12), r=(0, -0.8, 0))


def fence_post(k):
    k.box_on("timber_dark", (0, 0, 0), (0.16, 0.16, 1.1))
    k.hull("timber", [(x, y, 1.1) for x in (-0.09, 0.09) for y in (-0.09, 0.09)] + [(0, 0, 1.2)])


def crate(k):
    s = 0.7
    k.box_on("timber_light", (0, 0, 0), (s - 0.04, s - 0.04, s - 0.04))
    h = s / 2
    e = 0.07
    for a in (-1, 1):
        for b in (-1, 1):
            k.box("timber", (a * (h - e / 2), b * (h - e / 2), h), (e, e, s))
            k.box("timber", (a * (h - e / 2), 0, h + b * (h - e / 2)), (e, s, e))
            k.box("timber", (0, a * (h - e / 2), h + b * (h - e / 2)), (s, e, e))
    k.box("timber", (0, -h + 0.01, h), (s * 1.25, 0.05, e), r=(0, 45, 0))
    k.box("timber", (h - 0.01, 0, h), (0.05, s * 1.25, e), r=(45, 0, 0))


def barrel(k):
    k.rings("timber", [(0, 0.34, 0.34, 0, 0), (0.5, 0.41, 0.41, 0, 0), (1.0, 0.34, 0.34, 0, 0)], seg=10)
    for z, r in ((0.12, 0.375), (0.36, 0.41), (0.64, 0.41), (0.88, 0.375)):
        k.cyl("iron", (0, 0, z - 0.03), r, 0.06, seg=10)
    k.cyl("timber_dark", (0, 0, 0.995), 0.29, 0.015, seg=10)


def sack(k):
    k.rings("burlap", [(0.0, 0.18, 0.14, 0, 0), (0.08, 0.26, 0.2, 0, 0), (0.3, 0.27, 0.21, 0, 0.01),
                       (0.46, 0.15, 0.12, 0, 0.02), (0.5, 0.07, 0.06, 0, 0.02)], seg=8)
    k.cyl("rope", (0, 0.02, 0.47), 0.075, 0.06, seg=6)
    k.rings("burlap", [(0.52, 0.06, 0.05, 0, 0.02), (0.6, 0.11, 0.09, 0.02, 0.03)], seg=6)


def signpost(k):
    k.box_on("timber", (0, 0, 0), (0.15, 0.15, 2.2))
    k.hull("timber_dark", [(x, y, 2.2) for x in (-0.09, 0.09) for y in (-0.09, 0.09)] + [(0, 0, 2.32)])
    for z, direction, tilt in ((1.85, 1, 0), (1.45, -1, -6)):
        with k.group(t=(0, -0.1, z), r=(0, tilt, 0)):
            x0, x1 = 0.05 * direction, 1.0 * direction
            k.hull("timber_light", [(x, y, zz) for y in (-0.03, 0.03) for (x, zz) in
                                    ((x0, -0.13), (x0, 0.13), (x1 - 0.18 * direction, 0.13),
                                     (x1, 0.0), (x1 - 0.18 * direction, -0.13))])
            k.box("timber_dark", ((x0 + x1) / 2 - 0.05 * direction, -0.035, 0), (0.55, 0.02, 0.04))
    for i in range(4):
        a = i * math.pi / 2 + 0.4
        k.ico("stone", (0.22 * math.cos(a), 0.22 * math.sin(a), 0.05), 0.12, subdiv=1, s=(1, 1, 0.6), jitter=0.2)


# ----------------------------------------------------------------------------------------------
# Wagons


def wheel(k, center, radius, colour="timber_dark", axis_r=(0, 90, 0)):
    with k.group(t=center, r=axis_r):
        k.cyl(colour, (0, 0, -0.045), radius, 0.09, seg=10)
        k.cyl("iron", (0, 0, -0.06), radius * 0.28, 0.12, seg=6)
        for i in range(3):
            k.box("timber", (0, 0, 0), (radius * 1.9, 0.05, 0.1), r=(0, 0, i * 60))


def wagon_bed(k, broken=False):
    k.box_on("timber", (0, 0, 0.7), (1.3, 2.5, 0.1))
    for sx in (-1, 1):
        if broken and sx < 0:
            k.box_on("timber_light", (sx * 0.62, 0.65, 0.8), (0.07, 1.2, 0.35))
        else:
            k.box_on("timber_light", (sx * 0.62, 0, 0.8), (0.07, 2.5, 0.4))
        for y in (-1.15, 0, 1.15):
            k.box_on("timber_dark", (sx * 0.68, y, 0.72), (0.06, 0.08, 0.52))
    k.box_on("timber_light", (0, 1.22, 0.8), (1.3, 0.07, 0.4))
    k.box_on("timber_light", (0, -1.22, 0.8), (1.3, 0.07, 0.3))
    for y in (-0.8, 0.8):
        k.box("iron", (0, y, 0.62), (1.6, 0.08, 0.08))


def wagon(k):
    """Iven's supply wagon, intact: canvas-covered load, shafts forward (-Y)."""
    wagon_bed(k)
    for sx in (-1, 1):
        wheel(k, (sx * 0.78, 0.8, 0.46), 0.46)
        wheel(k, (sx * 0.78, -0.8, 0.4), 0.4)
        k.box("timber_dark", (sx * 0.45, -2.1, 0.62), (0.07, 1.9, 0.08), r=(-6, 0, 0))
    k.box("timber_dark", (0, -2.9, 0.53), (0.97, 0.08, 0.08))
    k.hull("canvas", [(x, y, z) for y in (0.0, 1.15) for (x, z) in
                      ((-0.58, 0.8), (0.58, 0.8), (-0.5, 1.3), (0.5, 1.3), (-0.25, 1.55), (0.25, 1.55))])
    k.box("rope", (0, 0.55, 1.4), (1.2, 0.04, 0.04), r=(0, 0, 0))
    k.box_on("timber_light", (0, -0.6, 0.8), (0.5, 0.5, 0.45))
    k.rings("burlap", [(0.8, 0.18, 0.15, -0.35, -0.75), (1.05, 0.2, 0.16, -0.35, -0.75), (1.25, 0.08, 0.07, -0.35, -0.75)])


def wagon_wrecked(k):
    """The Missing Wagon at the Old Road: tipped onto its broken left side, two wheels off, load spilled."""
    with k.group(t=(0, 0, -0.18), r=(0, -16, 0)):
        wagon_bed(k, broken=True)
        for y, rad in ((0.8, 0.46), (-0.8, 0.4)):
            wheel(k, (0.78, y, rad), rad)
        k.box("timber_dark", (0.45, -2.0, 0.6), (0.07, 1.8, 0.08), r=(-8, 0, 0))
        k.box("timber_dark", (-0.45, -1.55, 0.55), (0.07, 0.9, 0.08), r=(-14, 0, 0))   # snapped shaft
        k.hull("canvas", [(x, y, z) for y in (0.3, 1.15) for (x, z) in
                          ((-0.58, 0.8), (0.58, 0.8), (-0.45, 1.15), (0.45, 1.15), (-0.1, 1.3))])
    wheel(k, (-1.35, 0.9, 0.05), 0.46, axis_r=(0, 0, 0))                          # lying flat
    wheel(k, (-1.0, -1.1, 0.36), 0.4, axis_r=(0, 70, 25))                          # leaning
    k.box_on("timber_light", (-0.95, 0.0, 0), (0.07, 1.2, 0.35), r=(80, 0, 12))    # fallen side board
    k.box_on("timber_light", (-1.5, -0.4, 0), (0.5, 0.5, 0.45), r=(0, 0, 25))     # spilled crate
    k.box_on("timber_light", (-2.0, 0.4, 0), (0.35, 0.35, 0.32), r=(8, 12, 50))
    k.rings("burlap", [(0.0, 0.25, 0.18, -1.7, 1.4), (0.18, 0.27, 0.2, -1.7, 1.4), (0.3, 0.12, 0.1, -1.75, 1.55)])
    for i in range(4):
        k.box_on("timber", (-0.6 - i * 0.35, -2.0 + (i % 2) * 0.3, 0), (0.5, 0.06, 0.04), r=(0, 0, 20 + i * 37))


# ----------------------------------------------------------------------------------------------
# Nature


def tree_pine(k):
    k.cyl("bark", (0, 0, 0), 0.2, 2.2, seg=6, top=0.12)
    for base, rad, h in ((1.2, 1.6, 2.6), (2.75, 1.25, 2.4), (4.3, 0.85, 2.7)):
        faces = k.cyl("pine", (0, 0, base), rad, h, seg=7, top=0.0)
        # One random drop per rim vertex, in a stable order, so a rebuild makes the same tree.
        rim = sorted({v for f in faces for v in f.verts if v.co.z < base + 0.01},
                     key=lambda v: (round(v.co.x, 5), round(v.co.y, 5)))
        for v in rim:
            v.co.z -= k.rng.uniform(0.0, 0.2)


def tree_broadleaf(k):
    k.limb("bark", (0, 0, 0), 0.28, (0.05, 0.05, 2.8), 0.17, seg=7)
    k.limb("bark", (0.05, 0, 2.2), 0.12, (0.9, 0.2, 3.4), 0.07, seg=5)
    k.limb("bark", (0, 0.05, 2.5), 0.1, (-0.7, -0.3, 3.6), 0.06, seg=5)
    for c, r, col in (((0.0, 0.0, 4.4), 1.65, "leaf"), ((1.0, 0.3, 3.8), 1.15, "leaf_dark"),
                      ((-0.9, -0.3, 4.0), 1.2, "leaf"), ((0.2, 0.5, 5.0), 1.0, "leaf_dark"),
                      ((-0.2, -0.6, 4.9), 0.9, "leaf")):
        k.ico(col, c, r, subdiv=1, jitter=0.18)


def rock(k, size, squash, colour, seed):
    k.rng.seed(seed)
    faces = k.ico(colour, (0, 0, 0), 1.0, subdiv=2, s=size, jitter=0.22)
    verts = {v for f in faces for v in f.verts}
    for v in verts:
        if v.co.z < -0.15 * size[2]:
            v.co.z = -0.15 * size[2]
    for f in faces:
        f.normal_update()
    k.recolour_up_faces(faces, "moss", min_dot=0.82)


def rock_a(k):
    rock(k, (1.3, 1.0, 1.05), 0.8, "stone", 3)


def rock_b(k):
    rock(k, (0.8, 0.65, 0.6), 0.6, "stone_light", 11)


def herb_plant(k):
    """Feverleaf: a low clump of broad leaves with a few purple flower heads (the herbs quest)."""
    for i in range(7):
        a = i * 2 * math.pi / 7
        with k.group(r=(0, 0, math.degrees(a))):
            k.hull("herb" if i % 2 else "grass", [(0, 0.0, 0.02), (0.06, 0.12, 0.12), (-0.06, 0.12, 0.12),
                                                  (0, 0.3, 0.2), (0, 0.15, 0.13)])
    for i, (x, y) in enumerate(((0.03, 0.02), (-0.06, 0.05), (0.04, -0.06))):
        k.limb("leaf_dark", (x, y, 0), 0.012, (x * 1.5, y * 1.5, 0.36 + i * 0.03), 0.01, seg=4)
        k.ico("flower_purple" if i != 1 else "flower_white", (x * 1.5, y * 1.5, 0.38 + i * 0.03), 0.04, subdiv=1)


# ----------------------------------------------------------------------------------------------
# Ashwatch


def merlons(k, x0, x1, y, z, colour, depth=0.5, w=0.6, gap=0.5, h=0.6):
    n = max(1, int((x1 - x0 + gap) // (w + gap)))
    span = n * w + (n - 1) * gap
    start = (x0 + x1) / 2 - span / 2 + w / 2
    for i in range(n):
        k.box_on(colour, (start + i * (w + gap), y, z), (w, depth, h))


def ashwatch_wall(k):
    """A 6 m curtain-wall module of Ashwatch: tile along X every 6 m; the gate is 9 m wide."""
    L, T, H = 6.0, 2.0, 4.4
    k.box_on("stone", (0, 0, 0), (L, T, H))
    k.box_on("stone_dark", (0, 0, 0), (L, T + 0.2, 0.6))
    k.box("stone_light", (0, -T / 2 - 0.03, 3.0), (L, 0.08, 0.12))
    for sy in (-1, 1):
        merlons(k, -L / 2, L / 2, sy * (T / 2 - 0.25), H, "stone_light")
    for x in (-1.8, 1.8):
        k.box("soot", (x, -T / 2 - 0.02, 2.4), (0.14, 0.06, 0.7))


def ashwatch_gate(k):
    """Ashwatch's gatehouse: two towers, an arched 3 m passage with open doors, banners."""
    T = 2.4
    for sx in (-1, 1):
        cx = sx * 3.0
        k.box_on("stone", (cx, 0, 0), (3.0, T + 0.4, 6.2))
        k.box_on("stone_dark", (cx, 0, 0), (3.2, T + 0.6, 0.7))
        for sy in (-1, 1):
            merlons(k, cx - 1.5, cx + 1.5, sy * (T / 2 - 0.05), 6.2, "stone_light", w=0.55, gap=0.4, h=0.8)
        for sxx in (-1, 1):
            k.box_on("stone_light", (cx + sxx * 1.25, 0, 6.2), (0.5, 0.5, 0.8))
        k.box("soot", (cx, -T / 2 - 0.22, 4.6), (0.16, 0.06, 0.8))
        # banner
        k.box("banner_red", (cx, -T / 2 - 0.25, 3.4), (1.0, 0.04, 1.8))
        k.hull("banner_red", [(cx - 0.5, -T / 2 - 0.25, 2.5), (cx + 0.5, -T / 2 - 0.25, 2.5), (cx, -T / 2 - 0.25, 2.1),
                              (cx - 0.5, -T / 2 - 0.29, 2.5), (cx + 0.5, -T / 2 - 0.29, 2.5), (cx, -T / 2 - 0.29, 2.1)])
        k.box("banner_gold", (cx, -T / 2 - 0.28, 3.4), (0.4, 0.04, 0.4), r=(0, 45, 0))
        k.box("timber_dark", (cx, -T / 2 - 0.3, 4.32), (1.2, 0.08, 0.08))
    # span over the passage: lintel block, arch stones, merlons
    k.box_on("stone", (0, 0, 4.1), (3.0, T, 1.6))
    for i in range(7):
        a = math.pi * (i + 0.5) / 7
        x = -1.5 * math.cos(a)
        z = 3.1 + 1.0 * math.sin(a)
        k.box("stone_light", (x, -T / 2 - 0.02, z), (0.5, 0.2, 0.36), r=(0, math.degrees(a) - 90, 0))
        k.box("stone", (x, 0, z + 0.25), (0.55, T, 0.9), r=(0, math.degrees(a) - 90, 0))
    for sy in (-1, 1):
        merlons(k, -1.5, 1.5, sy * (T / 2 - 0.25), 5.7, "stone_light")
    # doors swung open inward (+Y), hinged at the passage edges
    for sx in (-1, 1):
        with k.group(t=(sx * 1.45, T / 2, 0), r=(0, 0, sx * -88)):
            k.box_on("timber", (-sx * 0.72, 0.06, 0), (1.45, 0.12, 3.6))
            for z in (0.6, 1.8, 3.0):
                k.box("iron", (-sx * 0.72, 0.0, z), (1.45, 0.04, 0.1))


# ----------------------------------------------------------------------------------------------
# Broken Watch (the raiders' camp)


def ruin_wall(k):
    """Broken Watch: a ruined stretch of old watch wall, coursed blocks with a jagged top, fallen stones."""
    rng = k.rng
    rng.seed(5)
    L, bw, bh = 5.0, 0.62, 0.42
    heights = [6, 5, 6, 4, 3, 4, 2, 3, 1]
    cols = int(L / bw)
    for c in range(cols):
        top = heights[c % len(heights)]
        for row in range(top):
            off = (row % 2) * bw / 2
            x = -L / 2 + bw / 2 + c * bw + off
            if x > L / 2 - bw / 4:
                continue
            col = ("stone", "stone_light", "stone_dark")[rng.randrange(3)]
            k.box_on(col, (x, rng.uniform(-0.03, 0.03), row * bh), (bw - 0.03, 0.8, bh - 0.02),
                     r=(0, 0, rng.uniform(-2, 2)))
        if top >= 4:
            k.box_on("moss", (-L / 2 + bw / 2 + c * bw, 0, top * bh), (bw - 0.1, 0.7, 0.06))
    for i in range(5):
        side = -1 if i % 2 else 1
        k.box_on(("stone", "stone_light")[i % 2], (-1.5 + i * 0.9, side * (0.8 + rng.uniform(0, 0.4)), 0),
                 (bw - 0.05, 0.75, bh - 0.04), r=(rng.uniform(-14, 14), rng.uniform(-14, 14), rng.uniform(0, 60)))


def camp_tent(k):
    """A raider's A-frame tent of patched canvas, opening to the front (-Y)."""
    L, half, ridge = 2.4, 1.25, 1.75
    for sx in (-1, 1):
        k.hull("canvas", [(x, y, z) for y in (-L / 2, L / 2) for (x, z) in
                          ((sx * half, 0.0), (sx * (half - 0.06), 0.0), (sx * 0.0, ridge), (sx * 0.06, ridge - 0.02))])
    k.hull("canvas", [(x, L / 2 - 0.02, z) for (x, z) in ((-half, 0), (half, 0), (0, ridge))] +
           [(x, L / 2 - 0.06, z) for (x, z) in ((-half, 0), (half, 0), (0, ridge))])
    k.hull("cloth_dark", [(x, -L / 2 + 0.3, z) for (x, z) in ((-half + 0.4, 0), (half - 0.4, 0), (0, ridge - 0.45))] +
           [(x, -L / 2 + 0.34, z) for (x, z) in ((-half + 0.4, 0), (half - 0.4, 0), (0, ridge - 0.45))])
    k.box("cloth_brown", (0.72, -0.2, 0.75), (0.04, 0.6, 0.5), r=(0, -55, 0))
    k.box("cloth_brown", (-0.55, 0.5, 0.95), (0.04, 0.45, 0.4), r=(0, 55, 0))
    for y in (-L / 2 - 0.05, L / 2 + 0.05):
        k.box_on("timber_dark", (0, y, 0), (0.08, 0.08, ridge + 0.2))
    k.box("timber_dark", (0, 0, ridge + 0.03), (0.06, L + 0.3, 0.06))
    for y in (-L / 2 - 0.05, L / 2 + 0.05):
        sign = -1 if y < 0 else 1
        k.box("rope", (0, y + sign * 0.5, ridge / 2 + 0.1), (0.025, 0.025, 2.1), r=(sign * 30, 0, 0))
        k.box_on("timber_dark", (0, y + sign * 1.05, 0), (0.06, 0.06, 0.25))


def campfire(k):
    for i in range(9):
        a = 2 * math.pi * i / 9
        k.ico("stone_dark" if i % 2 else "stone", (0.48 * math.cos(a), 0.48 * math.sin(a), 0.06), 0.13,
              subdiv=1, s=(1.1, 0.9, 0.7), jitter=0.25)
    k.cyl("soot", (0, 0, 0), 0.4, 0.03, seg=9)
    for i in range(4):
        a = i * 90 + 20
        with k.group(r=(0, 0, a)):
            k.limb("timber_dark" if i % 2 else "bark", (0.36, 0, 0.04), 0.05, (0.02, 0, 0.42), 0.035, seg=5)
    k.cyl("ember", (0, 0, 0.02), 0.26, 0.38, seg=6, top=0.0)
    k.cyl("flame", (0.03, -0.02, 0.04), 0.15, 0.55, seg=5, top=0.0)
    k.cyl("flame", (-0.1, 0.07, 0.03), 0.09, 0.32, seg=5, top=0.0)


# ----------------------------------------------------------------------------------------------
# Characters

OUTFITS = {
    # tunic, sleeves, legs, boots, belt, hair, skin, accent kind
    "player":   dict(tunic="vale_blue", sleeves="cloth_cream", legs="cloth_brown", boots="leather",
                     belt="leather_dark", hair="hair_brown", skin="skin", accent="cloak", accent_col="cloth_grey"),
    "villager": dict(tunic="cloth_green", sleeves="cloth_cream", legs="cloth_brown", boots="leather_dark",
                     belt="leather", hair="hair_blond", skin="skin_light", accent="apron", accent_col="canvas"),
    "healer":   dict(tunic="cloth_teal", sleeves="cloth_teal", legs="cloth_teal", boots="leather",
                     belt="rope", hair="hair_grey", skin="skin_dark", accent="robe", accent_col="cloth_teal"),
    "raider":   dict(tunic="cloth_dark", sleeves="leather", legs="cloth_dark", boots="leather_dark",
                     belt="leather_dark", hair="hair_black", skin="skin_light", accent="hood", accent_col="cloth_red"),
    "guard":    dict(tunic="tabard", sleeves="steel", legs="cloth_grey", boots="leather_dark",
                     belt="leather_dark", hair="hair_brown", skin="skin", accent="helmet", accent_col="steel"),
}


def humanoid(k, o):
    """A 1.8 m low-poly person, arms relaxed at the sides (not a T-pose), facing -Y. No rig yet."""
    # legs and boots
    for sx in (-1, 1):
        x = sx * 0.1
        k.limb(o["legs"], (x, 0.0, 0.12), 0.065, (x * 1.05, 0.0, 0.92), 0.095, seg=6)
        k.limb(o["boots"], (x, 0.0, 0.0), 0.07, (x, 0.0, 0.3), 0.068, seg=6)
        k.hull(o["boots"], [(x + dx, y, z) for dx in (-0.06, 0.06) for (y, z) in
                            ((-0.17, 0.0), (-0.17, 0.07), (-0.05, 0.12), (0.07, 0.0), (0.07, 0.12))])
    # hips and torso
    k.rings(o["legs"], [(0.86, 0.17, 0.11, 0, 0), (1.0, 0.17, 0.11, 0, 0)], seg=8)
    k.rings(o["tunic"], [(0.74, 0.19, 0.13, 0, 0), (0.96, 0.17, 0.115, 0, 0), (1.25, 0.2, 0.13, 0, -0.005),
                         (1.42, 0.21, 0.11, 0, 0.01), (1.48, 0.12, 0.08, 0, 0.01)], seg=8)
    k.rings(o["belt"], [(0.93, 0.18, 0.125, 0, 0), (1.0, 0.18, 0.125, 0, 0)], seg=8)
    k.box(o["belt"] if o["belt"] != "rope" else "brass", (0, -0.13, 0.965), (0.06, 0.02, 0.05))
    # arms: shoulder -> elbow -> wrist, hanging a little away from the body
    for sx in (-1, 1):
        sh = (sx * 0.22, 0.0, 1.4)
        el = (sx * 0.27, 0.02, 1.12)
        wr = (sx * 0.29, -0.03, 0.86)
        k.ico(o["tunic"], sh, 0.075, subdiv=1)
        k.limb(o["tunic"], sh, 0.062, el, 0.052, seg=6)
        k.limb(o["sleeves"], el, 0.05, wr, 0.042, seg=6)
        k.hull(o["skin"], [(wr[0] + dx, wr[1] + dy, wr[2] + dz) for dx in (-0.03, 0.03) for dy in (-0.04, 0.04)
                           for dz in (-0.01, -0.12)])
    # neck, head, face
    k.limb(o["skin"], (0, 0.0, 1.44), 0.055, (0, 0.0, 1.56), 0.05, seg=6)
    k.sphere(o["skin"], (0, 0.0, 1.65), 0.115, seg=8, rings=6, s=(0.95, 1.0, 1.1))
    k.hull(o["skin"], [(-0.02, -0.1, 1.66), (0.02, -0.1, 1.66), (0, -0.14, 1.62), (-0.025, -0.1, 1.6), (0.025, -0.1, 1.6)])
    for sx in (-1, 1):
        k.box("eye", (sx * 0.042, -0.103, 1.675), (0.025, 0.02, 0.018))
        k.box(o["hair"], (sx * 0.042, -0.106, 1.705), (0.04, 0.02, 0.012))
    hair = []
    for i in range(10):
        a = 2 * math.pi * i / 10
        front = math.sin(a) < -0.5   # keep the fringe above the eyes
        for z, rr in ((1.735 if front else 1.69, 0.125), (1.76, 0.118), (1.79, 0.09), (1.81, 0.04)):
            hair.append((rr * math.cos(a), rr * math.sin(a), z))
        if math.sin(a) > -0.3:   # down the back and sides, never over the face
            hair.append((0.122 * math.cos(a), 0.125 * math.sin(a), 1.58))
    k.hull(o["hair"], hair)
    accent = o["accent"]
    ac = o["accent_col"]
    if accent == "cloak":
        k.hull(ac, [(x, y, z) for (x, y, z) in ((-0.2, 0.12, 1.42), (0.2, 0.12, 1.42), (-0.24, 0.17, 0.6),
                                                (0.24, 0.17, 0.6), (-0.2, 0.15, 1.42), (0.2, 0.15, 1.42),
                                                (-0.24, 0.2, 0.6), (0.24, 0.2, 0.6))])
        k.rings(ac, [(1.4, 0.2, 0.13, 0, 0.01), (1.48, 0.13, 0.1, 0, 0.01)], seg=8)
    elif accent == "apron":
        k.hull(ac, [(x, y, z) for (x, y) in ((-0.14, -0.13), (0.14, -0.13), (-0.16, -0.15), (0.16, -0.15))
                    for z in (0.5, 0.96)])
    elif accent == "robe":
        k.rings(ac, [(0.1, 0.23, 0.2, 0, 0.01), (0.75, 0.2, 0.14, 0, 0), (0.95, 0.17, 0.115, 0, 0)], seg=8)
        k.limb("leather", (-0.2, -0.05, 1.42), 0.015, (0.18, -0.12, 0.95), 0.015, seg=4)
        k.box("leather", (0.19, -0.03, 0.88), (0.12, 0.08, 0.16))
    elif accent == "hood":
        hood = []
        for i in range(10):
            a = 2 * math.pi * i / 10
            for z, rr in ((1.52, 0.17), (1.7, 0.15), (1.82, 0.1), (1.86, 0.03)):
                if not (math.sin(a) < -0.5 and z < 1.8):
                    hood.append((rr * math.cos(a), rr * math.sin(a) + 0.01, z))
        k.hull(o["tunic"], hood)
        k.rings(ac, [(1.5, 0.11, 0.11, 0, -0.02), (1.62, 0.1, 0.11, 0, -0.03)], seg=8)   # face scarf
        k.rings(o["tunic"], [(1.38, 0.24, 0.15, 0, 0.0), (1.5, 0.17, 0.12, 0, 0.0)], seg=8)
    elif accent == "helmet":
        k.rings(ac, [(1.68, 0.135, 0.135, 0, 0), (1.78, 0.12, 0.12, 0, 0), (1.85, 0.05, 0.05, 0, 0)], seg=8)
        k.rings(ac, [(1.67, 0.15, 0.15, 0, 0), (1.69, 0.15, 0.15, 0, 0)], seg=8)
        k.box(ac, (0, -0.12, 1.62), (0.02, 0.03, 0.1))
        for sx in (-1, 1):
            k.ico(ac, (sx * 0.23, 0.0, 1.43), 0.09, subdiv=1, s=(1.1, 1.0, 0.7))
        k.box("banner_gold", (0, -0.135, 1.22), (0.1, 0.02, 0.1), r=(0, 45, 0))


def make_humanoid(outfit):
    return lambda k: humanoid(k, OUTFITS[outfit])


def sword(k):
    """Arming sword, pivot at the grip's centre, blade along +Z (Unity +Y)."""
    k.cyl("leather", (0, 0, -0.08), 0.018, 0.16, seg=6)
    k.ico("brass", (0, 0, -0.105), 0.03, subdiv=1)
    k.box("iron", (0, 0, 0.095), (0.22, 0.035, 0.03))
    k.box("brass", (0, 0, 0.095), (0.05, 0.045, 0.04))
    k.hull("steel", [(x, y, z) for z in (0.11, 0.72) for (x, y) in ((-0.025, 0), (0.025, 0), (0, -0.007), (0, 0.007))]
           + [(0, 0, 0.84)])
    k.box("iron", (0, 0, 0.42), (0.008, 0.016, 0.6))


def wolf(k):
    """A grey wolf, ~0.95 m to the ear tips, head to -Y."""
    k.rings("wolf", [(0.6, 0.15, 0.13, 0, -0.32), (0.66, 0.13, 0.16, 0, 0.1), (0.62, 0.13, 0.12, 0, 0.42)], seg=8)
    k.hull("wolf", [(x, y, z) for x in (-0.13, 0.13) for (y, z) in ((-0.42, 0.78), (-0.38, 0.45), (0.45, 0.48),
                                                                     (0.45, 0.74), (-0.1, 0.8), (0.2, 0.78))])
    k.rings("wolf_light", [(0.42, 0.11, 0.1, 0, -0.38), (0.6, 0.14, 0.1, 0, -0.43), (0.75, 0.11, 0.08, 0, -0.4)], seg=6)
    # neck and head
    k.limb("wolf", (0, -0.36, 0.72), 0.12, (0, -0.55, 0.8), 0.09, seg=6)
    k.hull("wolf", [(x, y, z) for x in (-0.1, 0.1) for (y, z) in ((-0.5, 0.88), (-0.5, 0.7), (-0.66, 0.9), (-0.66, 0.7))])
    k.hull("wolf_light", [(-0.05, -0.65, 0.84), (0.05, -0.65, 0.84), (-0.06, -0.65, 0.7), (0.06, -0.65, 0.7),
                          (-0.03, -0.84, 0.77), (0.03, -0.84, 0.77), (-0.03, -0.83, 0.71), (0.03, -0.83, 0.71)])
    k.box("eye", (0, -0.855, 0.765), (0.04, 0.03, 0.03))
    for sx in (-1, 1):
        k.hull("wolf_dark", [(sx * 0.04, -0.54, 0.88), (sx * 0.1, -0.54, 0.88), (sx * 0.07, -0.5, 0.88),
                             (sx * 0.075, -0.55, 0.99)])
        k.box("eye", (sx * 0.06, -0.665, 0.83), (0.025, 0.02, 0.02))
        # legs
        k.limb("wolf", (sx * 0.09, -0.32, 0.55), 0.06, (sx * 0.09, -0.36, 0.06), 0.035, seg=5)
        k.limb("wolf", (sx * 0.1, 0.4, 0.58), 0.08, (sx * 0.1, 0.52, 0.28), 0.045, seg=5)
        k.limb("wolf", (sx * 0.1, 0.52, 0.28), 0.04, (sx * 0.1, 0.45, 0.05), 0.033, seg=5)
        for y in (-0.38, 0.43):
            k.box_on("wolf_dark", (sx * 0.09, y - 0.02, 0), (0.07, 0.1, 0.05))
    k.limb("wolf_dark", (0, 0.45, 0.7), 0.06, (0, 0.78, 0.4), 0.035, seg=5)


# ----------------------------------------------------------------------------------------------
# (id, builder, pivot at base?)

ASSETS = [
    ("cottage_a", cottage_a, True), ("cottage_b", cottage_b, True), ("market_stall", market_stall, True),
    ("well", well, True), ("fence_segment", fence_segment, True), ("fence_post", fence_post, True),
    ("crate", crate, True), ("barrel", barrel, True), ("sack", sack, True), ("signpost", signpost, True),
    ("wagon", wagon, True), ("wagon_wrecked", wagon_wrecked, True),
    ("tree_pine", tree_pine, True), ("tree_broadleaf", tree_broadleaf, True),
    ("rock_a", rock_a, True), ("rock_b", rock_b, True), ("herb_plant", herb_plant, True),
    ("ashwatch_gate", ashwatch_gate, True), ("ashwatch_wall", ashwatch_wall, True),
    ("ruin_wall", ruin_wall, True), ("camp_tent", camp_tent, True), ("campfire", campfire, True),
    ("humanoid_player", make_humanoid("player"), True), ("humanoid_villager", make_humanoid("villager"), True),
    ("humanoid_healer", make_humanoid("healer"), True), ("humanoid_raider", make_humanoid("raider"), True),
    ("humanoid_guard", make_humanoid("guard"), True),
    ("sword", sword, False), ("wolf", wolf, True),
]

ROWS = [
    ("buildings", ["cottage_a", "cottage_b", "market_stall", "well"]),
    ("props", ["fence_segment", "fence_post", "crate", "barrel", "sack", "signpost", "wagon", "wagon_wrecked"]),
    ("nature", ["tree_pine", "tree_broadleaf", "rock_a", "rock_b", "herb_plant"]),
    ("fortifications", ["ashwatch_gate", "ashwatch_wall", "ruin_wall", "camp_tent", "campfire"]),
    ("characters", ["humanoid_player", "humanoid_villager", "humanoid_healer", "humanoid_raider",
                    "humanoid_guard", "wolf", "sword"]),
]
