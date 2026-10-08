---
name: comfyui-assets
description: Make game assets on the team's ComfyUI asset server (an RTX 5080 machine) and check them before handing them over - images and concept art (.png), textured 3D models of props (.glb), and sound effects or ambience (.mp3). Use when the user asks to generate, create or make an image, a 3D model, mesh or prop, or a sound for the demo games.
---

# Making assets on the ComfyUI asset server

The rules and the details are in `tools/comfyui/AGENT_USAGE.md`. Read it once per session
before the first request: it holds the token rules, every parameter and the meaning of each
error. This file is the procedure. Run every command from the repository root.

## Procedure

1. **Connect.** `python tools/comfyui/comfy.py check`. If it says `COMFY_URL` or
   `COMFY_API_TOKEN` is missing, follow the guide's "First time on a machine": the user fills
   in `tools/comfyui/.env` themselves. Never open or print `.env`, and never ask for the token
   in the chat. For any other error, find it in the guide's troubleshooting table, tell the
   user, and stop.
2. **Make one asset at a time,** from the user's own description. `comfy.py list` shows each
   workflow's parameters. Add `--json` to a run to record the seed.
3. **Check it before reporting it:**
   - **Image:** open the PNG and look at it. An image meant for a 3D model must show one whole
     object, centred, on a plain white background, from a three-quarter view. If it does not,
     change the seed or the wording and try again.
   - **3D model:** render it with `python tools/comfyui/preview_glb.py <the .glb>` and open the
     PNG it prints: front, three-quarter and side on top; back, top and a close-up below.
     Look for flat shards or holes, a lean in the side view, a smeared or wrong back, and
     missing parts. See "When a model looks wrong" below. If the script exits with code 3,
     Blender is not installed: say so and ask the user to open the `.glb`.
   - **Sound:** you cannot listen to it. Report the path and the prompt, and let the user judge.
4. **Retry at most twice** on your own. A mesh run takes minutes of the shared GPU; after two
   failed attempts, show the user what you have and ask.
5. **Report** each file's path, its seed and settings, and for a model the preview sheet's
   path. Everything generated is a draft: a person decides what goes into a game (the guide's
   "Into a game" covers glTFast, `.meta` files and the LFS quota).

## Settings for 3D models

| The user wants | Add to `comfy.py run mesh --set image=<png>` |
|---|---|
| A normal prop (default) | nothing: about 50,000 triangles, 2048 px textures |
| A light, low-poly prop | `--set faces=20000 --set decimation=qem --set texture_size=1024` |

Below 50,000 faces, always add `decimation=qem`; without it the mesh breaks into shards.

Higher settings were tested and barely show: 700,000 faces with 4096 px textures and normal
map looked only slightly crisper up close, at five times the file size, and raising
`94.target_resolution` to 2048 took 14 minutes and broke the mesh. Don't raise them unless the
user asks, and then say what it costs.

## When a model looks wrong

| In the preview | Likely cause | Do this |
|---|---|---|
| Flat shards, spikes, holes | decimation below 50,000 faces without `qem` | rerun with `--set decimation=qem`, or with more faces |
| The object leans about 10 degrees in the side view | normal for this generator, even from a low camera | don't regenerate for it; tell the user to stand it upright in Blender or Unity |
| The back is smeared or wrong | the image showed too little of the object | remake the image from a three-quarter view |
| Parts missing or cut off | the object touched the image's edge | remake the image with the whole object in frame |
| Correct but soft or blurry, especially up close | the generator's own limit | tell the user; higher settings barely change it (see above) |
