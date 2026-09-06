# Agenerela — AI Agents for Unity

Let a language model drive game agents — NPCs, companions, factions, colonies —
**constrained to actions you explicitly register**. The model selects a behaviour;
validated deterministic code executes it. Local-first, with a provider interface for
cloud backends.

> **Status: early development.** The API does not exist yet. This repository is being
> built from a measured prototype; see [the build plan](docs/FRAMEWORK_BUILD_PLAN.md)
> for the architecture, phases and the evidence behind the design decisions.

## Why it exists

Wiring a language model into a game is not the hard part. Making its output *safe to
execute* is. In prototype measurements, a 2B local model chose the correct game action
**58.5%** of the time when the rules lived in the prompt. With a state-derived schema,
constrained sampling and a deterministic validation layer, the same model reached
**95%** — and beat a 4B model that had none of that scaffolding.

**The scaffolding is the framework.** The model is a swappable commodity behind an
interface.

## How it works

```
GAME (developer)        FRAMEWORK              MODEL                  VALIDATE + EXECUTE
────────────────        ─────────              ─────                  ──────────────────
Agent identity       →  Agent state         →  Grammar-constrained →  Re-check action
Registered actions   →  Builds schema       →  sampling from the      and target
Allowed targets      →  Masks illegal          supplied schema     →  Reject on mismatch
Unity methods           actions                                    →  Run Unity method
```

Two independent safety layers: illegal actions and unknown targets are **removed from the
sampling grammar** so the model cannot emit them, and the executor **re-validates
independently** so a provider without constrained decoding is still contained.

The LLM never touches Unity directly.

## Repository layout

| Path | Contents |
|---|---|
| `UnityProject/Packages/com.agenerela.framework/` | The package — everything shippable |
| `UnityProject/Assets/Demos/` | Demo games (not shipped to consumers) |
| `UnityProject/Assets/Evaluation/` | Accuracy benchmark harness and prompt set |
| `docs/FRAMEWORK_BUILD_PLAN.md` | Architecture, build phases, decision records |
| `docs/llm-wiki/` | Measured findings and working conventions |
| `tools/benchmarks/` | Standalone probes for fast iteration outside Unity |
| `AGENTS.md` | Rules for AI coding agents — and the shortest description of how this project works |

## Developing

Requires **Unity 6000.3.23f1** (pinned — see the build plan §6.2) and
[Ollama](https://ollama.com) with a local model for the development provider:

```bash
git clone https://github.com/agenerela/agenerela.git
ollama pull qwen3.5:4b
```

Open `UnityProject/` in Unity. The package is embedded, so it compiles automatically.
Run tests via **Window → General → Test Runner → EditMode**.

Copy `.env.example` to `.env` if you intend to test a cloud provider. `.env` is
gitignored and must never be committed.

**One local setup step per machine** — wire up Unity's YAML merge tool so scene and
prefab conflicts are mergeable:

```bash
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"'
git config merge.unityyamlmerge.recursive binary
```

## Related work

[LLMUnity](https://github.com/undreamai/LLMUnity) runs a model inside Unity and is the
intended in-process provider. It answers *"how do I run a model in Unity?"*; Agenerela
answers *"which action may this agent take, is the model's choice legal, and was it even
asked for?"* It sits below this framework, not alongside it.

## License

To be confirmed — MIT intended, pending a university IP review. Until a `LICENSE` file
is added, all rights are reserved and this code is published for review only.

---

Senior design project, CSUN COMP 490 / 491.
