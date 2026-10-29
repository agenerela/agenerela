# Environment Setup

## Unity

**6000.3.23f1** — pinned. Every project's `ProjectSettings/ProjectVersion.txt` is
committed and is what enforces this; CI fails if a project differs from `UnityProject/`.
Nobody upgrades unilaterally: opening a project in a newer patch release rewrites that
file and can silently upgrade serialized assets for everyone.

Open `UnityProject/` for framework work. The framework is an *embedded* package under
`UnityProject/Packages/com.agenerela.framework/`, so it compiles automatically with no
install step.

Each demo game is its own Unity project under `Demos/`, loading the same package from
disk by relative path. [`Demos/README.md`](../../Demos/README.md) covers creating one, and
how to skip downloading the art of games you don't work on.

### Running the EditMode tests without the editor

The same run as *Test Runner → EditMode → Run All*, from a shell. It is useful for checking a
branch, and it is how an agent reviewing a pull request can compile it (`REVIEWING.md`,
step 4). Close any editor that has the same project folder open first: Unity lets one
editor at a time open a project.

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe" -batchmode -nographics -projectPath <checkout>/UnityProject -runTests -testPlatform EditMode -testResults results.xml -logFile unity.log
```

- Exit code 0 means every test passed and 2 means at least one failed. Any other code means
  the run itself failed, most often on a compile error. The counts are on the `<test-run>`
  element of `results.xml`; compiler problems are the `error CS` and `warning CS` lines in
  `unity.log`.
- Do not add `-quit`: `-runTests` quits on its own.
- Give `-testResults` and `-logFile` paths with no `..` in them. Unity rejects each one
  with `.. is not a valid directory name`; use an absolute path instead.
- The first run in a fresh checkout imports every package and takes several minutes. Runs
  after that are much faster.
- To test a branch without touching your own checkout, run it in a worktree:
  `git worktree add --detach <short path> origin/<branch>`. Remove it afterwards with
  `git worktree remove <path>`.
- **Keep that path short on Windows.** Under a deep folder, such as a temp directory, paths
  in `Library/PackageCache` pass 260 characters and third-party package assets (URP, Shader
  Graph, Input System) fail to import with `DirectoryNotFoundException`. The C# still
  compiles and the tests still run, but the log fills with errors that are not the branch's.
  Turning on Windows long paths does not help.

### Running the PlayMode tests that need a model

The Phase 2 gate's tests (`RequiresOllama`) call a real Ollama, so they need it running with
the model pulled; they are the only tests that do. The same command with the other platform
and a category:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe" -batchmode -nographics -projectPath <checkout>/UnityProject -runTests -testPlatform PlayMode -testCategory RequiresOllama -testResults results.xml -logFile unity.log
```

Each timed decision prints a `LATENCY` line. `AGENERELA_OLLAMA_MODEL` and
`AGENERELA_OLLAMA_URL` choose another model or endpoint; the findings entry of 25 October 2026
has the conditions to match before comparing a run with its numbers.

### The Unity CLI (optional)

Unity's own command line, `unity`, is installed on the lead's laptop (1.0.0-beta.8 when
checked on 26 October 2026, a beta that may change). Two commands are useful with no project
change: `unity status` lists the open editors, and `unity test <project> --mode EditMode
--output <xml>` runs the same EditMode tests as the raw command above, choosing the editor
from `ProjectVersion.txt`. Set `UNITY_NO_BANNER=1` and `UNITY_NON_INTERACTIVE=1` when an
agent calls it. Its live editor control (`unity command`, `unity list`) needs the
experimental `com.unity.pipeline` package, which no project here has added: it would change a
committed manifest and overlaps MCP for Unity, below. The `unity-cli` skill is installed per
user, not in this repository.

## Per-machine setup — do this BEFORE cloning

Order matters for the first command.

```bash
# 1. Git LFS. Run this BEFORE `git clone`: it installs the filters that fetch real
#    binaries during checkout. Clone without it and you get pointer files instead.
#    Already cloned? Run `git lfs install` then `git lfs pull` to repair.
git lfs install

# 2. Unity's YAML merge driver (see note below).
git config --global merge.unityyamlmerge.name "Unity SmartMerge"
git config --global merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"'
git config --global merge.unityyamlmerge.recursive binary
```

