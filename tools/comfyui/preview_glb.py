"""Render a .glb from six angles into one PNG, so an agent can look at a generated model.

    python tools/comfyui/preview_glb.py tools/comfyui/out/mesh/mesh_00001_.glb
    python tools/comfyui/preview_glb.py a.glb b.glb --out somewhere/

Needs Blender (any 4.x or 5.x), which it finds by itself: --blender PATH, the BLENDER
environment variable, the PATH, the usual install folders, or a Steam library. It runs
Blender headless with factory settings, so the user's own Blender setup is never read
or changed, and nothing is saved but the PNGs.

Each sheet is a 3x2 grid, 512 px per view, with the textures and a neutral light:
    front          three-quarter   side
    back           top             close-up (3x, front)
"Front" faces the camera of the image the mesh was made from. For each model it prints
    PREVIEW <glb> -> <png> | <n> triangles | textures <sizes> | height <h>
Exit code 3 means Blender was not found; say so, and ask the user to open the .glb.

This file runs twice: first under plain Python (standard library only) to find Blender,
then inside Blender, where `bpy` exists, to render.
"""
import glob
import math
import os
import shutil
import subprocess
import sys
from pathlib import Path

VIEWS = [  # name, azimuth, elevation, zoom; in grid order
    ("front", 0, 10, 1.0), ("three-quarter", 45, 20, 1.0), ("side", 90, 10, 1.0),
    ("back", 180, 10, 1.0), ("top", 0, 80, 1.0), ("close-up", 15, 10, 3.0),
]
CELL = 512


# --- Under plain Python: find Blender and hand over ------------------------------

def steam_libraries():
    roots = []
    if sys.platform == "win32":
        try:
            import winreg
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, r"Software\Valve\Steam") as key:
                roots.append(Path(winreg.QueryValueEx(key, "SteamPath")[0]))
        except OSError:
            pass
        roots.append(Path(r"C:\Program Files (x86)\Steam"))
    elif sys.platform == "darwin":
        roots.append(Path.home() / "Library/Application Support/Steam")
    else:
        roots += [Path.home() / ".steam/steam", Path.home() / ".local/share/Steam"]
    libraries = []
    for root in roots:
        libraries.append(root)
        vdf = root / "steamapps" / "libraryfolders.vdf"
        if vdf.is_file():
            for line in vdf.read_text(encoding="utf-8", errors="replace").splitlines():
                parts = line.strip().split('"')
                if len(parts) >= 4 and parts[1] == "path":
                    libraries.append(Path(parts[3].replace("\\\\", "\\")))
    return libraries


def find_blender(explicit):
    candidates = [explicit, os.environ.get("BLENDER"), shutil.which("blender")]
    if sys.platform == "win32":
        candidates += sorted(glob.glob(r"C:\Program Files\Blender Foundation\Blender*\blender.exe"), reverse=True)
        candidates += [str(lib / "steamapps/common/Blender/blender.exe") for lib in steam_libraries()]
    elif sys.platform == "darwin":
        candidates += ["/Applications/Blender.app/Contents/MacOS/Blender"]
        candidates += [str(lib / "steamapps/common/Blender/Blender.app/Contents/MacOS/Blender") for lib in steam_libraries()]
    else:
        candidates += ["/snap/bin/blender"] + [str(lib / "steamapps/common/Blender/blender") for lib in steam_libraries()]
    for c in candidates:
        if c and Path(c).is_file():
            return c
    return None


def launch():
    import argparse
    parser = argparse.ArgumentParser(description="Render .glb files from six angles with headless Blender.")
    parser.add_argument("glb", nargs="+", type=Path)
    parser.add_argument("--out", type=Path, default=Path(__file__).resolve().parent / "out" / "preview",
                        help="folder for the PNGs (default: tools/comfyui/out/preview)")
    parser.add_argument("--blender", help="path to the Blender executable")
    args = parser.parse_args()
    missing = [str(g) for g in args.glb if not g.is_file()]
    if missing:
        sys.exit(f"error: no such file: {', '.join(missing)}")
    blender = find_blender(args.blender)
    if not blender:
        print("error: Blender was not found. Install it, set BLENDER=<path to the executable>, or pass "
              "--blender. Without it, ask the user to open the .glb themselves.", file=sys.stderr)
        return 3
    args.out.mkdir(parents=True, exist_ok=True)
    cmd = [blender, "-b", "--factory-startup", "--python", str(Path(__file__).resolve()), "--",
           str(args.out.resolve())] + [str(g.resolve()) for g in args.glb]
    proc = subprocess.run(cmd, capture_output=True, text=True, errors="replace")
    lines = [l for l in proc.stdout.splitlines() if l.startswith(("PREVIEW", "FAILED"))]
    for line in lines:
        print(line)
    if proc.returncode != 0 or len(lines) < len(args.glb):
        print(f"error: Blender exited with {proc.returncode}; its last output:\n" + "\n".join(
            (proc.stdout + proc.stderr).splitlines()[-15:]), file=sys.stderr)
        return 1
    return 1 if any(l.startswith("FAILED") for l in lines) else 0


