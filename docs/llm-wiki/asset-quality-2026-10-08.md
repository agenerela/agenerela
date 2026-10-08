# Sword asset quality experiments — 8 October 2026

This report tests the ComfyUI asset pipeline, not language-model decision accuracy.
Generated references, GLBs, raw job records and Blender renders stay under the gitignored
`.scratch/sword-quality-tests/` and `.scratch/swords/` folders in this checkout.

## Question

The gothic longsword's source image showed crisp silver engraving and leather wraps, but
the GLB looked smeared and the black onyx inset became a hollow-looking recess. Does
raising normal-map resolution fix it? Do bake distance, texture sampling, decimation,
smoothing or closer references help more?

## Conditions and method

- Server: ComfyUI 0.39.1, NVIDIA GeForce RTX 5080, Python 3.13.16,
  PyTorch 2.14.1+cu130. Access is through the repo's `comfy.py` client only.
- Workflow: checked-in `mesh_multiview`, Pixal3D multiview int8 with TRELLIS.2 shape and
  texture decoders. No model or committed workflow changes.
  Two additional recipe comparisons use the checked-in single-view `mesh` workflow.
- Common settings: seed `49081001`, 50,000 target triangles, midpoint decimation,
  4096 px colour/material textures, 2048 px normal map, default 20 degree FOV.
- Jobs run sequentially; each job unloads models at completion.
- Full-sword cases use exactly the same four PNG files. Close-up cases use a second set
  of four references, with the whole hilt and a short blade stub filling the frame.
  They are new reference-guided image generations, so this changes both framing and
  the available source detail; it does not isolate cropping alone.
- A fresh control runs in this session. Setting comparisons change one conceptual factor
  at a time. Repeated controls check how stable a fixed seed is.
- All GLBs are imported in Blender 5.2.2 LTS and rendered using identical Cycles lighting,
  camera rules and 48 samples. Each comparison shows the whole object, front hilt,
  three-quarter hilt and reverse hilt. File dimensions and triangle counts are read from
  actual exports.
  Each model also passes through the repo's six-angle `preview_glb.py`; sheets were opened
  to check sides, backs, missing parts and shards, rather than judging only the front.
- This is a small qualitative study of one design, not a blinded or statistical quality
  benchmark. A larger normal map is not automatically a sharper model.
- Job durations include upload/download and whatever the server recomputed. They are not
  clean cold-inference benchmarks; graph caching and postprocessing affect them.

## Test matrix

| Case | Change from its group's control |
|---|---|
| `full-control` | None; original full-sword references |
| `full-normal4k` | `224.resolution=4096` |
| `full-cage01` | `224.cage_distance=0.01`, from 0.05 |
| `full-texture24` | `12.steps=24`, from 12 |
| `full-qem` | `decimation=qem` at the same face budget |
| `full-smooth4` | `241.smooth_iters=4`, from 20 |
| `full-crease60` | `238.crease_angle=60` and `260.crease_angle=60`, from 180 |
| `full-control-repeat` | Repeat the unchanged control |
| `full-single` | Single-view `mesh` workflow using the identical front PNG |
| `hilt-control` | Same baseline settings, close-up hilt references |
| `hilt-normal4k` | `224.resolution=4096` with close-up references |
| `hilt-texture24` | `12.steps=24` with close-up references |
| `hilt-control-repeat` | Repeat the unchanged hilt control |
| `hilt-texture2k` | `texture_size=2048`, from 4096 |
| `hilt-qem20k` | Practical low-budget recipe: 20,000 target faces and QEM decimation |
| `hilt-single` | Single-view `mesh` workflow using the identical hilt front PNG |

The single-view comparisons change the workflow and conditioning as well as the number
of views. They compare practical recipes; they do not isolate view count alone.

## Why output resolution can mislead

