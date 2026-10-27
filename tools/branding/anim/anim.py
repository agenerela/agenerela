# Agenerela logo animation and hero still, in Blender 5.2.
#   blender -b --factory-startup -P anim.py -- still     4K Cycles still of the final pose
#   blender -b --factory-startup -P anim.py -- preview   a few low-res EEVEE frames to check the look
#   blender -b --factory-startup -P anim.py -- anim      720 frames, 3840x2160, 120 fps, EEVEE, PNGs
# Every animated value is keyed on every frame from the easing functions below, so the timing
# is exact and matches the sound design in sfx.py (same TIMES table).

import math
import os
import sys
import bmesh
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
MODE = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else "preview"
FPS, FRAMES = 120, 720

# Seconds; sfx.py reads the same numbers.
TIMES = dict(rise_end=1.25, drop_start=1.30, impact=2.00, letters=2.25, letter_gap=0.10,
             sweep_start=3.55, sweep_end=4.75, fade_out=5.70)
F = {k: round(v * FPS) for k, v in TIMES.items()}

CYAN = (0.161, 0.851, 1.000, 1)

# ---------- easing ----------
clamp = lambda x: max(0.0, min(1.0, x))
lerp = lambda a, b, t: a + (b - a) * t
ease_out_cubic = lambda t: 1 - (1 - t) ** 3
ease_in_out = lambda t: 3 * t * t - 2 * t * t * t
ease_in_quad = lambda t: t * t
def ease_out_back(t, s=1.7):
    t -= 1
    return 1 + (s + 1) * t ** 3 + s * t ** 2
def phase(f, a, b):
    return clamp((f - a) / max(1, b - a))

# ---------- scene ----------
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
scene = bpy.context.scene
scene.render.fps = FPS
scene.frame_start, scene.frame_end = 0, FRAMES - 1
scene.render.resolution_x, scene.render.resolution_y = 3840, 2160
scene.view_settings.view_transform = "AgX"
try:
    scene.view_settings.look = "AgX - Medium High Contrast"
except TypeError:
    pass

world = bpy.data.worlds.new("world")
scene.world = world
world.use_nodes = True
bg = world.node_tree.nodes["Background"]
bg.inputs["Color"].default_value = (0.004, 0.007, 0.013, 1)
bg.inputs["Strength"].default_value = 1.0


def material(name, base, rough, emit_strength=0.0, metallic=0.0, coat=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = base
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metallic
    p.inputs["Coat Weight"].default_value = coat
    p.inputs["Emission Color"].default_value = base
    p.inputs["Emission Strength"].default_value = emit_strength
    return m, p


logo_mat, logo_bsdf = material("logo", (0.03, 0.62, 1.0, 1), 0.22, 1.0, coat=0.6)
text_mat, text_bsdf = material("text", (0.82, 0.93, 1.0, 1), 0.35, 0.0)
floor_mat, _ = material("floor", (0.010, 0.014, 0.022, 1), 0.18)


def solid(name, outline_xz, depth, mat, bevel=0.012):
    """Extrude an outline drawn in the XZ plane by depth along +Y, centred on the origin."""
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    verts = [bm.verts.new((x, -depth / 2, z)) for x, z in outline_xz]
    face = bm.faces.new(verts)
    ext = bmesh.ops.extrude_face_region(bm, geom=[face])
    bmesh.ops.translate(bm, verts=[v for v in ext["geom"] if isinstance(v, bmesh.types.BMVert)], vec=(0, depth, 0))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    scene.collection.objects.link(ob)
    ob.data.materials.append(mat)
    if bevel:
        b = ob.modifiers.new("bevel", "BEVEL")
        b.width, b.segments, b.limit_method = bevel, 3, "ANGLE"
    return ob


# Logo traced from the 1254 px image: the A and the square, 2.0 units wide.
A_PX = [(244, 978), (568, 262), (686, 262), (1010, 978), (866, 978), (627, 437), (388, 978)]
SQ_PX = [(563, 683), (691, 683), (691, 817), (563, 817)]
S = 2.0 / 766
LOGO_Z = 1.72                                     # logo centre height
to_xz = lambda pts, cx, cy: [((x - cx) * S, -(y - cy) * S) for x, y in pts]

A = solid("A", to_xz(A_PX, 627, 620), 0.26, logo_mat)
SQ = solid("square", to_xz(SQ_PX, 627, 750), 0.26, logo_mat)
SQ_REST = (0, 0, LOGO_Z - (750 - 620) * S)

floor = bpy.data.objects.new("floor", bpy.data.meshes.new("floor"))
bm = bmesh.new()
bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=30)
bm.to_mesh(floor.data)
bm.free()
scene.collection.objects.link(floor)
floor.data.materials.append(floor_mat)

