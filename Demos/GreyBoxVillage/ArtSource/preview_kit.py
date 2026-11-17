"""Renders contact sheets of the kit, so a person (or an agent) can look at every model at once.

    blender --background --factory-startup --python Demos/GreyBoxVillage/ArtSource/preview_kit.py -- <out folder> [name filter ...]

Writes sheet_<group>.png into the out folder: the models laid out in a grid with the palette on,
seen from the front and above, as the village camera sees them.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, "..", "Assets", "GreyBoxVillage", "Art"))
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.abspath(args[0]) if args else os.path.join(HERE, "previews")
FILTERS = args[1:]
os.makedirs(OUT, exist_ok=True)

GROUPS = {
    "buildings": ["gate_house", "wall_stone", "wall_palisade", "well", "stall_bread", "stall_goods", "stall_fruit",
                  "shelter", "bridge", "docks", "north_tower", "armory", "keep", "cave", "cottage_a", "cottage_b",
                  "cottage_c", "cottage_d", "cottage_e", "cottage_f"],
    "props": ["campfire", "seat_log", "guard_tent", "bell_frame", "bell", "training_dummy", "rowboat", "signpost",
              "cart", "weapon_rack", "barrel", "crate", "sack", "hay_bale", "woodpile", "lamp_post", "fence", "bench",
              "banner_blue", "banner_red", "pine_0", "pine_1", "pine_2", "oak_0", "oak_1", "bush_0", "bush_1",
              "rock_0", "rock_1", "rock_2", "grass_0", "grass_1", "flowers_0", "flowers_1", "reeds", "stump"],
    "people": ["char_bram", "char_wren", "char_hale", "char_odile", "char_player", "char_thief", "char_villager_tomas",
               "char_villager_ada", "char_villager_pell", "char_villager_mira", "wolf"],
    "items": ["item_bread", "item_rope", "item_torch", "item_lantern", "item_moonpetal", "item_pelt", "item_coins",
              "item_apple"],
    "terrain": ["terrain", "water"],
}


def material():
    mat = bpy.data.materials.new("Palette")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.join(ART, "Palette.png"))
    tex.interpolation = "Closest"
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    emit = nodes.new("ShaderNodeTexImage")
    emit.image = bpy.data.images.load(os.path.join(ART, "PaletteEmission.png"))
    emit.interpolation = "Closest"
    mat.node_tree.links.new(emit.outputs["Color"], bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 1.5
    bsdf.inputs["Roughness"].default_value = 0.9
    return mat


def render(group, names):
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    mat = material()
    placed = []
    for name in names:
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(ART, "Models", name + ".fbx"))
        new = [o for o in bpy.data.objects if o not in before]
        for o in new:
            if o.type == "MESH":
                o.data.materials.clear()
                o.data.materials.append(mat)
        roots = [o for o in new if o.parent is None]
        placed.append((name, roots, new))
    bpy.context.view_layer.update()

    def bounds(objs):
        lo = Vector((1e9, 1e9, 1e9))
        hi = Vector((-1e9, -1e9, -1e9))
        for o in objs:
            if o.type != "MESH":
                continue
            for c in o.bound_box:
                w = o.matrix_world @ Vector(c)
                lo = Vector(map(min, lo, w))
                hi = Vector(map(max, hi, w))
        return lo, hi

    cols = max(1, math.ceil(math.sqrt(len(placed))))
    cell = max(max((bounds(n)[1] - bounds(n)[0]).length for _, _, n in placed), 1.0) * 0.8
    for i, (name, roots, objs) in enumerate(placed):
        lo, hi = bounds(objs)
        centre = (lo + hi) / 2
        gx, gy = i % cols, i // cols
        for r in roots:
            r.location += Vector((gx * cell - centre.x, -gy * cell - centre.y, -lo.z))
    bpy.context.view_layer.update()
    lo, hi = bounds([o for _, _, objs in placed for o in objs])
    centre = (lo + hi) / 2
    size = max(hi.x - lo.x, hi.y - lo.y) * 1.1

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 1200
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.65, 0.75, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.8
    scene.world = world
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = size
    cam.rotation_euler = (math.radians(58), 0, math.radians(-25))
    direction = cam.rotation_euler.to_matrix() @ Vector((0, 0, 1))
    cam.location = centre + direction * 200
    cam.data.clip_end = 1000
    scene.camera = cam
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 3.5
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(-40))
    scene.collection.objects.link(sun)
    scene.render.filepath = os.path.join(OUT, "sheet_%s.png" % group)
    bpy.ops.render.render(write_still=True)
    print("SHEET", scene.render.filepath)


for group, names in GROUPS.items():
    if FILTERS and group not in FILTERS:
        continue
    render(group, names)
