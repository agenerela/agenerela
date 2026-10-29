# One-colour versions of the logo items: the logo shows by depth, not by colour.
#     blender -b --factory-startup -P make_mono.py
# Reuses the helpers (logo geometry, prism, cut, render...) from make_pins.py.

import os
_here = os.path.dirname(os.path.abspath(__file__))
exec(open(os.path.join(_here, "make_pins.py"), encoding="utf-8").read().split("# ---------- 1.")[0])

OUT = os.path.join(_here, "out", "one-colour")
os.makedirs(OUT, exist_ok=True)


def cut_logo(target, width, cy, z0, z1):
    """Cut the A and the square out of target between z0 and z1."""
    for i, shape in enumerate(logo_shapes(width, cy=cy)):
        cut(target, prism(f"logo{i}", [shape], z0, z1, NAVY))


# 1. Engraved pin: 36 mm tile, 3.2 mm thick, logo sunk 1.2 mm. Fill it with paint or a marker for contrast.
clear()
pin = prism("pin-engraved", [rounded_rect(36, 36, 5)], 0, 3.2, NAVY)
cut_logo(pin, 24, 0, 2.0, 4.0)
export([pin], os.path.join(OUT, "pin-engraved.stl"))
render(os.path.join(OUT, "pin-engraved.png"), [pin], 70)

# 2. Stencil pin: the logo cut right through a 2.4 mm tile, so whatever is behind it shows through.
clear()
pin = prism("pin-stencil", [rounded_rect(36, 36, 5)], 0, 2.4, NAVY)
cut_logo(pin, 24, 0, -1, 4)
export([pin], os.path.join(OUT, "pin-stencil.stl"))
render(os.path.join(OUT, "pin-stencil.png"), [pin], 70)

# 3. Stencil keychain: same cut-through logo, plus the 5 mm ring hole.
clear()
key = prism("keychain-stencil", [rounded_rect(36, 46, 6, cy=5)], 0, 2.4, NAVY)
cut(key, cylinder(2.6, -1, 4, 0, 23))
cut_logo(key, 24, -1, -1, 4)
export([key], os.path.join(OUT, "keychain-stencil.stl"))
render(os.path.join(OUT, "keychain-stencil.png"), [key], 80)

# 4. Engraved desk sign: 5 mm plate, logo and name sunk 1.4 mm. Fits the same stand? No: the
#    stand's slot is for 4 mm, so this one gets its own stand with a 5.4 mm slot.
clear()
plate = prism("sign-engraved", [rounded_rect(70, 80, 6)], 0, 5.0, NAVY)
cut_logo(plate, 40, 8, 3.6, 6.0)
cut(plate, text_mesh("name", "AGENERELA", 8.0, 0, -27, 3.6, 6.0, NAVY))
export([plate], os.path.join(OUT, "sign-engraved.stl"))
render(os.path.join(OUT, "sign-engraved.png"), [plate], 140)

clear()
stand = prism("stand", [rounded_rect(70, 24, 3)], 0, 10, NAVY)
slot = prism("slot", [[(-40, -2.7), (40, -2.7), (40, 2.7), (-40, 2.7)]], 0, 40, NAVY)
slot.location, slot.rotation_euler = (0, 0, 3), (-math.radians(12), 0, 0)
bpy.context.view_layer.objects.active = slot
slot.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True)
cut(stand, slot)
export([stand], os.path.join(OUT, "sign-engraved-stand.stl"))

print("done:", sorted(os.listdir(OUT)))