# The name, one object per letter so each can arrive on its own.
FONT = bpy.data.fonts.load(r"C:\Windows\Fonts\bahnschrift.ttf")
letters, widths = [], []
for ch in "AGENERELA":
    cu = bpy.data.curves.new(ch, "FONT")
    cu.body, cu.font, cu.size, cu.extrude = ch, FONT, 0.30, 0.03
    cu.align_x = "CENTER"
    ob = bpy.data.objects.new("L_" + ch, cu)
    scene.collection.objects.link(ob)
    ob.rotation_euler = (math.radians(90), 0, 0)
    ob.data.materials.append(text_mat)
    letters.append(ob)
bpy.context.view_layer.update()
for ob in letters:
    xs = [v[0] for v in ob.bound_box]
    widths.append(max(xs) - min(xs))
TRACK = 0.11
total = sum(widths) + TRACK * (len(widths) - 1)
x = -total / 2
TEXT_Z = 0.36
LETTER_REST = []
for ob, w in zip(letters, widths):
    LETTER_REST.append((x + w / 2, 0, TEXT_Z))
    x += w + TRACK

# ---------- lights ----------
def area(name, loc, rot_deg, power, size, color, shape="RECTANGLE", size_y=None):
    d = bpy.data.lights.new(name, "AREA")
    d.energy, d.color, d.shape, d.size = power, color, shape, size
    if size_y:
        d.size_y = size_y
    ob = bpy.data.objects.new(name, d)
    ob.location, ob.rotation_euler = loc, [math.radians(a) for a in rot_deg]
    scene.collection.objects.link(ob)
    return ob

area("rim", (0, 3.5, 4.2), (-140, 0, 0), 900, 4, (0.35, 0.75, 1.0), size_y=1.5)
area("key", (-3.2, -3.5, 3.2), (55, 0, -40), 260, 3, (1.0, 0.96, 0.92))
area("fill", (3.5, -2.5, 1.2), (80, 0, 55), 90, 3, (0.5, 0.7, 1.0))
sweep = area("sweep", (-5, -2.2, 1.9), (90, 0, 0), 0, 0.12, (0.9, 0.97, 1.0), size_y=3.5)
flash_d = bpy.data.lights.new("flash", "POINT")
flash = bpy.data.objects.new("flash", flash_d)
flash.location = (0, -1.2, LOGO_Z - 0.3)
flash_d.color = (0.4, 0.85, 1.0)
scene.collection.objects.link(flash)

# ---------- camera ----------
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
scene.collection.objects.link(cam)
scene.camera = cam
cam.data.lens = 50
cam.data.dof.use_dof = True
cam.data.dof.aperture_fstop = 2.8
target = bpy.data.objects.new("target", None)
target.location = (0, 0, 1.45)
scene.collection.objects.link(target)
cam.data.dof.focus_object = target
tr = cam.constraints.new("TRACK_TO")
tr.target, tr.track_axis, tr.up_axis = target, "TRACK_NEGATIVE_Z", "UP_Y"
CAM_A, CAM_B = (2.8, -10.2, 0.8), (0.5, -7.9, 1.35)

# ---------- the animation, one key per frame ----------
def pose(f):
    t = f / FPS
    # A: rises out of the floor and turns from edge-on to facing the camera.
    p = ease_out_cubic(phase(f, 0, F["rise_end"]))
    A.location = (0, 0, lerp(LOGO_Z - 2.7, LOGO_Z, p))
    A.rotation_euler = (0, 0, math.radians(lerp(-95, 0, p)))
    # Square: falls in (accelerating), lands at the impact, one small bounce, squash and settle.
    land = F["impact"]
    if f < F["drop_start"]:
        z = SQ_REST[2] + 3.4
    elif f <= land:
        z = lerp(SQ_REST[2] + 3.4, SQ_REST[2], ease_in_quad(phase(f, F["drop_start"], land)))
    else:
        b = (f - land) / FPS
        z = SQ_REST[2] + 0.07 * abs(math.sin(b * math.pi / 0.13)) * math.exp(-b * 9)
    SQ.location = (0, 0, z)
    squash = math.exp(-((f - land) / FPS) * 14) * 0.22 if f >= land else 0.0
    SQ.scale = (1 + squash * 0.6, 1, 1 - squash)
    SQ.rotation_euler = (0, math.radians(lerp(35, 0, ease_out_cubic(phase(f, F["drop_start"], land)))), 0)
    # Emission: grows with the rise, flashes at the impact, breathes gently after.
    e = 0.45 * p
    if f >= land:
        e += 4.0 * math.exp(-((f - land) / FPS) * 6)
    e += 0.10 * math.sin(t * 2.2) * phase(f, land, land + 120)
    logo_bsdf.inputs["Emission Strength"].default_value = e
    flash_d.energy = 900 * math.exp(-((f - land) / FPS) * 7) if f >= land else 0.0
    # Letters: one every 0.1 s, popping up with a little overshoot.
    for i, (ob, rest) in enumerate(zip(letters, LETTER_REST)):
        a = F["letters"] + round(i * TIMES["letter_gap"] * FPS)
        q = phase(f, a, a + 22)
        s = max(0.0001, ease_out_back(q)) if q > 0 else 0.0001
        ob.scale = (s, s, s)
        ob.location = (rest[0], 0, rest[2] - 0.12 * (1 - ease_out_cubic(q)))
    text_bsdf.inputs["Emission Strength"].default_value = 0.35 * phase(f, F["letters"], F["letters"] + 120)
    # Light sweep: a thin bright bar passes in front, left to right, catching the bevels.
    ps = phase(f, F["sweep_start"], F["sweep_end"])
    sweep.location = (lerp(-5, 5, ease_in_out(ps)), -2.2, 1.9)
    sweep.data.energy = 650 * math.sin(math.pi * ps)
    # Camera: one slow push in across the whole piece.
    c = ease_in_out(phase(f, 0, FRAMES - 1))
    cam.location = tuple(lerp(a, b, c) for a, b in zip(CAM_A, CAM_B))
    # Fade in from black, and out at the end.
    scene.view_settings.exposure = lerp(-7, 0, ease_out_cubic(phase(f, 0, 70))) \
        - 7 * ease_in_quad(phase(f, F["fade_out"], FRAMES - 1))