The current workflow crops each reference into a 1024 square, and Pixal3D itself resizes
views to that size. A whole thin sword leaves few pixels across the blade and grip. Its
texture stage synthesizes material voxels, then bakes them into a UV atlas. Increasing the
atlas dimensions does not restore source detail that the conditioning or reconstruction
lost. Normal baking captures the generated high-poly surface, not engraving directly from
the input photograph.

For these references, a conservative nonwhite object bounding box and the workflow's
1.1 padding predict approximately **31 pixels across the full-sword grip versus 110
across the close-up grip**, and **218 versus 840 across the guard**, after fitting to a
1024 square. These are approximate input budgets, not measured texture sharpness or a
claim that all of those pixels survive the encoder. The image foreground was measured
without modifying the images (`reference-budget.json` in the scratch folder).

Implementation references:
[conditioning and texture decoding](https://github.com/Comfy-Org/ComfyUI/blob/v0.39.1/comfy_extras/nodes_trellis2.py),
[baking, UVs and shading normals](https://github.com/Comfy-Org/ComfyUI/blob/v0.39.1/comfy_extras/nodes_mesh_postprocess.py).

## Results

**Sixteen successful server runs, plus one local assembled sword.** The most convincing
visible improvement came from generating the detailed hilt separately, then fitting it
to an authored blade. Tweaking the original whole-sword run did not recover its engraving.

The machine-readable [measurement record](asset-quality-2026-10-08.json) preserves reference,
workflow and GLB SHA-256 hashes, overrides, job IDs, durations, dimensions and actual triangle
counts. MB below means decimal megabytes. Durations are client-reported job times, with the
cache and transfer limitations above.

| Case | Seconds | Exported triangles | GLB MB |
|---|---:|---:|---:|
| `full-control` | 68.5 | 49,996 | 14.14 |
| `full-normal4k` | 69.4 | 50,000 | 19.22 |
| `full-cage01` | 65.9 | 50,000 | 14.53 |
| `full-texture24` | 64.4 | 49,996 | 13.74 |
| `full-qem` | 70.8 | 49,888 | 14.68 |
| `full-smooth4` | 68.1 | 49,996 | 16.55 |
| `full-crease60` | 65.6 | 49,996 | 15.08 |
| `full-control-repeat` | 62.7 | 50,000 | 15.00 |
| `full-single` | 54.7 | 49,896 | 19.05 |
| `hilt-control` | 81.4 | 49,986 | 26.98 |
| `hilt-normal4k` | 91.9 | 49,998 | 31.74 |
| `hilt-texture24` | 85.4 | 49,980 | 26.25 |
| `hilt-control-repeat` | 85.1 | 49,988 | 26.77 |
| `hilt-texture2k` | 76.6 | 49,986 | 12.57 |
| `hilt-qem20k` | 83.5 | 19,934 | 25.53 |
| `hilt-single` | 67.4 | 49,936 | 25.47 |

### What changed visibly

| Test | Observation at matching render size | Practical consequence |
|---|---|---|
| Separate hilt references | Clearer silver vines, visible leather wrap seams and a solid-looking onyx inset. Full-sword controls keep the smeared guard and hollow-looking pommel. | For long, thin ornate props, try detailed components before spending more on whole-object settings. |
| Normal bake 2048 → 4096 | No convincing engraving improvement in either group. Full GLB grew about 36%; hilt about 18%. | The existing normal map already captures reconstructed detail. Leave it at 2K unless a close-up shows a benefit. |
| Bake cage 0.05 → 0.01 | Did not visibly rescue the whole-sword hilt. | A smaller cage is a projection adjustment, not a source-detail fix. Test it when a bake has specific projection artifacts. |
| Texture sampling 12 → 24 steps | No convincing detail gain in either group. | More sampling was not a useful default here. These cached job times do not establish its inference cost. |
| QEM at 50K | Intact model, no clear detail gain over midpoint here. | Keep QEM mandatory below 50K as the existing guide requires; this is not evidence that midpoint is always safe. |
| Smoothing 20 → 4 iterations | Full sword still soft; no convincing recovery of fine engraving. | Do not use reduced smoothing as a substitute for better conditioning. |
| Crease angle 180 → 60 degrees | No convincing hilt detail gain. | Shading normals can change highlights and edges but cannot recreate missing decoration. |
| Hilt textures 4096 → 2048 | Decoration and silhouette remain close at the 768 px comparison size; finer texture differences may matter closer. 26.98 → 12.57 MB, about 53% smaller. | Texture size is the effective file-size control in these outputs. Choose by intended screen coverage. |
| Hilt 50K midpoint → 20K QEM | Intact at 19,934 triangles, similar silhouette and decoration at comparison size. 26.98 → 25.53 MB, only about 5% smaller with textures held at 4K. | Faces control geometry cost; textures dominate this GLB's storage. This arm changes two settings and tests a usable recipe. |
| Full sword single-view | Blade visibly curves in the side view; texture remains smeared. | Prefer the consistent multiview references for this straight sword. A front render alone hides the failure. |
| Hilt single-view | More faceted grip, softer wrap detail, and invented pommel decoration on narrow sides. | Multiview better matches this set of references. Neither workflow preserves every source engraving. |

### Repeatability and limits

Same-seed controls were not identical: full-sword exports had 49,996 versus 50,000
triangles and 14.14 versus 15.00 MB; hilt controls had 49,986 versus 49,988 triangles.
They looked close. The observed variation is consistent with reconstruction/postprocessing
variation, but this study did not isolate its cause. File-size or tiny shading differences
alone are insufficient evidence of a quality gain. Repeats reused the graph and images;
they do not prove cold regeneration is deterministic.

The close-up result changes reference framing, generated source detail, component scope
and triangle allocation. It is a successful production recipe for this design, not a
measurement of cropping alone. Neither the sabre nor the bronze sword was subjected to
this setting matrix. There are no numerical perceptual scores or claims of general
statistical superiority. Inspecting the exported atlas and rendered GLB confirmed that
the softness exists in the asset; this study did not audit the user's Blender viewport.

## Recommended workflow for these swords

1. Design the silhouette and material separation first. Make a clear front reference and
   use it to guide the other three views. Verify matching guard tips, pommel faces, insets,
   grip length and blade thickness. Keep the complete object inside each frame with a
   small margin, a plain background and consistent scale and camera height. Do not feed
   an entire four-panel sheet into one view input.
2. Check detail at the generator's effective 1024 square input. A tall blade can consume
   nearly all the height while leaving the ornate grip only tens of pixels across. If
   that happens, generate the hilt or other complex component separately. Model long,
   simple precision parts such as a straight blade locally, with real edge bevels and a
   fuller, then assemble them. This also avoids asking image reconstruction to maintain
   a straight blade from a single view.
3. Start from the existing defaults: 50K faces, 2K normal bake and 12 texture steps. Use
   2K colour/material maps for the first draft, then compare 4K at the actual gameplay
   or inspection distance. More pixels, faces or steps are options to test, not promises
   of more source detail. Use `faces=20000 decimation=qem` when reducing geometry.
4. Inspect the generated mesh before baking more detail: the side silhouette, thickness,
   recessed versus solid insets, back decoration and contact between parts. More texture
   resolution cannot fill an unintended hole in the geometry.
5. Evaluate materials under neutral studio lighting and a moving highlight. Confirm that
   base colour, roughness/metalness and the normal map are connected. Keep colour maps in
   sRGB and data maps in Non-Color when assigning them manually. Use the tangent-space
   convention expected by the target renderer; this bake produces OpenGL (+Y) normals.
   The GLB import connects the material automatically. Bake fine authored relief into the
   normal map when the relief is real, and retain geometry for details that alter silhouette.
6. Compare changes with the same references, seed, camera, lights and output size. Repeat
   the control before interpreting subtle wins, record hashes and overrides, and avoid
   treating cached wall time as an inference benchmark. Check all six angles before choosing.

These are recommendations supported by this design and the pipeline implementation, with
the reference consistency, material wiring and authored-detail advice following directly
from how the workflow conditions and bakes its outputs. They are not all independently
tested variables in this matrix.

## Revised gothic sword and retexturing

The revised draft is `.scratch/swords/gothic-v2/final/gothic_sword_v2.glb`, with an editable
`.blend` alongside it. It combines `hilt-normal4k` with a locally authored straight steel
blade: narrow edge bevels, a shallow fuller and a separate metallic material. It is 1.23 m
tall, 50,606 triangles and 31.46 MB, with embedded 4096 maps on the hilt. The 4K normal
variant was retained as the high-resolution draft; the tests do not establish that it
looks better than the 2K normal hilt. The blade uses geometry and a clean material rather
than preserving the old smeared blade atlas. The hilt is still an unoptimized generated
mesh, and its finest engraving remains softer than the source image.

`original-vs-revised.png` in the test scratch folder shows the fresh full-sword control
on the left and the assembled revision on the right under the matching studio render rules.
The six-angle export inspection found an intact hilt and straight blade. This is a new
assembled asset, **not a retexture of the exact original mesh**. It has not been integrated
or validated inside a game.

For preserving an existing mesh, the current registered workflows accept images and
generate a new mesh; they do not expose a mesh-in/retexture-out workflow. A practical local
Blender route is to retain its mesh and UVs, replace or paint the base-colour and material
maps, model/sculpt missing relief on a high-poly copy, then bake that relief onto the
original low-poly mesh using a checked cage and UV padding. A reusable steel material can
replace the old blade material directly if the blade has its own material assignment.
Material replacement can run on this machine; it does not require sitting at the RTX 5080.
Remote reconstruction uses that GPU through the client. This study tests the assembled
replacement route, not a full texture-painting or high-to-low rebake of the old sword.

## Reproduce and inspect

The exact PNG hashes are in the measurement record. The four full-sword references are
under `.scratch/swords/gothic-longsword/references/`; the close hilt references are under
`.scratch/swords/gothic-v2/references/`. Reusing the filenames with different PNG bytes
would not reproduce these conditions. Images and models are local ignored artifacts;
cloning this branch alone does not include them.

Example control from the repository root (PowerShell):

```powershell
$refs = '.scratch/swords/gothic-longsword/references'
python tools/comfyui/comfy.py run mesh_multiview `
  --set "front=$refs/front.png" --set "left=$refs/left.png" `
  --set "back=$refs/back.png" --set "right=$refs/right.png" `
  --set seed=49081001 --set faces=50000 --set texture_size=4096 `
  --set filename_prefix=agenerela/sword_tests/reproduction `
  --out .scratch/sword-quality-tests/reproduction/model --json
```

Apply the matrix's override to that command for each arm; substitute the hilt reference
folder for the hilt group. For the single-view arms, use `run mesh --set image=<front.png>`
with the same common settings. Run jobs sequentially and keep automatic unloading enabled.

Local experiment scripts in `.scratch/sword-quality-tests/`:

- `run_cases.py all`: all 16 cases through `comfy.py`; skips a case with `run.json` already
  present. Use a fresh output folder/run prefix for genuinely new trials, rather than
  mistaking a skipped result for a rerun.
- `inspect_cases.py`: matching 768 px Cycles whole/front/three-quarter/back renders and
  `inspection.json` per case. Camera spans scale with object dimensions; area light power
  scales with height squared. 48 samples with denoising, neutral world strength 0.7.
- `reference_budget.py`: read-only foreground pixel-budget estimate.
- `record_results.py`: aggregates measurements and hashes into the committed JSON record.
- `make_pair.py`: assembles the Blender-rendered before/after views.

Every case contains `case.json`, `run.json`, `run.log`, `stdout.json`, a `model/` folder,
four studio views and `comparison.png`. Six-angle sheets are in `six-view/`.
The local `build_sword.py` in `.scratch/swords/gothic-v2/` records the blade construction
and assembly. Original sword files remain available for comparison.
