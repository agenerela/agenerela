"""Build Living Vale's whole art kit from code: palette PNG, one FBX per asset, a manifest, and renders.

    blender -b --factory-startup --python Demos/LivingVale/Art~/blender/build_kit.py -- [--render DIR] [--only id,id]

Writes, relative to the Living Vale project (two folders up from this script):
    Assets/LivingVale/Art/Textures/LV_Palette.png
    Assets/LivingVale/Art/Models/<id>.fbx
    Art~/kit_manifest.json          (triangles, vertices and Unity-space bounds per asset)
With --render DIR it also renders contact sheets and per-row views into DIR (Workbench, so it
needs no GPU). Nothing is read from or saved to a .blend; --factory-startup keeps the user's
own Blender settings out of it.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True   # keep __pycache__ out of the repository
sys.path.insert(0, HERE)

import assets    # noqa: E402
import palette   # noqa: E402
from kitlib import Kit   # noqa: E402

PROJECT = os.path.normpath(os.path.join(HERE, "..", ".."))
MODELS = os.path.join(PROJECT, "Assets", "LivingVale", "Art", "Models")
TEXTURES = os.path.join(PROJECT, "Assets", "LivingVale", "Art", "Textures")
MANIFEST = os.path.join(PROJECT, "Art~", "kit_manifest.json")


def args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = {"render": None, "only": None}
    i = 0
    while i < len(argv):
        if argv[i] == "--render":
            out["render"] = argv[i + 1]
            i += 2
        elif argv[i] == "--only":
            out["only"] = set(argv[i + 1].split(","))
            i += 2
        else:
            raise SystemExit(f"unknown argument {argv[i]}")
    return out


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    return scene


def palette_image():
    os.makedirs(TEXTURES, exist_ok=True)
    path = os.path.join(TEXTURES, "LV_Palette.png")
    img = bpy.data.images.new("LV_Palette", palette.SIZE, palette.SIZE, alpha=False)
    img.pixels = palette.pixels()
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    return img


def palette_material(img):
    mat = bpy.data.materials.new("LV_Palette")
    if mat.node_tree is None:
        mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.interpolation = "Closest"
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 1.0
    return mat


def build_object(asset_id, fn, base_to_zero, mat):
    k = Kit(asset_id, seed=sum(map(ord, asset_id)))
    fn(k)
    stats = k.finish(base_to_zero=base_to_zero)
    mesh = bpy.data.meshes.new(asset_id)
    k.bm.to_mesh(mesh)
    k.bm.free()
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(asset_id, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj, stats


def export_fbx(obj):
    os.makedirs(MODELS, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    location = obj.location.copy()
    obj.location = (0, 0, 0)
    # Unity reads the axes the FBX declares, so axis_forward alone cannot turn a model round:
    # with these settings Blender's -Y arrives as Unity's -Z. Turning the mesh half a turn for
    # the export puts the front (-Y here) on Unity's +Z, with an identity root rotation.
    half_turn = Matrix.Rotation(math.pi, 4, "Z")
    obj.data.transform(half_turn)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(MODELS, obj.name + ".fbx"),
        use_selection=True, object_types={"MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", global_scale=1.0,
        axis_forward="-Z", axis_up="Y", bake_space_transform=True,
        mesh_smooth_type="FACE", use_mesh_modifiers=True, use_tspace=False,
        add_leaf_bones=False, bake_anim=False, use_custom_props=False,
        path_mode="STRIP", embed_textures=False)
    obj.data.transform(half_turn.inverted())
    obj.location = location


def unity_bounds(stats):
    """Blender (x, y, z) -> Unity (-x, z, -y) after the export's half turn, for the manifest."""
    (x0, y0, z0), (x1, y1, z1) = stats["min"], stats["max"]
    return {"min": [round(-x1, 3), round(z0, 3), round(-y1, 3)], "max": [round(-x0, 3), round(z1, 3), round(-y0, 3)]}


# --- Renders --------------------------------------------------------------------------------

