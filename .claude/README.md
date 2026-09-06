# `.claude/` — Claude Code configuration

Everything in here is project-scoped and shared with the team. It is read
automatically by Claude Code when a session starts in this repository.

| Path | Committed | Purpose |
|---|---|---|
| `settings.json` | yes | Team-wide settings. Applies to everyone. |
| `settings.local.json` | **no** (gitignored) | Personal overrides. Never commit — it is where machine-specific paths and personal preferences go. |
| `commands/` | yes | Custom slash commands, one Markdown file per command. |
| `agents/` | yes | Custom subagent definitions. |
| `skills/` | yes | Project-specific skills. |

Settings load in order **user → project → local**, so `settings.local.json`
overrides `settings.json`, which overrides your personal `~/.claude/settings.json`.

## Where the rules actually live

Not here, and not in `CLAUDE.md` either. **[`AGENTS.md`](../AGENTS.md) at the
repository root is the single source of truth** for every AI agent used on this
project — Claude Code, Codex, Cursor, Copilot and the rest.

[`CLAUDE.md`](../CLAUDE.md) is a thin file whose first line is `@AGENTS.md`, which
imports it. It exists only because Claude Code loads `CLAUDE.md` rather than
`AGENTS.md`; everything else reads `AGENTS.md` natively.

So there are three layers, and it matters which you edit:

| Layer | File | Holds |
|---|---|---|
| Rules | `AGENTS.md` | Everything an agent must know or do. **Edit this one.** |
| Adapter | `CLAUDE.md` | The `@AGENTS.md` import, plus Claude-only notes |
| Configuration | `.claude/settings.json` | How the tool behaves, not what it should do |

A rule written only in `CLAUDE.md` is invisible to Codex. A rule written in
`AGENTS.md` reaches everything.

## Current settings

`attribution.commit` and `attribution.pr` are set to empty strings, which removes
the `Co-Authored-By: Claude` trailer from commits and the "Generated with Claude
Code" line from pull request descriptions. This is a house style choice — the
commit history should read as the team's work, and authorship is already recorded
by the committer field.

Note this replaces the older `includeCoAuthoredBy` key, which is deprecated.
