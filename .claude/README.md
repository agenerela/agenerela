# `.claude/` — Claude Code configuration

Everything in here is project-scoped and shared with the team. It is read
automatically by Claude Code when a session starts in this repository.

| Path | Committed | Purpose |
|---|---|---|
| `settings.json` | yes | Team-wide settings. Applies to everyone. |
| `settings.local.json` | **no** (gitignored) | Personal overrides. Never commit — it is where machine-specific paths and personal preferences go. |
| `commands/` | yes | Custom slash commands, one Markdown file per command. None yet. |
| `agents/` | yes | Custom subagent definitions. None yet. |
| `skills/` | yes | Project skills, as pointers: each `SKILL.md` here sends Claude Code to the real one in `../.agents/skills/`, which Codex reads too. `comfyui-assets` makes and checks assets on the ComfyUI server; `review-pull-request` reviews a pull request the way `REVIEWING.md` asks. |

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

## Permissions

`permissions` in `settings.json` does two things, for everyone who runs Claude Code here.

**`deny` keeps `.env` files out of reach.** Claude Code's Read tool refuses `.env` and
`.env.local` at any depth, including `tools/comfyui/.env` with the asset server's token. This
backs hard rule 1 with a setting rather than only a sentence. It covers the Read tool, not a
shell command such as `cat`, so the rule in `AGENTS.md` still applies; `.env.example` stays
readable, because it holds no secrets and says what to fill in.

**`allow` skips the approval prompt for commands that only read.** Status, diffs, logs, pull
requests and issues, and `tools/ci/hygiene.py`, which reads the repository and writes nothing
unless given `--install-hook`. Anything that commits, pushes, merges or posts still asks
first. Add your own allowances to `settings.local.json`, not here, unless the whole team
wants them.
