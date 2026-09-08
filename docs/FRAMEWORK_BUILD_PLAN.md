# AI Agent Framework for Unity — Build Plan

> **How to use this document.** This is a portable, self-contained plan for building the
> AI Agent Framework as a real Unity package, from an empty repository. Drop it into the
> new repo (suggested location: repo root or `docs/`), then work through the phases in
> order. Every design rule in here was validated by measurement in the feasibility
> prototype (repo: `test-ai-framework`) — the numbers are carried inline in **Appendix A**
> so this document does not depend on that repo existing. Where the plan says a thing is
> *settled*, it means "measured, don't relitigate without new evidence." Where it says
> *open*, it means the decision is genuinely yours to make when you get there.

**Project**: CSUN COMP 490/491 senior design.
**Goal**: a reusable Unity framework that lets a developer attach a language model to any
game entity (NPC, companion, enemy, faction, colony) and have it choose from actions the
developer explicitly registered. The model decides; validated deterministic code executes.

---

## 0. The one-paragraph thesis (what the prototype proved)

A small local model (2B parameters) driving game agents is unreliable naively (~60%
correct) but reaches **95%+** when surrounded by the right engineering: a per-request JSON
schema whose enums are masked by agent state, `action` emitted before the free-text field,
generated few-shot examples, a required `target` field with a `no_target` sentinel, and a
~10-line deterministic grounding guard. A 2B model with this scaffolding beat a 4B model
without it. **The framework IS the scaffolding.** That is the product; the model is a
swappable commodity behind an interface.

---

## 1. Repository and package skeleton (Phase 0)

### 1.1 Repo shape — settled recommendation

One repository containing **one Unity dev project with the framework embedded as a local
package**. This is the standard Unity package-development pattern: the dev project gives
you a compiler, a test runner, and a place for sample scenes; consumers install the
package alone.

```
<repo root>/
├── FRAMEWORK_BUILD_PLAN.md            ← this file
├── README.md
├── .gitignore                         ← Unity template + secrets rules (see 1.4)
├── .env.example                       ← committed template; .env is gitignored
├── docs/
│   └── llm-wiki/                      ← start one immediately (see 1.5)
├── tools/
│   └── benchmarks/                    ← standalone Python probes (copy from prototype)
└── UnityProject/                      ← the dev/host Unity project (Unity 6000.3+, URP or built-in — framework must not care)
    ├── Assets/
    │   ├── Demos/                     ← THE DEMO GAMES live here — real, compiled, editable (see 1.6)
    │   │   ├── _Shared/               ← player controller, camera rig, debug UI reused by demos
    │   │   ├── CompanionRPG/          ← the one polished demo
    │   │   ├── GreyBox2D/             ← deliberately ugly — proves genre independence
    │   │   └── GreyBoxStrategy/       ← deliberately ugly — proves "agent ≠ NPC"
    │   ├── Evaluation/                ← Phase-4 eval harness + labelled prompt set (see 1.7)
    │   └── DevSandbox/                ← throwaway test scenes; never referenced by anything
    ├── Packages/
    │   └── com.<team>.agentframework/ ← THE PACKAGE — everything shippable lives here
    └── ProjectSettings/
```

Consumers install via git URL with a path query:
`https://github.com/<you>/<repo>.git?path=/UnityProject/Packages/com.<team>.agentframework`

### 1.2 Package layout

```
com.<team>.agentframework/
├── package.json                  ← name, version (semver, start 0.1.0), unity min "6000.0", description
├── CHANGELOG.md                  ← keep from day one; one line per merged change
├── Runtime/
│   ├── <Team>.AgentFramework.asmdef
│   ├── Core/                     ← agent, decision types, context, telemetry
│   ├── Actions/                  ← action definitions, availability, registration
│   ├── Schema/                   ← provider-neutral schema model + per-provider serializers
│   ├── Providers/                ← ILLMProvider, OllamaProvider, cloud providers, in-process
│   ├── Validation/               ← guard pipeline: whitelist, grounding, state legality
│   ├── Memory/                   ← IMemoryStrategy, rolling history default
│   └── Scheduling/               ← request queue, priorities, cancellation, budgets
├── Editor/
│   └── <Team>.AgentFramework.Editor.asmdef   ← references Runtime; nothing references it back
├── Tests/
│   ├── Runtime/                  ← PlayMode tests (integration, needs Ollama — tagged, skippable)
│   └── Editor/                   ← EditMode tests (pure C#, no LLM — the bulk of tests)
├── Samples~/                     ← SMALL API examples only, NOT the demo games (see 1.6)
│   ├── 01_MinimalAgent/          ← ~50 lines: one agent, two actions, no art
│   ├── 02_TargetedActions/       ← adding a target registry + the grounding guard
│   └── 03_CustomProvider/        ← implementing ILLMProvider against a fake backend
└── Documentation~/
    └── index.md
```

> **Trap to know about:** Unity's Asset Database **ignores any folder ending in `~`**. Code
> in `Samples~/` is therefore *not compiled* while you develop, and its scenes can't be
> opened from the Project window. That is fine for three tiny illustrative samples you
> write once and rarely touch — it is completely unworkable for a demo game you are
> actively building. This is why the demos live in `Assets/Demos/` instead (§1.6).

Assembly-definition rules (enforce from the first commit):
- `Runtime` asmdef has **zero** references to Editor assemblies and zero `#if UNITY_EDITOR`
  business logic.
- Tests reference Runtime (and Editor where needed); nothing references Tests.
- **The core has no third-party runtime dependencies.** Use Unity 6's built-in `Awaitable`
  for async (not UniTask, not Tasks-over-coroutines hacks) and `UnityWebRequest` for HTTP.
  *Providers are the exception*: the in-process provider depends on LLMUnity for embedded
  llama.cpp (§2.4). Isolate it behind a version define or a separate package so a
  developer using only Ollama or a cloud backend never pulls native binaries they don't
  need. The rule is "the core is dependency-free," not "nothing may ever depend on anything."

### 1.3 Phase 0 — the first commits, in exact order

**Read this before creating anything.** Three of these steps are effectively irreversible
if done in the wrong order:

> - `.gitignore` must exist **before** Unity ever opens the project. Unity generates
>   `Library/` (hundreds of MB, sometimes GB) the instant it opens one. A single
>   `git add -A` without the ignore file in place puts that in your history permanently —
>   and removing it later means rewriting history for both teammates.
> - `.gitattributes` must exist **before** the first commit of any text file. Line endings
>   are normalized at `git add` time; retrofitting it means one enormous "changed every
>   line of every file" commit that destroys `git blame`.
> - **Git LFS must be initialized before the first binary lands.** LFS only intercepts
>   files committed *after* it is configured. A 40 MB texture committed normally stays in
>   history forever, even if you LFS-track that pattern afterwards.

