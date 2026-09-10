## What this changes

<!-- One or two sentences. What and why, not how. -->

## Phase / issue

<!-- e.g. "Phase 1 — Actions registry", or a link to an issue. -->

## Checks

- [ ] Unity console is clean — no new errors or warnings
- [ ] EditMode tests pass (`Window → General → Test Runner`)
- [ ] No secrets, keys or `.env` contents included
- [ ] No generated files committed (`Library/`, `Logs/`, `*.csproj`)
- [ ] Every new file and folder has its `.meta` committed (open the project in Unity before committing)

## If this touches accuracy or performance

<!-- Delete this section if it doesn't. -->

- [ ] Measured with a **control arm** — same model, machine, prompt set and session
- [ ] Only one model resident during timing (`ollama ps`)
- [ ] No overlap between few-shot examples and evaluation prompts
- [ ] Result recorded in `docs/llm-wiki/findings.md`

Control: __ %  →  Treatment: __ %  (n = __, model = __)

## Notes for the reviewer

<!-- Anything you're unsure about, or want a second opinion on. -->
