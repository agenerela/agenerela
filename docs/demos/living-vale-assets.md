# Living Vale — generated asset production plan

**Status:** production proposal, 7 October 2026. The user plans a Docker-hosted
ComfyUI setup for models and sound. Nothing is installed, generated or benchmarked
by this document. Windows is the initial game target; visual style is deliberately
unselected. See the [build plan](living-vale.md) and [game design](living-vale-design.md).

## Choose style by a small Unity pilot

Make the same four assets in three candidate styles: a potion, a market stall,
a stone gate module, and a humanoid outfit on a usable shared rig.

| Candidate | What the pilot should establish |
|---|---|
| Stylized low-detail fantasy | Readable shapes, easy cleanup, consistent materials |
| Painted storybook fantasy | Cohesive surfaces and attractive close conversation views |
| Restrained semi-realistic fantasy | Whether detail survives optimization and matches shared characters |

Do not choose a style only because one generated render looks good. Judge the imported
asset in the same lit Unity scene from close chat distance and normal gameplay distance.
Score silhouette consistency, material coherence, usable geometry, cleanup time,
animation suitability and runtime cost. Compare actual accepted outputs, not model
marketing screenshots. No claim is made that any style is generated best.

Approve one compact art reference sheet and reuse its palette, proportions, materials
and negative constraints across batches. Establish repeatable prompt/workflow templates
for houses, props, equipment and characters. Prefer modular construction for villages
and the castle; generate decorative objects or reference designs where helpful.

## What ComfyUI can support, and what must be proved

A plausible production chain is text → approved concept/reference image → 3D mesh →
cleanup and material work → Unity prefab. Treat text-to-3D as a workflow, not a guarantee
that every checkpoint accepts text directly.

