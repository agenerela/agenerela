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
package**, plus **one Unity project per demo game** that loads the same package from disk
(DR-010). This is the standard Unity package-development pattern: the dev project gives
you a compiler, a test runner, and a place for sample scenes; consumers install the
package alone.

```
<repo root>/
├── README.md
├── AGENTS.md                          ← rules for every AI agent; CLAUDE.md imports it
├── .gitignore                         ← Unity template + secrets rules (see 1.4)
├── .env.example                       ← committed template; .env is gitignored
├── docs/
│   ├── FRAMEWORK_BUILD_PLAN.md        ← this file
│   ├── llm-wiki/                      ← start one immediately (see 1.5)
│   ├── design/                        ← sitemap, screen wireframes, the developer walkthrough
│   └── course/                        ← COMP 490 deliverables, archived
├── tools/
│   └── benchmarks/                    ← standalone Python probes (copy from prototype)
├── UnityProject/                      ← the framework's dev project (Unity 6000.3+; any render pipeline — framework must not care)
│   ├── Assets/
│   │   ├── Evaluation/                ← Phase-4 eval harness + labelled prompt set (see 1.7)
│   │   └── DevSandbox/                ← throwaway test scenes; never referenced by anything
│   ├── Packages/
│   │   └── com.<team>.agentframework/ ← THE PACKAGE — everything shippable lives here
│   └── ProjectSettings/
└── Demos/                             ← THE DEMO GAMES — one full Unity project each (see 1.6, DR-010)
    ├── README.md                      ← checklist for adding a game project
    ├── CompanionRPG/                  ← its own Assets/, Packages/, ProjectSettings/
    ├── GreyBox2D/                     ← proves genre independence
    ├── GreyBoxStrategy/               ← proves "agent ≠ NPC"
    ├── GreyBoxVillage/                ← the guard playground; the first game built
    └── Shared/                        ← local packages two or more games need (created on demand)
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
│   ├── Scheduling/               ← request queue, priorities, cancellation, budgets
│   └── Unity/                    ← AgentBehaviour, Targetable, scene sources: the only folder that touches a scene
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
> actively building. This is why the demo games are full Unity projects under `Demos/`
> instead (§1.6).

Assembly-definition rules (enforce from the first commit):
- `Runtime` asmdef has **zero** references to Editor assemblies and zero `#if UNITY_EDITOR`
  business logic.
- Tests reference Runtime (and Editor where needed); nothing references Tests.
- **Only `Runtime/Unity/` may touch a scene** (DR-011). Everywhere else in `Runtime/`, no
  `GameObject`, `Component`, `MonoBehaviour`, `Transform`, scene query or physics call, so an
  agent can exist with no scene at all — a country in `GreyBoxStrategy` has no Transform, and
  an EditMode test has no scene. `UnityEngine` itself is fine: `Awaitable`, `ScriptableObject`
  and serialization attributes like `[Tooltip]` do not need a scene. The hygiene workflow
  enforces this, along with the no-`UnityEditor` rule above.
- **The core's only third-party runtime dependency is Newtonsoft JSON**
  (`com.unity.nuget.newtonsoft-json`), declared in `package.json` so Package Manager
  installs it with the framework (DR-009). Beyond that, use what Unity 6 ships: `Awaitable`
  for async (not UniTask, not Tasks-over-coroutines hacks) and `UnityWebRequest` for HTTP.
  Core asmdefs set `overrideReferences: true` and list `Newtonsoft.Json.dll` explicitly, so
  a DLL that merely exists in the project cannot leak into the core.
  *Providers are the exception*: the in-process provider depends on LLMUnity for embedded
  llama.cpp (§2.4). Isolate it behind a version define or a separate package so a
  developer using only Ollama or a cloud backend never pulls native binaries they don't
  need. The rule constrains the core; it is not "nothing may ever depend on anything."

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
`docs/llm-wiki/` seeded with `README.md` + an empty `findings.md` (§1.5).

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
   - [ ] `Demos/` and `UnityProject/` do **not** appear anywhere in the consuming
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

### 1.6 Demo games — where they live and why (settled — DR-010)

**Samples and demo games are different artifacts.** Conflating them is the most common
structural mistake in Unity package projects:

| | Samples (`Samples~/` in package) | Demo games (`Demos/<Game>/`) |
|---|---|---|
| Purpose | Teach one API concept | Prove the framework works, and get presented at review |
| Size | Tens of lines, no art | Near-polished games: scenes, prefabs, art, audio |
| Audience | A developer who installed the package | Your review panel, and you |
| Ships to consumers | Yes, on demand via Package Manager | **No** — would bloat every install |
| Compiled during dev | **No** (`~` folders are ignored by Unity) | Yes, in the game's own project |

So: **each demo game is a full Unity project under `Demos/`, outside the package and
outside the dev project.** It consumes the framework exactly the way a real developer
would — through its public API, from a `Packages/` reference — but it is not part of what
gets shipped. Each game loads the package from this repo by relative path, in its
`Packages/manifest.json`:

```json
"com.agenerela.framework": "file:../../../UnityProject/Packages/com.agenerela.framework"
```

Relative `file:` paths resolve from the project's `Packages/` folder, hence three `..`.
Never a version or a git URL: either one pins a copy, and the game quietly stops testing
the framework you are changing. CI rejects both.

**Why separate projects rather than `Assets/Demos/` inside the dev project.** The demos end
as near-polished games, and a polished game tunes settings Unity keeps per project: render
pipeline, layers and tags, the physics collision matrix, quality levels, Player settings.
Three games cannot each have their own in one project. Separate projects also stop one
game's compile error from blocking Play mode in the other two. DR-010 has the options
weighed.

**Each game picks its own render pipeline** — Built-in, URP (with the 2D Renderer for a
2D game) or HDRP. The framework references no pipeline and must keep working under all of
them. A game that needs a pipeline-specific hook has found a missing extension point in the
framework, not a reason to add a pipeline dependency to it.

**Each game gets its own asmdef** referencing only the framework's public assemblies.
This is not bureaucracy; it is the plan's main API-design forcing function:

> If a demo ever needs `InternalsVisibleTo`, a `public` field that shouldn't be public, or
> a copy-pasted chunk of framework code to work, **the framework is missing a public API**.
> Fix the framework, not the demo. Three demos across three genres, each restricted to the
> public surface, is the cheapest available proof that the API is genuinely reusable rather
> than shaped around one game.

Practical notes:
- [`Demos/README.md`](../Demos/README.md) is the checklist for creating a game project.
- Each game is tracked as a sub-issue of #47, and its steps (grey box, first real
  decision, polish) become sub-issues of that game.
- Code two or more games legitimately share — a camera rig, a debug overlay — goes in a
  local package under `Demos/Shared/`, which each game references by `file:` path the
  same way it references the framework. Discipline: if the *framework* would want it, it
  belongs in the framework package; if only demos want it, it belongs in `Demos/Shared/`.
  When in doubt, keep it in the one game that needs it — promoting later is easy,
  un-shipping a bad public API is not.
- Framework work happens in `UnityProject/`, where its tests are. Add new framework files
  from there, so that one editor generates their `.meta` files.
- A framework API change must still compile in every game. Until Unity runs in CI (§6.3),
  that means opening each game project before merging — the main cost of this layout.
- Start each game grey-box and let it break loudly while the API moves; polish comes once
  the phases it exercises are done (§3).
- Art goes through **Git LFS**, already configured in `.gitattributes`. Three polished
  games will press on the LFS quota (§6.4); `Demos/README.md` shows how each person
  fetches only the game they work on.
- The strategy demo is the one that proves the thesis — a *faction* agent with no
  Transform, no navmesh, no dialogue box, driven through the same `Agent` API as a
  talking NPC. If time collapses, cut the 2D demo before this one.

**Also verify the real consumer path.** Game projects compile against the package as an
editable folder on disk, not as the read-only git install a real developer gets, so
packaging mistakes can hide from them. Once per phase, install the package from its git
URL into a *blank* Unity project outside the repo and confirm it compiles and the samples
import. Ten minutes; catches packaging mistakes that no project inside the repo
structurally can.

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
    public Awaitable<DecisionResult> DecideAsync(string stimulus, DecideOptions options = null,
                                                 CancellationToken ct = default);  // decides only
    public void Execute(DecisionResult result);   // re-checks legality, then runs the handler
}

// Thin MonoBehaviour adapter for scene objects, in Runtime/Unity/ (§1.2). A faction
// manager or colony sim can own Agent instances directly with no GameObject involved.
public class AgentBehaviour : MonoBehaviour { public Agent Agent { get; } ... }
```

`AgentDecision` is the bare answer, `{ actionId, targetId, statement }`. `DecideAsync`
returns it inside a `DecisionResult`, beside its `DecisionTelemetry` — latency, token
counts, schema mode, which guards fired (#3). Telemetry is not optional — every decision is
measurable or the eval harness (Phase 4) can't exist. Deciding and executing are separate
calls on purpose: `Execute` re-checks the decision independently before any handler runs
(hard rule 5, #17).

`Memory` is what lets an agent carry a conversation from one decision to the next. It is an
empty slot until Phase 2; what it holds, and the rules it follows, are in §2.9.

### 2.2 Actions (`Runtime/Actions/`) — the heart of the framework

**Settled rule: the action vocabulary is data, not an enum.** The prototype's fixed
`NPCAction` enum is the single biggest thing this rewrite exists to kill. An action is
*data the game developer authors*, and it is a plain type so that nothing about an action
needs a scene (same reasoning that keeps `Agent` off `MonoBehaviour`):

```csharp
// Runtime/Actions — plain data, no scene dependency (DR-011).
[Serializable]
public sealed class ActionDefinition {
    public string Id;                    // snake_case, becomes the schema enum value
    public string Description;           // one short clause — see symmetry rule below
    public bool RequiresTarget;
    public string ExampleStimulus;       // "{0}" placeholder for target; see few-shot rules
    public string[] PreferredExampleTargets;   // targets this verb sensibly applies to
}
```

**Two front doors produce it, and only one registry consumes it (DR-011).** A developer
either writes the action in code, or authors it as an asset:

```csharp
// Front door 1 — code. One script, one method per action, no assets.
[AgentAction("move_to", "Walk to a named place.", RequiresTarget = true)]
[Example("Head over to the {0}.", "tower", "bridge")]
public void MoveTo(AgentContext ctx, Transform target) => nav.SetDestination(target.position);

