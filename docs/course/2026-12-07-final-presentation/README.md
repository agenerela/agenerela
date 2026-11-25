# Final presentation: the assignment and its rubric

COMP 490 final group presentation, in the week of **7 to 11 December 2026**. This page is the
assignment as the course gives it, reformatted but not reworded, so every session and teammate
works from the same text. The deck, its figures and who presents what are in this folder,
described in [The deck](#the-deck-first-draft-24-november) below.

**Our own notes so far** (not the course's):

- The demo is Painted Realm (`Demos/GreyBoxStrategy/`), shown as a **recorded video** of about
  5 minutes, built from a replay of a recorded game, so it has no model waits.
- What the demo should show: **how the framework serves the game**, every decision and tool in it
  that goes through the framework (rulers, personas, storyteller, advisor, guards, memory,
  observations, the decision inspector), not the game for its own sake.

## The deck (first draft, 24 November)

| File | What it is |
|---|---|
| `Agenerela-Final-Presentation.pptx` | The deck, 21 slides with speaker notes. Import this into Google Slides |
| `Agenerela-Final-Presentation.pdf` | A preview exported from PowerPoint. Submit the PDF you export after the team's edits, not this one |
| `contact-sheet.png` | All 21 slides on one page |
| `build-deck.js` | Builds the .pptx. Every figure and chart is drawn here as shapes, so it stays editable after the import |
| `figures/painted-realm-council.jpg` | Painted Realm at turn 1, from the 17 November playtest pass |
| `figures/agenerela-logo-wordmark.png` | The logo and the name, for the title and Q & A slides: drawn from `tools/branding`'s traced geometry, the name in Bahnschrift as in the logo animation |
| `numbers.md` | Every number on the slides and the command or page it came from |
| `numbers/` | `fetch_gh.py` and `stats.py`, which pull and count the GitHub data |
| `render-deck.ps1`, `contact_sheet.py` | Export the .pptx to PDF and slide PNGs with PowerPoint, and draw the contact sheet |

**Style and template.** The deck uses our own style, the one from the October deck
(`../2026-10-15-design-presentation/build-deck.js`):

- title and Q & A slides on the logo's navy, with the logo and the name
- a blue kicker over each title, and the presenter in a pill at the top right
- a source line at the bottom of each slide
- figures drawn the way `docs/design/` draws them
- Arial only, so Google Slides doesn't reflow the text; code names are bold, not in a code font.
  The name on the title and Q & A slides is part of the logo image, so it keeps its Bahnschrift

Titles are 26 pt, as in October. Body text is 14 pt (October used 11.5), tables are 11 to 12 pt and
sources 10 pt.

The deck does not use the course template's design. We read the template for its instructions: its
`/export/pptx` and `/export/txt` URLs both downloaded with a plain GET and no sign-in on
24 November. Every instruction in it is listed below, with where the deck meets it.

### The template's instructions, and where each is met

The template has 13 slides. Most of its instructions are in the callout on the outline slide and in
the placeholder text on each section slide.

| # | The template says | Where the deck meets it |
|---|---|---|
| 1 | Title slide: "COMP 490 Senior Design Project", "Project Report for &lt;project name&gt;", the members' names, Department of Computer Science, California State University, Northridge | Slide 1: the logo and the name Agenerela as the title (the lead's choice, in place of "Project Report for Agenerela"), then the members, "COMP 490 Senior Design Project", the department and the university |
| 2 | Outline: Introduction, Updates on Requirements, Updates on Design, Demo of Project Prototype, GitHub Repository, Group Management, Acknowledgement, References. Names need not match, and a section may take several slides | Slide 2, drawn like the October deck's outline, then the same order through slide 20 |
| 3 | Total time 15–21 minutes plus 3 minutes of Q & A | About 16:25 by the notes (see the table below), with a Q & A slide (21) |
| 4 | About 1–2 minutes a slide; practise to control the time | Each slide's notes give its time, from 15 to 60 seconds; the video slide is 5 minutes |
| 5 | Less text: use diagrams, images, tables and keywords | Figures 1–5, two charts, and tables on slides 6, 7, 14, 15, 17 and 18 |
| 6 | Format text consistently | One type scale in `build-deck.js` (`S`), used on every slide |
| 7 | Capitalise the first word of a sentence | Every phrase on the slides starts with a capital, except names like `test` and `main` |
| 8 | Use a larger font size for slide text (> 24 pt) | **Not met, by choice.** Titles are 26 pt, body text 14 pt and tables 11–12 pt, close to our October deck. At 24 pt, the tables the rubric's content needs would not fit. This is for the lead to decide |
| 9 | Increase the text size in figures | Figure text is set on the slide at 11 to 14 pt rather than shrunk from an image. The one image, Figure 4, is a game frame |
| 10 | Dark text; no light colours (yellow, light green, pink) in figures | Text is near-black, or white on dark. There are no yellow, light-green or pink fills |
| 11 | Add reference information for others' work | References on slide 20, a source line on every content slide, and where the map painting came from in Figure 4's caption |
| 12 | Questions may come before the end; prepare for questions on each slide | "If asked" lines in the notes of most slides, and a list of likely questions on slide 21 |
| 13 | The presenter's name on each content slide (a "Presenter:" box) | The pill on slides 2–20, and the outline's cards; TBD until the team decides |
| 14 | A source line at the bottom of a slide | On every content slide |
| 15 | Speaking notes: the time, the presenter, and keywords to mention | Each slide's notes open with "Speaking notes · Presenter · time", then the points |
| 16 | Introduction: the project's background, with data, figures or tables from articles or reports showing why the topic matters | **Partly, by choice.** Our October presentation gave the background and the prototype's data; slide 3 is a short recap, at the lead's request |
| 17 | Introduction: the problem to solve, in one sentence | Slide 3, the blue box |
| 18 | Introduction: goals and approach, possibly on a following slide; explain why the work is interesting | Slide 5 gives the goals with their status and the approach; slide 3 says what it gives a developer |
| 19 | Introduction in 1–2 slides, 1–2 minutes | Slides 3–5, about 2:15 (the assignment allows 2–3 minutes) |
| 20 | Background knowledge (optional): key terms and technologies, explainable to someone new in a minute or two | Slide 4, "Background knowledge": one decision through the framework, and the same flow in our strategy demo |
| 21 | Goals and approach: list the goals, perhaps as a diagram, and a short approach linking goals to design | Slide 5 |
| 22 | Requirements: summarise the major user requirements and key system requirements; mark the new ones and the major changes; don't list every detailed requirement; 1–2 minutes | Slide 6 has the initial six, slide 7 the framework features added since, with their status. Requirements and design together take about 3:20, inside the assignment's 3–4 minutes |
| 23 | Design: the major changes, with figures that highlight the modified part (by colour or a bounding box) | Slide 8: the September design in grey, the additions in blue chips |
| 24 | Design: captions and numbers on figures | Figures 1 to 5, each with a caption |
| 25 | Design: why the changes were made, and how they support the requirements | Slide 8's notes, and slide 9's decision records |
| 26 | Design: animation is allowed | Not used |
| 27 | Demo of about 5 minutes; an embedded video is better than a live demo | Slide 12, a placeholder for the recorded video |
| 28 | Demo: the features, how to use the software, and how it responds to user actions | That is the video's job. Slide 11 lists the features to watch for |
| 29 | Demo without a finished system: each member's component, partial results and resources | Not needed, because the system works. Slide 10's phase plan shows what is left |
| 30 | GitHub: how many branches, and the purpose of each | Slide 13: 294 pull-request branches, and what `test`, `main`, feature and demo branches are for |
| 31 | GitHub: the work shown by commits on the branches | Slide 14: commits, merged pull requests by month and by base branch |
| 32 | GitHub: merge rules and the merges done | Slide 13's rules, and slide 14's merge counts and release PR numbers |
| 33 | GitHub: problems with git or GitHub, the solutions and their results; 2–3 minutes | Slide 15; part E takes about 2:10 |
| 34 | Project management: experience with tools (for example Jira or GitHub Projects) | Slide 16: GitHub Projects, issues and milestones |
| 35 | Project management: pie charts or tables of the work done, the workload and each member's contribution | Slide 17: a table and two pie charts |
| 36 | Project management: problems in group work, the strategies used and their results; 2–3 minutes | Slide 18; part F takes about 2:35 |
| 37 | Acknowledgement: thank the people and organisations that helped; list them, with no need to read them out | Slide 19 |
| 38 | References: a list | Slide 20 |
| 39 | Q & A: "Thank you!" | Slide 21, with the logo as on the title slide |

**What the rubric asks for, and where it is.**

| Rubric line | Slides |
|---|---|
| Title and acknowledgement slides | 1, 19 |
| Introduction (2 pts) | 3–5: what Agenerela is, one decision through it, goals |
| Requirements and design updates (5 pts) | 6 the six initial requirements, 7 the framework features added since; 8 design then and now, 9 decision records, 10 phase plan |
| Demo (5 pts), about 5 minutes | 11 what to watch for, 12 the video (placeholder) |
| GitHub (4 pts) | 13 branches and merge rules, 14 pull requests and commits, 15 git problems and fixes |
| Group management (4 pts) | 16 tools (GitHub Projects), 17 contribution table and two pie charts, 18 group-work problems |
| Figures captioned and cited | Every figure has a caption and a source line; References on slide 20 |
| Accuracy | Every number is in `numbers.md`. The rubric doesn't ask for measured results, so no slide shows one; they are in the notes for questions, with n, the control and the noise floor (hard rule 2) |

### Who presents what

Not decided yet. Six parts, one each; fill in the names here, on slide 2's cards, and in the
"Presenter: TBD" pill on each slide. Times are the speaker notes' spoken parts at about 140 words
a minute; the "If asked" lines under them are for questions, not for the talk.

| Part | Slides | Section | Time | Presenter |
|---|---|---|---|---|
| A | 1–5 | Introduction (title and outline included) | ≈ 2:45 | TBD |
| B | 6–7 | Requirements: then and now | ≈ 1:25 | TBD |
| C | 8–10 | Design updates | ≈ 1:55 | TBD |
| D | 11–12 | Demo: about 35 s of speaking plus the 5-minute video | ≈ 5:35 | TBD |
| E | 13–15 | GitHub repository | ≈ 2:10 | TBD |
| F | 16–19 | Group management, acknowledgement | ≈ 2:35 | TBD |
| Everyone | 21 | Questions | 3:00 | |

The estimate is about **16 minutes 25 seconds**, inside the 15 to 21 the course allows. Parts
B and C are short because the assignment gives requirements and design 3 to 4 minutes together;
part D is mostly the video.

**Removed on 24 November, at the lead's request:** "What we measured" (the rubric doesn't ask
for it; its results are in the notes of slides 7 and 21) and "What you saw, and what is still
open" after the video (its hand-off and "Can I play it?" answer moved to slide 12's notes).