def key(f):
    pose(f)
    for ob in [A, SQ, cam, sweep] + letters:
        for path in ("location", "rotation_euler", "scale"):
            ob.keyframe_insert(path, frame=f)
    logo_bsdf.inputs["Emission Strength"].keyframe_insert("default_value", frame=f)
    text_bsdf.inputs["Emission Strength"].keyframe_insert("default_value", frame=f)
    flash_d.keyframe_insert("energy", frame=f)
    sweep.data.keyframe_insert("energy", frame=f)
    scene.view_settings.keyframe_insert("exposure", frame=f)


bpy.context.preferences.edit.keyframe_new_interpolation_type = "LINEAR"
for f in range(FRAMES):
    key(f)

# ---------- compositor: soft bloom ----------
ng = bpy.data.node_groups.new("comp", "CompositorNodeTree")
ng.interface.new_socket("Image", in_out="OUTPUT", socket_type="NodeSocketColor")
rl = ng.nodes.new("CompositorNodeRLayers")
glare = ng.nodes.new("CompositorNodeGlare")
for v in ("Bloom", "BLOOM"):
    try:
        glare.inputs["Type"].default_value = v
        break
    except (TypeError, ValueError):
        pass
glare.inputs["Threshold"].default_value = 0.9
glare.inputs["Strength"].default_value = 0.4
glare.inputs["Size"].default_value = 0.6
out = ng.nodes.new("NodeGroupOutput")
ng.links.new(rl.outputs["Image"], glare.inputs["Image"])
ng.links.new(glare.outputs["Image"], out.inputs[0])
scene.compositing_node_group = ng

# ---------- render ----------
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_depth = "8"
if MODE == "still":
    scene.render.engine = "CYCLES"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    prefs.compute_device_type = "OPTIX"
    prefs.get_devices()
    for d in prefs.devices:
        d.use = d.type == "OPTIX"
    scene.cycles.device = "GPU"
    scene.cycles.samples = 384
    scene.cycles.use_denoising = True
    scene.frame_set(610)
    # Hero framing for the still: lower, three-quarter, glossy floor, punchier grade.
    cam.animation_data_clear()
    cam.location = (3.0, -8.4, 0.7)
    cam.data.lens = 50
    logo_mat.node_tree.animation_data_clear()
    logo_bsdf.inputs["Emission Strength"].default_value = 1.1
    target.location = (0.1, 0, 1.35)
    cam.data.dof.aperture_fstop = 2.0
    target.location = (0.1, 0, 1.35)
    floor_mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.07
    scene.view_settings.exposure = 0.15
    try:
        scene.view_settings.look = "AgX - Punchy"
    except TypeError:
        pass
    scene.render.filepath = os.path.join(HERE, "out", "agenerela-4k.png")
    bpy.ops.render.render(write_still=True)
else:
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = 32
    scene.eevee.use_raytracing = True
    if MODE == "preview":
        scene.render.resolution_percentage = 25
        for f in (40, 150, 241, 330, 500, 640):
            scene.frame_set(f)
            scene.render.filepath = os.path.join(HERE, "out", "preview", f"f{f:03d}.png")
            bpy.ops.render.render(write_still=True)
    else:
        scene.render.filepath = os.path.join(HERE, "out", "frames", "f")
        bpy.ops.render.render(animation=True)
print("RENDER DONE", MODE)