[Available("move_to")]   // state masking: not offered while already walking
public bool CanMove() => !nav.pathPending && nav.remainingDistance < 0.2f;
// ...registered with agent.Actions.RegisterMethods(this).

// Front door 2 — asset. A thin ScriptableObject wrapper holding one ActionDefinition.
[CreateAssetMenu(menuName = "Agenerela/Action", fileName = "NewAction")]
public sealed class ActionDefinitionAsset : ScriptableObject { public ActionDefinition Action; }
```

Both end as the same `ActionDefinition` in the same registry; nothing downstream — schema,
guards, telemetry, the eval harness — can tell which was used. **Where both define the same
id on one agent, the asset wins**, so a developer can start in code and later drop in an
asset for just the one action whose wording is being A/B tested. The asset wins the
definition, and the method still runs the action: `RegisterMethods(this, profile.Actions)`
registers each matching asset's wording in place of the attribute's (DR-011's note of 28
September). An attributed method's parameters are filled by type: the `AgentContext`, the
`AgentDecision`, and at most one target, passed exactly as it was registered.

Registration binds a definition to gameplay and to availability:

```csharp
public interface IActionHandler {
    bool IsAvailable(AgentContext ctx);        // state masking — see settled rules
    void Execute(AgentContext ctx, AgentDecision decision);   // deterministic Unity code
}
agent.Actions.Register(definition, handler);
```

**A handler is not required to be its own file.** The interface is the contract; an
attributed method, a delegate, a plain class and a `MonoBehaviour` on the same GameObject
are all adapted to it. Three actions on one `VillageGuard.cs` is the expected shape for a
scene NPC, and a plain class is what a `Country` with no GameObject uses.

Targets resolve into the same kind of registry (`TargetRegistry`: id → object reference), so
"what can this agent currently reference" is queryable, not hand-maintained prose. **The
registry is computed per decision rather than typed (DR-014):** a `Targetable` component
marks an object as nameable and carries its id, and the `ITargetSource`s on the agent, an
ordered list, assemble the set each time. A hand-supplied list remains available for agents
with nothing to query from. What the enum contains at the instant of the decision is what
matters; where it came from does not.

**The proximity query is one way to choose targets, not the way.** `ProximityTargetSource`
(radius, layers, categories, optional line of sight, nearest first, capped) suits a scene
agent that should name what is near it, and it is only used if the developer adds it. Who may
name what, and when, is the developer's call: their own `ITargetSource` can follow what a
faction has scouted, a quest stage or the time of day, and several sources can be combined. A
strategy game is the plain case where proximity does not apply: a country has no position,
so it supplies its own set through `ExplicitTargetSource` or a source of its own.

**Settled design rules baked into this module (each is a measured result — Appendix A):**
1. *State masking*: `IsAvailable` decides whether an action appears in the schema enum
   at all. An agent already following is never *offered* `follow_player`. Never write a
   prose rule ("don't repeat an action...") for something the enum can make unexpressible.
2. *Description symmetry*: every action's description is one short clause of comparable
   length. A long emphatic description on one option (the prototype's `none`) biases a
   small model toward it — this alone caused a measured regression.
3. *The idle/none option is listed last* in the enum, so it reads as fallback, not default.

**Revisit later — letting an agent opt out of `none`.** Every agent gets `none` today, added
by the framework (#8). An agent that must act every time it is asked, such as a country
choosing its move each turn, might do better without it, through a switch on its profile.
Not now: a guard rewrites a bad decision to `none` (Phase 3), and #10's idle and refusal
examples both answer `none`, so an opt-out needs another fallback for the guards and drops
both examples with it. It changes what the model can answer, so it needs an A/B with a
control arm before it ships.

**Revisit later — renaming an action after it is registered.** `ActionRegistry` (#6) files
each action under the id it had when `Register` was called, but keeps the developer's own
`ActionDefinition` rather than a copy. Change the id afterwards, for instance in the
Inspector during Play mode, and `Definitions` shows the new id while lookups still expect
the old one: `TryGet` of the new id fails and `HandlerFor` throws. Left this way on purpose
(#57 review): renaming an action mid-game is rare, and keeping the developer's object means a
description edited during Play mode is what the next request reads. Revisit if a game needs
to rename actions at runtime; the options are to copy the definition in `Register`, or to
look handlers up by the definition object rather than its id.

**Revisit later — a target registered as one type and taken as another.** An attributed
method receives its target exactly as it was registered (#50): `MoveTo(AgentContext ctx,
Transform target)` needs a `Transform` in the `TargetRegistry`, and anything else is a clear
error when the action runs. Converting one into the other, a `Targetable` into its
`Transform` say, would take scene calls, which only `Runtime/Unity/` may make. #32 settled
the common case: `ProximityTargetSource` registers each `Targetable`'s `Transform`, so a
`Transform` parameter works with no conversion, and a method that wants the `Targetable` or
another component calls `GetComponent` on it. Revisit if games keep writing that
`GetComponent`: the fix is a conversion that `Runtime/Unity/` supplies, not scene calls in the
reader.

**Revisit later — which targets an action accepts.** Proposed in the COMP 490 requirements
specification (3.4.2.2, October 2026), and not designed: each action declares the target
categories it accepts, so `give` is offered props and characters but never the tower. Today
one `target` enum serves every action, so the model can pair `give` with `tower`, which the
schema allows and the game cannot use. The category already exists as the free-text
`Category` on `Targetable` (#32); a target from `ExplicitTargetSource` has none, so targets
with no scene presence would need one too. How to enforce it is open. A separate target list
per action inside the schema makes the bad pair unexpressible, the same move as state masking
(rule 1 above), but it changes the request shape the prototype measured, so it needs an A/B
with a control arm before it ships. A guard (§2.5) leaves the schema alone but only catches
the pair after the model has chosen it. Revisit when a demo needs it; until then a handler
given a target it cannot use does nothing with it, and once guards land in Phase 3 a
developer's own guard can reject the pair.

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
   `ExampleStimulus` + a `PreferredExampleTarget` **that the verb sensibly applies to**
   (blind rotation produced "Pick up the Blacksmith" — a nonsense demonstration), plus an
   idle example answered with `none`, whose stimulus is the profile's `IdleExampleStimulus`
   (#15), and one negative example showing a refusal with `no_target`. The idle example has
   never been measured on its own; with vs without it is owed in Phase 4
   ([findings](llm-wiki/findings.md)). **Example utterances must never overlap the
   evaluation prompt set** (see Phase 4). Few-shot was the largest single lever measured:
   +35 points.

### 2.4 Providers (`Runtime/Providers/`)

```csharp
public interface ILLMProvider {
    string Name { get; }
    ProviderCapabilities Capabilities { get; }   // constrained decoding? property ordering? enum-of-empty-string?
    Awaitable<ProviderResult> RequestAsync(DecisionRequest req, CancellationToken ct);
}
```

- `OllamaProvider` first (port from prototype's `OllamaClient`: non-blocking, `think:false`
  for Qwen-family reasoning modes, `num_ctx` configurable, telemetry from the response
  envelope). Its default endpoint is `http://127.0.0.1:11434`, not `localhost`: on Windows
  `localhost` resolves to IPv6 first, Ollama listens on IPv4 only, and the prototype's
  Python probes paid about 2 s a request for it ([findings](llm-wiki/findings.md)).
- The abstraction must absorb **schema dialect differences** (that's why `Capabilities`
  exists), not just base URLs. This was proven necessary, not speculative.
- The provider is *untrusted* by design: everything in 2.5 runs regardless of what the
  provider claims to guarantee, so a plain-chat provider with no constrained decoding is
  still contained.
- **Token probabilities, where the backend has them.** The grounding guard (§2.5, DR-016)
  reads, token by token through the model's target, how much of its probability could still
  lead to a target on the list. So `ProviderCapabilities` states
  whether a provider reports token probabilities, and `ProviderResult` carries them for the
  answer. Ollama returns them from before the schema's mask (`logprobs`, checked on 0.34.2),
  for about 0.07 s a decision. LLMUnity's `LLMClient` has an `nProbs` setting for the
  in-process provider, not yet tried; cloud vendors differ by model.

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

**Where configuration lives (DR-013).** Endpoint, model, context size, timeout, requests
in flight and the session budget are a **provider config asset** — several may exist in one
project. A Project Settings page names only the project default and the two evaluation arms.
Resolution is agent override → scene override → project default, so an agent with an empty
provider field works with no configuration at all, and swapping models is selecting a
different asset rather than editing every agent in the scene. The config that actually
resolved is recorded in telemetry, so a misbehaving provider never needs guessing about
which of the three levels won.

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
   in the registry or `no_target`, and an action that needs a target got a real one.
   (Execution-time re-check of what the grammar should have enforced — providers are
   untrusted.)