As checked on 7 October 2026, ComfyUI documents Hunyuan3D-2 workflows, including
image-conditioned mesh output in GLB. Its tutorial says native support does not yet
cover texture/material generation, so a textured pipeline must separately prove
its selected nodes and dependencies. [Official ComfyUI 3D tutorial](https://docs.comfy.org/tutorials/3d/hunyuan3D-2).

Memory requirements depend on version and workflow. Tencent's Hunyuan3D-2 repository
lists 6 GB for shape and 16 GB for shape plus texture, while ComfyUI's tutorial lists
different figures for its workflows. These are upstream statements, not measurements
on this machine. Choose and test an exact variant; do not assume the predecessor's
8 GB development GPU can run the full texture pipeline.
[Hunyuan3D upstream](https://github.com/Tencent-Hunyuan/Hunyuan3D-2),
[ComfyUI workflow guidance](https://docs.comfy.org/tutorials/3d/hunyuan3D-2).

TRELLIS is an alternative research candidate, not the selected backend: its original
repository recommends image conditioning and lists Linux-tested code and an NVIDIA GPU
with at least 16 GB. Recheck the exact current model/variant if considered.
[Official TRELLIS repository](https://github.com/microsoft/TRELLIS).

Docker isolates dependencies, not GPU memory or model incompatibilities. Docker Desktop's
documented NVIDIA GPU passthrough on Windows uses Linux containers with the WSL 2 backend.
Verify the user's actual GPU/backend before choosing the container stack.
[Docker GPU documentation](https://docs.docker.com/desktop/features/gpu/).

For sound, evaluate a dedicated audio workflow only after testing its actual ComfyUI
nodes/container dependencies. Stable Audio Tools is one upstream inference candidate;
its code license and a downloaded checkpoint's terms are separate. No specific audio
checkpoint is selected here.
[Stable Audio Tools](https://github.com/Stability-AI/stable-audio-tools).

## Container handoff needed for later automation

The user-provided setup should expose a local endpoint and writable output volume.
Before using it, verify:

- ComfyUI version, container image digest, Python/CUDA dependencies, model/checkpoint
  names and checksums, and required node revisions.
- Actual GPU/VRAM, free disk space, supported workflows, output paths and container
  health. Save a smoke-test result from each workflow.
- Host/container path mapping for inputs and outputs; keep model caches and raw output
  outside the Git checkout. Do not mount the whole home directory.
- API-format workflow JSON and exactly which inputs are parameterized: prompt, reference,
  seed, output name and any resolution/detail controls.
- A queue coordinator or agreed generation window so other sessions' GPU work is respected.

ComfyUI's server supports submitting workflows via `POST /prompt`, tracking a
`prompt_id`, receiving progress through `/ws`, and reading `/history/{prompt_id}`.
Use those to submit, wait, inspect failures and collect outputs. File output conventions
vary by node; inspect each workflow instead of assuming every artifact is an image.
[Official server API](https://docs.comfy.org/development/comfyui-server/comms_routes).

Bind the host endpoint locally, pin validated versions, and keep workflow execution in
a production tool outside the game's runtime. Shipping the game must not require Docker,
ComfyUI or asset-generation weights.

## Three-dimensional asset acceptance

Every candidate passes the following production steps before replacing a placeholder:

1. Review concept and silhouette against the approved style.
2. Generate mesh, inspect all sides, remove floaters, hidden junk and broken surfaces.
3. Normalize scale, orientation, origin/pivot and transforms.
4. Retopologize or simplify where required; create usable UVs, bake textures and verify
   URP material maps. A visually impressive mesh is not automatically game-ready.
5. Produce LODs and simple colliders. Modular walls/gates must meet exact dimensions;
   traversable terrain and navigation surfaces are intentionally authored.
6. For humanoids, prove skinning, hands, outfit deformation, weapon attachment and a shared
   skeleton. Test idle, walk/run, attack, block, stagger and incapacitation.
7. Convert the accepted result to a verified Unity import format such as FBX plus textures;
   GLB source output does not imply a built-in Unity importer is available.
8. Import in the pinned editor, inspect materials/scale/animation, profile, and make a
   prefab with gameplay components. Unity generates and preserves its metadata.

Early candidates are props, containers, signs, stones and outfit concepts. A single
unrigged generated humanoid must not block the playable game: start from a usable shared
rig or a placeholder. Licensing and source provenance are checked for every base asset.

Proposed initial review budgets: ordinary props under roughly 5,000 triangles, major
modular pieces under 15,000, humanoids around 20,000–35,000 at LOD0; mostly 1K textures,
2K only where close views justify it. These are adjustable review thresholds, not proven
hardware limits. Avoid giving every prop several unique high-resolution materials.

## Audio production

Prioritize reliable short effects: footsteps by surface, weapon swing/impact, block,
dodge, potion, item pickup, quest completion, UI feedback, and environment loops
(wind, birds, water, village work). Two quiet music loops can support village and danger.

Generate several candidates, then trim, remove clipping/unwanted speech, normalize
loudness, inspect duration and create seamless loops where required. Audition transitions
inside Unity, not only in an audio player. Use mixer groups for UI, effects, ambience
and music; subtitles/text convey all important information.

Runtime NPC speech adds latency, pronunciation, voice consistency and cancellation work.
Keep it optional after text chat works. Offline voiced greetings are an inexpensive
alternative, but should never claim to narrate a dynamic generated response.

## Asset manifest, storage and reproducibility

For each accepted asset record: stable asset ID, intended use, generator/checkpoint,
workflow and node revisions, seed, prompt, reference IDs, source/output hashes, applicable
license/attribution records, cleanup/export steps, Unity destination, triangle/material/
texture counts or audio format/duration, review status and any regeneration notes.

Commit recipes and compact manifests. Raw candidates, generation caches and checkpoint
weights stay outside the repository. Final art/audio uses the existing Git LFS patterns.
Before committing an extension not already covered (for example GLB), add its LFS rule
first or convert to a covered final format; do not accidentally commit a large binary
into ordinary Git history.

A seed alone does not guarantee identical outputs across GPU/runtime versions.
Preserve accepted final files and hashes. Once Unity creates an asset's `.meta`,
replacement should preserve the same asset path and GUID.

## Generation and runtime inference share the GPU

Run generation as offline production. Stop only this task's generation jobs and unload
its models before measuring the RPG's runtime model. Coordinate before changing another
session's model or container. On an 8 GB-class GPU, assume concurrent generation plus
Unity plus inference needs investigation rather than promising it will fit.

The asset pilot's exit gate is one accepted prop, one working modular piece, one animated
character/outfit solution, and one clean audio effect in a Windows player build. If the
pipeline fails, retain placeholders or compatible sourced assets and continue gameplay.
Record measured generation time and memory in the wiki; any quality/accuracy comparison
must state its conditions and control rather than generalizing from one attractive asset.
