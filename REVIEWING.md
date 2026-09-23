# Reviewing a pull request

For everyone who reviews here, teammates and AI agents alike. [`CONTRIBUTING.md`](CONTRIBUTING.md)
is the author's side of a pull request; this is the reviewer's. Every pull request into
`test` gets one teammate's review before it merges. Nothing enforces that; the team agreed
to it.

A review answers two questions. For the author: **what has to change before this merges,
and why?** For the team: **what did the reviewer actually check?** CI answers neither.
`Repo hygiene` never compiles C# and never runs a test, so a green check is compatible with
code that does not build.

## Check, in this order

The early steps are the cheapest, and they catch the most expensive problems. Start from the
pull request's branch, with `test` up to date:

```bash
gh pr checkout <number>
```

```bash
git fetch origin
```

Without the GitHub CLI: `git fetch origin pull/<number>/head:pr-<number>`, then
`git switch pr-<number>`.

1. **Read the issue before the diff.** Note its *Files you own* and its *Definition of
   done*. You are checking the pull request against those, not against its own description.

2. **Compare the changed files with *Files you own*.** Every file outside the list needs a
   reason in the description. A four-file issue whose pull request touches forty files is a
   finding before you have read a line of code. The line counts show empty files as `0`.

   ```bash
   git diff --stat origin/test...HEAD
   ```

3. **Scan the Unity plumbing.** The first two commands must print nothing. `Repo hygiene`
   runs both and fails the pull request if either prints a line, so on a green check they
   are already clean; they are here to check a branch on your own machine, or before its CI
   has finished. The third may print lines, but every line needs a reason in the
   description.

   ```bash
   git diff --diff-filter=M -G'^guid:' --name-only origin/test...HEAD -- '*.meta'
   ```

   An existing asset whose GUID changed. Every scene, setting and asset that referred to it
   now points at nothing, and no error appears until someone opens the thing that broke.

   ```bash
   git ls-files -ci --exclude-standard
   ```

   Tracked files that `.gitignore` now ignores. If this lists `.meta` files, every new
   file will silently lose its own.

   ```bash
   git diff --name-only origin/test...HEAD -- '*/ProjectSettings/*' '*/Packages/manifest.json' '*/Packages/packages-lock.json'
   ```

   Unity rewrites these on its own when a project is opened in another version or a package
   is added. While you are here, check that no scene or prefab changed that the author does
   not own.

4. **Compile it and run the tests.** Open `UnityProject/` in Unity 6000.3.23f1 and let it
   import. The console must be clean. Then *Window → General → Test Runner → EditMode → Run
   All*, and write down the counts. If the framework's public API changed, open every
   project under `Demos/` as well. The same run works from a shell without opening the
   editor; [`environment.md`](docs/llm-wiki/environment.md) has the command.

   **If you cannot do this step, say so in the review.** That includes an AI agent with no
   Unity. A review that did not compile the code is still worth having, as long as it does
   not imply otherwise.

5. **Hold the code against the Definition of done.** For each item: done, partly done, not
   done, or done differently. Done differently is fine when the description says so and why;
   some deviations have been improvements. A silent one is a finding.

6. **Read the tests as if the code were wrong.** Would each test fail if the behaviour its
   name promises broke? An empty test file, or a test that only constructs an object, tests
   nothing.

7. **Read prompt-bearing text as specification.** Doc comments, `[Tooltip]`s and
   descriptions on anything that reaches a prompt change what the model is told. Compare
   their meaning with the issue and the build plan, word for word.

8. **Check the settled rules CI cannot.** From [`AGENTS.md`](AGENTS.md): response fields in
   the order `action`, `target`, free text; `target` required, with `no_target`; no
   reasoning field before `action`; availability derived from state rather than forbidden in
   prose; no taxonomy of when or why an agent is asked; the model selects, and the executor
   validates and runs the Unity code.

9. **Check the paperwork.** The description matches the diff: nothing claimed that is not
   there, nothing there that is not mentioned. `Closes #n` only when every
   Definition-of-done item is met; otherwise `Part of #n`, so merging does not close
   unfinished work. An accuracy claim needs a control arm and an entry in
   [`findings.md`](docs/llm-wiki/findings.md). If the commit messages do not say why, ask
   for a squash merge with one good message rather than a history rewrite.

