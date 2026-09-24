# Screen designs

The sitemap and six wireframes of everything a human ever looks at in Agenerela: the four
Unity Editor tools a game developer uses, and the two demo games a player and our review
panel see. Drawn for the COMP 490 Section 04 sketch/mockup practice on 17 September 2026,
and kept here because they are also the first picture of the framework's user-facing surface.

Beside them sits one figure that is not a screen: the **framework design**, the package's
modules with one decision passing through them, drawn for the system-design practice on
24 September 2026.

> **These are proposals, not a spec.** Nothing here is built yet. Where a wireframe encodes a
> rule that *is* settled — state-masked action lists, the built-in `none`, `no_target`, the
> five decision outcomes, field order — the rule comes from
> [the build plan](../FRAMEWORK_BUILD_PLAN.md) and its decision records, not from the
> drawing. Everything else (the description length check, editing an action inline, the
> "Try a decision" box, the live "offered right now" panel) is a design idea that should be
> argued with before anyone implements it.

## The figures

| File | Screen | Where it lands |
|---|---|---|
| [`png/00-sitemap.png`](png/00-sitemap.png) | Sitemap and screen flow: both zones, plus the six steps of one decision | — |
| [`png/01-agent-profile.png`](png/01-agent-profile.png) | **Agent Profile & Actions** — Inspector for the profile asset | Assets in Phase 1 (#4, #15); Inspector polish in Phase 5 |
| [`png/02-agent-behaviour.png`](png/02-agent-behaviour.png) | **Agent Behaviour** — the component that puts an agent in a scene | Component in Phase 2; polish in Phase 5 |
| [`png/03-decision-log.png`](png/03-decision-log.png) | **Decision Log** — why an agent did what it did | Phase 5, over the Phase 1 telemetry types (#3) |
| [`png/04-evaluation-report.png`](png/04-evaluation-report.png) | **Evaluation Report** — A/B over the labelled prompt set | Phase 4 (#13) |
| [`png/05-village.png`](png/05-village.png) | **GreyBoxVillage play screen** — talking to the guard | Grey box today (#19); real decisions in Phase 2 |
| [`png/06-strategy.png`](png/06-strategy.png) | **GreyBoxStrategy turn screen** — countries as agents | Grey box next (#20); real decisions in Phase 3 |
| [`png/07-framework-design.png`](png/07-framework-design.png) | **Framework design** — the game, the package and the model, with one decision through them in nine steps | — |

**Screen 2 is partly superseded.** Three decisions made after the lab changed it: the model is
configured in a provider config asset that the component only overrides (DR-013), targets are
discovered from `Targetable` components instead of typed per agent (DR-014), and only
asset-authored actions are bound to a handler (DR-011). The figure keeps what the lab drew and
marks each change in its notes column; the sitemap's screen 2 card carries the same mark. The
current picture is figure 6 of the walkthrough, below.

## The walkthrough

[`workflow.html`](workflow.html) is the other half of this folder: not a screen, but the
**developer's path through all of them**. Nine steps from an empty Unity project to a guard
that answers a player — every asset created, every GameObject added, every window opened, and
the exact line where the developer's code takes over from the framework's. It is what the
screens above are *for*.

Unlike the six figures it is a scrolling page rather than a fixed-size sheet, so it lives
outside `src/` and `render.ps1` does not touch it. Open it in any browser.

Walking that path is what produced DR-011 to DR-015 in
[the build plan](../FRAMEWORK_BUILD_PLAN.md) — five questions the plan had never answered,
two of which reversed the recommendation the page first carried. The page keeps both
reversals visible rather than quietly editing them out. It also carries the two screens the
six-figure set is missing: **provider configuration** (where anyone types an endpoint and a
model name) and a **New Agent wizard**, which is what makes the Phase 5 fifteen-minute target
reachable. Both are proposals.

Also published for the team: <https://claude.ai/artifact/1XGr1eewmN69dvbWvq7oZc>

## The notes

[`screen-notes.md`](screen-notes.md) describes each figure in plain English — what it shows,
and what each numbered callout points at. It was written as the text we posted with the
figures, so it reads as prose rather than as a specification.

**The numbers inside the Decision Log and Evaluation Report figures are invented**, and
labelled as such on the figures themselves. They exist to show the shape of the windows.
No accuracy number is real until it comes out of a Phase-4 run with a control arm
(hard rule 2), and real ones go in [`../llm-wiki/findings.md`](../llm-wiki/findings.md).

## Changing a figure

Each figure is one self-contained HTML file in [`src/`](src), laid out at a fixed pixel
size — 1600 × 1000 for the six screen sheets, 1800 × 1125 for the sitemap, 1280 × 1280 for
the framework figure. The wireframes share [`src/wf.css`](src/wf.css); the framework figure is
one inline SVG. It is square because Canvas shows an image at Medium with its longest side at
320 px, and a square keeps the most of it readable at that size. Open the file in any browser
to edit it; what you see is what renders.

Conventions worth keeping, because they are what makes the set read as one deliverable:

- Grayscale only. The single blue is reserved for the numbered callouts and their notes, so
  a reader can tell our annotations from the interface being drawn. The framework figure
  spends it the same way, on the numbered steps of one decision.
- Every callout number in the drawing has a matching entry in the notes column on the right.
  The framework figure has no notes column; [`screen-notes.md`](screen-notes.md) explains its
  nine steps.
- Real data, never lorem: the village guard, its three actions, the four countries.
- A figure that shows made-up numbers says so on the figure.

Then re-render:

```powershell
.\docs\design\render.ps1
```

It uses headless Edge (or Chrome) to screenshot each page at 2x — no Unity, no Node, no
packages — and writes `png/<name>.png`. Pass a name to render one: `.\render.ps1 00-sitemap`.

`*.png` is tracked by Git LFS (see [`.gitattributes`](../../.gitattributes)), so these come
down as real files only if `git lfs install` was run before cloning — `git lfs pull` fixes a
clone that has pointer files instead.

The same figures are also published as a page the whole team can open:
<https://claude.ai/artifact/EtcUHSQ7a4b49Mg7M4unsQ>. That page is the lab kit, a course
snapshot holding what the team entered, so it is not updated: it shows screen 2 as first
drawn. The PNGs here are current.
