# Docs

Everything written down about Agenerela that is not source code.

| Where | What is in it | Read it when |
|---|---|---|
| [`FRAMEWORK_BUILD_PLAN.md`](FRAMEWORK_BUILD_PLAN.md) | **The plan of record.** Architecture, the eight build phases with their definitions of done, testing standards, decision records, and Appendix A's measurements carried from the prototype | Before doing any work here — especially before re-trying something Appendix A already measured |
| [`RELEASING.md`](RELEASING.md) | When a version is cut, which number it gets, and the steps from version bump to published release | Closing a phase, or installing a released version |
| [`releases/`](releases/) | Each release's notes as published on GitHub, written for people rather than copied from the changelog | Cutting a release |
| [`../CHANGELOG.md`](../CHANGELOG.md) | What changed in the repository, release by release: the evaluation set, demo games, CI, tools and docs, with one line per release on the framework. The package's own [`CHANGELOG.md`](../UnityProject/Packages/com.agenerela.framework/CHANGELOG.md) has its API changes | Picking up work, or before saying what exists |
| [`llm-wiki/`](llm-wiki/README.md) | What we have learned since: [`findings.md`](llm-wiki/findings.md) for measurements taken in this repo, [`environment.md`](llm-wiki/environment.md) for setting a machine up | Setting up, or the day you measure something |
| [`design/`](design/README.md) | The sitemap, a wireframe of every screen with the HTML it is rendered from, the framework design figure, and the developer walkthrough that strings them together | Building editor tooling or a demo's UI, or arguing with a screen |
| [`demos/`](demos/) | Design write-ups for demo games: what each one proves, when its agents are asked, and the steps to build it. So far [`risk-race.md`](demos/risk-race.md) | Starting a demo game, or deciding what one should prove |
| [`phases/`](phases/) | One page per phase in flight: what it is for, how its issues fit together, and what each one does. So far [`phase-2.html`](phases/phase-2.html), also published for the team [as an artifact](https://claude.ai/artifact/EvDVRcbNAfS2i3bdG8j3Mu) | Picking up an issue in that phase, or explaining the phase to someone new |
| [`assets/readme/`](assets/readme/) | The front page's images: the animated hero, the how-it-works figure and the roadmap, with `hero.py` and `roadmap.py`, which generate them (`hero.py` from `hero-bg.jpg` and `guard.png`) | Changing the README's look, or moving the roadmap on when a phase closes |
| [`course/`](course/README.md) | COMP 490 deliverables built as shared pages: requirements, interviews, the sketch lab, system design | Preparing a class deliverable, or looking up what we told the class |

Rules for working in this repo — branches, hard rules, conventions — are in
[`../AGENTS.md`](../AGENTS.md), which every AI agent reads and which is the shortest
description of how the project works. Humans have [`../CONTRIBUTING.md`](../CONTRIBUTING.md),
and every reviewer, human or agent, has [`../REVIEWING.md`](../REVIEWING.md).

Three habits keep these pages worth reading:

- **A change gets its changelog entry in the pull request that makes it**, in the package's
  changelog if it changes the public API and in the root one otherwise, never both.
- **A measurement goes in [`llm-wiki/findings.md`](llm-wiki/findings.md) the day it is
  taken**, negative results included, with its control arm and conditions.
- **A decision that costs more than an hour to reverse gets a decision record** in the build
  plan's §7, so nobody relitigates it from memory.