Then clone, and verify LFS worked:

```bash
git clone https://github.com/agenerela/agenerela.git
cd agenerela && git lfs ls-files      # must list URP.png; empty means LFS was inactive
```

**Why the merge driver needs local config.** The committed `.gitattributes` says
`*.unity merge=unityyamlmerge` — that names a driver but does not say where the
executable is, and the path differs per machine and OS, so it cannot be committed. Each
person configures it once. Scenes and prefabs are YAML with `fileID` cross-references;
git's line-based merge interleaves them into files Unity cannot open or that silently
lose objects. UnityYAMLMerge merges at the object level instead.

**A wrong path fails silently** — git falls back to the line merge with no error. Verify
with `git config --global --get merge.unityyamlmerge.driver` and check the file exists.

Paths by platform:
- Windows: `C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Data/Tools/UnityYAMLMerge.exe`
- macOS: `/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/Tools/UnityYAMLMerge`

The tool helps, but is not magic. The social rule still applies: **one person owns a
scene at a time.** Talking prevents more conflicts than any tool resolves.

## Branch workflow

`test` is the default and integration branch; `main` is protected and restricted.

- Branch off `test`, open pull requests **into `test`**.
- Do not target `main` — pushes are restricted to the repository owner, and merges
  additionally require a Code Owner review.
- Promotion from `test` to `main` is done by the owner when `test` is in good shape.

## Local models — Ollama

The development provider. Install [Ollama](https://ollama.com), then:

```bash
ollama pull qwen3.5:4b
```

**What the framework uses.** `OllamaSettings`, and an `OllamaProviderConfig` asset made with
Create → Agenerela → Ollama Provider, default to `http://127.0.0.1:11434`,
`qwen3.5:2b`, a 4096-token context, temperature 0 and a 30 s timeout. Always `127.0.0.1`,
never `localhost`: on Windows `localhost` tries IPv6 first, Ollama listens on IPv4 only, and
each request waited about 2 s (findings, 5 October 2026). Check a server with
`curl http://127.0.0.1:11434/api/version`.

**Versions seen.** Ollama 0.34.2 on the lead's laptop (the 5 and 10 October findings, and
the live check of `OllamaProvider`) and 0.33.0 on the RTX 5080 desktop (the Phase 2 gate).
The token-probability behaviour, the 404 bodies and the schema probe were all checked on
0.34.2 only. After an upgrade, re-run
`python tools/benchmarks/schema_visibility_probe.py` (seconds); it settles whether the
response schema has started reaching the model, which would change what a local model reads.

Models used in prototype measurements: `qwen3.5:0.8b`, `qwen3.5:2b`, `qwen3.5:4b`. The
5 October probes ran `2b` as Q8_0 and `4b` as Q4_K_M.
All Apache-2.0 (verified September 2026 — re-check per release, model licences vary a
lot and some families carry real commercial restrictions).

**On an 8 GB card, do not run a 9B-class model** for anything but a one-off curiosity —
it spills out of VRAM and the latency numbers stop meaning anything. For reference, a 2B
model plus a minimal scene already measured 4.2 GB of 8 GB.

**Before any timing-sensitive run**, confirm only one model is resident:

```bash
ollama ps                 # should list one entry, or none
ollama stop <other-model>
```

Two models on one card share compute and flatten every latency measurement toward the
same value. This has caught people out already; see the build plan Appendix A.

Running out of VRAM does not fail, it quietly slows: the part of the model that does not fit
runs on the CPU. `curl http://127.0.0.1:11434/api/ps` reports `size` and `size_vram` for each
model; `size_vram` of 0 means the model is not on the GPU at all (build plan §4 describes the
check). On the lead's laptop the `ollama` command line was seen to hang in Git Bash, even
`ollama --version`, so scripts there use the HTTP API; unload a model with
`{"model": "<name>", "keep_alive": 0, "messages": []}` posted to `/api/chat`. That was seen
on one machine and not investigated further. On a laptop, `size_vram` of 0 was once the sign that the discrete GPU was off when Ollama
started; restarting Ollama after the GPU woke fixed it (seen once, not investigated). Run on
AC power: one 5 October session was run on battery (findings), and a battery run is not
comparable with an AC one.