### Still to do before presenting

- **Presenters.** The table above, slide 2 and every slide's pill.
- **The demo video** (slides 11–12) is being made in another session. Insert it on slide 12
  (Insert → Video in Google Slides), update slide 11's list to match its order, and keep a
  backup `.mp4`.
- **Re-check slide 7's statuses** the day before: the evaluation harness (Phase 4) and the
  speech provider (DR-024, not built on 24 November). Slides 7 and 8 list framework features
  only, the ones any game can use; a game's own features (Painted Realm's map naming, its
  pictures in the court) belong in the demo, not in the requirements.
- **Re-run the numbers** (`numbers.md` says how) if more merges land, and change the slides with them.
- **Phase 4** closes on 7 December, the first presentation day. If the second annotator's
  agreement (#148) lands, slides 5, 7, 8 and 10 change from In work.

### Rebuilding

```powershell
# in any scratch folder, once
npm install pptxgenjs react react-dom react-icons sharp
```

```powershell
$env:NODE_PATH = "<that folder>\node_modules"; node build-deck.js
```

```powershell
.\render-deck.ps1 -Pptx Agenerela-Final-Presentation.pptx -OutDir <scratch>\png -Pdf "$PWD\Agenerela-Final-Presentation.pdf"
```

The render script drives PowerPoint through COM, so it needs PowerPoint installed;
`python contact_sheet.py <scratch>\png contact-sheet.png` draws the contact sheet.

## The assignment

Each group member takes part in the presentation, in the week of 7 to 11 December 2026.

Each group has **15 to 21 minutes**, depending on its size, so each member prepares to present
for **about 3 minutes**. The grading rubric is below; refer to it while preparing.

Template: [COMP490_L Final Presentation Template](https://docs.google.com/presentation/d/1SRnesCNS7u3JGw5XdChP_uTRGOOimzDthaxlJObOmdo/edit?usp=sharing)

### What the presentation covers

1. **Introduction (2 to 3 min):** a brief introduction of the system's background and goals.
2. **Updates on requirements and/or design (3 to 4 min):**
   - Briefly introduce the major requirements in the initial version (the major features, not
     every detailed requirement).
   - The new requirements added to the product.
   - How the design was modified to support the requirements (if applicable).
3. **Demo of the software, system or project (about 5 min):** the work done for the project.
   - A live demo or a recorded video.
   - With a workable system: demonstrate the features it supports, how to use the software, and
     how the system responds when users act (pressing a button, clicking a tab, and so on).
   - If the system is not integrated yet: demo the workable components or preliminary results.
   - The resources or data gathered for the project can also be explained.
   - If the work is delayed: explain why, and the plan to catch up with the timeline.
4. **GitHub repository, or other mechanisms used for code collaboration (2 to 3 min):**
   - How the repository or repositories are managed: how many branches were created and the
     purpose of each.
   - Work reflected by the commits on the branches, merge rules, and merge PR numbers.
   - If applicable: problems met in collaborating with git or GitHub, the solutions applied, and
     their results.
5. **Group management (2 to 3 min):**
   - Experience using project management tools (for example JIRA) to manage the group's work.
   - Pie charts or tables showing the work accomplished, the workload distribution, and each
     member's contribution.
   - Problems met in group work, the strategies applied, and the results or experience.

Submit the final presentation slides to the assignment.

### Rules

- **Time counts toward the grade.** Each member practises beforehand and keeps to the team's
  time limit.
- A presentation may be recorded in a personal Zoom meeting and replayed to find improvements.
  The recording can be used if there is a technical problem during the presentation, or if a
  member cannot present in class.
- **The demo is graded on how the finished work is presented, not on how complete the project
  is.** With a workable system, demo it; if not, show the work that is finished.
- Depending on the project's state, one person may demo the whole project, or each member may
  talk about their own work.

### Deadline and submission

- Section 01: Monday or Wednesday (7 or 9 December).
- Section 04: Tuesday or Thursday (8 or 10 December).
- Upload the group's slides as **a single PDF file** to the assignment.

### Suggestions from the course

- Put the presenter's name on each slide, except the title slide.
- Use consistent font sizes across all slides: choose a template and keep its default sizes.
- Use figures, images, screenshots and videos for the demo.
- Caption figures and images, and cite any that are not ours.
- Add connecting sentences between slides, naming the next presenter and whoever controls the
  slides (for example, "&lt;name&gt; will introduce feature 1 of our project").
- Review each other's slides and give feedback as questions in the speaker notes.
- Keep the key information on the slides, and present from the speaker notes.
- Each member's grade depends on the whole group's presentation and on their own work: the
  slides they prepared and the part they present.

## Rubric (50 points)

Each criterion is graded full marks or no marks.

| Criterion | What earns it | Points |
|---|---|---:|
| Content accuracy | (1) Content is accurate and cited properly. (2) Relevant information about the topic. | 5 |
| Length of presentation | (1) Great detail. (2) Right on the target minutes (15 to 21 min). (3) The demo is about 5 minutes. | 5 |
| Slides, text, colour | (1) Title and acknowledgement slides. (2) Good font size for text. (3) Good colour, contrast, creativity. | 5 |
| Contents | (1) Introduction, 2 points. (2) Updates on requirements and/or design, 5 points. (3) Demo of the current version of the software, 5 points. (4) GitHub repository or other code repository, 4 points. (5) Group management, 4 points. | 20 |
| Slides and presentation, graded per member | (1) Font size consistent with the other slides. (2) Images and graphs clear, captioned, and cited when not our own. (3) Text balanced, not heavy, with enough to explain the topic, problem or results. (4) No spelling or grammar errors. (5) Tables, diagrams and the like for complex information. (6) Unused information removed. (7) Clear voice, using the slides as a reference rather than reading them. (8) Good connection from one slide to the next. (9) Engages the audience. (10) Right on the target minutes, about 3 minutes a person. | 10 |
| Questions and answers | (1) Can answer questions from the audience. (2) Answers are clear and detailed. | 5 |
| **Total** | | **50** |
