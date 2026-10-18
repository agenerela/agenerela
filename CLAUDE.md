@AGENTS.md

# Claude Code — project notes

The line above imports [`AGENTS.md`](AGENTS.md), which holds **all** the project rules and
is shared with every other AI agent used here (Codex, Cursor, Copilot, and so on).

**Add project rules to `AGENTS.md`, not to this file.** Anything written only here is
invisible to every other agent, and the two files drift apart silently.

This file exists because Claude Code loads `CLAUDE.md` rather than `AGENTS.md`. Only
Claude-specific configuration notes belong below.

## Claude-specific configuration

Tool configuration lives in [`.claude/`](.claude/README.md):

- `.claude/settings.json` — committed, team-wide. Sets `attribution.commit` and
  `attribution.pr` to empty strings, which is how hard rule 7 (no attribution trailers)
  is enforced for Claude Code specifically; other agents must honour the same rule by
  convention, and CI checks it. Its `permissions` deny reading any `.env` (hard rule 1)
  and let read-only git, `gh` and hygiene-check commands run without a prompt.
- `.claude/skills/` — pointers to the shared skills in `.agents/skills/`.
- `.claude/settings.local.json` — gitignored, personal overrides.
