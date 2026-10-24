# GitHub Project roadmap and insights — 20 October 2026

The COMP 490 task asking for the project board's roadmap and five Insights charts, submitted
as a PDF of the figures plus the roadmap's data as a CSV.

| File | What it is |
|---|---|
| [`Agenerela-Roadmap-and-Insights.pdf`](Agenerela-Roadmap-and-Insights.pdf) | The submission: a title page, then the roadmap, burnup, issues by assignee, status by assignee, issues by label and priority by label, each with a caption |
| [`Agenerela-Roadmap.csv`](Agenerela-Roadmap.csv) | One row per issue: title, URL, assignees, status, start and target date, milestone, priority and labels |
| [`report.html`](report.html) | The page the PDF is printed from |
| [`figures/`](figures) | The screenshots the PDF uses, taken from the project's Roadmap view and Insights |

**How the board was filled in for this.** Before 20 October no issue had a start or target
date and Priority was unset. Start date is the day each issue was opened; target date is the
day it closed, or its milestone's due date while it is open (the project's last milestone,
26 April 2027, for #18 and #69, which have none). Every issue is Low priority except the two
phase gates, #11 and #94, which are High — set to show the chart, not a triage decision.
The same day, Phase 2's due date moved from 19 to 29 October, on the milestone and on its
six open issues.

**The CSV is built from the project, not exported.** GitHub's *Export view data* writes only
the columns a view shows, and the Roadmap view's export carried just title, URL, assignees
and status. The CSV was read from the project through the API on 20 October instead.

**Rebuilding the PDF.** Print `report.html` to PDF from Edge or Chrome, landscape Letter,
with headers and footers off:

```bash
msedge --headless=new --no-pdf-header-footer --print-to-pdf=out.pdf report.html
```

The roadmap is shown at Quarter zoom, which reads better than the Year zoom the task names.
The charts all filter on `is:issue` (44 issues).
