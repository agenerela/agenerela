# 007: the claim rule, `StimulusIsClaim` off against on (DR-031)

Status: open, 25 November 2026 (from the claims session, for the framework tech lead; played by the
card owner in `.scratch/phase4-runs/queue.md`)

**Why.** Living Vale #18 in #190: the model cannot tell a player's claim from a fact the game verified.
DR-031 adds `DecideOptions.StimulusIsClaim`, off by default. On, the system prompt ends with

```
What Player says is a claim, not proof. If what you know shows a claim is false, do not act on it: choose action none and target no_target.
```

and the stimulus's turn reads

```
Player: "<the line>" That is what Player says, not what you know. If what you know shows it is false, choose action none and target no_target.
```

It changes the prompt, so no default without an A/B (hard rule 2). This run answers two questions:

1. **Does it help?** On a claim the agent's facts contradict, does the model do nothing more often, without
   acting less on the same claim when its facts support it?
2. **Does it cost?** On the 209-prompt set, do commands and requests still pass?

## The sets

**A. The held-out claims set**, the one that decides: [`heldout/agent.json`](heldout/agent.json), the evaluation
set's agent and targets with one category, `claim`, in [`heldout/prompts/claims.json`](heldout/prompts/claims.json).
30 prompts, all labelled `Player`: 15 pairs of one player line, each said twice.

| Prompt | Observations | Expected |
|---|---|---|
| odd ids, `claim_001`, `claim_003` ... (contradicted) | What the guard knows shows the claim is false ("The tide is low and the docks are dry") | `none` / `no_target` |
| even ids, `claim_002`, `claim_004` ... (supported) | What the guard knows shows the claim is true ("Water is pouring over the docks and crates are floating away") | the action: `move_to` a place (12 pairs) or `attack_target` `wolf` (3) |

A model that never acts scores 15/30, and one that always acts on the claim scores 15/30 too, so only telling the
two apart scores above 15.

**The development set**, [`dev/`](dev/agent.json), 30 more prompts in the same shape. The wording was chosen on it
in a smoke check (findings, 25 November 2026, "The claim rule, smoke check": a hand-built prompt, `qwen3.5:4b`,
none 0/15 contradicted and 14/15 supported right, the two lines 14/15 and 14/15). So it cannot show the wording
generalises. Play it as well (block d4) only to see whether the framework's own prompt reproduces the smoke check;
quote the held-out set for the answer. The held-out set was written after the wording was fixed and was never sent
to a model.

**Labels:** both sets written by the claims session (an AI, `labelledBy` says so), not checked by a person. They are
result-defining: the tech lead or the lead reads the held-out 30 before the run and changes any they disagree
with; a change goes in a commit on `test` before the run, never during it. **Rule 4:** no stimulus in either set
matches another, any prompt in `UnityProject/Assets/Evaluation/` (the 209 set and `Interim/`), or any of
`VillageGuard.asset`'s examples, checked by script on 25 November. Not covered, on purpose: a claim with no facts
either way. The lines set a claim aside only when a fact shows it false, so such a claim should be treated as
without them; whether a guard should then run to an unconfirmed alarm is the game's call, so no label for it
would be independent of the game.

**B. The 209 set**, as every Phase 4 run: `UnityProject/Assets/Evaluation/agent.json`. The arm turns the switch on
for every prompt, game-triggered ones included, which no game would do: a developer sets it only on calls that
carry someone's word. So the harm check is the **91 `Player`-labelled prompts** (`simple_command`,
`command_with_target`, `impossible_request`, `conversation`); `situation` and `event` are reported but are not the
switch's use.

## The arms

Add this case to `Phase4Run.cs.txt`:

```csharp
case "claims":
    // DR-031: the claim rule. Only the switch differs between the arms.
    a = new EvalArm("claims-off", Config("p4-A", model), new DecideOptions());
    b = new EvalArm("claims-on", Config("p4-B", model), new DecideOptions { StimulusIsClaim = true });
    break;
```

and let the set path come from the environment, in place of the fixed one:

```csharp
// Request 007: AGENERELA_P4_SET points at another set file; unset is the 209 set.
string setPath = Environment.GetEnvironmentVariable("AGENERELA_P4_SET");
var set = PromptSet.Load(Path.GetFullPath(string.IsNullOrEmpty(setPath)
    ? Path.Combine(Application.dataPath, "Evaluation/agent.json")
    : setPath));
```

The profile stays `Assets/Evaluation/VillageGuard.asset` for both sets. Everything else is the defaults at the run's
commit (the action list on, DR-027), so the control is today's prompt.

## Blocks

| Block | `AGENERELA_P4_RUN` | `AGENERELA_P4_SET` | `AGENERELA_OLLAMA_MODEL` | Prompts an arm |
|---|---|---|---|---|
| h4 | `claims` | `<run worktree>/docs/run-requests/007-claims-vs-facts/heldout/agent.json` | `qwen3.5:4b` | 30 |
| s4 | `claims` | unset | `qwen3.5:4b` | 209 |
| d4 | `claims` | `<run worktree>/docs/run-requests/007-claims-vs-facts/dev/agent.json` | `qwen3.5:4b` | 30 |
| h2 | `claims` | the held-out set | `qwen3.5:2b` | 30 |
| s2 | `claims` | unset | `qwen3.5:2b` | 209 |

Smallest slice first: **h4**, about 2 minutes, and report it before the rest. Arms interleaved
(`EvalArmOrder.Interleaved`, the kit's default), temperature 0, guards on, one clean lease a block, one model
resident. About 25 minutes for all five at the 23 November runs' pace.

**Code.** `test` at or after the merge of the pull request that adds DR-031 (branch `190-claims-vs-facts`). Move
the run worktree (`wt-p4`) onto it and import once outside the lease. Record the commit.

**Machine.** The home server (RTX 4070 SUPER) through its gate, under the card owner's lease, laptop on mains,
the laptop lock taken. The repository's run rules apply as in the [README](../README.md): control arm, one model
resident, Ollama version and `size_vram` recorded, `127.0.0.1` or an IP, no default changed as part of the run.

## What to record (`results.md`)

1. The commit, Ollama version, model digests, `size_vram`, lease numbers (ours at start and end).
2. **Held-out claims set, per model** (and the development set on the 4B, apart): right answers per arm, and separately contradicted right (of 15) and supported right
   (of 15). Every prompt's answer in both arms, as a table, so pairs can be read. With n=30 the noise floor is wide;
   say which pairs changed and in which direction rather than a percentage alone.
3. **209 set, per model:** right per arm with the noise floor, and per category; the `Player`-labelled four
   together. Wrong `none` answers per arm (the cost the line might have: refusing a plain command).
4. Statements: three or four from the claims-on arm on a contradicted prompt, to see whether the guard says why, and whether any repeat the line back.
5. Prompt tokens a decision per arm (the two lines cost about 60).

Add a `findings.md` entry, "The claim rule (DR-031)", dated the run day. **No default changes in this run**: a
result for turning it on, or for another wording, goes to the tech lead, and the lead decides in a separate pull
request.