def label(text, location, size=0.32):
    curve = bpy.data.curves.new("label_" + text, "FONT")
    curve.body = text
    curve.size = size
    curve.align_x = "CENTER"
    obj = bpy.data.objects.new("label_" + text, curve)
    obj.location = location
    mat = bpy.data.materials.get("label") or bpy.data.materials.new("label")
    mat.diffuse_color = (0.08, 0.07, 0.06, 1)
    curve.materials.append(mat)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def layout(objects, stats):
    """Rows along X, one per category, front (-Y) toward the camera. Returns row bounds."""
    rows = {}
    y = 0.0
    for name, ids in assets.ROWS:
        x = 0.0
        row_min, row_max = Vector((1e9, 1e9, 0)), Vector((-1e9, -1e9, -1e9))
        depth = 0.0
        for asset_id in ids:
            if asset_id not in objects:
                continue
            s = stats[asset_id]
            w = s["max"][0] - s["min"][0]
            obj = objects[asset_id]
            lift = -s["min"][2]
            obj.location = (x - s["min"][0], y, lift)
            label(asset_id, (x + w / 2, y + s["min"][1] - 0.5, 0.01), size=max(0.18, min(0.35, w * 0.12)))
            lo = Vector((x, y + s["min"][1], 0))
            hi = Vector((x + w, y + s["max"][1], s["max"][2] + lift))
            row_min = Vector(map(min, row_min, lo))
            row_max = Vector(map(max, row_max, hi))
            depth = max(depth, s["max"][1] - s["min"][1])
            x += w + 1.5
        rows[name] = (row_min, row_max)
        y += depth + 4.0
    return rows


def camera(scene, target_min, target_max, azimuth, elevation, res=(1920, 1080), margin=1.12):
    """Orthographic camera looking at a box from azimuth (0 = from -Y, the models' front)."""
    cam_data = bpy.data.cameras.get("cam") or bpy.data.cameras.new("cam")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.get("cam") or bpy.data.objects.new("cam", cam_data)
    if cam.name not in scene.collection.objects:
        scene.collection.objects.link(cam)
    scene.camera = cam
    center = (target_min + target_max) / 2
    az, el = math.radians(azimuth), math.radians(elevation)
    direction = Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el), math.sin(el)))
    cam.location = center + direction * 200
    cam.rotation_euler = (-direction).to_track_quat("-Z", "Y").to_euler()
    cam_data.clip_end = 1000
    # fit: project the box corners on the camera's right/up axes
    rot = cam.rotation_euler.to_matrix()
    right, up = rot @ Vector((1, 0, 0)), rot @ Vector((0, 1, 0))
    corners = [Vector((x, y, z)) for x in (target_min.x, target_max.x) for y in (target_min.y, target_max.y)
               for z in (target_min.z, target_max.z)]
    w = max(abs((c - center).dot(right)) for c in corners) * 2
    h = max(abs((c - center).dot(up)) for c in corners) * 2
    aspect = res[0] / res[1]
    cam_data.ortho_scale = max(w, h * aspect) * margin
    scene.render.resolution_x, scene.render.resolution_y = res


def render(scene, path):
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print(f"RENDER {path}")


