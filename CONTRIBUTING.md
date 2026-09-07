# Contributing to Agenerela

For humans. AI agents should read [`AGENTS.md`](AGENTS.md) — which holds the same rules in
more detail, and is worth reading here too.

## First time — set up in this order

Order matters for the first command. `git lfs install` sets up the filters that fetch real
binary files during checkout; clone without it and you get 130-byte pointer files instead.

```bash
git lfs install
```

```bash
git config --global merge.unityyamlmerge.name "Unity SmartMerge"
git config --global merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"'
git config --global merge.unityyamlmerge.recursive binary
```

```bash
git clone https://github.com/agenerela/agenerela.git
```

Then check it worked:

```bash
cd agenerela && git lfs ls-files
```

That must list `URP.png`. If it prints nothing, LFS was not active — run `git lfs pull`.

**On macOS**, the merge driver path is
`/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/Tools/UnityYAMLMerge`.

**A wrong path fails silently.** Git falls back to a plain line-based merge with no error,
and that merge corrupts Unity scenes. Verify with
`git config --global --get merge.unityyamlmerge.driver` and check the file exists.

## Then

- **Unity 6000.3.23f1** — exactly. The version is pinned by `ProjectVersion.txt`; opening
  the project in a newer patch rewrites that file for everyone and can silently upgrade
  serialized assets. Nobody upgrades alone.
- Open `UnityProject/`. The framework is an embedded package, so it compiles automatically.
- **Ollama** is only needed once there is code that calls a model — not for Phase 1.

## Branches

| Branch | What it is |
|---|---|
| `test` | Default and integration branch. **Work here.** |
| `main` | Protected. Pushes restricted to the repository owner. |

Branch off `test`, open pull requests **into `test`**. Do not target `main` — it will be
rejected. Promotion from `test` to `main` is done by the owner when `test` is in good shape.

## Claiming work

Issues are unassigned by design — **assign yourself** to whatever you pick up, so nobody
duplicates it. Issues labelled `ready` have no blockers; `blocked` ones name what they are
waiting on.

Each issue lists the **files it owns**. Stick to them and merge conflicts mostly disappear.

## Before opening a pull request

- Unity console clean — no new errors or warnings
- EditMode tests pass (`Window → General → Test Runner`)
- No secrets, no generated files (`Library/`, `Logs/`, `*.csproj`)
- The PR template's checklist filled in honestly

CI runs automatically and checks the mechanical things: JSON validity, `.meta` file parity,
and that no generated files slipped in.

## If your change touches accuracy

This project's credibility rests on its measurements, so there is one extra bar:

- **A control arm.** Same model, same machine, same prompt set, same session. A single
  before/after run is an anecdote — at n≈50 the observed noise floor was about 10 points.
- **One model resident** while timing (`ollama ps`, then `ollama stop` the others). Two
  models on one GPU share compute and flatten every latency number.
- **No overlap** between few-shot examples and evaluation prompts.
- Record the result in [`docs/llm-wiki/findings.md`](docs/llm-wiki/findings.md) **the day
  you measure it** — including negative results. Two of three attempts to improve accuracy
  in the prototype made it worse, and those write-ups are the most useful pages we have.

## Commits

Explain *why*, not just *what*. No attribution trailers — no `Co-Authored-By`, no
"Generated with &lt;tool&gt;". The history should read as the team's work.

## Scenes and prefabs

Unity scenes are YAML with cross-references, and merges go badly. The merge driver above
helps, but the reliable rule is social: **one person owns a scene at a time.** Say so in
chat before you open one someone else might be in.

## Where things are

| Path | What |
|---|---|
| `UnityProject/Packages/com.agenerela.framework/` | The package — everything shippable |
| `UnityProject/Assets/Demos/` | Demo games, outside the package |
| `UnityProject/Assets/Evaluation/` | Benchmark harness and labelled prompts |
| `docs/FRAMEWORK_BUILD_PLAN.md` | **The plan of record** — read before building |
| `docs/llm-wiki/` | Measured findings and conventions |
| `tools/benchmarks/` | Standalone probes, no Unity required |

If you are about to change how the model is prompted or how the schema is built, read
**Appendix A** of the build plan first. Several intuitive-sounding changes were measured
and made accuracy worse; the appendix says which.