Do it in this sequence. Each numbered block is one commit.

#### Commit 1 — hygiene files only, before Unity exists

```bash
# In a GitHub org (see 6.1), create an EMPTY repo — no auto-generated README,
# because you want .gitattributes present before literally any other file.
git clone https://github.com/<org>/<repo>.git && cd <repo>
git lfs install
```

Create `.gitattributes` (contents in §1.8), then `.gitignore`:

```gitignore
# Unity generated — never commit these
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]ser[Ss]ettings/
[Mm]emoryCaptures/
[Rr]ecordings/

# IDE / editor
.vs/
.vscode/
.idea/
*.csproj
*.sln
*.user
*.pidb
*.booproj
*.svd
*.pdb
*.opendb
*.VC.db

# OS
.DS_Store
Thumbs.db
Desktop.ini

# Secrets — never commit API keys
.env
.env.*
!.env.example
```

Then commit **and verify before going further**:

```bash
git add .gitattributes .gitignore
git commit -m "Add gitignore, gitattributes and LFS config"
git push -u origin main
```

- [ ] `git check-ignore -v Library` prints a matching rule (even though the folder doesn't
      exist yet — this proves the pattern works).
- [ ] `git lfs env` runs without error.

#### Commit 2 — the empty Unity project

Create the project through Unity Hub at `UnityProject/`, using the **exact** Unity version
the team agreed to pin (§6.2). Open it once, let it finish importing, then close it.

```bash
git status --porcelain          # MUST NOT list Library/, Temp/, obj/, or *.csproj
```

If `Library/` appears here, stop — your `.gitignore` is wrong or in the wrong place. Fix
it before committing anything.

```bash
git add UnityProject/
git commit -m "Add empty Unity project (pinned <version>)"
```

- [ ] `ProjectSettings/ProjectVersion.txt` **is** committed (this is what pins the version
      for your teammate).
- [ ] `git count-objects -vH` shows a repo well under ~50 MB. If it's hundreds of MB,
      `Library/` got in — fix now, not later.

#### Commit 3 — package skeleton that compiles

Create `UnityProject/Packages/com.<team>.<pkgname>/` with `package.json` (§1.8) and this
minimum set of empty-but-valid assemblies:

`Runtime/<Team>.<Pkg>.asmdef`
```json
{
  "name": "<Team>.<Pkg>",
  "rootNamespace": "<Pkg>",
  "references": [],
  "autoReferenced": true
}
```

`Editor/<Team>.<Pkg>.Editor.asmdef`
```json
{
  "name": "<Team>.<Pkg>.Editor",
  "rootNamespace": "<Pkg>.Editor",
  "references": ["<Team>.<Pkg>"],
  "includePlatforms": ["Editor"]
}
```

`Tests/Editor/<Team>.<Pkg>.Tests.Editor.asmdef` — note the last two fields; without them
the test assembly tries to compile into player builds and fails:
```json
{
  "name": "<Team>.<Pkg>.Tests.Editor",
  "rootNamespace": "<Pkg>.Tests",
  "references": ["<Team>.<Pkg>", "<Team>.<Pkg>.Editor"],
  "includePlatforms": ["Editor"],
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"],
  "defineConstraints": ["UNITY_INCLUDE_TESTS"]
}
```

Add one placeholder test so the runner has something to find:

```csharp
using NUnit.Framework;
namespace <Pkg>.Tests {
    public class SmokeTests {
        [Test] public void PackageAssemblyLoads() => Assert.Pass();
    }
}
```

Reopen Unity and verify:

- [ ] Console is **completely clean** — zero errors, zero warnings from your assemblies.
- [ ] Package Manager → *In Project* lists your package under **Custom** with the
      `displayName` from `package.json`.
- [ ] `Window → General → Test Runner → EditMode` shows `PackageAssemblyLoads` and it
      passes.
- [ ] `Assets/` still contains nothing but Unity's defaults — all your code is in
      `Packages/`, not `Assets/`. (Easy to get wrong on day one and annoying to unpick.)

```bash
git add UnityProject/Packages/
git commit -m "Add package skeleton with runtime, editor and test assemblies"
```

#### Commit 4 — project documentation and secrets template

`README.md` (10-line quickstart — the first thing a reviewer reads), `LICENSE.md` (after
the IP check in §1.8), `CHANGELOG.md`, `.env.example`, `CLAUDE.md`, and
`docs/llm-wiki/` seeded with `README.md` + an empty `03-findings.md` (§1.5).

```bash
git add README.md LICENSE.md CHANGELOG.md .env.example CLAUDE.md docs/
git commit -m "Add project docs, license and llm-wiki skeleton"
git push
```

- [ ] `printf 'GEMINI_API_KEY=fake\n' > .env && git status --porcelain | grep -c '\.env$'`
      returns `0` — proving a real key could never be committed. Delete the fake after.

#### Final gate — prove the consumer path works

This is the check that catches packaging mistakes the dev project structurally cannot,
because inside the dev project the package is *embedded* and resolves regardless of
whether `package.json` is correct.

1. Create a throwaway blank Unity project somewhere else.
2. `Window → Package Manager → + → Install package from git URL`:
   `https://github.com/<org>/<repo>.git?path=/UnityProject/Packages/com.<team>.<pkgname>`
3. Verify:
   - [ ] It resolves and compiles with zero errors.
   - [ ] The package appears in Package Manager with the right name and version.
   - [ ] `Assets/Demos/` and `UnityProject/` do **not** appear anywhere in the consuming
         project — only the package subfolder is installed.

**Phase 0 is done when all boxes above are ticked.** Do not start Phase 1 with any of them
outstanding; every one of them is cheap now and expensive in November.

> **Note for consumers running package tests** (relevant once you publish): a consuming
> project only sees a package's tests if it lists the package in `"testables"` in its
> `Packages/manifest.json`. Not needed for your embedded dev project, but worth a line in
> your README.

### 1.4 Secrets policy (settled — carried from prototype)

`.env` at repo root holds API keys; gitignore rules are `.env`, `.env.*`, `!.env.example`.
Keys are read from `.env` by tools and passed in HTTP **headers**, never URLs; error
output must redact them. Never paste a key into chat, a commit, or source. Add the
gitignore rule **before** the first key exists.

### 1.5 Start an llm-wiki immediately (settled)

Create `docs/llm-wiki/` with a README and a findings page on day one, and a root
`CLAUDE.md` pointing at it. The prototype's single most valuable artifact was its recorded
findings — including the failures. Rule for the wiki: when a measurement surprises you, it
goes in the findings page *the same day*.

