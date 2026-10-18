---
name: review-pull-request
description: Review a pull request in the agenerela repository the way REVIEWING.md asks - read the issue first, check the changed files against its Files you own, run the hygiene checks, compile it and run the EditMode tests in Unity batch mode in a throwaway worktree, then draft a labelled review with the summary template. Use when the user asks to review, check or look over a pull request or a teammate's branch here.
---

# Reviewing a pull request

[`REVIEWING.md`](../../../REVIEWING.md) is the standard: the checks in order, the four finding
labels and the summary template. Read it before the first review in a session. This file is
the procedure an agent follows to meet it. Run every command from the repository root.

## Procedure

1. **Read the issue before the diff.** `gh pr view <n>` gives the body; find the issue it
   closes or is part of, and `gh issue view <issue> --comments`. A request in an issue
   comment is part of the spec. Note its *Files you own* and its *Definition of done*: the
   pull request is checked against those, not against its own description.
2. **Compare the changed files with *Files you own*.**
   `gh pr diff <n> --name-only`. Each file outside the list needs a reason in the
   description.
3. **Make a throwaway worktree with the pull request merged in,** so the user's own project
   and its `Library/` stay untouched: `git fetch origin`, then
   `git worktree add --detach <short path> origin/test` and, inside it,
   `git merge --no-commit origin/<branch>`. Keep the path short and outside any temp folder,
   such as a sibling of the checkout (`../wt<n>`): under a deep path Unity's
   `Library/PackageCache` passes 260 characters, and package imports fail with errors that
   are not the branch's.
4. **Scan the Unity plumbing.** In the worktree, `python tools/ci/hygiene.py --base origin/test`
   runs every check CI runs, against what the merge staged. A green CI run already covers
   it; it matters when CI has not finished, or for a branch with no pull request yet. Then
   run the `ProjectSettings/` and `Packages/` command from REVIEWING.md step 3: every line it
   prints needs a reason in the description.
5. **Compile it and run the EditMode tests** in the worktree:
   - Run the batch-mode command in
     [`docs/llm-wiki/environment.md`](../../../docs/llm-wiki/environment.md) ("Running the EditMode
     tests without the editor") against `<worktree>/UnityProject`, with absolute paths for
     the results and the log. Exit 0 is all passed, 2 is a failure, anything else means the
     run failed, most often to a compile error (`error CS` in the log). The first import
     takes several minutes; run it in the background.
   - If the public API changed, do the same for every project under `Demos/`.
   - **If Unity cannot run here, say so in the review.** Never imply the code builds.
   - Remove the worktree afterwards: `git worktree remove --force <path>`.
6. **Steps 5 to 9 of REVIEWING.md:** hold the code against each Definition-of-done item, read
   every test as if the code were wrong, read prompt-bearing text (doc comments,
   `[Tooltip]`s, descriptions) as specification, check the settled rules in AGENTS.md that CI
   cannot, and check the paperwork (`Closes` only when every item is met, otherwise
   `Part of`).

## Writing it up

- Label every finding **Blocking**, **Should fix**, **Nit** or **Question**, and write each
  as where, what, why it matters, and a fix. REVIEWING.md says what earns Blocking.
- **A design choice is not a defect.** When the issue itself calls a rule provisional, it is
  at most a Nit. When you disagree with the design, or find a gap no issue assigned, put it
  to the user as a question with your recommendation, and let them decide what the review
  asks for.
- Fill in the summary template from REVIEWING.md, including *How I reviewed* with the real
  counts: files owned and not, the plumbing scan, compiled or **not compiled** and why, and
  the test counts.

## Posting

A review an agent drafted goes out under a person's name, so it becomes theirs.

1. Show the user the whole draft: the summary and every inline comment, with a few plain
   sentences on what it amounts to.
2. Apply their changes, and post **only when they say to post**.
3. Before posting, check the pull request's head has not moved since you reviewed it
   (`gh pr view <n> --json headRefOid`). If it has, review what changed first.
4. Post one review with the summary and inline comments together:
   `gh api repos/agenerela/agenerela/pulls/<n>/reviews --input <payload.json>`, where the
   payload has `commit_id` (the head you reviewed), `event` (`APPROVE`, `REQUEST_CHANGES` or
   `COMMENT`), `body`, and `comments` as `{path, line, side: "RIGHT", body}`. Write the payload
   in `.scratch/`, never in a tracked folder.