2. **TargetGroundingGuard: did the model want something that is not on the list?** It runs
   on every decision unless the caller turns it off for that call. From the same request's
   token probabilities (§2.4), it reads, at every token of the model's target, how much of
   the model's probability could still lead to a target on the list, and keeps the lowest.
   Below 0.8, a threshold calibrated in Phase 4, it rewrites the decision to `none`.

   - **"Attack the scarecrow."** scores 0.05, because the model wanted to write "sc…".
   - **"Go to the south tower."** scores 0.007 on the 2B. The start "south" fits
     `south_gate`, but the model then wanted "tower", and no south tower is listed.
   - **"Attack the training dummy."** scores 0.998.

   It needs no word lists, so a paraphrase ("Hit the mannequin.", 0.99) or a reference
   resolved from the previous turn ("Attack it.") passes. Telemetry records the number on
   every decision. DR-016 has the measurements and the options it beat.

   **Why it exists.** A model forced to answer from the target list swaps a missing target
   for a legal one: "Attack Godzilla" attacks the training dummy. That is the worst failure,
   because it looks like obedience. The swap shows in the model's own probability. On a probe
   of 130 prompts in three scenes, the guard took a 2B model from 83 to 108 correct and a 4B
   from 113 to 124. It turned away three good requests the 2B would otherwise have carried
   out, and none on the 4B ([findings](llm-wiki/findings.md)).

   **On by default, whoever calls.** The framework cannot tell a player's line from
   something the game raised, and must not guess (DR-008), but the guard does not need to
   know. A guess and a free choice look different in the numbers. When nothing specific was
   asked for and two targets are equally good, the model splits its probability between
   them, and all of it stays on the list, so the guard passes. Only probability that went to
   something absent counts against the answer. In the probe's open choices, such as "Climb
   either watchtower and look out.", the 4B was only 0.43 sure of the tower it picked, yet
   0.98 of its probability stayed on the list, and the guard passed every one. It refused one
   good game-triggered decision, on the 2B, which weighed writing the event's own word
   "weapon" (0.77). `DecideOptions.CheckTarget` turns it off for one
   call, for a developer who would rather the agent act on its best guess than do nothing:

   ```csharp
   // Checked: the default, whoever is calling.
   await guard.DecideAsync(stimulus);
   // Not checked, for this one call.
   await guard.DecideAsync(stimulus, new DecideOptions { CheckTarget = false });
   ```

   **Without token probabilities**, on a provider that cannot report them, the guard records
   that it could not run. The fallback is the next best option that costs nothing: one line
   in the system prompt, saying that only the listed things are here and the model must never
   act on a different one. `PromptBuilder` adds it when `CheckTarget` is on and the provider
   reports no probabilities. Measured: 2B 73 → 88, 4B 98 → 101 (DR-016).

   **Limits.** A near miss the model takes for the listed thing passes: Excalibur for the
   sword on the 2B, a bucket for the barrel and a lantern for the torch on the 4B. So does a
   pronoun the model resolves to the wrong thing ("Attack it!" after the player mentioned a
   dragon). On a small model, a paraphrase that starts like the right id can be refused. The
   2B began writing "healing" for "Pass me the healing draught.", and the schema completed
   it as `health_potion`, which was right; the guard read the unfinished "healing" and
   refused. A wrong action on the right target
   ("That's an interesting sword you have." picks the sword up) is beyond any target check;
   only model scale fixed it (Appendix A). Any change to the threshold or the signal needs
   an A/B run with a control arm before it ships.

   **The name check it replaces** required the chosen target's name to appear in the
   player's line. Measured on the same probe, it refused 15–16 good requests per model,
   paraphrases and "it". On the 4B it scored below no check at all, 89 against 98. It is no
   longer built in. A developer who wants strict wording can add it as their own guard
   (item 3).
3. Developer-supplied guards append here (game-specific rules: line-of-sight, cooldowns…).

A guard rewriting to `none` is a **contained refusal**, not an error — telemetry records
which guard fired so the eval harness can distinguish "model right", "model wrong but
contained", and "model wrong and executed". After a rewrite, the agent's `statement` no
longer matches what it does: it says "Attacking the dummy!" while nothing happens. So the
guard's reason travels with the result, and the developer supplies the line, or asks the
model for one in a second, short request only when a guard fires.

**Still open, for Phase 4.** The threshold comes from 130 prompts that one person wrote for
three invented scenes, eight of them open choices. Phase 4 calibrates it on the 200+ prompt
set and re-measures through the in-process provider. The 2B answered `none` to six of the
eight open choices by itself, before any guard ran. Whether a few-shot example of a free
choice fixes that is a Phase 4 A/B. When a request names one thing but the probability splits
between two listed targets, asking the player which one they meant may beat guessing, as
KnowNo does (DR-016).

### 2.6 Scheduling (`Runtime/Scheduling/`)

Port the prototype's single-queue serialization as the starting point (measured safe: 5
simultaneous requests, 0 cross-talk), then extend: priority tiers (player-facing preempts
background), per-request cancellation, and a hard per-session request budget for cloud
providers (the Gemini probe scripts were budget-capped for a reason — keep that
discipline). Full scheduler redesign is Phase 6+; don't gold-plate it early. Known
ceiling to state honestly: serialized local inference ≈ 21s for 30 agents/round.

**Priority is declared by the caller (DR-015).** `DecideOptions.Priority` — player-facing
by default, or background — is how the queue tells a request a human is waiting on from one
nobody asked for. It has to come from the caller because the framework never classifies why
an agent is being asked (DR-008): a developer's own `Update` loop, a behaviour-tree node and
a coroutine all look identical to us otherwise, and at a ceiling of ≈21 s for 30 agents that
distinction is the difference between a responsive NPC and a queue full of ambient chatter.

### 2.7 Editor (`Editor/`)

Custom inspectors for `AgentProfile` (identity fields, the drag-in list of action assets —
#15) and `AgentBehaviour` (profile, optional provider override, the targets it discovered —
DR-013, DR-014), `Create → Agenerela → …` menus, and a **Decision Log window** streaming
per-decision telemetry (chosen action, guard verdicts, latency, token counts). The log
window is not a luxury — it is how a developer debugs "why did my agent do that", which is
the framework's main support burden. Built **last** (Phase 5), once the data shapes
underneath have stopped moving.