### 1.6 Demo games — where they live and why (settled)

**Samples and demo games are different artifacts.** Conflating them is the most common
structural mistake in Unity package projects:

| | Samples (`Samples~/` in package) | Demo games (`Assets/Demos/`) |
|---|---|---|
| Purpose | Teach one API concept | Prove the framework works, and get presented at review |
| Size | Tens of lines, no art | Scenes, prefabs, possibly art |
| Audience | A developer who installed the package | Your review panel, and you |
| Ships to consumers | Yes, on demand via Package Manager | **No** — would bloat every install |
| Compiled during dev | **No** (`~` folders are ignored by Unity) | Yes |

So: **demos are first-class projects in the dev project's `Assets/Demos/`, outside the
package.** They consume the framework exactly the way a real developer would — through
its public API, from a `Packages/` install — but they are not part of what gets shipped.

**Each demo gets its own asmdef** referencing only the framework's `Runtime` assembly.
This is not bureaucracy; it is the plan's main API-design forcing function:

> If a demo ever needs `InternalsVisibleTo`, a `public` field that shouldn't be public, or
> a copy-pasted chunk of framework code to work, **the framework is missing a public API**.
> Fix the framework, not the demo. Three demos across three genres, each restricted to the
> public surface, is the cheapest available proof that the API is genuinely reusable rather
> than shaped around one game.

Practical notes:
- `Assets/Demos/_Shared/` (own asmdef) holds things demos legitimately share — a player
  controller, a camera rig, a debug overlay. Discipline: if the *framework* would want it,
  it belongs in the package; if only demos want it, it belongs here. When in doubt, leave
  it here — promoting later is easy, un-shipping a bad public API is not.
- Add a tiny `DemoLauncher` scene with three buttons. Presenting is far smoother when you
  can switch demos live instead of editing Build Settings on a projector.
- Keep the two grey-box demos genuinely grey-box. Their ugliness is the argument.
- Enable **Git LFS** before the first art asset lands in `CompanionRPG` (`*.png`, `*.fbx`,
  `*.wav`, `*.tga`). Retrofitting LFS after binaries are in history is painful.
- The strategy demo is the one that proves the thesis — a *faction* agent with no
  Transform, no navmesh, no dialogue box, driven through the same `Agent` API as a
  talking NPC. If time collapses, cut the 2D demo before this one.

**Also verify the real consumer path.** Because demos live inside the dev project, they
compile against the embedded package and will happily use APIs that a real install might
not expose. Once per phase, install the package from its git URL into a *blank* Unity
project and confirm it compiles and the samples import. Ten minutes; catches packaging
mistakes that the dev project structurally cannot.

### 1.7 The evaluation harness lives outside the package (initially)

The Phase-4 eval harness and its labelled prompt set go in `Assets/Evaluation/`, not the
package — the 200-prompt dataset is specific to your action vocabulary, not to consumers.
If the *runner* later proves generally useful ("measure your own agents' accuracy" is a
real selling point for the framework), it can graduate into
`Runtime/Evaluation/` behind its own asmdef. Don't design for that on day one.

### 1.8 `package.json` and repo hygiene files (easy to forget, annoying to retrofit)

**`package.json` — samples must be declared or Package Manager will not show them.**
Putting files in `Samples~/` is not enough; the `samples` array is what surfaces them:

```jsonc
{
  "name": "com.<team>.<pkgname>",          // all-lowercase, reverse-domain, no spaces
  "version": "0.1.0",                       // semver; stay 0.x until the API stops moving
  "displayName": "<Product Name>",          // what appears in Package Manager
  "description": "AI agents for Unity games — the model decides, your code executes.",
  "unity": "6000.0",
  "author": { "name": "<team>", "url": "https://github.com/<org>/<repo>" },
  "license": "MIT",                         // see IP note below BEFORE choosing
  "samples": [
    { "displayName": "01 Minimal Agent",    "description": "One agent, two actions.",
      "path": "Samples~/01_MinimalAgent" },
    { "displayName": "02 Targeted Actions", "description": "Target registry + grounding guard.",
      "path": "Samples~/02_TargetedActions" },
    { "displayName": "03 Custom Provider",  "description": "Implement ILLMProvider.",
      "path": "Samples~/03_CustomProvider" }
  ]
}
```

Also set `"rootNamespace"` in each asmdef so new scripts are generated with the right
namespace automatically — small thing, saves constant manual fixing.

**Files to create in the first commit, not later:**

- `LICENSE.md` at repo root **and** inside the package (UPM convention). **Before picking
  a license, check your university's student-IP policy** — CSUN, like most universities,
  has rules about ownership of work produced for credit, and some sponsored/capstone
  arrangements restrict open-sourcing. Ask your advisor in week one; it is far easier than
  relicensing later. MIT is the usual default for a student framework if you're free to
  choose.
- `.gitattributes` — **belongs in commit 1, before any other file** (§1.3 explains why
  retrofitting it is destructive). Genuinely needed here, for three separate reasons:
  ```gitattributes
  * text=auto
  *.cs text diff=csharp
  # Unity YAML: prevent line-ending churn and enable smart merge
  *.unity   merge=unityyamlmerge eol=lf
  *.prefab  merge=unityyamlmerge eol=lf
  *.asset   merge=unityyamlmerge eol=lf
  # Binary assets via LFS (add before the first art commit)
  *.png  filter=lfs diff=lfs merge=lfs -text
  *.fbx  filter=lfs diff=lfs merge=lfs -text
  *.wav  filter=lfs diff=lfs merge=lfs -text
  ```
  Without `text=auto` you will get CRLF/LF churn on every commit from a mixed-OS team;
  without the `merge=unityyamlmerge` lines, two people touching one scene produces an
  unmergeable conflict.
- `CHANGELOG.md`, `README.md` (with a 10-line quickstart — the first thing a reviewer
  reads), and `.env.example`.

---

## 2. Architecture (what each module is, and the settled design rules inside it)

### 2.1 Core (`Runtime/Core/`)

The agent abstraction, deliberately **not** welded to `MonoBehaviour`:

```csharp
// Plain class — the brain. Owns identity, memory, registered actions.
public sealed class Agent {
    public AgentIdentity Identity;            // name, role, personality, goals
    public IMemoryStrategy Memory;            // default: RollingHistory(turns: 6)
    public ActionRegistry Actions;            // see 2.2
    public Awaitable<AgentDecision> DecideAsync(string stimulus, DecideOptions opts = default);
}

// Thin MonoBehaviour adapter for scene objects. A faction manager or colony sim
// can own Agent instances directly with no GameObject involved.
public class AgentBehaviour : MonoBehaviour { public Agent Agent { get; } ... }
```