## MCP for Unity (optional)

The dev project depends on [MCP for Unity](https://github.com/CoplayDev/unity-mcp), pinned
to one git revision (`2fcc179`) in `UnityProject/Packages/manifest.json`, so an AI agent can
drive the editor — create objects, run tests, read the console — instead of describing what
it would do. `Demos/GreyBoxVillage` pins the same revision.

It is a development convenience, not a dependency of the framework: nothing in
`com.agenerela.framework` references it, and it never ships to anyone installing the package.
Only the version bump matters to everyone, so treat it like the Unity version — nobody
changes the pin alone.

To use it, open **Window ▸ MCP For Unity** in the project you have open, select local HTTP at
`http://127.0.0.1:8080`, start the server if it is not running, and connect your client to
the `/mcp` endpoint on that port. Keep **one** Unity project open while a client is
connected; two editors competing for the same port is the usual reason a connection fails.

## Asset server — ComfyUI (optional)

3D models, sound effects and images for the demo games come from ComfyUI running in Docker
on a separate machine with an RTX 5080, which also runs the lead's own ComfyUI. The stack
lives in [`tools/comfyui/`](../../tools/comfyui/README.md) and keeps to its own container,
ports (8288, 8289), image and volume. Setting that machine up is
[`tools/comfyui/AGENT_SETUP.md`](../../tools/comfyui/AGENT_SETUP.md), for a person or an
agent.

To use it from any other machine, copy `tools/comfyui/.env.example` to `tools/comfyui/.env`
and fill in `COMFY_URL` and `COMFY_API_TOKEN`, then run
`python tools/comfyui/comfy.py check`. The token is the server's; carry it over by USB stick
or a password manager, never through a chat. On the RTX 5080 machine itself, the server's own
`.env` already holds the token; set `COMFY_URL=http://127.0.0.1:8289` there. Nothing else is
installed: `comfy.py` uses only Python's standard library. An agent asking for assets follows
[`tools/comfyui/AGENT_USAGE.md`](../../tools/comfyui/AGENT_USAGE.md). That machine's GPU
is also where Ollama ran for the Phase 2 gate, so the two share its 16 GB; the gate was
measured with no ComfyUI container running. Checking a generated model needs Blender
(`tools/comfyui/preview_glb.py` finds it). Measured times and sizes are summarised in
[`findings.md`](findings.md) (8 October 2026) and kept in full in the ComfyUI README.

## Cloud provider (optional)

Copy `.env.example` to `.env` and fill in `GEMINI_API_KEY` from
[Google AI Studio](https://aistudio.google.com/apikey). `.env` is gitignored — verify with
`git check-ignore .env` if you are ever unsure.

Free tier is roughly **15 requests/minute, 1000/day**. Benchmark tooling throttles and
budget-caps itself; do not remove those guards.

Note `gemini-2.5-flash-lite` returns 404 for keys created recently — use
`gemini-3.5-flash-lite`. Don't assume a model name from memory; list what a key actually
has access to before picking one.

## Hardware the numbers came from

Two machines, and numbers from one do not transfer to the other.

| Machine | Used for |
|---|---|
| RTX 4060 Laptop, **8 GB VRAM**, Windows 11 | Appendix A's prototype figures, the 5 and 10 October probes (Ollama 0.34.2), the live check of `OllamaProvider` (19 October) |
| Desktop with an **RTX 5080, 16 GB**, driver 617.14, Windows 11 | The Phase 2 gate's latency (25 October, Ollama 0.33.0), the asset server (`tools/comfyui/`) |

Absolute latency and VRAM figures are specific to the machine that took them. On different
hardware, re-measure rather than assuming they transfer; the gate's findings entry asks for
a re-run on the laptop for a number Appendix A can be compared with.
