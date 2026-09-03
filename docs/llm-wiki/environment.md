# Environment Setup

## Unity

**6000.3.23f1** — pinned. `UnityProject/ProjectSettings/ProjectVersion.txt` is committed
and is what enforces this. Nobody upgrades unilaterally: opening the project in a newer
patch release rewrites that file and can silently upgrade serialized assets for everyone.

Open `UnityProject/`. The framework is an *embedded* package under
`UnityProject/Packages/com.agenerela.framework/`, so it compiles automatically with no
install step.

## Per-machine git setup (each teammate does this once)

The repo's `.gitattributes` routes Unity YAML through `unityyamlmerge`, but **that merge
driver only works if it is also configured locally** — the attribute names a driver, git
needs to know where it lives. Without this, scene and prefab conflicts fall back to a
plain text merge and become unmergeable.

```bash
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"'
git config merge.unityyamlmerge.recursive binary
```

On macOS the tool is at
`/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/Tools/UnityYAMLMerge`.

Also ensure Git LFS is active (`git lfs install`) before pulling any binary assets.

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
