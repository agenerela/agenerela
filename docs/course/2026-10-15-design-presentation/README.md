# Project design presentation, 15 October 2026

The COMP 490 group presentation: 15 minutes plus 2–3 minutes of questions, submitted as one
PDF. Every slide names its presenter, and the speaker notes carry the talking points, the
hand-off line to the next person, and likely questions with short answers.

| File | What it is |
|---|---|
| `Agenerela-Project-Design.pptx` | The deck, 20 slides. Import this into Google Slides |
| `Agenerela-Project-Design.pdf` | A preview exported from PowerPoint. Submit the PDF you export after the team's edits, not this one |
| `diagrams/*.html` | Sources of the nine figures, in the style of `docs/design/src/07-framework-design.html` |
| `diagrams/png/` | The rendered figures, plus crops of the framework figure and the Agent Profile wireframe from `docs/design/png/` |
| `build-deck.js` | Builds the .pptx from the figures |

## Who presents what

| Presenter | Slides | Time |
|---|---|---|
| Yevhen Mishchenko | 1–5 title, outline, motivation, problem · 13 providers | ≈ 2:50 + 0:50 |
| Hunter Hudson | 6 goals · 7 architecture (black box) · 8 inside the box | ≈ 2:45 |
| Timothy Brustinov | 9 action vocabulary · 10 Agent Profile screen | ≈ 1:45 |
| Tigran Kolsuzyan | 11 availability · 12 schema and prompt | ≈ 2:00 |
| Hero Jaiyen | 14 targets · 15 how "Attack Godzilla" is stopped · 16 methods measured | ≈ 2:45 |
| Maxim Goloubitsky | 17 telemetry · 18 evaluation plan · 19 references | ≈ 2:00 |
| Everyone | 20 questions | 2–3 min |

The estimates come to about 15 minutes, so rehearse the hand-offs tightly.

## Editing in Google Slides

Upload the .pptx to Drive and open it with Google Slides. It uses only Arial and Courier New,
which Slides has, so text should not reflow. Figures are images. To change one, edit its HTML
file, re-render it, and replace the image on the slide.

## Rebuilding

```powershell
.\diagrams\render.ps1              # every figure; needs only Edge or Chrome
.\diagrams\render.ps1 07-guards    # one figure
```

`build-deck.js` needs `pptxgenjs`, `react`, `react-dom`, `react-icons` and `sharp` from npm,
installed in any scratch folder and found through `NODE_PATH` (they are not project
dependencies). The header of the script shows the command.

The one-word status tag on each slide (Built, In work or Design) reflects `test` on 4 October
2026. Change the `tag(s, ...)` calls in `build-deck.js` if more lands before the 15th.
