# Using the asset server: guide for an AI agent

For a Claude Code, Codex or other agent session on **any machine that asks the server for
assets**: the lead's laptop, a teammate's machine, or the RTX 5080 machine itself. It covers
connecting, making images, 3D models and sound effects, judging them, and handing them to a
game. Read all of it before your first request.

Claude Code and Codex also have this as a skill, `comfyui-assets` (`/comfyui-assets` in
Claude Code, `$comfyui-assets` in Codex), which they pick up by themselves when asked for an
asset. The skill is the procedure; this guide stays the reference it points to. It lives in
[`.agents/skills/comfyui-assets/`](../../.agents/skills/comfyui-assets/SKILL.md), with a
pointer in `.claude/skills/` for Claude Code.

Setting the server up is a different job, on the RTX 5080 machine only:
[AGENT_SETUP.md](AGENT_SETUP.md). Never follow it from a client machine.

## Two servers

`comfy.py` reaches either of two servers. `COMFY_URL` in `tools/comfyui/.env` decides which, and
`check` names the card it found, so run `check` before anything else and read that line.

| | The RTX 5080 machine | The home server |
|---|---|---|
| `check` names | `NVIDIA GeForce RTX 5080` | `NVIDIA GeForce RTX 4070 SUPER` |
| Address | its LAN address on port 8289 | `http://192.168.1.4:8289` |
| VRAM | 16 GB | 12 GB |
| ComfyUI | v0.39.1 in Docker | v0.39.1 in Docker |
| In front of it | the gateway in this folder | a gate that queues every job, and gives the card to ComfyUI or to Ollama, one at a time |
| Set up from | [AGENT_SETUP.md](AGENT_SETUP.md) | its own repository, not this one |