`AgentDecision` is `{ actionId, targetId, statement }` plus full telemetry (latency, token
counts, schema mode, which guards fired). Telemetry is not optional — every decision is
measurable or the eval harness (Phase 4) can't exist.

### 2.2 Actions (`Runtime/Actions/`) — the heart of the framework

**Settled rule: the action vocabulary is data, not an enum.** The prototype's fixed
`NPCAction` enum is the single biggest thing this rewrite exists to kill. An action is a
`ScriptableObject` the *game developer* authors:

```csharp
[CreateAssetMenu(menuName = "AI Agent/Action Definition")]
public class ActionDefinition : ScriptableObject {
    public string Id;                    // snake_case, becomes the schema enum value
    [TextArea] public string Description;      // one short clause — see symmetry rule below
    public bool RequiresTarget;
    public string ExampleUtterance;      // "{0}" placeholder for target; see few-shot rules
    public string[] PreferredExampleTargets;   // targets this verb sensibly applies to
}
```

Registration binds a definition to gameplay and to availability:

```csharp
public interface IActionHandler {
    bool IsAvailable(AgentContext ctx);        // state masking — see settled rules
    void Execute(AgentContext ctx, AgentDecision decision);   // deterministic Unity code
}
agent.Actions.Register(definitionAsset, handler);
```

Targets are registered the same way (`TargetRegistry`: id → object reference), so "what
can this agent currently reference" is queryable, not hand-maintained prose.

**Settled design rules baked into this module (each is a measured result — Appendix A):**
1. *State masking*: `IsAvailable` decides whether an action appears in the schema enum
   at all. An agent already following is never *offered* `follow_player`. Never write a
   prose rule ("don't repeat an action...") for something the enum can make unexpressible.
