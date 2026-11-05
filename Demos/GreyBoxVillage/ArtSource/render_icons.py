"""Renders the inventory icons and the dialogue portraits from the kit, so the UI matches the world.

    blender --background --factory-startup --python Demos/GreyBoxVillage/ArtSource/render_icons.py

Writes Assets/GreyBoxVillage/Resources/Icons/<item>.png (128 px, transparent) and
Resources/Portraits/<character>.png (128 px, head and shoulders on a soft backdrop).
Run build_kit.py first.
"""
import math
import os

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.normpath(os.path.join(HERE, "..", "Assets", "GreyBoxVillage"))
ART = os.path.join(ASSETS, "Art")
ICONS = os.path.join(ASSETS, "Resources", "Icons")
PORTRAITS = os.path.join(ASSETS, "Resources", "Portraits")
os.makedirs(ICONS, exist_ok=True)
os.makedirs(PORTRAITS, exist_ok=True)

ITEMS = ["bread", "rope", "torch", "lantern", "moonpetal", "pelt", "coins", "apple"]
PEOPLE = ["bram", "wren", "hale", "odile", "player", "thief"]


def material():
    mat = bpy.data.materials.new("Palette")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes.get("Principled BSDF")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.join(ART, "Palette.png"), check_existing=True)
    tex.interpolation = "Closest"
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    emit = nodes.new("ShaderNodeTexImage")
    emit.image = bpy.data.images.load(os.path.join(ART, "PaletteEmission.png"), check_existing=True)
    emit.interpolation = "Closest"
    mat.node_tree.links.new(emit.outputs["Color"], bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 1.2
    bsdf.inputs["Roughness"].default_value = 0.85
    return mat


def stage(transparent, background=(0.2, 0.17, 0.13)):
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = scene.render.resolution_y = 128
    scene.render.film_transparent = transparent
    world = bpy.data.worlds.new("w")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (*background, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 1.0
    scene.world = world
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 4.0
    sun.rotation_euler = (math.radians(45), math.radians(15), math.radians(-35))
    scene.collection.objects.link(sun)
    return scene


def load(name, mat):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(ART, "Models", name + ".fbx"))
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        if o.type == "MESH":
            o.data.materials.clear()
            o.data.materials.append(mat)
    bpy.context.view_layer.update()
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for o in new:
        if o.type == "MESH":
            for c in o.bound_box:
                w = o.matrix_world @ Vector(c)
                lo = Vector(map(min, lo, w))
                hi = Vector(map(max, hi, w))
    return lo, hi


def camera(scene, target, size, pitch, yaw):
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scene.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = size
    cam.rotation_euler = (math.radians(pitch), 0, math.radians(yaw))
    cam.location = target + (cam.rotation_euler.to_matrix() @ Vector((0, 0, 1))) * 30
    cam.data.clip_end = 100
    scene.camera = cam


def render(scene, path):
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("ICON", path)


mat = material()
for item in ITEMS:
    scene = stage(True)
    lo, hi = load("item_" + item, mat)
    centre = (lo + hi) / 2
    camera(scene, centre, max(hi - lo) * 1.35, 62, 35)
    render(scene, os.path.join(ICONS, item + ".png"))

for person in PEOPLE:
    scene = stage(False, (0.32, 0.27, 0.2))
    load("char_" + person, mat)
    # Head and shoulders, three-quarter from the front (+Y).
    camera(scene, Vector((0, 0, 1.55)), 0.95, 82, 180 + 25)
    render(scene, os.path.join(PORTRAITS, person + ".png"))
