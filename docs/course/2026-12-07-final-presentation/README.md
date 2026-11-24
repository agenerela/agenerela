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
| `Agenerela-Final-Presentation.pptx` | The deck, 23 slides with speaker notes. Import this into Google Slides |
| `Agenerela-Final-Presentation.pdf` | A preview exported from PowerPoint. Submit the PDF you export after the team's edits, not this one |
| `contact-sheet.png` | All 23 slides on one page |
| `build-deck.js` | Builds the .pptx. Every figure and chart is drawn here as shapes, so it stays editable after the import |
| `figures/painted-realm-council.jpg` | The one image: Painted Realm at turn 1, from the 17 November playtest pass |
| `numbers.md` | Every number on the slides and the command or page it came from |
| `numbers/` | `fetch_gh.py` and `stats.py`, which pull and count the GitHub data |
| `render-deck.ps1`, `contact_sheet.py` | Export the .pptx to PDF and slide PNGs with PowerPoint, and draw the contact sheet |

**Template.** The course template's `/export/pptx` URL downloaded without signing in (24 November),
so the deck keeps its layout and section order: white slides, the title at the top left, the
presenter at the top right, a source line at the bottom, and Introduction → Requirements → Design →
Demo → GitHub → Group management → Acknowledgement → References → Q & A. The template's own type
is Arial 28 pt for titles and 18 pt for body text. The deck uses Arial and Courier New only, as in
October, with one scale throughout: titles 28 pt, body 14 pt, tables 11 to 12 pt, captions and
sources 10 to 11 pt. That is smaller than the template's 18 pt body, because tables of numbers
need the room; the October deck used 11.5 pt body text.

**What the rubric asks for, and where it is.**

| Rubric line | Slides |
|---|---|
| Title and acknowledgement slides | 1, 21 |
| Introduction (2 pts) | 3–5: the problem for Unity developers, one decision through the framework, goals |
| Requirements and design updates (5 pts) | 6 the six initial requirements, 7 the ones added since, 8 what was measured; 9 design then and now, 10 decision records, 11 phase plan |
| Demo (5 pts), about 5 minutes | 12 what to watch for, 13 the video (placeholder), 14 what is still open and the plan |
| GitHub (4 pts) | 15 branches and merge rules, 16 pull requests and commits, 17 git problems and fixes |
| Group management (4 pts) | 18 tools (GitHub Projects, no Jira), 19 contribution table and two pie charts, 20 group-work problems |
| Figures captioned and cited | Every figure has a caption and a source line; References on slide 22 |
| Accuracy | Every number is in `numbers.md`; measured results give n, the control and the noise floor (hard rule 2) |

### Who presents what

Not decided yet. Six parts, one each; fill in the names here, on slide 2, and in the
"Presenter: TBD" pill on each slide. Times are the speaker notes' spoken parts at about 140 words
a minute; the "If asked" lines under them are for questions, not for the talk.

| Part | Slides | Section | Time | Presenter |
|---|---|---|---|---|
| A | 1–5 | Introduction | ≈ 3:10 | TBD |
| B | 6–8 | Requirements: then and now | ≈ 2:50 | TBD |
| C | 9–11 | Design updates | ≈ 2:45 | TBD |
| D | 12–14 | Demo: about 70 s of speaking plus the 5-minute video | ≈ 6:10 | TBD |
| E | 15–17 | GitHub repository | ≈ 2:15 | TBD |
| F | 18–21 | Group management, acknowledgement | ≈ 2:40 | TBD |
| Everyone | 23 | Questions | 3:00 | |

The estimate is about **19 minutes 50 seconds**, inside the 15 to 21 the course allows but near
its top. If a rehearsal runs long, cut slide 14 to one line and shorten slide 3's notes first.

### Still to do before presenting

- **Presenters.** The table above, slide 2 and every slide's pill.
- **The demo video** (slides 12–13) is being made in another session. Insert it on slide 13
  (Insert → Video in Google Slides), update slide 12's list to match its order, and keep a
  backup `.mp4`.
- **Re-check slide 7's statuses** the day before: provider retry and settings profiles (an
  unmerged branch on 24 November), image generation (PR #394, open), and **map naming and
  backstory, for which no issue, branch or commit existed on 24 November**. It is shown as
  Planned; drop the row if it is not going to be filed.
- **Re-run the numbers** (`numbers.md` says how) if more merges land, and change the slides with them.
- **Phase 4** closes on 7 December, the first presentation day. If the second annotator's
  agreement (#148) lands, slides 5, 7, 11 and 14 change from In work.

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
