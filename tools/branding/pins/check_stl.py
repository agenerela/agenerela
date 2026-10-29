import bpy, bmesh, glob, os
for f in sorted(glob.glob(os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "**", "*.stl"), recursive=True)):
    for o in list(bpy.data.objects): bpy.data.objects.remove(o)
    bpy.ops.wm.stl_import(filepath=f)
    o = bpy.context.selected_objects[0]
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    bad = sum(1 for e in bm.edges if len(e.link_faces) != 2)
    xs=[v.co.x for v in bm.verts]; ys=[v.co.y for v in bm.verts]; zs=[v.co.z for v in bm.verts]
    print(f"CHECK {os.path.basename(f):22s} non-manifold edges={bad:3d} size={max(xs)-min(xs):.1f}x{max(ys)-min(ys):.1f}x{max(zs)-min(zs):.1f} mm z={min(zs):.1f}..{max(zs):.1f}")
