"""The low-poly kit's tools: one palette, mesh parts that UV onto it, and FBX export for Unity.

Every face of every model is mapped to the centre of one palette swatch, so the whole village
shares one texture and one material. Models are authored in metres, Blender Z up, with the
front facing Blender -Y and the origin on the ground at the model's centre.
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

# 16 x 16 swatches. Order matters only for the texture layout; names are what models use.
PALETTE = [
    # nature
    ("grass", "6FA44A"), ("grass_light", "8CC15A"), ("grass_dark", "4E7F3A"), ("leaf", "4F8A3C"),
    ("leaf_light", "74A94A"), ("leaf_dark", "33652F"), ("pine", "2F5E3A"), ("pine_dark", "234A30"),
    ("moss", "6B8E3E"), ("reed", "9AA858"), ("bush", "3F7A3A"), ("flower_pink", "E78FB3"),
    ("flower_yellow", "F2D45C"), ("flower_blue", "7FA8E8"), ("petal", "D9F4FF"), ("herb_stem", "3C8A5A"),
    # earth and stone
    ("dirt", "9C7A52"), ("dirt_dark", "7A5C3C"), ("path", "C2A57A"), ("sand", "D9C392"),
    ("stone", "A6A39B"), ("stone_light", "C7C3B8"), ("stone_dark", "77746E"), ("stone_darker", "57554F"),
    ("rock", "8C8A86"), ("rock_dark", "66645F"), ("cobble", "9A948A"), ("slate", "5E6670"),
    ("clay", "B86F4C"), ("brick", "A0533E"), ("mud", "5C4632"), ("gravel", "8F8577"),
    # wood and building
    ("wood", "9B6B43"), ("wood_light", "B98756"), ("wood_dark", "6E4A2F"), ("wood_darker", "4A3220"),
    ("plank", "C49A68"), ("bark", "6A4B33"), ("thatch", "D1A85A"), ("thatch_dark", "A9823E"),
    ("straw", "E2C273"), ("rope", "C9A66B"), ("plaster", "EFE6D2"), ("plaster_shade", "D8CDB4"),
    ("timber", "5A3B24"), ("roof_red", "B2473B"), ("roof_blue", "4A6A9A"), ("roof_green", "5E8A4E"),
    # cloth and metal
    ("cloth_red", "C0392B"), ("cloth_blue", "2E6DB4"), ("cloth_green", "3E8E4F"), ("cloth_yellow", "E0B43A"),
    ("cloth_white", "F3EFE6"), ("cloth_purple", "7A4E9C"), ("cloth_brown", "8A5E3C"), ("cloth_grey", "8C8F94"),
    ("cloth_dark", "3A3D45"), ("cloth_teal", "2F8C8C"), ("cloth_orange", "D9772B"), ("gold", "D8A93B"),
    ("silver", "C4CCD6"), ("iron", "6E737A"), ("iron_dark", "44484E"), ("leather", "7B5434"),
    # people and creatures
    ("skin", "F1C7A1"), ("skin_tan", "D9A27A"), ("skin_dark", "9E6B4A"), ("hair_brown", "6B4428"),
    ("hair_black", "2A2522"), ("hair_blond", "D8B25E"), ("hair_red", "A9482C"), ("hair_grey", "A9A9A9"),
    ("fur", "8E8D8A"), ("fur_dark", "5D5C59"), ("fur_light", "BDBAB3"), ("eye", "1E1E24"),
    ("mouth", "8E3B3B"), ("white", "FFFFFF"), ("black", "18181B"), ("bread", "C98A42"),
    # water, light and goods
    ("water", "3C7EA6"), ("water_deep", "2B5C7F"), ("foam", "E8F4F7"), ("glass", "9FD3E6"),
    ("window_glow", "FFC86B"), ("fire", "FF8A2A"), ("ember", "FF4E1A"), ("lantern_glow", "FFD27A"),
    ("herb_glow", "9FF3FF"), ("bell", "C9A23E"), ("apple", "C9302C"), ("cheese", "F1C14E"),
    ("fish", "8FA8B8"), ("pot", "B0643E"), ("banner_blue", "2C4F8F"), ("banner_red", "9E2B2B"),
]
# Swatches that glow: written to the emission texture, black everywhere else.
EMISSIVE = {"window_glow", "fire", "ember", "lantern_glow", "herb_glow"}

GRID = 16
SWATCH = 16
INDEX = {name: i for i, (name, _) in enumerate(PALETTE)}
assert len(INDEX) == len(PALETTE) <= GRID * GRID, "duplicate or too many palette names"


def uv_of(color):
    i = INDEX[color]
    col, row = i % GRID, i // GRID
    return ((col + 0.5) / GRID, 1.0 - (row + 0.5) / GRID)


def write_palettes(folder):
    """Writes Palette.png (albedo) and PaletteEmission.png (glow) into folder."""
    size = GRID * SWATCH
    for name, glow_only in (("Palette", False), ("PaletteEmission", True)):
        image = bpy.data.images.new(name, size, size, alpha=False)
        pixels = [0.0] * (size * size * 4)
        for i, (color, hexcode) in enumerate(PALETTE):
            rgb = [int(hexcode[k:k + 2], 16) / 255.0 for k in (0, 2, 4)]
            if glow_only and color not in EMISSIVE:
                rgb = [0.0, 0.0, 0.0]
            col, row = i % GRID, i // GRID
            for y in range(SWATCH):
                # Blender's pixel rows run bottom to top.
                py = size - 1 - (row * SWATCH + y)
                for x in range(SWATCH):
                    p = (py * size + col * SWATCH + x) * 4
                    pixels[p:p + 4] = [rgb[0], rgb[1], rgb[2], 1.0]
        image.pixels.foreach_set(pixels)
        image.filepath_raw = os.path.join(folder, name + ".png")
        image.file_format = "PNG"
        image.save()
        bpy.data.images.remove(image)


# ---- Parts --------------------------------------------------------------------------------------

def _matrix(at=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    m = Matrix.Translation(Vector(at))
    m = m @ Matrix.Rotation(math.radians(rot[2]), 4, "Z")
    m = m @ Matrix.Rotation(math.radians(rot[1]), 4, "Y")
    m = m @ Matrix.Rotation(math.radians(rot[0]), 4, "X")
    m = m @ Matrix.Diagonal(Vector((scale[0], scale[1], scale[2], 1.0)))
    return m


class Part:
    """A mesh being built from primitives, every face coloured by one palette swatch."""

    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        # Recalculating normals suits closed shapes; a sheet of loose triangles (the terrain) is
        # already wound upward, and recalculating could flip some of them.
        self.recalc_normals = True

    def _absorb(self, src, color, matrix, bevel=0.0):
        uv_src = src.loops.layers.uv.verify()
        if bevel > 0:
            bmesh.ops.bevel(src, geom=list(src.verts) + list(src.edges), offset=bevel,
                            offset_type="OFFSET", segments=1, profile=0.5, affect="EDGES",
                            clamp_overlap=True)
        bmesh.ops.transform(src, matrix=matrix, verts=src.verts)
        u, v = uv_of(color)
        vmap = {}
        for vert in src.verts:
            vmap[vert] = self.bm.verts.new(vert.co)
        for face in src.faces:
            try:
                nf = self.bm.faces.new([vmap[x] for x in face.verts])
            except ValueError:
                continue
            nf.smooth = False
            for loop in nf.loops:
                loop[self.uv].uv = (u, v)
        src.free()
        return self

    def box(self, size, color, at=(0, 0, 0), rot=(0, 0, 0), bevel=0.03, taper=1.0):
        """A box size=(x, y, z) whose base sits at at.z. taper narrows the top (1 = straight)."""
        src = bmesh.new()
        bmesh.ops.create_cube(src, size=1.0)
        for vert in src.verts:
            if vert.co.z > 0 and taper != 1.0:
                vert.co.x *= taper
                vert.co.y *= taper
            vert.co.z += 0.5
        m = _matrix(at, rot, size)
        b = min(bevel, 0.45 * min(size)) if bevel else 0.0
        # Bevel in model space, so the bevel width is in metres, not in unit-cube space.
        bmesh.ops.transform(src, matrix=Matrix.Diagonal(Vector((size[0], size[1], size[2], 1.0))), verts=src.verts)
        if b > 0:
            bmesh.ops.bevel(src, geom=list(src.verts) + list(src.edges), offset=b, offset_type="OFFSET",
                            segments=1, profile=0.5, affect="EDGES", clamp_overlap=True)
        return self._absorb(src, color, _matrix(at, rot))

    def cyl(self, radius, height, color, at=(0, 0, 0), rot=(0, 0, 0), segments=8, radius_top=None,
            bevel=0.0, scale=(1, 1, 1)):
        """A cylinder or cone standing on at.z along its local Z."""
        src = bmesh.new()
        r2 = radius if radius_top is None else radius_top
        bmesh.ops.create_cone(src, cap_ends=True, cap_tris=False, segments=segments, radius1=radius,
                              radius2=max(r2, 0.0001), depth=height)
        for vert in src.verts:
            vert.co.z += height / 2
        if r2 <= 0.0001:
            bmesh.ops.remove_doubles(src, verts=src.verts, dist=0.0005)
        if bevel > 0:
            bmesh.ops.bevel(src, geom=list(src.verts) + list(src.edges), offset=bevel, offset_type="OFFSET",
                            segments=1, profile=0.5, affect="EDGES", clamp_overlap=True)
        return self._absorb(src, color, _matrix(at, rot, scale))

    def ball(self, radius, color, at=(0, 0, 0), scale=(1, 1, 1), subdiv=1, jitter=0.0, seed=0):
        src = bmesh.new()
        bmesh.ops.create_icosphere(src, subdivisions=subdiv, radius=radius)
        if jitter:
            rng = random.Random(seed)
            for vert in src.verts:
                vert.co *= 1.0 + rng.uniform(-jitter, jitter)
        return self._absorb(src, color, _matrix(at, (0, 0, 0), scale))

    def prism(self, width, depth, height, color, at=(0, 0, 0), rot=(0, 0, 0), overhang=0.0, thickness=0.12):
        """A gable roof: ridge along local X, eaves along Y, base at at.z."""
        w, d, h = width / 2 + overhang, depth / 2 + overhang, height
        t = thickness
        src = bmesh.new()
        # Two sloped slabs with thickness, so the roof reads from below too.
        pts = [(-w, -d, 0), (w, -d, 0), (w, 0, h), (-w, 0, h),
               (-w, -d, -t), (w, -d, -t), (w, 0, h - t), (-w, 0, h - t)]
        for sign in (1, -1):
            vs = [src.verts.new((x, y * sign, z)) for (x, y, z) in pts]
            faces = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (3, 2, 6, 7), (0, 3, 7, 4)]
            for f in faces:
                idx = f if sign == 1 else tuple(reversed(f))
                src.faces.new([vs[i] for i in idx])
        return self._absorb(src, color, _matrix(at, rot))

    def gable_wall(self, width, height, color, at=(0, 0, 0), rot=(0, 0, 0), thickness=0.2):
        """The triangle that closes a gable roof's end, standing on at.z, in the local XZ plane."""
        w, h, t = width / 2, height, thickness / 2
        src = bmesh.new()
        front = [src.verts.new(p) for p in [(-w, -t, 0), (w, -t, 0), (0, -t, h)]]
        back = [src.verts.new(p) for p in [(-w, t, 0), (w, t, 0), (0, t, h)]]
        src.faces.new(front)
        src.faces.new(list(reversed(back)))
        for a, b in ((0, 1), (1, 2), (2, 0)):
            src.faces.new([front[b], front[a], back[a], back[b]])
        return self._absorb(src, color, _matrix(at, rot))

    def poly(self, verts, faces, color, at=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
        # Each face gets its own vertices: flat shading needs no sharing, and a double-sided face
        # (front and back over the same corners) would otherwise be refused as a duplicate.
        src = bmesh.new()
        for f in faces:
            src.faces.new([src.verts.new(verts[i]) for i in f])
        return self._absorb(src, color, _matrix(at, rot, scale))

    def merge(self, other, at=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
        """Copies another part in, transformed. The other part is left as it was."""
        src = other.bm.copy()
        bmesh.ops.transform(src, matrix=_matrix(at, rot, scale), verts=src.verts)
        uv_src = src.loops.layers.uv.active
        vmap = {v: self.bm.verts.new(v.co) for v in src.verts}
        for face in src.faces:
            try:
                nf = self.bm.faces.new([vmap[x] for x in face.verts])
            except ValueError:
                continue
            nf.smooth = False
            for new, old in zip(nf.loops, face.loops):
                new[self.uv].uv = old[uv_src].uv
        src.free()
        return self

    def to_object(self, name, collection):
        mesh = bpy.data.meshes.new(name)
        if self.recalc_normals:
            bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.to_mesh(mesh)
        for poly in mesh.polygons:
            poly.use_smooth = False
        obj = bpy.data.objects.new(name, mesh)
        collection.objects.link(obj)
        return obj


# ---- Export -------------------------------------------------------------------------------------

def clear_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)


def export(path, objects):
    """Exports the given objects (and their children) as one FBX, Unity's axes and metres."""
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
        for child in obj.children_recursive:
            child.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH", "EMPTY"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
        axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", use_mesh_modifiers=True,
        add_leaf_bones=False, bake_anim=False, use_custom_props=False, path_mode="STRIP",
        use_tspace=False)


def single(name, part, folder):
    """Exports one part as <folder>/<name>.fbx with a single mesh object."""
    clear_scene()
    obj = part.to_object(name, bpy.context.scene.collection)
    export(os.path.join(folder, name + ".fbx"), [obj])


def rig(name, parts, folder):
    """Exports a figure as separate part objects. parts: list of (part_name, Part, parent, pivot).

    Each part's mesh is authored around its own pivot (a joint) and the object is placed at that
    pivot. The FBX holds no parenting: Blender's axis conversion bakes parented transforms wrongly,
    so every part is a top-level object, and the game's code re-parents them by name on import
    (the parent column says how) and swings the limbs about their joints.
    """
    clear_scene()
    collection = bpy.context.scene.collection
    made = []
    for part_name, part, parent, pivot in parts:
        obj = part.to_object(part_name, collection)
        obj.location = Vector(pivot)
        made.append(obj)
    export(os.path.join(folder, name + ".fbx"), made)