2. *Description symmetry*: every action's description is one short clause of comparable
   length. A long emphatic description on one option (the prototype's `none`) biases a
   small model toward it — this alone caused a measured regression.
3. *The idle/none option is listed last* in the enum, so it reads as fallback, not default.

### 2.3 Schema (`Runtime/Schema/`)

A provider-neutral `DecisionSchema` model built per request from the agent's currently
available actions and targets, then serialized by each provider.

**Two serializers are required, not one.** This is easy to miss because Ollama hides it:

```
DecisionSchema ──► JsonSchemaSerializer ──► Ollama, cloud APIs (they compile it to a grammar internally)
               └─► GbnfSerializer       ──► in-process provider (llama.cpp wants GBNF directly)
```

Ollama accepts a JSON Schema in its `format` field and converts it to a GBNF sampling
grammar for you. LLMUnity — and llama.cpp generally — expects **GBNF directly**. So the
in-process provider needs its own serializer emitting the same constraints (masked action
enum, target enum with the `no_target` sentinel, field ordering) in grammar form. Budget
this as real work in Phase 6b, roughly a day plus tests; it is not a free adapter.

Keeping `DecisionSchema` provider-neutral is what makes this a second serializer rather
than a second schema system — do not let provider-specific syntax leak into the model.

**Settled rules:**

1. **Field order: `action`, then `target`, then the free-text field (`statement`).**
   Generation is left-to-right; free-text-first makes the model chat first and then pick
   the action that agrees with its own chat (measured collapse to `none`). Worth +11.7
   accuracy points alone. For providers that need it explicitly (Gemini), emit
   `propertyOrdering`. The prototype named the field `dialogue`; it is `statement` in the
   framework because a country or colony does not have dialogue (DR-008). The measured rule
   is the *order*; the rename is unmeasured and is an early Phase 4 A/B run.
2. **`target` is REQUIRED, with a `"no_target"` sentinel in its enum.** Never optional
   (models omit it even when needed — measured 0/5 on a 4B model), and never `""` as the
   empty value (Gemini rejects empty enum strings with HTTP 400; `no_target` also gives
   the model a way to *say* "what you asked for isn't here").
3. **Do NOT add a reasoning/chain-of-thought field before `action`.** Measured: worst of
   five variants, below the unmodified baseline. Don't retry without new evidence.
4. Few-shot block: assembled at request time from each registered action's
   `ExampleUtterance` + a `PreferredExampleTarget` **that the verb sensibly applies to**
   (blind rotation produced "Pick up the Blacksmith" — a nonsense demonstration), plus one
   negative example showing a refusal with `no_target`. **Example utterances must never
   overlap the evaluation prompt set** (see Phase 4). Few-shot was the largest single
   lever measured: +35 points.

### 2.4 Providers (`Runtime/Providers/`)

```csharp
public interface ILLMProvider {
    ProviderCapabilities Capabilities { get; }   // constrained decoding? property ordering? enum-of-empty-string?
    Awaitable<ProviderResult> RequestAsync(DecisionRequest req, CancellationToken ct);
}
```

- `OllamaProvider` first (port from prototype's `OllamaClient`: non-blocking, `think:false`
  for Qwen-family reasoning modes, `num_ctx` configurable, telemetry from the response
  envelope).
- The abstraction must absorb **schema dialect differences** (that's why `Capabilities`
  exists), not just base URLs. This was proven necessary, not speculative.
- The provider is *untrusted* by design: everything in 2.5 runs regardless of what the
  provider claims to guarantee, so a plain-chat provider with no constrained decoding is
  still contained.

**Three providers, three different jobs.** These are not redundant — each exists because
the others cannot do its job:

| Provider | Job | Why the others can't do it |
|---|---|---|
| `OllamaProvider` | **Development.** Swap models in seconds, no packaging, easy benchmarking. | Requires the player to install Ollama and run a server — **not shippable in a game.** |
| `InProcessProvider` | **Shipping.** Model runs inside the game process; player installs nothing. | The only option that actually ships. |
| **Cloud API providers** (`GeminiProvider`, `OpenAIProvider`, …) | **Optional / comparison.** Higher quality for low-frequency decisions; a reference labeller when building the eval set. | Per-request cost collapses at many-agent scale (Appendix A), and needs a network. |

**The cloud tier is plural by design.** The goal is not "support Gemini" but "support cloud
APIs generally", with each vendor behind the same `ILLMProvider`. Gemini is simply the
first one implemented, because it is what the prototype measured against — so its quirks
are documented, and it doubles as the conformance reference for the next provider.

Expect vendors to differ in ways the interface must absorb, not paper over. Already
observed with Gemini: empty strings rejected as enum values, property ordering that must
be stated explicitly rather than inferred, and rate limits that need budget caps in code.
A second vendor will surface its own list. `ProviderCapabilities` exists for exactly this.

#### The in-process provider — how the framework actually ships

> **Plan of record: LLMUnity.** Rationale, options considered and revisit triggers are in
> [DR-001](#dr-001--in-process-inference-via-llmunity). Treated as probable rather than
> final until Phase 6b begins.

This is the provider that closes the "players don't have Ollama" gap, and it should not be
written from scratch. [**LLMUnity**](https://github.com/undreamai/LLMUnity) (undreamai,
Apache-2.0) already embeds llama.cpp in Unity: GGUF loading, CPU/GPU backends across
Windows/macOS/Linux/Android/iOS, streaming, and — importantly — **GBNF grammar support**,
which is the same constrained-decoding mechanism this framework's schema layer depends on.

Building that yourself means shipping and maintaining native llama.cpp binaries for every
platform. That is months of work, is not the project's contribution, and is a solved
problem. Wrap it instead:

```csharp
// Adapts the framework's provider-neutral schema to LLMUnity's GBNF grammar API.
public sealed class LLMUnityProvider : ILLMProvider { ... }
```

**Clarifying scope so this isn't mistaken for our contribution:** LLMUnity is *inference
plumbing* — it runs a model and returns text. Its `FunctionCalling` sample is a hardcoded
grammar over three zero-argument methods (`Weather()`, `Time()`, `Emotion()`) dispatched
by reflection, with no parameters, no state filtering, and no post-generation validation.
Everything in §2.2–§2.5 — the action registry, state masking, target whitelisting, the
grounding guard — sits *above* it and is exactly what it does not provide. It is a
dependency, not a competitor.

**Dependency isolation — and why it is not optional.** LLMUnity is distributed as
`ai.undream.llm` through **OpenUPM, which requires the consuming project to add a scoped
registry** to its `manifest.json`. Unity's Package Manager will not resolve it from a
plain `dependencies` entry.

That means: if you declare LLMUnity as a hard dependency, **every developer who installs
your framework must set up a scoped registry** — including the majority who only want the
Ollama or cloud path and will never run a model in-process. That is real, avoidable
friction on your install story, on top of pulling native binaries nobody asked for.

So do **not** declare it in `dependencies`. Ship the in-process provider as **either** a
separate optional package (`com.<team>.<pkg>.llmunity`) **or** an assembly guarded by a
version define, so it compiles only when LLMUnity is present:

```jsonc
// in the provider's asmdef
"versionDefines": [
  { "name": "ai.undream.llm", "expression": "", "define": "TROUPE_LLMUNITY" }
]
```

Your README then reads: *"Want the in-process provider? Install LLMUnity (this adds a
scoped registry) and `LLMUnityProvider` becomes available automatically."* Developers who
don't need it install nothing extra and never see a registry prompt.

**Pin an exact version, never a range.** LLMUnity tracks llama.cpp, which makes breaking
changes regularly; an unpinned dependency means an upstream release can silently change
your measured behaviour. Upgrade deliberately, and re-run the Phase-4 eval suite as the
regression check when you do.

**A useful diagnostic property of having two local providers:** when in-process results
look wrong, run the identical decision through `OllamaProvider`. Same model, same schema,
different transport — if they disagree, the bug is in your GBNF serializer or the
provider wrapper, not in the framework. Keep both working for this reason alone.

**Licensing checks before depending on it** (cheap now, awkward later):
- LLMUnity is Apache-2.0 — compatible with an MIT framework. Attribution requirements
  are satisfied by keeping its license file with the distribution.
- **Model weights carry their own separate licenses.** Qwen models are generally
  permissive; some other families ship custom terms with real restrictions on commercial
  use. The moment a demo build *bundles* a `.gguf`, that model's license applies to your
  build — record which model and which license in the wiki.
- Download size and VRAM are now product decisions, not lab curiosities: a ~1 GB model
  added to a game download, coexisting with the renderer inside an 8 GB card
  (Appendix A). This is what makes the "how small can the model be" study a
  *shippability* question.

### 2.5 Validation (`Runtime/Validation/`) — the actual product

An ordered guard pipeline every decision passes through before any handler executes:

```csharp
public interface IDecisionGuard {
    GuardVerdict Inspect(DecisionRequest req, AgentDecision d);  // Pass | RewriteToNone(reason) | Reject(reason)
}
```

Built-ins, in order:
1. **SchemaLegalityGuard** — actionId is registered and currently available; targetId is
   in the registry or `no_target`. (Execution-time re-check of what the grammar should
   have enforced — providers are untrusted.)
2. **TargetGroundingGuard** — if the chosen action requires a target, the chosen target
   must actually be referred to in the stimulus text (exact or de-CamelCased word match).
   This ~10-line guard is the highest-value component in the entire framework: it took a
   2B model from 60% → 95% and a 4B to 100% on the focused suite, because it kills the
   worst failure mode — the model substituting a *legal* target for an *illegal* one the
   player actually named ("Attack Godzilla" → attacks the TrainingDummy). Documented
   limitation to preserve honestly: it is lexical, and will wrongly block indirect
   reference ("attack it", "go there"). Hardening it (pronouns, multi-turn reference) is
   scheduled work, not a solved problem.
3. Developer-supplied guards append here (game-specific rules: line-of-sight, cooldowns…).

A guard rewriting to `none` is a **contained refusal**, not an error — telemetry records
which guard fired so the eval harness can distinguish "model right", "model wrong but
contained", and "model wrong and executed".

### 2.6 Scheduling (`Runtime/Scheduling/`)

Port the prototype's single-queue serialization as the starting point (measured safe: 5
simultaneous requests, 0 cross-talk), then extend: priority tiers (player-facing preempts
background), per-request cancellation, and a hard per-session request budget for cloud
providers (the Gemini probe scripts were budget-capped for a reason — keep that
discipline). Full scheduler redesign is Phase 6+; don't gold-plate it early. Known
ceiling to state honestly: serialized local inference ≈ 21s for 30 agents/round.

### 2.7 Editor (`Editor/`)

Custom inspector for `AgentBehaviour` (identity fields, drag-in list of ActionDefinition
assets, target registry view), `Create → AI Agent → …` menus, and a **Decision Log
window** streaming per-decision telemetry (chosen action, guard verdicts, latency, token
counts). The log window is not a luxury — it is how a developer debugs "why did my agent
do that", which is the framework's main support burden. Built **last** (Phase 5), once the
data shapes underneath have stopped moving.

---

## 3. Build order — phases, each independently shippable

