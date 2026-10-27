# Branding: the Agenerela logo as printable models, stills and an animation

Everything here is built from the logo's own geometry, traced once from `logo.webp`: the A
outline and the square are seven and four points in the scripts. Nothing is AI-generated, so
the shapes are exact and the meshes watertight.

Needs Blender 5.x (the repo's `tools/comfyui/preview_glb.py` finds it the same way; here it was
the Steam install) and, for the sound, Python with `numpy` and `scipy`. Run the scripts from
their own folder.

## `pins/`: 3D-printable pins, keychains and a desk sign

```
blender -b --factory-startup -P make_pins.py     # two-colour parts -> out/
blender -b --factory-startup -P make_mono.py     # one-colour versions -> out/one-colour/
blender -b --factory-startup -P check_stl.py     # every STL: watertight? size in mm
```

Units are millimetres. Print flat, logo up, no supports.

| Item | Size | Two colours (`out/`) | One colour (`out/one-colour/`) |
|---|---|---|---|
| Pin | 36 × 36 mm | `pin-base` + `pin-logo` (logo raised 1 mm) | `pin-stencil` (logo cut through), `pin-engraved` (sunk 1.2 mm) |
| Keychain | 36 × 46 mm, 5 mm ring hole | `keychain-base` + `keychain-logo` | `keychain-stencil` |
| Desk sign | 70 × 80 mm | `sign-plate` + `sign-logo` (logo and name), on `sign-stand` (4.4 mm slot, 12° lean) | `sign-engraved` on `sign-engraved-stand` (5.4 mm slot) |

Two colours in Bambu Studio: drag in both parts of an item together and answer **Yes** to
loading them as one object with several parts, then give each part a filament. Without an AMS,
add a filament change at the logo's first layer: 2.4 mm for the pin and keychain, 4.0 mm for
the sign. The engraved one-colour pieces read best with paint run into the engraving.

## `anim/`: 4K stills and a 4K 120 fps animation with sound

```
blender -b --factory-startup -P anim.py -- still     # out/agenerela-4k.png, Cycles on the GPU (OptiX)
blender -b --factory-startup -P anim.py -- preview   # six quarter-size EEVEE frames -> out/preview/
blender -b --factory-startup -P anim.py -- anim      # 720 frames, 3840x2160 -> out/frames/
python sfx.py                                        # out/sfx.wav
ffmpeg -framerate 120 -start_number 0 -i out/frames/f%04d.png -i out/sfx.wav -c:v libx264 -preset slow -crf 16 -pix_fmt yuv420p -c:a aac -b:a 320k -movflags +faststart -shortest out/agenerela-logo-4k120.mp4
```

The animation is 6 s: the A rises out of the floor and turns to face the camera, the square
drops in and lands with a flash at 2.0 s, the name pops up a letter every 0.1 s, a light sweeps
across, and it fades out. Every cue time lives in one `TIMES` table in `anim.py`, and `sfx.py`
reads that same table, so the synthesised sound (riser, falling whistle, impact boom and chime,
a blip per letter, a shimmer that pans with the sweep) stays on its frame when the timing
changes. On an RTX 5080 a still takes about 20 s and the full 720 frames about 12 minutes.
The name uses Bahnschrift from `C:\Windows\Fonts`, so `anim.py` as written runs on Windows.

`out/agenerela-4k.png` is the three-quarter hero still; `out/agenerela-4k-front.png` is the
same scene straight on, from the animation's last moments.
