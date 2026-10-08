# ComfyUI asset server

Generates 3D models, sound effects and images for the demo games on a separate GPU
machine, and lets Claude Code or Codex ask for them from any other machine.

```
 Machine running Claude Code / Codex          RTX 5080 machine (Docker Desktop)
 -----------------------------------          ----------------------------------------------
 python tools/comfyui/comfy.py  --- HTTP -->  :8289  gateway  (Caddy: checks the token,
                                                     |         passes API routes only)
                                                     v
                                              agenerela-comfyui  (ComfyUI v0.39.1, GPU)
                                                     ^
 a browser on the RTX 5080 machine ---------> 127.0.0.1:8288  web UI (this machine only)

 Your other ComfyUI keeps its own container or folder, Python and port. Nothing here touches it.
```

Setting it up with an AI agent on the RTX 5080 machine? Point the agent at
[AGENT_SETUP.md](AGENT_SETUP.md): the same steps as an exact runbook, with the rules that
keep your other ComfyUI untouched.

## What it makes

| Workflow | Input → output | Models it downloads |
|---|---|---|
| `image` | text → PNG | Z-Image Turbo, int8 (12.2 GB) |
| `mesh` | image → `.glb` with PBR textures, AO and a baked normal map | Pixal3D int8 with the TRELLIS.2 VAEs, DINOv3, MoGe-2, BiRefNet (10.0 GB) |
| `sfx` | text → stereo `.mp3` | Stable Audio 3 Medium (10.4 GB) |

Text to 3D model is two runs: `image`, look at the picture, then `mesh`. All three
workflows are converted from ComfyUI's own templates and use only nodes built into
ComfyUI, so there are no custom nodes to install or keep updated.

## Kept apart from your other ComfyUI

- **Its own names in Docker.** Compose project `agenerela-comfyui`, containers
  `agenerela-comfyui` and `agenerela-comfyui-gateway`, image `agenerela-comfyui:v0.39.1`,
  volume `agenerela-comfyui_models`. If your other ComfyUI also runs in Docker, it keeps its
  own container, image and volumes: `docker compose` run in this folder manages this stack
  only, and `setup.ps1` lists your other containers and stops if a name or port clashes.
- **It sees only what is mounted:** the `data/` folder here, and this folder's scripts and
  workflows read-only. Your other ComfyUI's folders are never mounted, so it cannot read or
  change them.
- **Its own Python, PyTorch and ComfyUI** inside the image. Updating one never changes
  the other.
- **Its own ports:** 8288 for the web UI and 8289 for the gateway. A ComfyUI install
  usually sits on 8188 (portable or Docker) or 8000 (desktop app). The different address
  also keeps the two web UIs' browser settings apart.
- **One thing is shared: the GPU's 16 GB of VRAM.** `comfy.py` unloads this server's
  models after every job, so your other ComfyUI gets the card back. Don't run jobs on both
  at the same moment.

Docker Desktop itself is shared too, so a few commands reach every container on the
machine. **Never run** `docker system prune` or `docker container|image|volume prune`: they
delete other projects' stopped containers, images or volumes. And remember that
`wsl --shutdown`, `wsl --update`, or quitting, restarting or updating Docker Desktop, stops
every running container, your other ComfyUI included.

## Set up the RTX 5080 machine

