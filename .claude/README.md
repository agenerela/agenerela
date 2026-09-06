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

## Why `CLAUDE.md` is not in here

[`CLAUDE.md`](../CLAUDE.md) stays at the **repository root**. That is the location
Claude Code discovers automatically as project memory; moving it into `.claude/`
would stop it being loaded. Root is also where a human contributor expects to find
it, next to `README.md`.

Rule of thumb: **`CLAUDE.md` is instructions** (what Claude should know and do in
this repo), **`.claude/` is configuration** (how the tool itself behaves).

## Current settings

`attribution.commit` and `attribution.pr` are set to empty strings, which removes
the `Co-Authored-By: Claude` trailer from commits and the "Generated with Claude
Code" line from pull request descriptions. This is a house style choice — the
commit history should read as the team's work, and authorship is already recorded
by the committer field.

Note this replaces the older `includeCoAuthoredBy` key, which is deprecated.
