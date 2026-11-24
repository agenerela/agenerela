# Every number in the final deck, and where it came from

Read on **24 November 2026**, from `origin/test` at `c32baec` (after #394, #395, #396 and #398 merged) and the GitHub API. Re-run the
commands before presenting and change `build-deck.js` and this page together if anything moved.
GitHub numbers come from [`numbers/fetch_gh.py`](numbers/fetch_gh.py) (pulls every pull request with
its reviews and every issue through `gh api graphql` into `numbers/data/`, not committed) and
[`numbers/stats.py`](numbers/stats.py) (prints the counts).

## Measured results (slides 8 and 23)

Hard rule 2: n, the control and the noise floor are on the slide with each result.

| Slide | Number | Source |
|---|---|---|
| 8, 23 | Phase 2 gate: 0.23 to 0.56 s a warm decision, `qwen3.5:2b`, RTX 5080, bar under 5 s | Build plan §3, Phase 2 row; findings, 25 October |
| 8 | Phase 3 gate: 19/20 guards on, 18/20 with `CheckTarget = false`; impossible requests 6/6 against 5/6; n = 20; RTX 4060 Laptop, Ollama 0.40.14 | Build plan §3, Phase 3 row; findings, "The Phase 3 gate passed live" (3 November) |
| 8, 23 | Phase 4 gate A/B: 138/209 (66.0%) guards on, 142/209 (67.9%) off; +1.9 points, floor 4.9 at n = 209, within noise; 14 wrong-but-legal answers turned to `none`, 4 correct ones lost; median 0.50 s | findings, "The Phase 4 gate's live A/B" (23 November), home server RTX 4070 SUPER |
| 23 | `qwen3.5:4b` 173/209 (82.8%) against `qwen3.5:2b` 138/209, +16.7, beyond the 4.9 floor | findings, "#151's first runs on the 209 set" (23 November), sequential arms |
| 8 | 3.10 characters a token (sd 0.05) over 6,304 logged decisions; no model run | findings, "Characters a token on `qwen3.5:4b`" (24 November) |

## The framework and the plan

| Slide | Number | Source |
|---|---|---|
| 3 | Prototype, 2B model: 58.5% → 84.9% correct with the constrained schema and few-shot examples, A/B, n = 53 an arm; noise floor about 10 points at n = 53 | Build plan Appendix A and "Statistical power" in findings |
| 3 | About $165 per 100 hours for 30 agents at 6 decisions a minute on a cloud API (Flash-Lite pricing, ~194 tokens in, 38 out) | Build plan Appendix A |
| 3, 14 | 3 demo games built on it (GreyBoxVillage, Living Vale, Painted Realm) | `Demos/README.md`; GreyBoxVillageClassic is a frozen copy, not counted |
| 5, 6, 11 | Phase gates and dates: Phase 1 closed 11 Oct (due 5 Oct), Phase 2 closed 25 Oct (due moved 19 → 29 Oct), Phase 3 closed 3 Nov (due 9 Nov), Phase 4 due 7 Dec; Phases 5–8 due 22 Feb to 26 Apr 2027 | `AGENTS.md` "Current state"; GitHub milestones (`gh api repos/agenerela/agenerela/milestones?state=all`); `docs/course/2026-10-20-github-roadmap/README.md` for the moved date |
| 6 | Six initial requirements, owners and issue numbers | Build plan §6.6 (the SRS of 10 September); issue numbers checked as closed by their assignees |
| 7 | Status of each added requirement | `git log origin/test`; PR #392 (prompt budget), #395 (retry and profiles, DR-026) and #394 (image provider's framework part, DR-025) merged 24 Nov; the game's image part is still open; speech is DR-024, accepted, nothing built in the framework; **map naming and backstory: no issue, branch or commit found on 24 Nov** |
| 10 | 18 decision records, DR-009 to DR-026, with their dates | Build plan §7, each record's Status line |

## GitHub (slides 15, 16, 19, 20)

| Slide | Number | Source |
|---|---|---|
| 16 | 299 merged pull requests: 267 into `test`, 26 into demo branches (19 `69-living-vale`, 5 `153-gbv-v2`, 2 `gbv-lab`), 6 into `main` | `stats.py` |
| 16 | Into `main`: #1 (CODEOWNERS and PR template), #95 (phases 0 and 1), releases #100, #114, #132, #339 | `stats.py`, base `main` |
| 16 | Merged a month: 28 Sep, 46 Oct, 225 Nov (to 24 Nov) | `stats.py`, by `mergedAt` |
| 16 | 878 commits on `test`, merges excluded | `git rev-list --count origin/test --no-merges` |
| 16 | 92 issues, 54 closed as completed, 38 open | `stats.py` |
| 15, 16 | 4 release tags: v0.2.0 (18 Oct), v0.3.0 (25 Oct), v0.4.0-preview.1 (29 Oct), v0.4.0-preview.2 (16 Nov) | `git tag -l`; `git log -1 --format=%ad <tag>`. `pre-rebase-209` is a backup tag, not a release |
| 15 | Protection: `test` requires the "Hygiene checks" status, no force-push, no deletion, no required reviews; `main` also restricts pushes to Draganoider | `gh api repos/agenerela/agenerela/branches/{main,test}/protection` |
| 15 | 294 distinct pull-request branches; 143 branches on GitHub today | `stats.py` (unique `headRefName`); `git ls-remote --heads origin \| wc -l` |
| 17 | 17 hygiene checks | `grep -c "^def check_" tools/ci/hygiene.py` |
| 19 | Commits per member: Yevhen 827, Maxim 21, Hero 16, Tim 7, Tigran 4, Hunter 2, "Claude" 1 (total 878) | `git log origin/test --no-merges --format='%an\|%ae' \| sort \| uniq -c`, authors matched to logins by e-mail (table below) |
| 19 | Merged PRs by author: Draganoider 274, phupanawat 9, MaximGolubitsky 6, TimBrustinov 5, JaySlifer 4, Hunaveli 1 | `stats.py` |
| 19 | Issues closed as completed, by assignee: Draganoider 26, phupanawat 10, MaximGolubitsky 5, TimBrustinov 5, JaySlifer 3, Hunaveli 2, none 3 | `stats.py` |
| 19, 20 | Formal reviews of someone else's PR: 15 by Draganoider on 11 PRs (#36, #39, #57, #61, #63, #74, #96, #118, #155, #181, #319); none by others; 11 of 299 merged PRs had one | `stats.py`. Reviews written as plain PR comments are not counted |

### Git authors and GitHub logins

| Member | GitHub login | Git author names and e-mails seen |
|---|---|---|
| Yevhen Mishchenko | Draganoider | "Yevhen Mishchenko", "Yevhen" (`…+Draganoider@users.noreply.github.com`) |
| Hero Jaiyen | phupanawat | "phupanawat", "Hero Jaiyen" (same e-mail) |
| Maxim Goloubitsky | MaximGolubitsky | "Maxim Goloubitsky", "MaximGolubitsky" (same e-mail) |
| Timothy Brustinov | TimBrustinov | "Timothy Brustinov", "TimBrustinov" (noreply) |
| Tigran Kolsuzyan | JaySlifer | "JaySlifer" (two e-mails) |
| Hunter Hudson | Hunaveli | "Hunaveli" |

**AI-assisted commits.** Work done by AI coding agents is committed under the git identity of the
member who ran them, and hard rule 7 forbids attribution trailers, so git cannot separate it. One
commit (c8e2507, 8 October) carries the author "Claude" and is left out of the per-member table.

## Group work (slide 20)

Risks R01, R04, R12 and R14 and their planned actions are from the risk management plan of
29 October (`docs/course/2026-10-29-risk-management/discussion-reply.md`). Research areas and
their owners (27 October) are build plan §6.6. The follow-up pairs #61 → #62 and #65 → #66 are
GitHub pull request and issue numbers.
