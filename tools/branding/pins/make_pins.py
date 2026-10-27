# Builds 3D-printable Agenerela logo items in Blender, in millimetres (1 Blender unit = 1 mm).
#     blender -b --factory-startup -P make_pins.py
# Each item is separate STL parts: a dark base and a raised cyan logo, so Bambu Studio
# (or any slicer) can load them as one object with two parts and give each a filament.

import math
import os
import bmesh
import bpy

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")
os.makedirs(OUT, exist_ok=True)

NAVY = (0.106, 0.141, 0.200, 1)   # #1B2433
CYAN = (0.161, 0.851, 1.000, 1)   # #29D9FF

# The logo, traced from the 1254 px image: the A outline and the square, in image pixels.
A_PX = [(244, 978), (568, 262), (686, 262), (1010, 978), (866, 978), (627, 437), (388, 978)]
SQ_PX = [(563, 683), (691, 683), (691, 817), (563, 817)]
LOGO_W_PX = 1010 - 244          # 766
LOGO_CX, LOGO_CY = (244 + 1010) / 2, (262 + 978) / 2


def logo_shapes(width_mm, cx=0.0, cy=0.0):
    """The A and the square in mm, centred on (cx, cy), y up."""
    s = width_mm / LOGO_W_PX
    conv = lambda pts: [((x - LOGO_CX) * s + cx, -(y - LOGO_CY) * s + cy) for x, y in pts]
    return [conv(A_PX), conv(SQ_PX)]


def rounded_rect(w, h, r, cx=0.0, cy=0.0, seg=12):
    pts = []
    for (qx, qy, a0) in [(w / 2 - r, h / 2 - r, 0), (-w / 2 + r, h / 2 - r, 90),
                         (-w / 2 + r, -h / 2 + r, 180), (w / 2 - r, -h / 2 + r, 270)]:
        for i in range(seg + 1):
            a = math.radians(a0 + 90 * i / seg)
            pts.append((cx + qx + r * math.cos(a), cy + qy + r * math.sin(a)))
    return pts


def prism(name, outlines, z0, z1, color):
    """One mesh object: each 2D outline (counter-clockwise or not) extruded from z0 to z1."""
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    for pts in outlines:
        verts = [bm.verts.new((x, y, z0)) for x, y in pts]
        face = bm.faces.new(verts)
        ext = bmesh.ops.extrude_face_region(bm, geom=[face])
        moved = [e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)]
        bmesh.ops.translate(bm, verts=moved, vec=(0, 0, z1 - z0))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])   # concave caps (the A) triangulated here, not by the exporter
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    ob.color = color
    return ob


def cut(target, cutter):
    mod = target.modifiers.new("cut", "BOOLEAN")
    mod.operation, mod.object, mod.solver = "DIFFERENCE", cutter, "EXACT"
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter)


def cylinder(r, z0, z1, cx, cy, seg=48):
    return prism("cyl", [[(cx + r * math.cos(2 * math.pi * i / seg), cy + r * math.sin(2 * math.pi * i / seg))
                          for i in range(seg)]], z0, z1, NAVY)


def text_mesh(name, body, size, cx, cy, z0, z1, color):
    bpy.ops.object.text_add()
    t = bpy.context.object
    t.data.body = body
    t.data.size = size
    t.data.align_x, t.data.align_y = "CENTER", "CENTER"
    t.data.extrude = (z1 - z0) / 2
    t.location = (cx, cy, (z0 + z1) / 2)
    bpy.ops.object.convert(target="MESH")
    t.name, t.color = name, color
    return t


def export(objs, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.ops.wm.stl_export(filepath=path, export_selected_objects=True, apply_modifiers=True)


def clear():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o)


TOP = False


def render(path, objs, cam_dist):
    global TOP
    for top in (False, True):
        TOP = top
        _render(path.replace(".png", "-top.png") if top else path, objs, cam_dist)


def _render(path, objs, cam_dist):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.color_type = "OBJECT"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.show_cavity = True
    scene.render.resolution_x = scene.render.resolution_y = 900
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("w") if not scene.world else scene.world
    scene.world = world
    world.color = (0.92, 0.92, 0.92)
    cam_data = bpy.data.cameras.new("cam")
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.location = (cam_dist * 0.55, -cam_dist * 0.75, cam_dist * 0.9) if not TOP else (0, -0.01, cam_dist * 1.4)
    target = bpy.data.objects.new("target", None)
    scene.collection.objects.link(target)
    zs = [v.co.z for o in objs for v in o.data.vertices]
    target.location = (0, 0, (min(zs) + max(zs)) / 2)
    track = cam.constraints.new("TRACK_TO")
    track.target, track.track_axis, track.up_axis = target, "TRACK_NEGATIVE_Z", "UP_Y"
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)
    bpy.data.objects.remove(target)


# ---------- 1. Pin: 36 mm tile, flat back for a glued pin back or magnet ----------
clear()
T = 2.4                      # base thickness
R = 1.0                      # logo relief
base = prism("pin-base", [rounded_rect(36, 36, 5)], 0, T, NAVY)
logo = prism("pin-logo", logo_shapes(24), T, T + R, CYAN)
export([base], os.path.join(OUT, "pin-base.stl"))
export([logo], os.path.join(OUT, "pin-logo.stl"))
render(os.path.join(OUT, "pin.png"), [base, logo], 70)

# ---------- 2. Keychain: 36 x 46 mm tag with a 5 mm ring hole ----------
clear()
base = prism("key-base", [rounded_rect(36, 46, 6, cy=5)], 0, T, NAVY)
cut(base, cylinder(2.6, -1, T + 1, 0, 23))
logo = prism("key-logo", logo_shapes(24, cy=-1), T, T + R, CYAN)
export([base], os.path.join(OUT, "keychain-base.stl"))
export([logo], os.path.join(OUT, "keychain-logo.stl"))
render(os.path.join(OUT, "keychain.png"), [base, logo], 80)

# ---------- 3. Desk sign: 70 x 80 mm plate, logo and name, plus a slotted stand ----------
clear()
PT = 4.0                     # plate thickness
plate = prism("sign-plate", [rounded_rect(70, 80, 6)], 0, PT, NAVY)
logo = prism("sign-logo", logo_shapes(40, cy=8), PT, PT + 1.2, CYAN)
name = text_mesh("sign-name", "AGENERELA", 8.0, 0, -27, PT, PT + 1.2, CYAN)
export([plate], os.path.join(OUT, "sign-plate.stl"))
export([logo, name], os.path.join(OUT, "sign-logo.stl"))
render(os.path.join(OUT, "sign.png"), [plate, logo, name], 140)

# The stand: a 70 x 24 x 10 mm foot with a slot that leans the plate back 12 degrees.
clear()
stand = prism("stand", [rounded_rect(70, 24, 3)], 0, 10, NAVY)
SLOT_W, LEAN = PT + 0.4, math.radians(12)
slot = prism("slot", [[(-40, -SLOT_W / 2), (40, -SLOT_W / 2), (40, SLOT_W / 2), (-40, SLOT_W / 2)]], 0, 40, NAVY)
slot.location = (0, 0, 3)                     # slot floor 3 mm above the bed
slot.rotation_euler = (-LEAN, 0, 0)          # top leans toward +y (backward)
bpy.context.view_layer.objects.active = slot
slot.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True)
cut(stand, slot)
export([stand], os.path.join(OUT, "sign-stand.stl"))
render(os.path.join(OUT, "stand.png"), [stand], 120)

print("done:", sorted(os.listdir(OUT)))
