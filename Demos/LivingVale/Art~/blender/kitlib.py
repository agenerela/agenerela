"""Small modelling toolkit for Living Vale's kit: primitives into one bmesh, one palette colour per face.

Blender convention used throughout: metres, Z up, the model's front faces -Y, pivot at the
base centre. The FBX export settings in build_kit.py turn that into Unity's Y up with the front
facing +Z, so a model placed with no rotation looks down the scene's +Z axis.

Colours are palette names (palette.py). A face keeps its colour in `material_index` while the
model is built; `Kit.finish` turns it into UVs on the colour's cell and resets the index, so the
exported mesh has one material.
"""
import math
import random
from contextlib import contextmanager

import bmesh
from mathutils import Euler, Matrix, Vector

import palette


def trs(t=(0, 0, 0), r=(0, 0, 0), s=(1, 1, 1)):
    """Translation, Euler rotation in degrees (XYZ), scale -> 4x4 matrix."""
    rot = Euler([math.radians(a) for a in r], "XYZ").to_matrix().to_4x4()
    scl = Matrix.Diagonal((s[0], s[1], s[2], 1.0))
    return Matrix.Translation(Vector(t)) @ rot @ scl


class Kit:
    def __init__(self, name, seed=1):
        self.name = name
        self.bm = bmesh.new()
        self.stack = [Matrix.Identity(4)]
        self.rng = random.Random(seed)

    # --- groups -------------------------------------------------------------------
    @contextmanager
    def group(self, t=(0, 0, 0), r=(0, 0, 0), s=(1, 1, 1)):
        self.stack.append(self.stack[-1] @ trs(t, r, s))
        try:
            yield
        finally:
            self.stack.pop()

    # --- internals ----------------------------------------------------------------
    def _place(self, verts, local, colour):
        m = self.stack[-1] @ local
        bmesh.ops.transform(self.bm, matrix=m, verts=verts)
        faces = list({f for v in verts for f in v.link_faces})
        if m.to_3x3().determinant() < 0:
            bmesh.ops.reverse_faces(self.bm, faces=faces)
        idx = palette.INDEX[colour]
        for f in faces:
            f.material_index = idx
            f.smooth = False
        return faces

    # --- primitives ---------------------------------------------------------------
    def box(self, colour, center, size, r=(0, 0, 0)):
        """Axis-aligned box of `size`, centred on `center`, rotated by `r` degrees."""
        res = bmesh.ops.create_cube(self.bm, size=1.0)
        return self._place(res["verts"], trs(center, r, size), colour)

    def box_on(self, colour, base, size, r=(0, 0, 0)):
        """Box standing on `base` (its bottom-centre)."""
        x, y, z = base
        return self.box(colour, (x, y, z + size[2] / 2), size, r)

    def cyl(self, colour, base, radius, height, seg=8, top=None, r=(0, 0, 0), s=(1, 1, 1)):
        """Cylinder or cone along local Z from `base`; `top` is the top radius (0 = cone)."""
        top = radius if top is None else top
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=seg,
                                    radius1=radius, radius2=top, depth=height)
        bmesh.ops.translate(self.bm, vec=(0, 0, height / 2), verts=res["verts"])
        return self._place(res["verts"], trs(base, r, s), colour)

    def ico(self, colour, center, radius, subdiv=1, s=(1, 1, 1), jitter=0.0, r=(0, 0, 0)):
        res = bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=radius)
        for v in res["verts"]:
            if jitter:
                v.co += Vector([self.rng.uniform(-jitter, jitter) for _ in range(3)]) * radius
        return self._place(res["verts"], trs(center, r, s), colour)

    def sphere(self, colour, center, radius, seg=8, rings=6, s=(1, 1, 1)):
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=seg, v_segments=rings, radius=radius)
        return self._place(res["verts"], trs(center, (0, 0, 0), s), colour)

    def hull(self, colour, points):
        """Convex hull of points (already in the current group's local space)."""
        verts = [self.bm.verts.new(Vector(p)) for p in points]
        res = bmesh.ops.convex_hull(self.bm, input=verts, use_existing_faces=False)
        unused = list({v for v in res["geom_interior"] + res["geom_unused"] if isinstance(v, bmesh.types.BMVert)})
        if unused:
            bmesh.ops.delete(self.bm, geom=unused, context="VERTS")
        verts = [v for v in verts if v.is_valid]
        faces = list({f for v in verts for f in v.link_faces})
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        return self._place(verts, Matrix.Identity(4), colour)

    def limb(self, colour, p0, r0, p1, r1, seg=6, flat=1.0):
        """Tapered prism from p0 (radius r0) to p1 (radius r1); `flat` squashes the cross-section."""
        p0, p1 = Vector(p0), Vector(p1)
        axis = (p1 - p0).normalized()
        helper = Vector((0, 0, 1)) if abs(axis.z) < 0.9 else Vector((1, 0, 0))
        u = axis.cross(helper).normalized()
        w = axis.cross(u).normalized()
        pts = []
        for p, rad in ((p0, r0), (p1, r1)):
            for i in range(seg):
                a = 2 * math.pi * (i + 0.5) / seg
                pts.append(p + (u * math.cos(a) + w * math.sin(a) * flat) * rad)
        return self.hull(colour, pts)

    def rings(self, colour, rings, seg=8):
        """Hull of horizontal elliptic rings: [(z, rx, ry, cx, cy), ...]."""
        pts = []
        for z, rx, ry, cx, cy in rings:
            for i in range(seg):
                a = 2 * math.pi * (i + 0.5) / seg
                pts.append((cx + rx * math.cos(a), cy + ry * math.sin(a), z))
        return self.hull(colour, pts)

    def slab(self, colour, x0, x1, profile):
        """Prism along X from x0 to x1 over a convex (y, z) profile."""
        return self.hull(colour, [(x, y, z) for x in (x0, x1) for (y, z) in profile])

    # --- colour by orientation ----------------------------------------------------
    def recolour_up_faces(self, faces, colour, min_dot=0.7):
        idx = palette.INDEX[colour]
        for f in faces:
            if f.is_valid and f.normal.z >= min_dot:
                f.material_index = idx

    def floor(self):
        """Flatten everything below z = 0 onto it, so finish() does not lift the model to its lowest part."""
        for v in self.bm.verts:
            if v.co.z < 0:
                v.co.z = 0.0

    # --- finish -------------------------------------------------------------------
    def finish(self, base_to_zero=True):
        """Write UVs from colours, set every face to material 0 and return stats."""
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        if base_to_zero:
            min_z = min(v.co.z for v in bm.verts)
            bmesh.ops.translate(bm, vec=(0, 0, -min_z), verts=bm.verts)
        uv = bm.loops.layers.uv.verify()
        for f in bm.faces:
            u, v = palette.uv(palette.COLOURS[f.material_index][0])
            for loop in f.loops:
                loop[uv].uv = (u, v)
            f.material_index = 0
        bm.normal_update()
        tris = sum(len(f.verts) - 2 for f in bm.faces)
        xs = [v.co.x for v in bm.verts]
        ys = [v.co.y for v in bm.verts]
        zs = [v.co.z for v in bm.verts]
        return {"triangles": tris, "vertices": len(bm.verts),
                "min": [min(xs), min(ys), min(zs)], "max": [max(xs), max(ys), max(zs)]}