Windows 10 or 11 with Docker Desktop (Linux: see [the end](#linux-server)). You need
roughly 45 GB free on the drive where Docker Desktop keeps its data, which is C: unless you
move it: about 33 GB of models plus the image.

**1. Check the NVIDIA driver.** In PowerShell:

```powershell
nvidia-smi
```

The top right of the table must say `CUDA Version: 13.0` or higher (newer drivers label it
`CUDA UMD Version`). If it is lower, update the driver (NVIDIA App, or nvidia.com).

**2. Install WSL 2 and Docker Desktop.** If Docker Desktop is already installed, for
example because your other ComfyUI runs in it, skip to step 3. Do not reinstall or update it
now: that stops every running container. Otherwise, in PowerShell opened *as administrator*:

```powershell
wsl --install
```

(If WSL is already installed, run `wsl --update` instead.) Restart Windows. Then install
[Docker Desktop](https://www.docker.com/products/docker-desktop/), keep the WSL 2 option
ticked, start it and wait until it shows *Engine running*. In its settings, turn on
*Start Docker Desktop when you sign in* if you want the server back after a reboot.
Docker Desktop is free for personal use, education and small businesses.

**3. Prove Docker can use the GPU:**

```powershell
docker run --rm --gpus all nvidia/cuda:13.0.3-base-ubuntu24.04 nvidia-smi
```

It should print the same table as step 1, naming the RTX 5080. If it fails, see
[Troubleshooting](#troubleshooting) before going on.

**4. Get this folder onto the machine.** Clone the repository's `test` branch, or copy
`tools/comfyui/` across:

```powershell
git clone --branch test https://github.com/agenerela/agenerela.git
```

Then open PowerShell in `agenerela\tools\comfyui`.

**5. Run the setup check:**

```powershell
powershell -ExecutionPolicy Bypass -File .\setup.ps1
```

It lists the containers already on the machine, names your other ComfyUI, and stops with
FAIL if one of this stack's container names or ports is already taken. It never changes
another container. It also creates `.env` with a random 64-character token, creates
`data\`, and prints the addresses other machines can use. `-ExecutionPolicy Bypass` applies
to this one run and changes no system setting. Fix anything marked FAIL (for a port, pick
another one in `.env`) and run it again.

**6. Build the image.** This downloads Python, PyTorch and ComfyUI, so the first build takes
a while:

```powershell
docker compose build
```

**7. Check that the image sees the GPU:**

```powershell
docker compose run --rm comfyui python -c "import torch; print(torch.cuda.get_device_name(0))"
```

It should print `NVIDIA GeForce RTX 5080`.

**8. Download the models** (about 33 GB). Interrupted downloads resume when you run it
again, and every file is checked against its SHA-256:

```powershell
docker compose run --rm comfyui python /opt/agenerela/download_models.py
```

To fetch one group only, name it: `... download_models.py sfx`. `--list` shows the groups.

**9. Start the server:**

```powershell
docker compose up -d
```

`docker compose ps` should show `agenerela-comfyui` and `agenerela-comfyui-gateway`
running; the first turns *healthy* after a minute or two. Open
<http://127.0.0.1:8288> in a browser on this machine to see the web UI.

**10. Check the workflows** against this server before connecting anything else. This runs
`comfy.py` inside the container, so the machine needs no Python of its own:

```powershell
docker compose exec -e COMFY_URL=http://127.0.0.1:8188 -e COMFY_API_TOKEN=local comfyui python /opt/agenerela/comfy.py validate
```

Each workflow should print `ok`. [AGENT_SETUP.md](AGENT_SETUP.md), steps 8 and 9, has a test
job for each workflow and a check of the gateway.

## Connect from the machine running Claude Code or Codex

1. In `tools/comfyui/` on that machine, copy `.env.example` to `.env` and fill in two lines:
   - `COMFY_API_TOKEN`: the same value as in the server's `.env`. Carry it over by USB stick
     or a password manager, not through a chat.
   - `COMFY_URL`: one of the addresses `setup.ps1` printed, such as
     `http://192.168.1.50:8289`.
2. Check the connection, then check the workflows against the real server:

   ```bash
   python tools/comfyui/comfy.py check
   ```

   ```bash
   python tools/comfyui/comfy.py validate
   ```

   `check` prints the ComfyUI version and the GPU; `validate` prints `ok` per workflow, or
   names the node and input that does not match the server. See
   [When validate complains](#when-validate-complains).
3. Make a first sound:

   ```bash
   python tools/comfyui/comfy.py run sfx --set "prompt=Heavy wooden door creaking open slowly on iron hinges, stone hallway, close-up. Length: 3 seconds" --set seconds=3
   ```

If `check` cannot reach the server, it is usually the Windows firewall on the RTX 5080
machine. The first time Docker Desktop publishes a port, Windows may ask whether to allow
*Docker Desktop Backend*: allow **private** networks only. That machine's network must also
be set to *Private* in Windows settings. If it never asked, open PowerShell there as
administrator and run:

```powershell
New-NetFirewallRule -DisplayName "Agenerela ComfyUI gateway" -Direction Inbound -Protocol TCP -LocalPort 8289 -Action Allow -Profile Private
```

## How it is exposed

- **ComfyUI has no login.** Anyone who reaches its port can queue any job on the GPU, so
  its own port is bound to `127.0.0.1`: only a browser on that machine can open it.
- **The gateway is the only door.** It answers 401 to any request without the token, and
  passes only the routes `comfy.py` uses: queue a job, poll it, download results, upload an
  input image, list nodes and models, free VRAM. The web UI, its settings and everything
  else answer 403. The token is 64 random hex characters, kept in `.env` files that git
  ignores, and Caddy's log redacts it.
- **Traffic is plain HTTP.** On your home network that is fine. On a network you do not
  trust, such as campus Wi-Fi, use [Tailscale](https://tailscale.com): install it on both
  machines with the same account and set `COMFY_URL=http://<machine name>:8289`. The link is
  encrypted and works from anywhere without opening anything on your router.
- **Never forward the port on your router** or otherwise expose it to the internet.
- **If the token leaks,** delete the server's `.env`, run `setup.ps1` again (re-apply any
  port changes you made), run `docker compose up -d`, and update the client's `.env`.
- **The machine must be awake.** Windows sleep stops everything; for remote use, set sleep
  to *Never* while plugged in.

## Using it

```bash
python tools/comfyui/comfy.py list
```

`list` shows each workflow and its parameters with their defaults. A run looks like:

```bash
python tools/comfyui/comfy.py run image --set "prompt=..." --set seed=1234
```

- `run` waits for the job, downloads every file it saved into `tools/comfyui/out/<workflow>/`
  (or `--out DIR`), and prints their paths one per line. `--json` prints a summary instead,
  including the seed used, so a result can be reproduced with `--set seed=...`.
- An input that is not a declared parameter can still be set as `node_id.input`, for
  example `--set 3.steps=10`. Node ids are in `workflows/*.json`.
- After every job the server unloads its models. For a batch, add `--keep-loaded` to every
  run except the last, or run `comfy.py free` at the end.
- The first job of each workflow after a restart is the slowest: its models load first.

### Text to 3D model

1. `run image` with a prompt for **one object**: the whole object in frame, centred, on a
   plain white background, soft even light, three-quarter view, nothing else in the scene.
   For example: *"A weathered wooden barrel with iron bands, game asset, centred, whole
   object visible, three-quarter view, plain white background, soft even studio light"*.
2. Look at the image before going on; a mesh run costs far more than an image run. If it is
   wrong, change the seed or the wording.
3. `run mesh --set image=<that png>`. Background removal is on by default.

The result is a `.glb` with base colour, metallic, roughness, AO and normal maps, at 50,000
triangles. Small props need far fewer: `--set faces=5000`. `--set texture_size=1024` makes
lighter textures. (The template's own defaults were 700,000 faces and 4096 px textures.)

### Sound prompts

Stable Audio 3 does best with one or two dense sentences naming the source, its material,
the space it is in and how the sound changes over time, ending with the length. Set
`seconds` to match.

- *"Short metallic sword strike against a steel shield, bright ringing decay, dry outdoor
  space. Length: 2 seconds"*
- *"Footsteps on gravel, slow walking pace, leather boots, outdoors, close-up. Length:
  5 seconds"*
- *"Quiet village square at night, crickets, soft wind, faint tavern chatter far away.
  Length: 12 seconds"*

Rough lengths: impacts and clicks 1–3 s, actions such as footsteps 3–6 s, ambience 6–15 s.

### Into Unity

- **`.glb`** imports only with the glTFast package (`com.unity.cloud.gltfast`). Add it to
  the demo game's `Packages/manifest.json`, never to the framework package (hard rule 6).
- **`.mp3`** imports as it is.
- Anything copied into a Unity project needs its `.meta` committed with it: open the
  project in Unity before committing ([AGENTS.md](../../AGENTS.md)).
- `.glb`, `.png` and `.mp3` are stored with Git LFS, whose free quota is about 1 GB for the
  whole repository ([Demos/README.md](../../Demos/README.md)). Commit only what a game uses,
  and prefer small textures and face counts.

### For AI agents

- **Setting the server up** is [AGENT_SETUP.md](AGENT_SETUP.md), and its ground rules
  apply: the RTX 5080 machine also runs the user's own ComfyUI, and nothing may touch it.
- Run `comfy.py` as shown above. Never print, echo or log `COMFY_API_TOKEN`, and never put
  it in a URL.
- Start with `check`; if it fails, report that rather than retrying in a loop.
- For a 3D model, always make and inspect the image first, then run `mesh` on it.
- If a run reports a model "not on the server" or a node that "does not exist", run
  `validate` and report what it says. Do not swap in model names that are not in
  `models.json`.
- Generated assets are drafts. Say where each file was saved and let a person decide what
  goes into a game.

## Everyday commands

On the RTX 5080 machine, in this folder:

| To | Run |
|---|---|
| Start | `docker compose up -d` |
| Stop, keeping everything | `docker compose stop` |
| See status | `docker compose ps` |
| Follow ComfyUI's log | `docker compose logs -f comfyui` |
| Follow the gateway's log | `docker compose logs -f gateway` |

These act on this stack only. The containers restart on their own when Docker Desktop
starts, unless you stopped them.

## Where things live

| What | Where | Removed by |
|---|---|---|
| Models | Docker volume `agenerela-comfyui_models`, inside Docker Desktop's disk | `docker compose down -v` |
| Generated files, uploaded images, web UI settings | `data/` here (`COMFY_DATA_DIR`) | deleting the folder |
| ComfyUI, Python, PyTorch | image `agenerela-comfyui:v0.39.1` | `docker image rm agenerela-comfyui:v0.39.1` |
| Token and ports | `.env` here | deleting the file |

Models sit in a volume rather than a Windows folder because a container reads a volume
much faster than a folder shared from Windows. To move Docker Desktop's disk off C:, use
*Settings → Resources → Advanced → Disk image location*.

To remove the stack completely, run `docker compose down -v` in this folder, then the
`docker image rm` command above and `docker image rm caddy:2.11.7-alpine` (Docker refuses if
another container still uses it), then delete `data\` and `.env`. Each step names only this
stack's things, so your other ComfyUI is unaffected throughout.

## Troubleshooting

Fixes marked ⚠ restart WSL or Docker Desktop, which stops every running container on the
machine, your other ComfyUI included. Do them when it is idle.

| Symptom | Fix |
|---|---|
| Step 3 fails with *could not select device driver "nvidia"* | ⚠ Docker Desktop → Settings → General → *Use the WSL 2 based engine*. Then `wsl --update`, update the NVIDIA driver, restart Windows. |
| `agenerela-comfyui` keeps restarting | `docker compose logs comfyui`. *CUDA driver version is insufficient* means the driver is older than CUDA 13.0 (step 1). |
| The gateway keeps restarting | `docker compose logs gateway`. A missing or short token in `.env`: run `setup.ps1`. |
| `check`: *cannot reach* | The machine is asleep or off, the stack is not up (`docker compose ps`), `COMFY_URL` is wrong, or the firewall (see above). |
| `check`: *rejected the token* | The two `.env` files hold different tokens. |
| `validate`: a model is *not on the server* | Download its group (step 8). |
| A job fails with *out of memory* | Your other ComfyUI is holding VRAM. Free it there, or close it. |
| A job dies with *Killed* in the log | ⚠ WSL ran out of RAM. Create `%UserProfile%\.wslconfig` with `[wsl2]` and `memory=24GB` on the next line (adjust to your RAM), then `wsl --shutdown` and restart Docker Desktop. |

## When validate complains

The workflows were converted from ComfyUI's templates and checked against the source code of
ComfyUI v0.39.1. If `validate` names a node or input that does not match, either fix that
entry in `workflows/<name>.json`, or re-export it from the template:

1. In the web UI, open the template from *Templates* (`image_z_image_turbo_int8`,
   `3d_pixal3d_trellis2_image_to_model` or `audio_stable_audio_3_medium`) and check it runs.
2. Use *Workflow → Export (API)*.
3. Replace the `prompt` object in `workflows/<name>.json` with the export, and update the
   node ids under `params` to match. Run `validate` again.

## Adding a workflow

1. Build and test it in the web UI. *Templates* holds many more, such as ACE-Step music and
   Hunyuan3D; those whose names start with `api_` are paid cloud services and are disabled
   here.
2. *Workflow → Export (API)*, and save the file.
3. Use it as it is, with `comfy.py run path/to/export.json --set 6.text=...`, or wrap it the
   way `workflows/sfx.json` does: `{"description": ..., "params": {...}, "prompt": <export>}`.
4. Add its model files to `models.json` (the template lists their URLs) and download them
   with `download_models.py <group>`.
5. `comfy.py validate <name>`.

There are deliberately no custom nodes. If one is ever needed, install it in the
`Dockerfile`: packages installed into a running container vanish when it is recreated.

## Updating ComfyUI

Change the version in the two marked lines of `docker-compose.yml` (and the PyTorch version
in the `Dockerfile` if ComfyUI's README asks for a newer one), then:

```powershell
docker compose build
```

```powershell
docker compose up -d
```

Then run `comfy.py validate` from the client, since node definitions change between
releases.

## Linux server

Install Docker Engine and the NVIDIA Container Toolkit instead of Docker Desktop, and do by
hand what `setup.ps1` does: copy `.env.example` to `.env`, set `COMFY_API_TOKEN` to the output
of `openssl rand -hex 32`, and create `data/input`, `data/output` and `data/user` as your own
user (the container runs as uid 1000). Then continue from step 6. Allow port 8289 through
the firewall from your own network only.

## Licences

- Z-Image Turbo: Apache-2.0.
- Pixal3D, TRELLIS.2, MoGe-2 and BiRefNet: MIT, as published by Comfy-Org. The DINOv3
  encoder comes from Meta, under its own licence.
- Stable Audio 3: the Stability AI Community License, linked from its model card.

Fine for a course project. Read each licence before shipping a game commercially.