A pull request that changes only docs skips steps 4 to 8; say so under *How I reviewed*.

## Label every finding

| Label | Use it for | The author |
|---|---|---|
| **Blocking** | Anything that must change before merge | fixes it, or changes your mind |
| **Should fix** | Worth doing in this pull request | fixes it, or opens an issue and links it |
| **Nit** | Taste, typos, small tidy-ups | may ignore it without replying |
| **Question** | Something you do not know | answers before merge; the answer may become any of the above |

**Blocking** is for code that does not compile or fails a test; a broken reference, asset or
other project; a broken hard rule; a Definition-of-done item missing with no reason given;
or a description that claims work the diff does not contain.

Write every finding as **where** (file and line), **what** is wrong, **why it matters** —
what breaks, or which rule or Definition-of-done item — and **a fix** when you have one.
Inline comments start with their label the same way: `**Nit** — ...`. Comment on the code,
never the person.

## Write the summary

GitHub has no review templates, so paste the block below into the summary box under *Review
changes*, or save it once as a [saved reply](https://github.com/settings/replies) and it is
one click away. Delete any section that would be empty, except *Verdict* and *How I
reviewed*.

The summary is the author's checklist. Every **Blocking** and **Should fix** item goes in it
in full, even one you also left inline.

```markdown
**Verdict: <Approve | Changes requested | Needs a decision from @who>.** <The main reason, in one sentence.>

### What works
- <Specific: what should survive the rework, and why it is right.>

### Blocking
1. **<Short title>** — `<path>:<line>`
   <What is wrong.> <Why it matters.> <Suggested fix.>

### Should fix
1. **<Short title>** — `<path>:<line>`
   <What, why, fix.>

### Nits
- `<path>:<line>` — <one line each>

### Questions
- <What you need to know, and why the verdict depends on it.>

### Definition of done (#<issue>)
| Item | Status | Note |
|---|---|---|
| <copied from the issue> | Done / Partly / Not done / Done differently | |

### How I reviewed
- Issue #<n> read. Changed files against *Files you own*: <n owned, n others, all explained / list>
- Unity plumbing: <GUID changes, ignored tracked files, ProjectSettings: none / list>
- Compiled in Unity 6000.3.23f1: <console clean / n errors>, or **not compiled**, because <reason>
- EditMode tests: <n passed, n failed, n new in this pull request>
- Demo projects: <opened, all compile / not needed, public API unchanged>
```

Then choose the matching GitHub state: *Approve*, *Request changes*, or *Comment* for a
decision someone else has to make. Reviews on `test` are a team agreement, not a setting, so
*Request changes* blocks nothing by itself; the verdict line is what the author acts on.

Posting a review an agent drafted makes it yours. Read it, and check its blocking claims,
before it goes out under your name.

## When the author pushes again

Review only what changed (*Files changed → Changes since your last review*). Resolve the
threads you opened once they are fixed, and update your verdict. A stale *Request changes*
tells everyone the pull request is still broken.

## Why these checks

Each one is here because skipping it costs something nothing else catches. Before dropping
one, read what it is for.

- **The file list and the GUID scan.** Delete the metas and let Unity regenerate them, and
  every reference to those assets breaks: the render pipeline, the build's scene list, a
  scene's volume profile. Unity reports nothing until someone opens the thing that broke.
  CI used to check only that every asset *has* a `.meta`, and passed exactly this; it now
  also fails on a changed GUID. The changed-files list still catches what no check knows to
  look for, such as a scene the author does not own or a setting Unity rewrote.
- **Compile it yourself.** CI never compiles C#, so a duplicate class, a missing `using` or a
  misspelled type all pass it. Until GameCI arrives in Phase 3 (build plan §6.3), the
  reviewer's Unity is the only compiler between a branch and `test`.
- **Tests as if the code were wrong.** An empty test file compiles, and so does a test that
  only constructs its subject. Both count as "tests" in a description, and neither can fail.
- **Prompt text is specification.** A reworded description or tooltip changes what the model
  reads. Nothing fails, and the cost surfaces as an accuracy drop in Phase 4 that nobody can
  trace back.
- **`Part of` versus `Closes`.** A pull request that closes its issue early hides the
  unfinished part. The issue leaves the board, and the missing piece has no owner.
- **Say what you did not check.** A review is evidence. One that quietly skipped the compile
  tells the team the code builds when nobody knows.