# --- Inside Blender: render ------------------------------------------------------

def render_all(out_dir, glbs):
    import bpy
    import numpy as np
    from mathutils import Vector

    def engine():
        items = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
        return next((n for n in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT", "CYCLES") if n in items), items[0])

    for glb in map(Path, glbs):
        try:
            bpy.ops.wm.read_factory_settings(use_empty=True)
            bpy.ops.import_scene.gltf(filepath=str(glb))
            meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
            if not meshes:
                raise RuntimeError("the file holds no mesh")
            corners = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
            lo = Vector([min(c[i] for c in corners) for i in range(3)])
            hi = Vector([max(c[i] for c in corners) for i in range(3)])
            centre, radius = (lo + hi) / 2, max((hi - lo).length / 2, 1e-6)
            tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes)
            textures = sorted({f"{i.size[0]}x{i.size[1]}" for i in bpy.data.images if i.size[0]})

            scene = bpy.context.scene
            scene.render.engine = engine()
            scene.render.resolution_x = scene.render.resolution_y = CELL
            if hasattr(scene, "eevee") and hasattr(scene.eevee, "taa_render_samples"):
                scene.eevee.taa_render_samples = 32
            world = bpy.data.worlds.new("grey")
            world.use_nodes = True
            world.node_tree.nodes["Background"].inputs[0].default_value = (0.45, 0.45, 0.45, 1)
            scene.world = world
            sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
            sun.data.energy = 2.5
            sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
            scene.collection.objects.link(sun)
            cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
            cam.data.lens = 50
            scene.collection.objects.link(cam)
            scene.camera = cam

            sheet = np.zeros((CELL * 2, CELL * 3, 4), dtype=np.float32)
            for k, (_, azimuth, elevation, zoom) in enumerate(VIEWS):
                dist = radius / math.sin(cam.data.angle / 2) * 1.05 / zoom
                az, el = math.radians(azimuth), math.radians(elevation)
                # glTF +Z (towards the source image's camera) is Blender -Y after import.
                cam.location = centre + Vector((math.sin(az) * math.cos(el), -math.cos(az) * math.cos(el),
                                                math.sin(el))) * dist
                cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
                tmp = Path(out_dir) / f".{glb.stem}_{k}.png"
                scene.render.filepath = str(tmp)
                bpy.ops.render.render(write_still=True)
                img = bpy.data.images.load(str(tmp))
                px = np.empty(CELL * CELL * 4, dtype=np.float32)
                img.pixels.foreach_get(px)
                bpy.data.images.remove(img)
                tmp.unlink()
                row, col = divmod(k, 3)  # Blender stores rows bottom-up: grid row 0 is the top
                y0 = (1 - row) * CELL
                sheet[y0:y0 + CELL, col * CELL:(col + 1) * CELL] = px.reshape(CELL, CELL, 4)

            out = bpy.data.images.new(f"{glb.stem}_preview", CELL * 3, CELL * 2, alpha=True)
            out.pixels.foreach_set(sheet.ravel())
            png = Path(out_dir) / f"{glb.stem}_preview.png"
            out.filepath_raw = str(png)
            out.file_format = "PNG"
            out.save()
            print(f"PREVIEW {glb} -> {png} | {tris} triangles | textures {', '.join(textures) or 'none'} | "
                  f"height {hi.z - lo.z:.2f}", flush=True)
        except Exception as e:  # report and go on to the next file
            print(f"FAILED {glb}: {e}", flush=True)


if __name__ == "__main__":
    try:
        import bpy  # noqa: F401  (present only inside Blender)
    except ImportError:
        sys.exit(launch())
    argv = sys.argv[sys.argv.index("--") + 1:]
    render_all(argv[0], argv[1:])