def render_all(out_dir, objects, stats):
    out_dir = os.path.abspath(out_dir)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    shading = scene.display.shading
    shading.light = "STUDIO"
    shading.color_type = "TEXTURE"
    shading.show_shadows = True
    shading.shadow_intensity = 0.35
    shading.show_cavity = False
    shading.show_backface_culling = True     # an inverted face shows as a hole
    scene.display.light_direction = (0.35, -0.45, 0.8)
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.exposure = 0.6
    world = bpy.data.worlds.new("bg")
    world.color = (0.36, 0.42, 0.48)
    scene.world = world
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"

    bpy.ops.mesh.primitive_plane_add(size=1)
    ground = bpy.context.active_object
    gmat = bpy.data.materials.new("ground")
    gmat.diffuse_color = (0.5, 0.53, 0.42, 1)
    ground.data.materials.append(gmat)

    rows = layout(objects, stats)
    lo = Vector(map(min, *[r[0] for r in rows.values()]))
    hi = Vector(map(max, *[r[1] for r in rows.values()]))
    ground.location = ((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, -0.001)
    ground.scale = (hi.x - lo.x + 10, hi.y - lo.y + 10, 1)
    os.makedirs(out_dir, exist_ok=True)
    camera(scene, lo, hi, -30, 35, res=(2400, 1500))
    render(scene, os.path.join(out_dir, "contact_sheet.png"))
    camera(scene, lo, hi, 150, 35, res=(2400, 1500))
    render(scene, os.path.join(out_dir, "contact_back.png"))
    camera(scene, lo, hi, 0, 89, res=(2400, 1500))
    render(scene, os.path.join(out_dir, "contact_top.png"))
    def solo(ids):
        for asset_id, obj in objects.items():
            obj.hide_render = ids is not None and asset_id not in ids
            lab = bpy.data.objects.get("label_" + asset_id)
            if lab:
                lab.hide_render = obj.hide_render

    row_ids = dict(assets.ROWS)
    for name, (rlo, rhi) in rows.items():
        solo(row_ids[name])
        camera(scene, rlo, rhi, -30, 20, res=(1920, 900))
        render(scene, os.path.join(out_dir, f"row_{name}_front.png"))
        camera(scene, rlo, rhi, 0, 5, res=(1920, 900))
        render(scene, os.path.join(out_dir, f"row_{name}_straight.png"))
        camera(scene, rlo, rhi, 180 + 30, 20, res=(1920, 900))
        render(scene, os.path.join(out_dir, f"row_{name}_back.png"))
    for asset_id in ("humanoid_player", "wolf", "wagon_wrecked", "ashwatch_gate"):
        if asset_id not in objects:
            continue
        obj = objects[asset_id]
        solo({asset_id})
        s = stats[asset_id]
        base = Vector(obj.location)
        olo = base + Vector(s["min"]) + Vector((0, 0, -s["min"][2]))
        ohi = base + Vector(s["max"]) + Vector((0, 0, -s["min"][2]))
        for az in (0, 90, 180):
            camera(scene, olo, ohi, az, 8, res=(900, 900), margin=1.3)
            render(scene, os.path.join(out_dir, f"{asset_id}_{az:03d}.png"))
    solo(None)


def main():
    opts = args()
    reset_scene()
    img = palette_image()
    mat = palette_material(img)
    objects, stats, manifest = {}, {}, {}
    for asset_id, fn, base in assets.ASSETS:
        if opts["only"] and asset_id not in opts["only"]:
            continue
        obj, st = build_object(asset_id, fn, base, mat)
        export_fbx(obj)
        objects[asset_id], stats[asset_id] = obj, st
        height = st["max"][2] - st["min"][2]
        manifest[asset_id] = {"triangles": st["triangles"], "vertices": st["vertices"],
                              "height": round(height, 3), "bounds": unity_bounds(st)}
        print(f"ASSET {asset_id:18s} tris {st['triangles']:5d}  height {height:5.2f}  "
              f"size x {st['max'][0] - st['min'][0]:5.2f} depth {st['max'][1] - st['min'][1]:5.2f}")
    if opts["only"] and os.path.exists(MANIFEST):   # --only updates just those entries
        with open(MANIFEST, encoding="utf-8") as f:
            manifest = {**json.load(f)["assets"], **manifest}
        manifest = {asset_id: manifest[asset_id] for asset_id, _, _ in assets.ASSETS if asset_id in manifest}
    with open(MANIFEST, "w", encoding="utf-8", newline="\n") as f:
        json.dump({"palette": "Assets/LivingVale/Art/Textures/LV_Palette.png", "assets": manifest}, f, indent=2)
        f.write("\n")
    if opts["render"]:
        render_all(opts["render"], objects, stats)


main()