**Since 21 November 2026 the lead's laptop points at the home server.** Everything below was
written for the RTX 5080 machine and holds for the home server too, except where
[On the home server](#on-the-home-server) says otherwise. Read that section whenever `check` names
the RTX 4070 SUPER.

## What is on the other end

The RTX 5080 machine runs ComfyUI v0.39.1 in Docker, behind a gateway that wants a token.
You reach it with one script, `tools/comfyui/comfy.py`, which needs Python 3.10 or newer and
nothing else. You cannot start, stop or inspect the server from here, and you never need to:
everything goes through `comfy.py`.

| Workflow | Makes | Typical time on the RTX 5080 |
|---|---|---|
| `image` | a 1024x1024 PNG from text (Z-Image Turbo) | about 7 s cold, 2.5 s with the model loaded |
| `mesh` | a textured `.glb` from one image (Pixal3D) | about 3 minutes |
| `sfx` | a stereo MP3 from text (Stable Audio 3 Medium) | about 20 s for a short clip |

## Rules

1. **The token is secret.** `comfy.py` reads `COMFY_API_TOKEN` from `tools/comfyui/.env` by
   itself. Never open, print, `cat` or echo that file or the token, never put the token in a
   URL, a command line, a commit or a chat, and never ask the user to paste it into the chat.
2. **Only `comfy.py` talks to the server.** No `docker`, `ssh` or remote commands aimed at
   the RTX 5080 machine; it also runs the lead's own ComfyUI, which nothing may touch.
3. **Ask for what the user wants, one asset at a time.** Each job takes the server's GPU;
   don't batch dozens of variations unprompted.
4. **Everything generated is a draft.** Say where each file was saved and let a person decide
   what goes into a game.
5. **Don't change `workflows/` or `models.json` to make a run work.** If a run reports a node
   or model problem, run `validate` and report what it says. A fix goes through a pull request
   into `test`, like any other change ([AGENTS.md](../../AGENTS.md)).
6. **Report failures; don't retry them in a loop.** The troubleshooting table below says what
   each one means.

## First time on a machine

1. Get the repository's current `test` branch (`git pull` if it is already checked out).
2. Look for `tools/comfyui/.env` without opening it: `python tools/comfyui/comfy.py check`
   prints `error: set COMFY_URL and COMFY_API_TOKEN ...` if it is missing or incomplete. In
   that case ask the user to create it themselves:
   - copy `tools/comfyui/.env.example` to `tools/comfyui/.env`;
   - set `COMFY_API_TOKEN` to the value in the server's `.env`, carried over by USB stick or a
     password manager;
   - set `COMFY_URL` to the server's gateway. On the lead's home network, or over a VPN into
     it, that is the RTX 5080 machine's LAN address on port 8289, which `setup.ps1` printed
     there. Over Tailscale it is `http://<machine name>:8289`. On the RTX 5080 machine itself
     it is `http://127.0.0.1:8289`.
3. Check the connection:

   ```bash
   python tools/comfyui/comfy.py check
   ```

   Expect `ComfyUI 0.39.1` and a line naming the `NVIDIA GeForce RTX 5080` with its free VRAM.
4. Check the workflows against the server:

   ```bash
   python tools/comfyui/comfy.py validate
   ```

   Expect `image: ok`, `mesh: ok`, `sfx: ok`.

## Making assets

`python tools/comfyui/comfy.py list` prints every workflow's parameters with their defaults;
it works offline. Experimental workflows come last, under their own heading; they run by name
like the others, and `validate` reports on them without failing when their optional models are
not downloaded. A run is:

```bash
python tools/comfyui/comfy.py run <workflow> --set name=value --set name=value
```

It waits for the job, saves every file it made into `tools/comfyui/out/<workflow>/` (or
`--out DIR`), and prints their paths, one per line. Add `--json` for a summary that includes
the seed, so a result can be repeated with `--set seed=...`. Quote any value with spaces:
`--set "prompt=..."`. The `out/` folder is gitignored.

### An image

```bash
python tools/comfyui/comfy.py run image --set "prompt=A stone well with a wooden roof, stylised game asset, centred, whole object visible, plain white background, soft even light"
```

Parameters: `prompt` (required), `seed`, `width` and `height` (multiples of 16, default 1024),
`steps` (keep 8). Open the PNG and look at it before reporting it.

### A 3D model

Always two runs, and always look at the image in between.

1. Make an image of **one object**: whole object in frame, centred, plain white background,
   soft even light, three-quarter view, nothing else in the scene. **If you can generate
   images yourself, as Codex can, do that instead of running the `image` workflow**: the lead
   prefers it, it saves a server round trip, and those models usually follow a prompt more
   closely. Save it as a PNG and pass that path to `mesh`. Claude Code cannot make images, so
   it uses the `image` workflow.
2. Open the PNG. If the object is cut off, has a busy background, or there is more than one
   object, change the seed or the wording and make another. A mesh run costs as much GPU time
   as dozens of image runs.
3. Run the mesh on it:

   ```bash
   python tools/comfyui/comfy.py run mesh --set image=tools/comfyui/out/image/image_00001_.png
   ```

   Use the path the image run printed. The default is about 50,000 triangles with 2048 px
   textures. **For fewer triangles, set the decimation too:**

   ```bash
   python tools/comfyui/comfy.py run mesh --set image=<png> --set faces=20000 --set decimation=qem
   ```

   Below 50,000 faces without `decimation=qem`, the mesh breaks into shards. With `qem` it lands
   somewhat under the budget (about 16,000 to 18,000 for 20,000). For a smaller file, lower
   the textures: `--set texture_size=1024`; the face count hardly changes the file size.
4. Look at the result before reporting it:

   ```bash
   python tools/comfyui/preview_glb.py tools/comfyui/out/mesh/<the .glb>
   ```

   It renders the model with its textures from six angles into one PNG (front, three-quarter
   and side on top; back, top and a close-up below) and prints its path; open that PNG. It
   needs Blender, which it finds by itself, Steam installs included. A broken mesh looks like
   a cluster of flat shards instead of the object. If the script exits with code 3, Blender is
   not installed: tell the user where the `.glb` is and ask them to open it in a glTF viewer,
   Blender or Unity.

What to expect, from tests on the RTX 5080: models often lean about 10 degrees in the side view,
even when the image was taken from a low camera, so the user stands them upright in Blender or
Unity. Textures look soft up close; that is mostly the generator's limit. For a prop the player
sees up close, `--set texture_size=4096` makes close-ups a little sharper (the lead preferred it)
at about 2.3 times the file size. 700,000 faces and lighter remesh smoothing made no visible
difference, and `--set 94.target_resolution=2048` took 14 minutes and broke the mesh. Use the
defaults unless the user asks for more.

Other parameters: `seed`, and `remove_background` (on by default; turn it off only for an image
that already has a transparent background).

### Experimental: a 3D model from four views

When the user has **four consistent views of the same object**, such as their own drawings,
photos or renders, `experimental/mesh_multiview` builds the model from all four instead of
guessing the unseen sides:

```bash
python tools/comfyui/comfy.py run mesh_multiview --set front=<png> --set left=<png> --set back=<png> --set right=<png>
```

- `front`: the object facing the camera. `left`: the camera on the left of the front view, so
  the object's front points to the image's right edge. `back`: seen from behind. `right`: the
  camera on the right, the front pointing to the left edge. Each view at camera height, the
  whole object in frame; backgrounds are removed for you. `fov` (default 20) is the views'
  horizontal field of view in degrees.
- `faces`, `decimation`, `texture_size`, `seed` and `filename_prefix` work as for `mesh`.
- Its model is not in the default download: on a new server, run
  `download_models.py mesh_multiview` first. `validate mesh_multiview` checks it.

Tested on a hand-made model rendered from four sides: the single-view `mesh` invented a second
crossbar and leaned, while `mesh_multiview` rebuilt the right shape, upright, in 80 s instead of
127 s. **Do not feed it views made by the `image` workflow.** Asked for four-view turnaround
sheets, Z-Image drew the front three times, skipped panels and opened a closed lid; none of six
attempts was usable, and inconsistent views make a worse model than one good image.

An agent that generates images itself, such as Codex, may try making the four views with its
own model. The [8 October sword study](../../docs/llm-wiki/asset-quality-2026-10-08.md) tested
reference-guided views from Codex's image generator. Multiview kept the straight blade and
hilt closer to those references than the single-view recipes, but did not preserve all fine
detail. Check every view before running: the same object with the same details, the requested
side, the whole object in frame. If any view disagrees, use `mesh` on the best single view
instead.

For **long, thin props with ornate small parts**, check how many pixels those parts retain
when the reference fits into the generator's 1024 square input. In the sword study, a whole
sword left the grip about 31 pixels across. New close-up hilt references gave it about 110;
generating that component separately and fitting it to an authored blade visibly preserved
more decoration. This changed both source detail and component scope, so it is a tested
recipe for that sword rather than an isolated cropping result.

The study ran 16 jobs with fresh and repeated controls. Raising normal bake resolution
from 2K to 4K, doubling texture steps, reducing bake distance or changing smoothing did not
convincingly recover the whole-sword engraving. A normal map already exists in the exported
GLB. For the close-up hilt, 2K colour/material maps looked close to 4K at the comparison size
and reduced the GLB by about 53%; 20K faces with QEM kept the hilt intact but saved only about
5% of its file size at fixed 4K textures. Choose resolution by actual viewing distance, and
use the report's conditions and limitations when interpreting these results. Existing
workflow defaults remain unchanged.

### A sound effect

```bash
python tools/comfyui/comfy.py run sfx --set "prompt=Short metallic sword strike against a steel shield, bright ringing decay, dry outdoor space. Length: 2 seconds" --set seconds=2
```

Write one or two dense sentences: the source, its material, the space it is in and how the
sound changes over time, ending with the length; set `seconds` to match. Impacts and clicks
take 1 to 3 s, actions such as footsteps 3 to 6 s, ambience 6 to 15 s. `quality` is `V0`
(default), `128k` or `320k`. You cannot listen to the result: say where it is and let the user
judge it.

### Several assets in a row

The server unloads its models after every job, so the lead's own ComfyUI gets the GPU back.
For a batch of the same workflow, add `--keep-loaded` to every run except the last, or run
`python tools/comfyui/comfy.py free` at the end.

## On the home server

What differs when `check` names the RTX 4070 SUPER. Checked on 21 November 2026, from the lead's
laptop at home, with one job of each kind.

**`validate` reports one problem, and that is expected:**

```
image: 1 problem(s)
  node 28 (UNETLoader): unet_name='z_image_turbo_nvfp4.safetensors' not on the server; is the model downloaded?
mesh: ok
sfx: ok
```

The image workflow names the NVFP4 file, which this card cannot use. The home server has the int8
file instead, so **every image run names it**:

```bash
python tools/comfyui/comfy.py run image --set 28.unet_name=z_image_turbo_int8_convrot.safetensors --set "prompt=..."
```

That is a parameter on the command line, not a change to `workflows/`, so rule 5 stands. `mesh` and
`sfx` run as written above. `validate` also says the experimental `mesh_multiview` model is not
downloaded: don't run that workflow here.

**Times, one job each:**

| Workflow | Here | Notes |
|---|---|---|
| `image` | 26 s with the model not loaded | |
| `sfx` | 6 s for a 2-second clip | |
| `mesh` | 3 min 48 s at the defaults | 49,917 triangles, 2048 px textures, an intact model; uses nearly all 12 GB of VRAM |

**Ollama and ComfyUI take turns on the one card.** The gate unloads Ollama's models by itself when
a job arrives, and a model request then waits for the job to end. So never start a job while a
live test, a game or a run is using a model on this server: the framework gives up on a decision
after 30 s, long before a mesh is done. After a job, `warm` the model again before a run
([the Ollama guide](../ollama-server/AGENT_USAGE.md#on-the-home-server)).

**Look without a token.** The gate's status page at `http://192.168.1.4:8291/`, and `/status.json`
beside it, say who holds the card, what is running and who is waiting. Read it before a mesh run
and when a run fails. Other people at home use this card too, each with their own key.

**Failures read differently**, because `comfy.py` words them for the RTX 5080's gateway:

| `comfy.py` says | Means here | What to do |
|---|---|---|
| `HTTP 503 from /prompt: ...` | The card is busy and your turn did not come within 120 s; the text names who holds it | Wait for them, then run once more; never retry in a loop |
| `cannot reach ... (timed out)` on a run, when `check` works | The same: the job waited at the gate for longer than `comfy.py` waits, 60 s | Read the status page, then as above |
| `cannot reach ...` on `check` too | The home server is off, or this machine is not on the home network or the VPN | Ask the user |
| `the gateway rejected the token` | The token here is not the `agenerela` client's key on the gate, or was replaced there | Ask the user to set it again themselves in `tools/comfyui/.env`. Never ask for the token |
| `the gateway is up but ComfyUI is not answering` | ComfyUI is restarting; the status page says when it is up | Wait, then `check` again |
| a node failed with `out of memory`, or a job ended as `oom` | The job needed more memory than this server has | Tell the user, with the time; don't run the same job again |

The server's logs are read by the user, on the server.

## Into a game

- Copy only the files a game will use, into that game's own Unity project under `Demos/`, never
  into the framework package.
- `.glb` imports only with the glTFast package (`com.unity.cloud.gltfast`), added to that
  game's `Packages/manifest.json`, never to the framework (hard rule 6). `.png` and `.mp3`
  import as they are.
- Every new asset needs its `.meta` committed with it: the project must be opened in Unity
  before committing ([AGENTS.md](../../AGENTS.md)).
- `.glb`, `.png` and `.mp3` go through Git LFS, whose free quota is about 1 GB for the whole
  repository. Prefer `faces=20000 decimation=qem` and `texture_size=1024` for props.

## When something goes wrong

On the RTX 5080 machine. The home server's table is in
[On the home server](#on-the-home-server).

| `comfy.py` says | Meaning | What to do |
|---|---|---|
| `error: set COMFY_URL and COMFY_API_TOKEN` | no `.env`, or a line is empty | "First time on a machine", step 2 |
| `cannot reach ...` | the RTX 5080 machine is off, asleep or rebooted, the VPN is down, or `COMFY_URL` is wrong | Ask the user to check the machine is on and, after a reboot, to start Docker Desktop there; the stack comes back by itself. On a VPN, check it is connected |
| `the gateway rejected the token` | the two `.env` files hold different tokens | Ask the user to copy the token over again |
| `the gateway is up but ComfyUI is not answering` | ComfyUI is still starting | Wait a minute, then `check` again |
| `... is not exposed through the gateway` | `comfy.py` asked for a route the gateway blocks | Report it; never work around the gateway |
| `the server refused the workflow` / `does not exist on this server` / `not on the server` | the workflow and the server disagree | Run `validate` and report its output; don't edit the workflow |
| a node failed with `out of memory` | the lead's own ComfyUI is using the GPU | Tell the user; try again when they say it is free |
| `gave up after 1800 s` | the job is still running on the server | Tell the user; a mesh on a busy GPU can take longer. Raise `--timeout` only if they agree |

For anything else, report the exact message. The server's logs are only on the RTX 5080
machine, and only the user can read them there.

More detail on the server, its security and its maintenance is in [README.md](README.md).