**The profile inspector shows the idle example on the `none` row.** `none` appears as a
locked last row of the action list (screen 1 of [`docs/design`](design/README.md)). Other
rows expand to show their action's example stimulus, so the `none` row expands to show and
edit `IdleExampleStimulus` — the example for `none`, where a developer looks for it. The
value stays on `AgentProfile`, because `none` has no action asset (#8). Until this inspector
exists, a `[Header("Idle example")]` sets the field apart in the default Inspector. This came
out of the review of #56, where the field read as a stray that belonged on an action.

### 2.8 Observations (`Runtime/Observations/`) — how an agent learns what is around it

`DecisionRequest.Observations` is what the agent knows right now, and it sits *after* the
few-shot block, immediately before the question — the last thing the model reads. That
placement makes it the most powerful block in the prompt and the most damaging place to put
noise. Everything in this module exists to fill it deliberately.

A developer can always pass observations per call, and always could (DR-008). This adds the
alternative, for agents that should notice their own surroundings rather than be told:

```csharp
public interface IObservationSource {
    IEnumerable<string> Observe(AgentContext ctx);   // pulled at decision time
}
```

**Pull-based, never a push bus.** Sources are asked during the decision, in order, and
return short natural-language lines. Nothing accumulates between decisions; the moment this
becomes a subscribe-and-accumulate blackboard we inherit every blackboard problem and lose
the ability to say what an agent knew at one instant.

**Composition and budget.** An agent holds an ordered list of sources; per-call
observations append last. The framework then applies a **hard cap** and records what it
dropped in telemetry. The cap is not tidiness — the measured good arm ran a 143-token
prompt against a 332-token prose baseline, and an unbounded gatherer is the easiest way to
regress accuracy while looking like a model problem.

**Sources that read the scene live in `Runtime/Unity/`.** Nothing else in the runtime may
touch a scene (§1.2, DR-011), so scene inspection ships there:

- `Targetable` — one component a developer puts on a scene object, carrying its **id**, a
  one-line self-description ("The gate is open.") and a category. It is deliberately the
  same component that makes the object nameable (DR-014): an object an agent can notice and
  an object it can refer to are the same object, and having one component guarantees the
  line the agent reads and the id it may choose come from the same place.
- `ProximityObservationSource` — collects lines from `Targetable`s within a radius, nearest
  first, so the cap truncates the far ones rather than arbitrary ones.

**An LLM summariser is one source, not a stage.** A source may call a model to compress a
large world state into prose, and it implements the same interface as any other — a front
door, not a branch in the pipeline. Three conditions apply, and they are what keep it from
breaking the rest of the framework:

1. **Cached and invalidated by an event** — per turn, per world change — **never run per
   decision.** A summary call per decision doubles latency, and the serialized queue is
   already ≈21 s for 30 agents.
2. **Registered target ids appear verbatim.** The model answers with ids, so observations
   should use them. A summariser that turns `tower` into "the big stone tower" leaves every
   agent that reads it to map the paraphrase back to the id, which is unmeasured and may
   cost accuracy. This condition was first written to protect the lexical name check, which
   DR-016 replaced; it still holds for this reason.
3. **Recorded as part of the arm in any evaluation run.** Generated observations are
   non-deterministic input, and a run that varies its own inputs cannot resolve a 10-point
   noise floor.

**Settle it by measurement, not by argument.** "Templated observations vs LLM-summarised
observations" is a clean Phase 4 A/B with a control arm. Build the interface in Phase 2,
when observations first reach a model; run the comparison in Phase 4 and record it in
`docs/llm-wiki/findings.md` (DR-012).

### 2.9 Memory (`Runtime/Memory/`) — what an agent remembers between decisions

**Status: planned for Phase 2, nothing built.** The slots exist already: `Agent.Memory`
(§2.1) and `DecisionRequest.History` (#12), which stays an empty list until a memory strategy
fills it. This section records what memory is for and the rules it has to follow, so that
Phase 2 designs it rather than bolting it on at the end.

**What it is for.** Without memory every decision starts from nothing. The guard cannot
follow "Follow me." with "Now wait here.", and it forgets that the player threatened it a
minute ago. Observations (§2.8) say what is true *now*; memory says what *happened*. They are
kept apart on purpose: §2.8 forbids anything accumulating between decisions, and memory is
the one deliberate exception, bounded and owned by a single agent.

```csharp
// Sketch only. Phase 2 settles the shape.
public interface IMemoryStrategy {
    IReadOnlyList<string> Recall(AgentContext ctx);            // turns for this request, oldest first
    void Remember(AgentContext ctx, DecisionResult result);    // after the guards have run
}
```

**The default is short-term:** `RollingHistory(turns: 6)` (§2.1), the last six exchanges,
oldest first. Where they go in the prompt is already fixed by `DecisionRequest`: after the
system side (system prompt, few-shot block, observations) and before the stimulus, which
stays the final user turn.

**Rules Phase 2 must keep:**

1. **Remember what happened, not what the model said.** A turn records the decision
   `DecideAsync` returns, after any guard has run, never the provider's raw text. If the
   grounding guard (Phase 3) rewrote "attack the training dummy" to `none`, memory
   holds `none`. Otherwise the next request tells the model it attacked something it never
   attacked, and the provider's untrusted output (§2.4) gets back in through the side door.
2. **One memory per agent instance.** Ten guards share one `AgentProfile` (#15), never their
   memories. An agent with no scene remembers the same way: a country's turns are the
   reports it read and the moves it made.
3. **Bounded by tokens, not only by turns.** History sits closer to the question than any
   other block, so §2.8's warning about placement applies with more force, and every turn is
   paid for on every request. The measured good arm ran a 143-token prompt (Appendix A) and
   the queue's ceiling is ≈21 s for 30 agents (§2.6). So the window gets a token cap, and
   telemetry records how many turns were sent and how many were dropped. #16 already lists
   truncating history to `num_ctx` as Phase 2 work.
4. **How a turn names its speaker is decided once, in Phase 2.** The remarks on
   `DecisionRequest.History` forbid inventing an encoding before then.
5. **Memory works with the grounding guard, not around it.** The guard reads where the
   model's probability went (§2.5, DR-016). So "Attack it." after "Do you see the training
   dummy?" passes when the model resolves "it" to a target on the list, and is refused when it
   reaches for something that is not there. On the probe the 2B resolved 2 of 3 such
   references and the 4B all 3. The legality guard
   and `Execute`'s re-check still run on every decision, whatever the history says.

**Long-term memory is a strategy, not a new stage.** Remembering beyond the window, such as
that the player lied yesterday or which faction betrayed which, fits behind the same
interface: a strategy that keeps facts, or one that summarises old turns. A summarising
strategy obeys the three conditions on the LLM summariser in §2.8: cached and refreshed by an
event rather than per decision, registered ids verbatim, and recorded as part of the arm in
any evaluation run. It is not planned for Phase 2; the interface simply must not rule it out.

**Measure it like any other block.** No measurement in Appendix A covers history, so nothing
is known yet about how it moves accuracy. Phase 4 owes multi-turn cases in the labelled set,
each with its history staged like any other precondition (§4, item 4), an A/B of history on
against off, and a comparison of window sizes, all recorded in `docs/llm-wiki/findings.md`.

**Revisit later: saving memory with the game.** Memory lives in the agent object, so it is
gone when the scene unloads. A save system will want to export and restore it, and the
strategy interface is where that would go. Wait until a demo needs it.

---

## 3. Build order — phases, each independently shippable

| Phase | Deliverable | Definition of done |
|---|---|---|
| **0. Skeleton** | Repo + package layout of §1 | Checklist §1.3 all green |
| **1. Core + Actions + Schema** (pure C#, no LLM, no scene) | `Agent`, `ActionDefinition`, registries, `DecisionSchema` builder + Ollama-dialect serializer | EditMode tests prove: state masking (following agent's schema omits `follow_player`); `target` required w/ `no_target`; field order action→target→statement; few-shot block matches registered actions and rotates only sensible example targets; empty target-registry removes `target` property entirely. **All testable without any model running — this is why Phase 1 has no LLM.** |
| **2. OllamaProvider + queue + memory** | End-to-end decision in a sandbox scene, with short-term memory (§2.9) | PlayMode test (tagged `RequiresOllama`): one agent, five actions, live decision round-trip < 5s; telemetry fields populated; provider failure (missing model) surfaces as a typed error, not an exception leak. EditMode tests for `RollingHistory`: it fills `DecisionRequest.History` oldest first within a token cap, records the decision `DecideAsync` returns rather than the provider's raw answer (so a Phase 3 guard's rewrite is what gets remembered), and keeps one memory per agent instance |
| **3. Validation pipeline** | Guards of §2.5 wired between provider and handler | EditMode tests with hand-built fake decisions: substitution attack rewritten to `none`; unavailable action rejected; an action that needs a target, answered with `no_target`, rejected; guard verdicts appear in telemetry. With `CheckTarget` on (the default), a decision where less than the threshold of the model's probability stayed on the target list is rewritten to `none`, and one split between two listed targets passes; with it off for a call, the guard does not run on that call; a provider that reports no probabilities is recorded as such rather than passing silently. Integration: prototype's "impossible request" suite passes ≥ 95% on a 2B model |
| **4. Evaluation harness** | The measurement instrument — **before more features** | 200+ labelled prompts (grow from the prototype's 53), each declaring its required precondition state, and covering game-triggered stimuli (events, reports, turns) as well as player commands rather than mostly commands; runner executes A/B (two configs, same model/session) and writes a classified report (correct / wrong-legal / contained / rejected / pipeline-error); second annotator labels a subset, agreement reported. Methodology checklist (§4) committed to the wiki |
| **5. Editor tooling** | §2.7 | A developer with zero framework knowledge builds a working 3-action agent in an empty scene in < 15 min without editing framework source (actually run this test on a teammate) |
| **6. Cloud API providers** | Proof the abstraction is real, and the path to supporting any vendor | At least one cloud provider (Gemini first — it is what the prototype measured) runs the same eval subset through `ILLMProvider` with **zero framework-code changes**: a provider class plus config, nothing more. A **provider conformance test suite** exists that any future vendor must pass, so adding OpenAI or Anthropic later is implementing an interface rather than editing the framework. Budget caps and RPM throttling enforced in code, not convention |
| **6b. In-process provider** | `GbnfSerializer` (§2.3) + `LLMUnityProvider` — the one a player can actually run (§2.4) | A **built player executable** (not the Editor) runs agent decisions with **no Ollama and no network** — the deliverable that makes "local-first" true rather than aspirational. Also: GBNF serializer emits the same constraints as the JSON-Schema path (unit-tested against the same `DecisionSchema` fixtures); same eval subset passes within noise of the Ollama arm; LLMUnity pinned to an exact version and **not** in `dependencies` (version-defined, so users who don't want it never add OpenUPM's scoped registry); model file's license recorded in the wiki |
| **7. Samples** | Three tiny in-package samples (`Samples~/`): minimal agent, targeted actions, custom provider | Each is tens of lines with no art; all three import cleanly via Package Manager into a blank project |
| **8. Demo games** | `Demos/` — three near-polished games, each its own Unity project: `CompanionRPG`, `GreyBox2D`, `GreyBoxStrategy` | Each game's asmdef references **only** the framework's public assemblies — no `InternalsVisibleTo`, no copied framework code — and the game loads the package by relative `file:` path. `GreyBoxStrategy` drives a faction agent with no Transform through the same `Agent` API as a talking NPC. See §1.6 and DR-010 — this phase runs *in parallel* with 2–7, not after |

**Demo games run alongside phases 2–8, not after them.** They are the integration test for
every phase — start `GreyBox2D` as soon as Phase 2 produces a working decision, and let it
break loudly whenever the API changes. Suggested cadence:

| After phase | Demo milestone |
|---|---|
| 2 (provider works) | `GreyBoxVillage` — the guard's placeholder string matching, from #19's grey box, gives way to a real decision. First proof the API is usable from outside the package. Then `GreyBox2D`: the same API in a second genre, no art yet. |
| 3 (guards) | `GreyBoxStrategy` — a **faction** agent with no Transform and no dialogue box. The thesis demo; build it early, because if the API can't express a non-NPC agent you want to know in month two, not month eight. |
| 5 (editor tooling) | `CompanionRPG` — started last on purpose: it's the demo that benefits most from Inspector tooling existing. |
| 8 | A player build of each game for the review presentation. Polish on all three runs through COMP 491 (§6.5). |

Phases 1–3 are the critical path and port proven prototype logic — low research risk.
Phase 4 is deliberately *before* editor polish: every accuracy claim after it inherits its
credibility from that instrument. If the semester compresses, cut polish first — a game's
art is the least load-bearing thing in the project — then drop `GreyBox2D` entirely.
**Never** cut `GreyBoxStrategy`, which is the only demo that proves the genre-agnostic
claim, and never cut Phase 4.

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

A measurement issue, most likely explained on 5 October 2026: the prototype measured ~1.3s
mean latency in-Unity but ~3.5s from standalone Python scripts, same model/machine. The
scripts called `localhost`, which on Windows tries IPv6 first while Ollama listens on IPv4
only, and each request waited about 2.1 s for the failed attempt ([findings](llm-wiki/findings.md)).
Use `127.0.0.1` in probes and as the provider's default endpoint. Unity was not re-measured,
so until it is, trust *relative* comparisons within one environment over absolute latency
claims across environments.

**Not yet scheduled: VRAM monitoring.** Nothing watches GPU memory while a local model
runs. The one figure we have, a 2B model plus a minimal scene at 4.2 GB of 8 GB
(Appendix A), was a single reading, and item 2 above is checked by hand with `ollama ps`.
That matters because running out of VRAM does not fail: the part of the model that does not
fit runs from system memory or on the CPU, and every latency number after that is quietly
wrong. When someone picks this up, record first and build a dashboard later, if ever:

- **During measurement.** The benchmark scripts and the Phase 4 harness log, per arm, which
  models are resident and how much of each sits in VRAM — Ollama's `/api/ps` reports `size`
  and `size_vram` for each model, and `ollama ps` shows the CPU/GPU split — and flag a run
  where a second model is loaded or the model is not fully on the GPU. On NVIDIA hardware
  `nvidia-smi` gives the whole card's usage, renderer included.
- **In a game.** Once the in-process provider ships (Phase 6b), the model shares the card
  with the renderer, so a developer needs to see the headroom. Telemetry or the Decision
  Log window (§2.7) is the natural place for it.

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
For a team of six it is clearly correct:

- **Atomic commits.** Change the API and update every demo in the same commit. With split
  repos, every breaking change becomes a two-repo dance and the demos drift.
- **The demos are your integration tests.** They should break in the same CI run that
  builds the framework, not silently rot in another repository. Each game is its own
  Unity project (DR-010), so that CI run has to compile every one of them (§6.3).
- **One clone for reviewers.** Your advisor and panel see everything at once.
- **No submodule pain.** Git submodules are the single most common source of "it doesn't
  build on my machine" in student projects.
- **Consumers don't get the demos.** With `?path=`, only the package subfolder is
  *installed* into a consuming project — the demos and dev project never appear in a
  user's `Packages/` folder. The convenience costs your users nothing in what they end up
  with.

*Caveat:* UPM resolves a git dependency by cloning the repository before extracting the
subfolder, so total repo size can affect install time even though installed size doesn't
change. Keep the games' art proportionate, use LFS, and this stays a non-issue at
student-project scale. Revisit only if the repo grows into the gigabytes — which three
near-polished games (DR-010) make a real possibility rather than a theoretical one.

**Own the repo in a GitHub organization, not a personal account.** Free, takes two
minutes, and means the project survives either teammate's account, and access can be
granted to your advisor without transferring anything.

### 6.2 Unity workflow for a team

Unity + Git has specific failure modes. Agree on these in week one. This plan was drafted
for two people; the team is now **six**, which changes none of the rules below and makes
the scene-ownership one matter considerably more.

- **Pin the Unity version exactly.** `ProjectSettings/ProjectVersion.txt` is committed;
  if one person opens the project in a newer patch release it rewrites that file and can
  silently upgrade serialized assets. Nobody upgrades Unity unilaterally. Every game
  project uses the same version as `UnityProject/`, and CI fails if one differs.
- **Scenes and prefabs merge badly.** Two mitigations, use both: configure
  **UnityYAMLMerge** (ships with Unity, wire it up per `.gitattributes` above), and adopt
  the social rule *one person owns a scene at a time*. Most scene conflicts are avoided by
  talking, not tooling.
- **Branching:** `main` protected and restricted to the owner; **`test` is the integration
  branch** everyone branches off and opens pull requests into; short-lived feature branches;
  one teammate reviews each. A green `Repo hygiene` check is required before a pull request
  can merge into `test`. Review is also the most reliable way to keep the wiki's findings
  honest, since the reviewer is the one who asks "where's the A/B run?"
- **Force Text serialization + visible meta files** (Unity's default now — verify, don't
  assume).

### 6.3 CI — start smaller than you think

Unity in CI needs **license activation**, which for GitHub Actions (via GameCI) means
storing an activation file in repository secrets. It works, but it is fiddly and a classic
week-long time sink. Recommended sequencing:

1. **Phase 0–2:** CI runs only the non-Unity parts — markdown link check, Python probe
   linting. Run Unity EditMode tests locally before pushing. (Both checks are in
   `repo-hygiene.yml`, beside the ones for the hard rules a machine can check.)
2. **Phase 3+**, once the test suite is worth protecting: add GameCI with EditMode tests.
   Keep PlayMode/Ollama tests **out** of CI permanently — no runner has a GPU or a model,
   and tagging them `RequiresOllama` (per §4) exists exactly so CI can skip them.
   GameCI must also compile **every game project**, not just `UnityProject/` — until it
   does, checking that a framework change still compiles in each game is manual (DR-010).
   The hygiene workflow already finds every project by its committed
   `ProjectSettings/ProjectVersion.txt`; reuse that rule rather than listing projects.

Don't let CI setup block Phase 1. A green test suite you actually run beats a red pipeline
nobody looks at.

### 6.4 Quotas and limits to know before you hit them

| Thing | Limit | Matters when |
|---|---|---|
| GitHub LFS (free tier) | ~1 GB storage, ~1 GB bandwidth/month | Demo game art; three near-polished games plus CI pulls will find this fast. Each person can fetch only their own game's files (`Demos/README.md`) |
| Gemini free tier | ~15 req/min, ~1000 req/day | Any cloud eval run — budget-cap it in code, as the prototype scripts do |
| GitHub Actions (free) | 2,000 min/month private repos | Unlimited if the repo is **public** — another reason to open-source if IP policy allows |
| VRAM | 8 GB typical student laptop | Model + game must coexist; see Appendix A |

### 6.5 Map phases to the academic calendar

The eight phases are not evenly sized. A realistic two-semester split:

| Term | Phases | What you can demo at the end |
|---|---|---|
| **COMP 490** (fall) | 0–4, plus `GreyBox2D` and `GreyBoxStrategy` | A working, *measured* framework: agents decide correctly ~95% with guards, proven on two genres including a non-NPC faction agent. This is a complete, defensible result even if 491 went badly. |
| **COMP 491** (spring) | 5–8, plus `CompanionRPG`, polish on all three games, and the polished write-up | A framework other developers can actually use: Inspector tooling, second provider, samples, three near-polished demo games, final paper/poster. |

Deliberately front-loading measurement (Phase 4 in the fall) means **your headline claim
exists by December**. Spring is then about usability and presentation, which is far lower
risk than discovering in March that the accuracy story doesn't hold.

**Map your course's actual deliverables onto this now** — proposal, design document,
progress reports, poster, demo day, final report all have fixed dates, and they are
*deliverables*, not overhead. Put every one in the repo's issue tracker or a milestone
board in week one. Senior design projects fail on missed paperwork far more often than on
technical difficulty.

### 6.6 Divide the work along seams, not files

Split by **module boundary** so two people are rarely in the same file, and give every
module one owner who reviews changes to it. With six people that is roughly:

- **Core + Actions** (§2.1–2.2) — the agent, the decision types, the action registry.
- **Schema + prompt assembly** (§2.3) — the request the provider actually sends, and the
  measured rules baked into it.
- **Providers + Scheduling** (§2.4, §2.6) — the transport, the queue, the budgets.
- **Validation** (§2.5) — the guard pipeline, which is the product's safety claim.
- **Evaluation** (Phase 4) — the instrument every later claim depends on. Shared by
  definition: inter-annotator agreement is impossible solo, so it needs at least two people.
- **Demos** — one owner per game, and they keep that game compiling as the API moves.

Two rules matter more than the exact split. **Nobody builds the demo that exercises their
own subsystem**: handing it to someone else is a free API usability test, and if the author
has to explain their own API to a teammate, the API needs work. And **one person owns a
scene at a time** — with six people, scene conflicts are the failure mode most likely to
cost a day.

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

### DR-009 — Newtonsoft JSON is the core's one dependency

**Status:** Decided, September 2026. Amends hard rule 6 in `AGENTS.md`.

**Context.** Rule 6 kept the core free of third-party runtime dependencies. Adding MCP for
Unity to the dev project brought in `com.unity.nuget.newtonsoft-json` as a transitive
dependency, and LLMUnity declares it too. Unity auto-references that DLL in every assembly
that does not set `overrideReferences`, and the core's asmdefs did not — so core code could
have used Newtonsoft, compiled and passed tests here, and then failed to compile for any
user whose project lacked it. Meanwhile the core needs JSON in both directions from Phase 1:
emitting schemas and parsing replies. Unity's built-in `JsonUtility` maps only onto fixed
classes and silently fills missing fields with defaults, so a reply without `target` would
parse without complaint.

**Options considered**

| Option | Cost | Verdict |
|---|---|---|
| Stay dependency-free: `JsonUtility` plus our own JSON reader | A reader to write and maintain, for a solved problem | Rejected |
| **Declare Newtonsoft in `package.json`** | One line; Package Manager installs it from Unity's own registry | **Chosen.** |
| Use it without declaring it | Nothing here; compile errors for users whose project lacks it | Rejected — the failure this record exists to prevent |

**Decision.** The core depends on `com.unity.nuget.newtonsoft-json`, declared in the
framework's `package.json`. It is the only exception to rule 6; any further core
dependency needs its own record. Core asmdefs set `overrideReferences: true` and list
`Newtonsoft.Json.dll` explicitly, so a DLL that is merely present in the project — from MCP
for Unity, LLMUnity or anything else — stays invisible to the core, and an undeclared
dependency fails to compile here rather than on a user's machine.

**Why this does not undercut rule 6.** The rule exists so that installing the framework
never forces a scoped registry or native binaries on anyone (§2.4). Newtonsoft is managed
code from Unity's own registry, and Package Manager installs it with no action from the user.

**Consequences**
- Declared as `3.2.2`, the version the dev project resolves on Unity 6000.3. Package
  Manager picks the highest version any package requests, so MCP for Unity asking for
  `3.0.2` does not conflict.
- Projects that ship a loose `Newtonsoft.Json.dll` (old Asset Store packages, a copy in
  `Plugins/`) get a duplicate-assembly error and must remove one copy. The same is true of
  every package that depends on it, MCP for Unity and LLMUnity included.
- Newtonsoft deserialises by reflection, so IL2CPP managed-code stripping can remove members
  it needs in a player build. Types it deserialises into need `[Preserve]` or a `link.xml`
  entry; the Phase 6b player build is where this would surface.

**Revisit if:** duplicate-DLL reports become a recurring support problem, or Unity's move to
CoreCLR makes `System.Text.Json` available in players, which would let the core drop the
dependency.

### DR-010 — Each demo game is its own Unity project

**Status:** Decided, September 2026. Replaces the location half of DR-003; its other half —
demo games never ship in the package's `Samples~/` — stands.

**Context.** DR-003 put the three demos in the dev project's `Assets/Demos/`, which suited
the original scope of one polished game and two deliberately ugly grey-box ones. The team
has since decided all three will end as near-polished games. A polished game tunes settings
Unity keeps once per project — render pipeline, layers and tags, the physics collision
matrix, quality levels, Player settings — so three games in one project would each have
to work around the other two. Sharing a project would also mean six people editing the same
few `ProjectSettings/` files, one game's compile error stopping the other two from entering
Play mode, and everyone importing every game's art and packages.

**Options considered**

| Option | Cost | Verdict |
|---|---|---|
| All demos in `UnityProject/Assets/Demos/` (DR-003) | Shared settings and one render pipeline for three games; one team's compile error blocks the others | Rejected once the demos became full games |
| **One Unity project per game under `Demos/`, same repo** | Every framework change must compile in each game project; one `Library/` per project | **Chosen.** |
| One repository per game | Every framework change becomes a multi-repo change and the demos drift (§6.1) | Rejected — DR-002 still holds |

**Decision.** Each demo game is a full Unity project at `Demos/<Game>/`, on the same
pinned Unity version as `UnityProject/`, and loads the framework from this repo through a
relative `file:` path in its `Packages/manifest.json`. `UnityProject/` remains the
framework's dev project: the package, its tests, the evaluation harness and the sandbox.
Each game picks its own render pipeline; the framework must work under any of them.
`Demos/README.md` is the checklist for adding a game.

**Consequences**
- Atomic commits survive. They come from having one repo (DR-002), not one project.
- A game sees only what is inside the package — closer to a real install than
  `Assets/Demos/` was, where demo code could lean on anything else in the dev project.
- Until Unity runs in CI (§6.3), checking that a framework change still compiles in every
  game means opening each project by hand. This is the main cost.
- No launcher build that switches between games; each is built and presented on its own.
- Code two games share goes in a local package under `Demos/Shared/`, never copied.
- The hygiene workflow finds every Unity project by its committed
  `ProjectSettings/ProjectVersion.txt` and runs each check on all of them: `.meta` parity,
  generated files, the pinned Unity version, and a relative `file:` reference to the
  framework. Adding a game needs no CI change.
- Scope: §1.6's old rule to keep two demos deliberately ugly no longer applies. The cut
  order in §3 still does — polish goes first, then `GreyBox2D`, never `GreyBoxStrategy`.
- Three polished games make the repo-size caveat (§6.1) and the LFS quota (§6.4) real
  constraints.

**Revisit if:** the repo grows into the gigabytes and slows installs from the git URL
(§6.1), or a game needs its own release cadence, at which point it can move to its own
repository and install the framework from the git URL like any consumer.

### DR-011 — Actions are authorable in code *and* as assets, over one plain data type

**Status:** Decided, 19 September 2026; amended 20 September 2026 (below). Refines §2.2;
supersedes nothing measured.

**Context.** §2.2 originally made `ActionDefinition` a `ScriptableObject`, which made one
`.asset` per verb mandatory. Walking the developer's whole path end to end (the workflow
walkthrough, September 2026) showed what that costs at the smallest size: a three-action
guard needed three action assets, a profile asset and — as first drawn — three handler
scripts, before anything ran. For most games that ceremony buys nothing, and it is a poor
fit for Phase 5's definition of done, which is a stranger building a working three-action
agent in under fifteen minutes. The opposite extreme, attributes only, fails us for a
reason specific to this project: description and example wording is exactly what Phase 4
A/B-tests, and text living in a C# attribute cannot be varied between arm A and arm B
without an edit and a domain reload, so the harness could not drive it.

**Options considered**

| Option | Cost | Verdict |
|---|---|---|
| ScriptableObject only (as first written) | One asset per verb, always; wording is designer-editable and A/B-swappable | Rejected as the *only* path — too much ceremony for a small agent |
| Attributes/code only | Least ceremony; wording cannot be varied without a recompile, so Phase 4 cannot drive it, and no non-programmer can edit it | Rejected as the *only* path |
| **Both, over one plain `ActionDefinition`** | Two small readers and one conflict rule; every front door needs the same conformance tests | **Chosen.** |

**Decision.** `ActionDefinition` is a plain serializable type with no scene dependency, in
`Runtime/Actions/` beside its wrapper. `ActionDefinitionAsset : ScriptableObject` is a thin
wrapper holding one.
Attributed methods and asset files are both *front doors* that produce the same value in
the same `ActionRegistry`. On an id collision within one agent, **the asset wins**.
`IActionHandler` stays the contract, and a handler is never required to be its own file:
attributed methods, delegates, plain classes and `MonoBehaviour`s are all adapted to it.

**Consequences**
- The minimum viable agent is one script and zero assets, using the project's default
  provider. The asset path remains first-class, not legacy.
- The Inspector shows the action list either way. In code mode the fields are read-only
  and link to the declaring line; the *actions → handler* binding column disappears
  entirely, because the method is the handler.
- Everything downstream sees only the registry, so the measured rules — state masking,
  field order, `none` last, `target` required — are implemented and tested once.
- **Every front door must be covered by the same Phase 1 EditMode assertions**, as one
  parameterized fixture. Three registration paths without that is three implementations
  that drift until a demo breaks.
- Reflection over attributes needs `[Preserve]` or a `link.xml` to survive IL2CPP
  stripping. That belongs in the Phase 6b player test, not in a demo-day surprise.
- Issues #4 and #6 change shape: #4 delivers the plain type plus the asset wrapper, and #6
  owns the conflict rule and the shared conformance fixture. #15 changes too: a profile's
  action list holds `ActionDefinitionAsset` references, because a list of the plain type would
  embed copies, and editing `move_to.asset` would then change no agent.

**Revisit if:** the attribute reader's cost in Editor scan time or IL2CPP workarounds
exceeds what it saves, in which case the fluent code API stays and the attributes go — the
core type and the registry are unaffected either way.

**Amendment, 20 September 2026 — the rule is "no scene", not "no `UnityEngine`".** As first
written, this record put `ActionDefinition` in Core with no `UnityEngine` reference. Building
it (#35) showed the cost that wording never counted: Unity draws tooltips from attributes on
the inner type's own fields, so an engine-free type has no Inspector tooltips unless a
hand-written `PropertyDrawer` supplies them — and on this type the tooltip is the spec at the
point of authoring. The wording also over-reached: Core was never going to be engine-free.
§1.2 chose `Awaitable` deliberately, `ILLMProvider` already returns it and `Agent.DecideAsync`
is specified to (#17), and `AgentProfile` (#15) is itself a `ScriptableObject`, while the
Phase 4 harness runs inside Unity (§1.7). What the separation actually protects is **scene independence**, the reason
`Agent` stays off `MonoBehaviour` and the thing that lets a country with no Transform be an
agent. So: `UnityEngine` is allowed, scene types are not, and the type sits in
`Runtime/Actions/` beside its wrapper, which is where §1.2 already put action definitions.
§1.2 states the rule; CI enforces it. Revisit only if something must run the core outside
Unity, which would also mean replacing `Awaitable` with `Task` across the provider contract
and `Agent`.

**Note, 23 September 2026 — where the code front door is built.** The decision stands; only
the issues moved. #4 shipped the plain type and the asset wrapper (#35, #37) as planned, but
the code front door never had an issue, and #6 never took on the conflict rule or the shared
conformance fixture. All three now live in #50: the rule and the fixture only mean something
once both front doors exist, and #6 stays on the Phase 1 critical path without them. #50 is
Phase 1 work that anyone can claim, not part of the gate: `Agent` (#17) can already be built
from a list of `ActionDefinition`s, and none of #11's five checks needs the code front door.

**Note, 28 September 2026 — what the asset wins.** Building the code front door (#50)
settled two things the decision left open. The asset wins the *definition*, not the handler:
this record's own use case, moving one action's wording into an asset for a Phase 4 A/B run,
works only if the method keeps running the action, so
`ActionRegistry.RegisterMethods(owner, assets)` registers each matching asset's definition in
place of the attribute's. An action still has exactly one handler, so an id bound through
`Register` cannot also come from a method; that stays an error rather than silently dropping
one of the two. And the code door gained `[Available]`, which the workflow walkthrough
already drew: without it, an action defined in code could never be masked by state. The
shared fixture runs four registration paths, the asset winning over an attribute among them.

### DR-012 — Agents may gather their own observations; the summariser is a source, not a stage

**Status:** Decided, 20 September 2026, except the last clause — whether an LLM summariser
beats templated lines is an open Phase 4 measurement, not an opinion to settle now.

**Context.** DR-008 leaves it to the developer to pass observations per call. That is right
for a guard with two facts and wrong for an agent that should notice things: a colony, a
country, an NPC that should react to what is near it. The team proposed a first model call
that writes a prose summary of the surroundings, which would then be passed to the decision
call. The intuition behind it is correct — an agent reasoning over natural-language facts is
what makes the decision human-like — but generating those facts with a model, per decision,
conflicts with three things this project has already measured.

**Options considered**

| Option | Cost | Verdict |
|---|---|---|
| Per-call observations only (DR-008 as it stood) | Nothing to gather from; five call sites retype the same facts | Kept, but no longer the only way |
| LLM summariser pass before every decision | Doubles latency (≈21 s → ≈42 s for 30 agents); paraphrases target ids and disables the lexical grounding guard; makes eval inputs non-deterministic against a 10-point noise floor | Rejected **as a pipeline stage** |
| **`IObservationSource`, pulled and capped, with the summariser as one optional source** | Two small pieces and a cap; the summariser's cost is paid only by projects that opt in | **Chosen.** |

**Decision.** §2.8. Observations are gathered by pull at decision time from an ordered list
of `IObservationSource`, capped, with drops recorded in telemetry. Scene-reading sources
(`Targetable`, `ProximityObservationSource`) ship in `Runtime/Unity/`, the one folder allowed
to touch a scene (§1.2). A summarising source is supported and must be cached rather than run
per decision, must emit registered target ids verbatim, and must be recorded as part of the arm
in any evaluation run.

**Consequences**
- The prompt gains a budget that is enforced in code rather than trusted to a developer.
  Telemetry says what was dropped, so a thin-looking prompt is diagnosable.
- Grounding stays lexical and keeps working, because the component that writes a line and
  the id the model may choose are the same object.
- "Why did my agent do that" gains one hop only for projects that opt into a summariser.
- An early Phase 4 A/B — templated versus summarised observations — is added to the
  evaluation backlog alongside the `dialogue` → `statement` rename (DR-008).

**Revisit if:** the A/B shows summarised observations winning by more than the noise floor,
in which case the caching and id-verbatim conditions stay and the default changes.

### DR-013 — Provider configuration is an asset, with a three-level resolution chain

**Status:** Decided, 20 September 2026.

**Context.** The Agent Behaviour wireframe (screen 2) drew the provider as a dropdown on
each agent, which means a village of eight guards carries eight copies of the endpoint,
timeout and budget. The obvious correction — one Project Settings page — cannot express
what Phase 4 requires: an A/B run needs **two** configurations alive in the same session,
each nameable, so the harness can point arm A and arm B at them.

**Options considered**

| Option | Cost | Verdict |
|---|---|---|
| Per-agent fields only (as drawn on screen 2) | Endpoint and budget duplicated per agent; changing a model means editing every agent | Rejected |
| Project Settings singleton only | One place to look; cannot hold two configurations, so Phase 4's A/B cannot be driven from it | Rejected |
| **Config asset + settings page naming the defaults** | Three places a value can come from | **Chosen.** |

**Decision.** A provider config is a ScriptableObject asset: backend, endpoint, model,
context size, timeout, requests in flight, session budget. Several coexist — `OllamaLocal`,
`OllamaLocal-noGuards`, `GeminiFlash`. A Project Settings page names the project default and
the two evaluation arms and holds nothing else. Resolution runs agent override → scene
override → project default. API keys are never serialised into the asset (hard rule 1); the
field records where the key is read from, not its value.

**Consequences**
- A freshly added `AgentBehaviour` with an empty provider field works immediately, which is
  what keeps the Phase 5 fifteen-minute target reachable.
- Phase 4's harness takes two asset references and needs no other configuration surface.
- Swapping a model across a whole game is selecting a different default, not an edit pass.
- **Three levels mean three places to look when a provider misbehaves**, so the resolved
  config is recorded in decision telemetry and shown in the Decision Log. This is the cost
  of the choice and it is paid in the log window, not in documentation.
- The scheduler's queue and budget belong to the provider config, not to an agent —
  requests in flight is a property of the backend (§2.6).

**Revisit if:** the scene-override level turns out to be unused after the demos are built,
in which case it can be dropped to two levels without touching the asset or the settings
page.

### DR-014 — Targets are discovered from the scene, not typed per agent

**Status:** Decided, 20 September 2026. Replaces the per-agent target list drawn on screen 2.

**Context.** Screen 2 drew the target registry as a list the developer fills in on every
agent. In a village where eight guards can reach the same three landmarks, that is the same
three rows typed eight times — authoring burden that buys nothing and starts to resemble the
hand-built behaviour tree this framework exists to replace. The objection to discovery was
that the target list is the whitelist that makes an impossible request safe. That objection
does not survive inspection: **the whitelist has to exist at the instant of the decision, but
nothing requires it to have been typed.** A set computed from the scene a millisecond earlier
constrains the schema exactly as well, and the guards re-check it either way.

**Options considered**

| Option | Cost | Verdict |
|---|---|---|
| Hand-typed list per agent (screen 2) | Duplicated across every agent; grows with the scene; the developer maintains what the engine already knows | Rejected as the default |
| One scene-wide registry shared by all agents | No per-agent narrowing at all — every agent may name everything in the scene | Rejected |
| A Unity tag on nameable objects | One flat string per object, with nowhere to put an id, a description or a category | Rejected — a component holds what a tag cannot |
| **`Targetable` component + per-agent query** | The enum grows with scene density, so discovery needs a cap; ids must be explicit | **Chosen.** |

**Decision.** A `Targetable` component marks an object as nameable and carries its **id**, a
one-line self-description and a category. It is the same component that supplies observations
(§2.8) — an object an agent can notice and one it can refer to are the same object. An
`ITargetSource` on the agent assembles the registry per decision. The source shipped for
scene agents is a proximity, layer and category query, optionally line-of-sight, and it is one
option rather than a requirement: a developer can write a source with their own rule for what
is nameable and when. A hand-supplied set remains available and is what a non-scene agent
uses.

**Consequences**
- Per-agent narrowing survives without per-agent typing, because the query runs from the
  agent: a shopkeeper does not discover things across the map.
- **Discovery needs the same cap and nearest-first ordering as observations (§2.8).** "Any
  entity around him" cannot be literally unbounded — forty targetables within radius is a
  forty-value enum and a prompt that has lost its measured size advantage.
- **Ids are explicit on the component**, never derived from `GameObject.name`, which yields
  values like `Training Dummy (1)`. A missing id is an Inspector error; a duplicate id
  within one agent's resolved set is a decision-time error recorded in telemetry.
- The grounding guard matters more, not less: a wider candidate set is exactly the case
  where a model substitutes a legal target for the illegal one the player named. The guard
  now catches it by the model's own confidence rather than by matching words (DR-016).
- `GreyBoxStrategy` is unaffected — a country has no Transform to query from and supplies
  its own set, which is why `ITargetSource` is an interface rather than a built-in query.
- The resolved set is recorded per decision, so "why was that offered" is answerable.

**Revisit if:** scene-density caps turn out to bite in a real demo, in which case the fix is
a better ordering heuristic (recency, salience, the developer's own comparer) rather than a
return to typing lists.

### DR-015 — Triggering stays the developer's; the call declares its priority

**Status:** Decided, 20 September 2026.

**Context.** Nothing in the framework triggers a decision — the developer calls
`DecideAsync`, which DR-008 requires. The question raised was whether `AgentBehaviour`
should also be able to fire on an interval, for ambient behaviour. The first answer given
was a flat no, on the grounds that a built-in timer would make the framework decide when an
agent is asked. That reading was too strict: DR-008 forbids us from *classifying the
situation*, not from shipping a convenience trigger the developer configures.

Working through it exposed a better-aimed problem. The risk was never that someone writes a
tick loop — it is that **every call currently looks identical to the queue.** When an
ambient tick and a player's question arrive together, the scheduler has no way to know which
one a human is waiting on, so it cannot do the right thing however well it is written.

**Options considered**

| Option | Cost | Verdict |
|---|---|---|
| No trigger of any kind, ever | Everyone writes their own loop; ambient requests still enter the queue indistinguishable from player-facing ones | Rejected — refuses the convenience without fixing the real problem |
| A tick interval on `AgentBehaviour` as the answer | Helps only agents that use our component; a behaviour tree or a custom loop is still invisible to the queue | Rejected as the primary mechanism |
| **`DecideOptions.Priority` on every call, ticker optional on top** | One more field callers should set; the default has to be the safe one | **Chosen.** |

**Decision.** Every call carries a priority — player-facing by default, or background.
The scheduler preempts background work for player-facing requests (§2.6). Any caller can
set it: a custom trigger, a behaviour-tree node, a coroutine, a perception event. **A
behaviour tree driving an agent is a supported and expected integration**, not a workaround —
the tree handles the cheap mechanical decisions and hands the open-ended one to the agent.
An optional ticker component ships as sugar over this: off by default, no default interval,
sets background priority, and cancels when the agent is disabled or off-screen.

**Consequences**
- The framework still never decides *when* or *why* an agent is asked; it only learns, from
  the caller, whether anyone is waiting.
- The ticker is a convenience, not the mechanism. Removing it would change nothing about
  scheduling behaviour, which is the test that it sits at the right level.
- Priority belongs in telemetry, so "the guard took nine seconds" is answerable with "it was
  behind four background requests" rather than guesswork.
- Default must be player-facing: a developer who never thinks about this gets the responsive
  behaviour, and only someone deliberately writing ambient behaviour opts into waiting.

**Revisit if:** two tiers prove too coarse once `GreyBoxStrategy` runs four agents a turn
alongside a player-facing NPC, in which case tiers become a small ordered enum rather than a
boolean — a change to the scheduler, not to the API shape.

### DR-016 — The grounding guard reads the model's confidence, not the player's words

**Status:** Decided for now, 5 October 2026, from a probe ([findings](llm-wiki/findings.md)).
Phase 4 re-measures it on the 200+ prompt set.

**Context.** The schema's target enum holds only registered ids, so a model asked to act on
something absent cannot name it. It names the closest legal target instead: "Attack Godzilla"
attacks the training dummy. This is the general weakness of a forced choice, not a quirk of
one model:

- Language models pick from a label set even when the right label is missing
  ([Classify-w/o-Gold](https://arxiv.org/abs/2406.16203)).
- Accuracy falls 30–50% across 28 models when "none of the above" is the right answer
  ([ACL Findings 2025](https://aclanthology.org/2025.findings-acl.1031/)).
- A grammar mask moves the forbidden word's probability onto the legal ones
  ([Grammar-Aligned Decoding](https://arxiv.org/abs/2405.21047)).

The plan was a name check: the chosen target's name had to appear in the player's line. It
needed a list of every other word players might use, had to be switched on per call, and
refused paraphrases and pronouns.

**Options considered.** Each ran on the same 109 prompts in two scenes, on `qwen3.5:2b` and
`qwen3.5:4b`, greedy, in one session. The two model columns give correct of 109, then wrong
actions carried out.

| Option | 2B | 4B | Cost | Verdict |
|---|---|---|---|---|
| Prompt and schema only: target enum, `no_target`, one refusal example | 73 · 29 | 98 · 10 | — | The base, kept |
| + the name check, on player requests | 83 · 4 | 89 · 3 | Word lists; turned away 15 and 16 more good requests | Dropped as a built-in |
| + a yes/no verifier request ("is X what the player asked for?") | 86 · 5 | 103 · 3 | A second request, +0.3 s per checked decision; turned away 11 more on the 2B | Rejected |
| + how sure the model was of the target it chose, below 0.8 → `none`, on player requests | 95 · 6 | 103 · 5 | Logprobs; two equally good targets also lower it, so it must be switched on per call | Not chosen |
| + the share of the model's probability that stayed on the target list at the target's first token, below 0.8 → `none`, on every decision | 95 · 6 | 103 · 5 | Logprobs; misses names that start like a listed one ("spear" passes as `sword`) | Replaced by the row below |
| **+ the same share at every token of the target, the lowest, below 0.8 → `none`, on every decision** | **94 · 5** | **104 · 4** | Logprobs, +0.07 s; turned away 3 more good requests on the 2B, none on the 4B | **Chosen** |
| A one-line rule in the prompt instead | 88 · 8 | 101 · 3 | None | Fallback without token probabilities |
| A near-miss refusal example in the few-shot block | 78 · 22 | 97 · 10 | None | Rejected: little gain |
| A field for the player's words before the target, checked in code | 88 · 6 | 100 · 1 | A schema field; the model wrote the matched name, not the player's | Rejected |
| A free-text target, resolved in code against names | 92 · 7 | 98 · 7 | Gives up the enum; one answer ran away | Rejected for now |

A second run added a fort scene: open choices ("Check one of the gates."), and near misses
whose names start like a listed target ("Go to the north tower." beside `north_gate`). That
made 130 prompts, scored the same way:

| Option | 2B | 4B |
|---|---|---|
| Prompt and schema only | 83 · 33 | 113 · 15 |
| + the name check | 97 · 4, turned away 29 good requests | 109 · 3, turned away 18 |
| + the on-list share at the first token | 105 · 10 | 118 · 10 |
| **+ the on-list share at every token** | **108 · 5** | **124 · 4** |

Reading every token caught all six look-alike near misses on both models; the first token
caught two on the 2B and one on the 4B. Telling right targets from wrong ones, it reached an
AUROC of 0.93 on the 2B and 1.00 on the 4B.

The same problem has the same shape elsewhere. Entity linking predicts "no match" by a
threshold on the linking score ([NIL prediction](https://arxiv.org/abs/2305.15725)). KnowNo
adds "an option not listed here", and asks a human when the model's option probabilities do
not settle on one ([KnowNo](https://arxiv.org/abs/2307.01928)).

**Decision.** `TargetGroundingGuard` keeps its name and its place in the pipeline, and
decides by the model's own probabilities. From the same request's token probabilities it
reads, at every token of the target, how much of the model's probability could still lead to
a target on the list, keeps the lowest, and rewrites the decision to `none` below 0.8. It
runs on every decision,
because probability that went to something absent is wrong whoever called, while a model
torn between two listed targets keeps all of it on the list. A caller turns it off for one
call with `DecideOptions.CheckTarget = false`. Telemetry records the number for every
decision. On a provider that reports no token probabilities, the guard records that it could
not run, and `PromptBuilder` adds the one-line rule instead.

**Consequences**
- `ProviderCapabilities` gains a flag for reporting token probabilities, and `ProviderResult`
  carries them (§2.4). Ollama returns them from before the schema's mask. LLMUnity's
  `LLMClient` has an `nProbs` setting, not yet tried. Gemini offers logprobs on some models
  only.
- The guard keeps the lowest share across the target's tokens rather than multiplying
  their probabilities. Multiplying punished the model for any split the mask forced on it.
  Reading only the first token missed names that start like a listed one: "spear" and
  `sword`, "the south tower" and `south_gate`. Reading every token catches them. The cost
  falls on a small model: a paraphrase that starts like the right id, such as "healing
  draught" for `health_potion`, can be refused.
- The extra-names field once planned for `Targetable` is dropped from the plan, since the
  guard needs no word lists.
- A reference resolved from history passes when the model is sure of it (§2.9, rule 5).
- It still misses near misses the model is sure of, and wrong actions on the right target
  (§2.5, Limits).

**Revisit if:** any of these happens:
- Phase 4's prompt set or the in-process provider separates right targets from wrong ones
  clearly worse than here (AUROC 0.93 on the 2B, 1.00 on the 4B).
- Open choices between equally good targets get refused.
- A model family with badly calibrated probabilities is adopted.
- Confident near misses prove common.

### Decisions already recorded elsewhere in this plan

| ID | Decision | Where |
|---|---|---|
| DR-002 | One repository for package, demos, dev project, wiki and tools | §6.1 |
| DR-003 | Demo games are not in the package's `Samples~/`. Where they live instead is now DR-010 | §1.6 |
| DR-004 | `action` emitted before the free-text field; `target` required with a `no_target` sentinel; no reasoning field | §2.3, Appendix A |
| DR-005 | Grounding guard in code rather than relying on model scale; how it decides is DR-016 | §2.5, Appendix A, DR-016 |
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
| Lexical grounding guard (~10 lines, post-hoc) | 2B 60% → **95%**; 4B 65% → **100%** (20-prompt focused suite of player commands); impossible-request refusals 1/6 → 6/6 on 2B. The "after" runs also made `target` required with `no_target`: the 4B's target naming went 0/5 → 5/5 between them, which a guard cannot do. The guard's own share is the refusals. Replaced by a confidence check (DR-016) |
| 2B + guards vs 4B without | **95% vs 65%** — scaffolding beats scale |
| Residual only model scale fixed | "That's an interesting sword you have." → 2B picks it up (target IS in text; guard correctly passes); 4B refuses. The honest boundary of code-side fixing |
| Gemini schema dialect | rejects `""` in enums (HTTP 400); supports `propertyOrdering`; `gemini-2.5-flash-lite` 404s for new keys → use `gemini-3.5-flash-lite`; free tier ≈15 RPM / 1,000 req-day |
| Cloud free tier vs local (17 prompts) | Gemini 100% acc, median 1.01s, **worst 91.16s**, 11.8% HTTP-503; local 88.2%, median 1.32s, **worst 2.23s**, 0 failures. Bounded tail beats better median for real-time agents |
| Cloud cost at measured token sizes (~194 in / 38 out, Flash-Lite pricing) | ~$0.000153/decision → $1.53 per 100h (1 companion) vs **$165 per 100h** (30 agents @ 6/min) |
| VRAM | model (2B) + minimal scene = **4.2GB / 8GB** |
| Concurrency (serialized queue) | 5 simultaneous requests: 0 failures, 0 cross-talk, ~3.5s batch — fine at 5, ≈21s at 30 |
| Regressions caught by the A/B harness | Run 1: 47.2% (dialogue-first + none-biased prompt); Run 2: 52.8% (bias removed, order still wrong) — both *below* baseline; methodology caught both |

**Not established, inherited as open**: generalization beyond one model family / one
scene / one annotator; statistical power below ~10 pts at n=53; the in-Unity latency,
not re-measured since the standalone scripts' extra ~2 s was traced to `localhost` (§4); how
conversation history (§2.9) affects accuracy, which no
measurement here covers; how accuracy holds on game-triggered decisions (events, reports,
turns), since these measurements centred on player commands.

**Shipping story — no longer open, but unproven.** "Players don't have Ollama" has a
concrete answer (an in-process provider built on LLMUnity's embedded llama.cpp, §2.4 and
Phase 6b) rather than being an unresolved risk. It is still *unproven* until a built
player executable runs decisions with no Ollama and no network, and until the accuracy
findings above are re-measured through that provider rather than through Ollama's HTTP
API. Treat every number in this appendix as measured against Ollama specifically until
that comparison exists.
