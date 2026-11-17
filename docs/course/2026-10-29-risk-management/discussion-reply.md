**Project name:** Agenerela, a Unity framework that lets a language model drive game agents by choosing from actions the developer registers

**Participating members:** Yevhen Mishchenko, Hunter Hudson, Timothy Brustinov, Tigran Kolsuzyan, Hero Jaiyen, Maxim Goloubitsky

**Risks and planned actions** (full analysis, with probability, effects, priority, indicators and status, is in the attached Excel file):

| # | Member | Risk (type) | Priority | Planned action |
|---|---|---|---|---|
| R01 | Yevhen | Most of the core is built by one person, so if the lead is out, every other area is blocked (People) | Medium | Every core PR is reviewed by a second member; decisions are written down; one teammate pairs on the guard pipeline |
| R02 | Yevhen | Small local models pick the wrong action too often (Technology) | **High** | Swappable providers; guards turn wrong answers into "no action"; every change is measured with a control arm; fall back to a larger or cloud model for demos |
| R03 | Hunter | Changing the observation format forces rework of the Agent API (Requirements) | Medium | Agree the interface with the area owner before building it; keep observations behind an interface |
| R04 | Hunter | Members unavailable during midterms, finals and holidays (People) | Medium | Post unavailable dates each sprint; a backup reviewer for every area; lighter sprints in exam weeks |
| R05 | Timothy | Writing and labelling 200+ evaluation prompts takes longer than estimated (Estimation) | **High** | Weekly labelling quota across all six members; track the count; measure on what exists and grow n |
| R06 | Timothy | Other providers support different parts of JSON Schema (Tools) | Medium | Stay inside the common subset; a test per provider before we claim support |
| R07 | Tigran | An Ollama upgrade silently changes results (Technology) | Medium | Pin and record the version for every run; re-run the schema probe after upgrades |
| R08 | Tigran | The framework plus three demo games is more than two semesters allow (Estimation) | Medium | Planned cut order: art polish first, then GreyBox2D; never cut GreyBoxStrategy or the evaluation phase |
| R09 | Hero | Agent memory grows the prompt past the context window or makes decisions too slow (Technology) | Medium | A memory size budget with a summarising strategy; log prompt length and latency per decision |
| R10 | Hero | The model picks a legal but wrong target (Technology) | Medium | Offer only reachable targets; track "wrong-but-legal" as its own outcome; add prompts with similar targets |
| R11 | Maxim | Unity and Git conflicts: .meta files, scene merges (Tools) | Medium | Hygiene CI and a pre-commit hook; one owner per scene |
| R12 | Maxim | Defect repair underestimated, since unreviewed merges let bugs through (Estimation) | Medium | Review before merge; prove every new test can fail; a follow-up issue for each defect found after merge |
| R13 | Team | Course deliverables pile up in the same week as a phase deadline (Organizational) | Medium | Check Canvas weekly; split course work so the phase owner keeps building |
| R14 | Team | Not everyone has a GPU that can run models locally (Tools) | Medium | EditMode tests need no model; small or cloud models for development; a backup machine |

**Monitoring:** we review the register once per sprint, update probability, effects and status (Monitoring / Observed / Resolved), and log each review on the "Sprint Review Log" sheet. Conditional formatting colours probability, effects, priority and status.

**Ground rule updates:** review before every merge; each member posts their unavailable dates at the start of each sprint; a weekly quota for labelling evaluation prompts.