| Phase | Deliverable | Definition of done |
|---|---|---|
| **0. Skeleton** | Repo + package layout of §1 | Checklist §1.3 all green |
| **1. Core + Actions + Schema** (pure C#, no LLM, no scene) | `Agent`, `ActionDefinition`, registries, `DecisionSchema` builder + Ollama-dialect serializer | EditMode tests prove: state masking (following agent's schema omits `follow_player`); `target` required w/ `no_target`; field order action→target→statement; few-shot block matches registered actions and rotates only sensible example targets; empty target-registry removes `target` property entirely. **All testable without any model running — this is why Phase 1 has no LLM.** |
| **2. OllamaProvider + queue** | End-to-end decision in a sandbox scene | PlayMode test (tagged `RequiresOllama`): one agent, five actions, live decision round-trip < 5s; telemetry fields populated; provider failure (missing model) surfaces as a typed error, not an exception leak |
| **3. Validation pipeline** | Guards of §2.5 wired between provider and handler | EditMode tests with hand-built fake decisions: substitution attack rewritten to `none`; unavailable action rejected; guard verdicts appear in telemetry. Integration: prototype's "impossible request" suite passes ≥ 95% on a 2B model |
| **4. Evaluation harness** | The measurement instrument — **before more features** | 200+ labelled prompts (grow from the prototype's 53), each declaring its required precondition state; runner executes A/B (two configs, same model/session) and writes a classified report (correct / wrong-legal / contained / rejected / pipeline-error); second annotator labels a subset, agreement reported. Methodology checklist (§4) committed to the wiki |
| **5. Editor tooling** | §2.7 | A developer with zero framework knowledge builds a working 3-action agent in an empty scene in < 15 min without editing framework source (actually run this test on a teammate) |
| **6. Cloud API providers** | Proof the abstraction is real, and the path to supporting any vendor | At least one cloud provider (Gemini first — it is what the prototype measured) runs the same eval subset through `ILLMProvider` with **zero framework-code changes**: a provider class plus config, nothing more. A **provider conformance test suite** exists that any future vendor must pass, so adding OpenAI or Anthropic later is implementing an interface rather than editing the framework. Budget caps and RPM throttling enforced in code, not convention |
| **6b. In-process provider** | `GbnfSerializer` (§2.3) + `LLMUnityProvider` — the one a player can actually run (§2.4) | A **built player executable** (not the Editor) runs agent decisions with **no Ollama and no network** — the deliverable that makes "local-first" true rather than aspirational. Also: GBNF serializer emits the same constraints as the JSON-Schema path (unit-tested against the same `DecisionSchema` fixtures); same eval subset passes within noise of the Ollama arm; LLMUnity pinned to an exact version and **not** in `dependencies` (version-defined, so users who don't want it never add OpenUPM's scoped registry); model file's license recorded in the wiki |
| **7. Samples** | Three tiny in-package samples (`Samples~/`): minimal agent, targeted actions, custom provider | Each is tens of lines with no art; all three import cleanly via Package Manager into a blank project |
| **8. Demo games** | `Assets/Demos/` — one polished (`CompanionRPG`), two grey-box (`GreyBox2D`, `GreyBoxStrategy`) + `DemoLauncher` | Each demo has its own asmdef referencing **only** the framework's public Runtime assembly — no `InternalsVisibleTo`, no copied framework code. `GreyBoxStrategy` drives a faction agent with no Transform through the same `Agent` API as a talking NPC. See §1.6 — this phase runs *in parallel* with 2–7, not after |

**Demo games run alongside phases 2–8, not after them.** They are the integration test for
every phase — start `GreyBox2D` as soon as Phase 2 produces a working decision, and let it
break loudly whenever the API changes. Suggested cadence:

| After phase | Demo milestone |
|---|---|
| 2 (provider works) | `GreyBox2D` — one agent, three actions, no art. First proof the API is usable from outside the package. |
| 3 (guards) | `GreyBoxStrategy` — a **faction** agent with no Transform and no dialogue box. The thesis demo; build it early, because if the API can't express a non-NPC agent you want to know in month two, not month eight. |
| 5 (editor tooling) | `CompanionRPG` — the polished one. Built last on purpose: it's the demo that benefits most from Inspector tooling existing, and the only one that needs art. |
| 8 | `DemoLauncher` scene + a build of all three for the review presentation. |

Phases 1–3 are the critical path and port proven prototype logic — low research risk.
Phase 4 is deliberately *before* editor polish: every accuracy claim after it inherits its
credibility from that instrument. If the semester compresses, cut `CompanionRPG` down to
grey-box (its art is the least load-bearing thing in the project) or drop `GreyBox2D`
entirely — **never** cut `GreyBoxStrategy`, which is the only demo that proves the
genre-agnostic claim, and never cut Phase 4.

---

## 4. Testing and measurement standards (settled — copy into the new wiki verbatim)

Before writing down any accuracy number:
1. **Control arm.** Baseline vs treatment on the identical prompt set, same model, same
   machine, same session. Single before/after runs are anecdotes — the prototype's
   baseline arm alone drifted 52.8%–64.2% across three otherwise-identical runs (n=53).
   Treat ~10 points as the noise floor at that n; grow n before trusting smaller deltas.
2. **One model resident.** `ollama stop` everything else before timing. Two models on an
   8GB card share compute and flatten every latency number toward the same value
   (observed directly).
3. **No few-shot/eval overlap**, even partial phrasings.
4. **Precondition state staged per case.** "Wait here" is only coherent for an agent
   currently following.
5. **Classify outcomes**, never a single percentage: correct / wrong-but-legal (the bad
   one) / contained-by-guard (the *safety layer working*) / rejected-when-expected /
   pipeline-error. A whitelist rejection scored as "failure" makes your own safety system
   look broken.
6. **Warm the model** (2–3 throwaway requests) so cold-load doesn't pollute the first arm.
7. CI: EditMode tests always run; PlayMode integration tests behind a `RequiresOllama`
   category so the suite is green on machines without a model.

Known unresolved measurement issue to carry forward: the prototype measured ~1.3s mean
latency in-Unity but ~3.5s from standalone Python scripts, same model/machine — backwards
from expectation, never root-caused. Until explained in the new codebase, trust *relative*
comparisons within one environment; distrust absolute latency claims across environments.

---

## 5. Positioning and scope guardrails (so reviews go well)

- **Say**: "the framework replaces the top-level behavior-*selection* node — where
  hand-authored condition trees grow combinatorially." **Never say** "replaces behavior
  trees": a BT ticks in microseconds deterministically for free; this decides in ~1–3s at
  95–100% with guards. Kelley 2024 (*Behavior Trees Enable Structured Programming of
  Language Model Agents*) argues BTs are the scaffolding *around* LLM agents — have an
  answer ready.
- **Differentiator to lead with**: genre-agnostic *agents* (factions, colonies), which no
  conversational-character product (Convai, Inworld) serves — and which is exactly the
  workload where per-request cloud billing collapses (~$165/player/100h at 30
  continuously-deciding agents vs ~$1.53 for one companion). Local-first is the correct
  engineering choice for that workload, not a budget apology. Ship local by default;
  support cloud via `ILLMProvider`; let developers choose with the numbers.
- **Honest constraint to state, not hide**: model + minimal scene already measured 4.2GB
  of an 8GB card. "How small can the model be under full scaffolding" (0.8B/2B/4B study
  on the Phase-4 eval set) is the shippability question — schedule it, cite it.
- **Out of scope, permanently**: solving "who pays for inference in shipped games";
  letting the LLM touch Unity state directly (it selects from registered actions,
  full stop); accuracy work without the Phase-4 instrument.

---

## 6. Team and course administration (the non-code things that sink projects)

### 6.1 One repository — settled, with one caveat

**Use one repo for the package, the demos, the dev project, the wiki, and the tools.**
For a two-person team this is clearly correct:

- **Atomic commits.** Change the API and update all three demos in one commit. With split
  repos, every breaking change becomes a two-repo dance and the demos drift.
- **The demos are your integration tests.** They should break in the same CI run that
  builds the framework, not silently rot in another repository.
- **One clone for reviewers.** Your advisor and panel see everything at once.
- **No submodule pain.** Git submodules are the single most common source of "it doesn't
  build on my machine" in student projects.
- **Consumers don't get the demos.** With `?path=`, only the package subfolder is
  *installed* into a consuming project — the demos and dev project never appear in a
  user's `Packages/` folder. The convenience costs your users nothing in what they end up
  with.

*Caveat:* UPM resolves a git dependency by cloning the repository before extracting the
subfolder, so total repo size can affect install time even though installed size doesn't
change. Keep `CompanionRPG`'s art proportionate, use LFS, and this stays a non-issue at
student-project scale. Revisit only if the repo grows into the gigabytes.

**Own the repo in a GitHub organization, not a personal account.** Free, takes two
minutes, and means the project survives either teammate's account, and access can be
granted to your advisor without transferring anything.

### 6.2 Two-person Unity workflow

Unity + Git has specific failure modes. Agree on these in week one:

- **Pin the Unity version exactly.** `ProjectSettings/ProjectVersion.txt` is committed;
  if one person opens the project in a newer patch release it rewrites that file and can
  silently upgrade serialized assets. Nobody upgrades Unity unilaterally.
- **Scenes and prefabs merge badly.** Two mitigations, use both: configure
  **UnityYAMLMerge** (ships with Unity, wire it up per `.gitattributes` above), and adopt
  the social rule *one person owns a scene at a time*. Most scene conflicts are avoided by
  talking, not tooling.
- **Branching:** `main` protected, short-lived feature branches, PR review. With two
  people, each reviews the other — this is also the most reliable way to keep the wiki's
  findings honest, since the reviewer asks "where's the A/B run?"
- **Force Text serialization + visible meta files** (Unity's default now — verify, don't
  assume).

### 6.3 CI — start smaller than you think

Unity in CI needs **license activation**, which for GitHub Actions (via GameCI) means
storing an activation file in repository secrets. It works, but it is fiddly and a classic
week-long time sink. Recommended sequencing:

1. **Phase 0–2:** CI runs only the non-Unity parts — markdown link check, Python probe
   linting. Run Unity EditMode tests locally before pushing.
2. **Phase 3+**, once the test suite is worth protecting: add GameCI with EditMode tests.
   Keep PlayMode/Ollama tests **out** of CI permanently — no runner has a GPU or a model,
   and tagging them `RequiresOllama` (per §4) exists exactly so CI can skip them.

Don't let CI setup block Phase 1. A green test suite you actually run beats a red pipeline
nobody looks at.

### 6.4 Quotas and limits to know before you hit them

| Thing | Limit | Matters when |
|---|---|---|
| GitHub LFS (free tier) | ~1 GB storage, ~1 GB bandwidth/month | `CompanionRPG` art; a few large textures plus CI pulls will find this fast |
| Gemini free tier | ~15 req/min, ~1000 req/day | Any cloud eval run — budget-cap it in code, as the prototype scripts do |
| GitHub Actions (free) | 2,000 min/month private repos | Unlimited if the repo is **public** — another reason to open-source if IP policy allows |
| VRAM | 8 GB typical student laptop | Model + game must coexist; see Appendix A |

### 6.5 Map phases to the academic calendar

The eight phases are not evenly sized. A realistic two-semester split:

| Term | Phases | What you can demo at the end |
|---|---|---|
| **COMP 490** (fall) | 0–4, plus `GreyBox2D` and `GreyBoxStrategy` | A working, *measured* framework: agents decide correctly ~95% with guards, proven on two genres including a non-NPC faction agent. This is a complete, defensible result even if 491 went badly. |
| **COMP 491** (spring) | 5–8, plus `CompanionRPG` and the polished write-up | A framework other developers can actually use: Inspector tooling, second provider, samples, one polished demo, final paper/poster. |

Deliberately front-loading measurement (Phase 4 in the fall) means **your headline claim
exists by December**. Spring is then about usability and presentation, which is far lower
risk than discovering in March that the accuracy story doesn't hold.

**Map your course's actual deliverables onto this now** — proposal, design document,
progress reports, poster, demo day, final report all have fixed dates, and they are
*deliverables*, not overhead. Put every one in the repo's issue tracker or a milestone
board in week one. Senior design projects fail on missed paperwork far more often than on
technical difficulty.

### 6.6 Divide the work along seams, not files

With two people, split by **module boundary** so you're rarely in the same file:

- One owns **Core + Actions + Schema** (§2.1–2.3) — the API surface.
- One owns **Providers + Validation + Scheduling** (§2.4–2.6) — the runtime path.
- **Evaluation (Phase 4) is shared** — and the second annotator requirement means it
  *needs* both of you anyway (inter-annotator agreement is impossible solo).
- Demos: whoever didn't build the subsystem a demo exercises should build that demo. It's
  a free API usability test — if the author has to explain their own API to their partner,
  the API needs work.

---

## 7. Decision records

Short entries capturing *why* a choice was made, so it isn't relitigated every time
someone new looks at the repo — and so "why didn't you build X yourself?" has a written
answer at review time. Add one whenever a decision costs more than an hour to reverse.

### DR-001 — In-process inference via LLMUnity

**Status:** Probable. Default plan of record; confirm when Phase 6b starts.

**Context.** The framework's thesis is local-first, but neither planned provider can be
shipped to a player: `OllamaProvider` requires the player to install and run a separate
server, and cloud costs collapse at many-agent scale (~$165/player/100h at 30 agents,
Appendix A). Something must run a model *inside the game process*.

**Options considered**

| Option | Cost | Verdict |
|---|---|---|
| **Depend on LLMUnity** (Apache-2.0) — wrap it behind `ILLMProvider` | ~1 file + a GBNF serializer (~1 day) | **Chosen.** |
| Vendor / fork LLMUnity into the package | Same upfront, plus permanent maintenance and strict Apache-2.0 attribution obligations | Fallback if upstream blocks us |
| Build llama.cpp integration from scratch | Native binaries for 5+ platforms, P/Invoke marshalling, GGUF loading, GPU backend detection, threading, tracking upstream churn. Realistically a full semester. | Rejected — see below |
| Ship Ollama-only, defer in-process | Zero | Rejected — leaves the core thesis undeliverable |

**Decision.** Depend on LLMUnity as an *optional, version-defined* provider (§2.4).

**Why not build it ourselves.** It is orthogonal to the contribution. The measured result
is that scaffolding — schema masking, the `no_target` sentinel, the grounding guard, the
validation pipeline — takes a 2B model from 60% to 95%. An inference runtime adds nothing
to that. Spending a semester on P/Invoke would most likely cost Phase 4 (the evaluation
harness every later claim depends on) and yield a thinner framework. "We evaluated
existing inference layers, chose one, and spent our effort on the unsolved layer above it"
is both the better engineering call and the better thing to defend at review.

**Consequences**
- Adds a GBNF serializer to the schema layer (§2.3) — llama.cpp needs grammar directly,
  where Ollama accepts JSON Schema and converts internally.
- Must stay out of `dependencies`: LLMUnity ships via OpenUPM and would force every
  framework user to add a scoped registry (§2.4).
- Pin an exact version; it tracks llama.cpp's breaking changes.
- Apache-2.0 imposes essentially no obligations while merely *depending*. Obligations
  (LICENSE, NOTICE, stating modifications) only attach if we later vendor or fork it.
- Model weights are licensed separately from LLMUnity. Qwen3.5 (2B/4B) verified
  Apache-2.0, Sept 2026. Ship no `.gguf` in the package — let the developer choose and
  download one, so model licensing stays their responsibility.

**Revisit if:** LLMUnity's grammar support proves insufficient for the schema layer; it
goes unmaintained or breaks against a needed Unity/llama.cpp version; a first-party option
(e.g. Unity Inference Engine) becomes viable for LLM-sized models; or a platform we must
support isn't covered. In any of those cases the fallback ladder is
**fork → vendor → build**, in that order.

### Decisions already recorded elsewhere in this plan

| ID | Decision | Where |
|---|---|---|
| DR-002 | One repository for package, demos, dev project, wiki and tools | §6.1 |
| DR-003 | Demo games live in `Assets/Demos/`, not in the package's `Samples~/` | §1.6 |
| DR-004 | `action` emitted before the free-text field; `target` required with a `no_target` sentinel; no reasoning field | §2.3, Appendix A |
| DR-005 | Grounding guard in code rather than relying on model scale | §2.5, Appendix A |
| DR-006 | Evaluation harness (Phase 4) built before editor tooling and demos | §3 |
| DR-007 | License MIT, pending the university IP check | §1.8 |
| DR-008 | No speaking-character assumption and no input taxonomy: the free-text field is `statement`, not `dialogue`; the developer decides when an agent is asked and passes a free-form stimulus, label and observations; the framework never classifies the call. Rename unmeasured — early Phase 4 A/B | §2.1, §2.3, issues #2, #8, #17, #18 |

---

## Appendix A — carried measurements (source: test-ai-framework prototype, Aug 2026, RTX 4060 Laptop 8GB, qwen3.5 family via Ollama)

| Finding | Numbers |
|---|---|
| Constrained schema + few-shot vs prose-rules baseline (A/B, n=53/arm, 2B, greedy) | 58.5% → **84.9%** correct; wrong-but-legal 20.8% → 3.8%; prompt 332 → 143 tok |
| Field order `action` before `dialogue` (isolated, 5-variant head-to-head) | **+11.7 pts** |
| Generated few-shot block (isolated) | **+35.3 pts** — largest single lever |
| Reasoning field before action (tried, rejected) | **35.3%** vs 41.2% unmodified baseline — made it worse |
| `target` optional → required + `no_target` sentinel | 4B target-naming 0/5 → **5/5**; 2B 3/5 → 5/5 |
| Lexical grounding guard (~10 lines, post-hoc) | 2B 60% → **95%**; 4B 65% → **100%** (20-prompt focused suite); impossible-request refusals 1/6 → 6/6 on 2B |
| 2B + guards vs 4B without | **95% vs 65%** — scaffolding beats scale |
| Residual only model scale fixed | "That's an interesting sword you have." → 2B picks it up (target IS in text; guard correctly passes); 4B refuses. The honest boundary of code-side fixing |
| Gemini schema dialect | rejects `""` in enums (HTTP 400); supports `propertyOrdering`; `gemini-2.5-flash-lite` 404s for new keys → use `gemini-3.5-flash-lite`; free tier ≈15 RPM / 1,000 req-day |
| Cloud free tier vs local (17 prompts) | Gemini 100% acc, median 1.01s, **worst 91.16s**, 11.8% HTTP-503; local 88.2%, median 1.32s, **worst 2.23s**, 0 failures. Bounded tail beats better median for real-time agents |
| Cloud cost at measured token sizes (~194 in / 38 out, Flash-Lite pricing) | ~$0.000153/decision → $1.53 per 100h (1 companion) vs **$165 per 100h** (30 agents @ 6/min) |
| VRAM | model (2B) + minimal scene = **4.2GB / 8GB** |
| Concurrency (serialized queue) | 5 simultaneous requests: 0 failures, 0 cross-talk, ~3.5s batch — fine at 5, ≈21s at 30 |
| Regressions caught by the A/B harness | Run 1: 47.2% (dialogue-first + none-biased prompt); Run 2: 52.8% (bias removed, order still wrong) — both *below* baseline; methodology caught both |

**Not established, inherited as open**: generalization beyond one model family / one
scene / one annotator; statistical power below ~10 pts at n=53; the in-Unity vs
standalone latency discrepancy.

**Shipping story — no longer open, but unproven.** "Players don't have Ollama" has a
concrete answer (an in-process provider built on LLMUnity's embedded llama.cpp, §2.4 and
Phase 6b) rather than being an unresolved risk. It is still *unproven* until a built
player executable runs decisions with no Ollama and no network, and until the accuracy
findings above are re-measured through that provider rather than through Ollama's HTTP
API. Treat every number in this appendix as measured against Ollama specifically until
that comparison exists.
