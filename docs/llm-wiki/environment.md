# Environment Setup

## Unity

**6000.3.23f1** — pinned. `UnityProject/ProjectSettings/ProjectVersion.txt` is committed
and is what enforces this. Nobody upgrades unilaterally: opening the project in a newer
patch release rewrites that file and can silently upgrade serialized assets for everyone.

Open `UnityProject/`. The framework is an *embedded* package under
`UnityProject/Packages/com.agenerela.framework/`, so it compiles automatically with no
install step.

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

Models used in prototype measurements: `qwen3.5:0.8b`, `qwen3.5:2b`, `qwen3.5:4b`.
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

RTX 4060 Laptop, **8 GB VRAM**. Absolute latency and VRAM figures in Appendix A are
specific to it. On different hardware, re-measure rather than assuming they transfer.
