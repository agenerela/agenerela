# 007: results

**Played** on 25 November 2026, 16:33 to 16:48 local, from Unity in batch mode on the lead's laptop. Every block
ran on mains and the home network (LAN).
- **Code:** a detached worktree at test `8a61eb8` (#421, which adds DR-031), imported once outside the leases.
- **Server:** the home server, an NVIDIA GeForce RTX 4070 SUPER 12 GB with Docker Ollama 0.40.14 behind the gate
  (`5728506a6d6b`).
- **Models:** `qwen3.5:4b` Q4_K_M (digest `d8b0f5e9760c`, 2,995 MB, all in VRAM) and `qwen3.5:2b` Q8_0
  (`0689d44085e0`, 2.4 GB, all on the GPU). One model was resident per block, warmed before it.
- **Arms:** interleaved, temperature 0, guards on, three warm-up requests an arm.
- **Defaults:** the arms are today's defaults, so they include `ListActionsAndTargets` (on by default since #404,
  DR-027). The 4B's control scores 184 on the 209 set, as in every list-on run since 23 November.
- **Leases:** each block held its own clean lease on the runs session's client `agenerela-runs`, plus the laptop
  lock.

| Block | Set | Model | Lease | Local time |
|---|---|---|---|---|
| h4 | held-out claims, 30 | `qwen3.5:4b` | #74283 | 16:33–16:35 |
| s4 | the 209 set | `qwen3.5:4b` | #74353 | 16:35–16:41 |
| d4 | development claims, 30 | `qwen3.5:4b` | #74781 | 16:42–16:43 |
| h2 | held-out claims, 30 | `qwen3.5:2b` | #74851 | 16:43–16:44 |
| s2 | the 209 set | `qwen3.5:2b` | #74921 | 16:44–16:48 |

- **Lease checks:** `degen_status.py` found the block's own lease before Unity started and after it ended, ten
  of ten. None was voided or replayed.
- **Other traffic:** the other agenerela key's daily request count rose by 2 during s2 and stayed the same during
  the other four blocks. Those 2 requests are small next to s2's 418, but they are noted, not ruled out.

**Labels.** The tech lead read the held-out 30 before the run and changed none (25 November). Three rows are
marked so a miss reads for what it is:
- **soft**, `claim_007` and `claim_021`: the contradiction is weaker there. On 021 the fact is itself a report;
  on 007 a locked tower argues against a break-in without ruling it out.
- **wolf**, the pairs whose supported prompt expects `attack_target wolf`: `move_to` that place is also acting on
  the claim. The runner also tells every declared target as an observation, so "wolf: A wolf that came out of the
  woods" stands beside the contradicting fact.

**Raw data:** [`data/<block>/`](data/) holds each block's `report.md` and `decisions.csv` (each decision's prompt
and completion tokens and seconds), and `report.json` (every answer) for the three claims blocks. The 209-set
`report.json` files are in the runs session's `.scratch/c007/out/`.

## 1. Does it help? The held-out claims set

The noise floor at n = 30 is 12.9 points. A model that always acts scores 15, and so does one that never acts.

| | 4B off | 4B on | 2B off | 2B on |
|---|---:|---:|---:|---:|
| Right, of 30 | 15 | **28** | 10 | 16 |
| Contradicted right (did nothing), of 15 | 0 | **15** | 1 | 15 |
| Supported right, of 15 | 15 | **13** | 9 | **1** |
| Acted on a contradicted claim / a supported one | 15 / 15 | 0 / 13 | 14 / 14 | 0 / 1 |
| Pairs both right, of 15 | 0 | 13 | 0 | 1 |
| Wolf pairs (3): contradicted right / supported right | 0 / 3 | 3 / 2 | 0 / 3 | 3 / 0 |
| The soft two, contradicted right | 0 of 2 | 2 of 2 | 0 of 2 | 2 of 2 |
| Prompt tokens a decision, mean | 463 | 526 | 463 | 526 |

- **On the 4B the line works.** Off, the model acts on every claim whatever it knows: 0 of 15 contradicted
  claims set aside. On, all 15 are set aside, and it still acts on 13 of the 15 supported ones. 13 of 15 pairs
  are now both right, against none. All 15 pairs that changed moved toward the label on their contradicted side.
  The two supported prompts it lost, `claim_002` (campfire) and `claim_004` (the wolf at the well), went to
  `none`.
- **On the 2B it is a refusal, not a judgement.** On, the 2B does nothing on 29 of 30, acting on one supported
  prompt (`claim_006`). It scores 16 for the same reason a model that never acts scores 15. Off, it acts on
  almost everything, sometimes with the wrong action (`attack_target` a place: three guarded, three rejected).
- **The wolf pairs and the soft two read like the rest.** Off, every contradicted one is acted on; on, every one
  is set aside. The 4B's one wolf miss is supported, not contradicted.
- **Statements (4B on, contradicted)** name the fact that disproves the claim, and none repeats the line:
  - `claim_005` "The docks are dry."
  - `claim_003` "The wolf was caught this morning."
  - `claim_017` "The gate is already locked."
  - `claim_019` "The carpenter confirmed the bridge is sound."

  Off, the same prompts say what it will do: "I will go to the docks.", "I will strike the wolf."

Every prompt, both models. "(wrong)" marks a wrong answer; "none" is `none no_target`.

| Prompt | Facts | Expected | 4B off | 4B on | 2B off | 2B on |
|---|---|---|---|---|---|---|
| `claim_001` | contradict | none | move_to campfire (wrong) | none | attack_target campfire (wrong) | none |
| `claim_002` | support | move_to campfire | move_to campfire | none (wrong) | attack_target campfire (wrong) | none (wrong) |
| `claim_003` wolf | contradict | none | attack_target wolf (wrong) | none | attack_target wolf (wrong) | none |
| `claim_004` wolf | support | attack_target wolf | attack_target wolf | none (wrong) | attack_target wolf | none (wrong) |
| `claim_005` | contradict | none | move_to docks (wrong) | none | move_to docks (wrong) | none |
| `claim_006` | support | move_to docks | move_to docks | move_to docks | move_to docks | move_to docks |
| `claim_007` soft | contradict | none | move_to north_tower (wrong) | none | move_to north_tower (wrong) | none |
| `claim_008` | support | move_to north_tower | move_to north_tower | move_to north_tower | move_to north_tower | none (wrong) |
| `claim_009` | contradict | none | move_to market (wrong) | none | attack_target wolf (wrong) | none |
| `claim_010` | support | move_to market | move_to market | move_to market | attack_target market (wrong) | none (wrong) |
| `claim_011` wolf | contradict | none | attack_target wolf (wrong) | none | attack_target wolf (wrong) | none |
| `claim_012` wolf | support | attack_target wolf | attack_target wolf | attack_target wolf | attack_target wolf | none (wrong) |
| `claim_013` | contradict | none | move_to eastern_road (wrong) | none | move_to eastern_road (wrong) | none |
| `claim_014` | support | move_to eastern_road | move_to eastern_road | move_to eastern_road | attack_target wolf (wrong) | none (wrong) |
| `claim_015` | contradict | none | move_to shelter (wrong) | none | move_to shelter (wrong) | none |
| `claim_016` | support | move_to shelter | move_to shelter | move_to shelter | move_to shelter | none (wrong) |
| `claim_017` | contradict | none | move_to village_gate (wrong) | none | move_to village_gate (wrong) | none |
| `claim_018` | support | move_to village_gate | move_to village_gate | move_to village_gate | move_to village_gate | none (wrong) |
| `claim_019` | contradict | none | move_to bridge (wrong) | none | move_to bridge (wrong) | none |
| `claim_020` | support | move_to bridge | move_to bridge | move_to bridge | move_to bridge | none (wrong) |
| `claim_021` soft wolf | contradict | none | move_to north_tower (wrong) | none | attack_target wolf (wrong) | none |
| `claim_022` wolf | support | attack_target wolf | attack_target wolf | attack_target wolf | attack_target wolf | none (wrong) |
| `claim_023` | contradict | none | move_to shelter (wrong) | none | none | none |
| `claim_024` | support | move_to shelter | move_to shelter | move_to shelter | none (wrong) | none (wrong) |
| `claim_025` | contradict | none | move_to well (wrong) | none | attack_target well (wrong) | none |
| `claim_026` | support | move_to well | move_to well | move_to well | attack_target wolf (wrong) | none (wrong) |
| `claim_027` | contradict | none | move_to market (wrong) | none | attack_target market (wrong) | none |
| `claim_028` | support | move_to market | move_to market | move_to market | attack_target market (wrong) | none (wrong) |
| `claim_029` | contradict | none | move_to campfire (wrong) | none | move_to campfire (wrong) | none |
| `claim_030` | support | move_to campfire | move_to campfire | move_to campfire | move_to campfire | none (wrong) |

## The development set on the 4B (d4)

The wording was chosen on this set, so it shows only whether the framework's own prompt reproduces the smoke
check. The smoke check found none 0/15 off, and 14/15 and 14/15 on.

| | 4B off | 4B on |
|---|---:|---:|
| Right, of 30 | 14 | 25 |
| Contradicted right / supported right, of 15 | 1 / 13 | 14 / 11 |
| Acted on a contradicted claim / a supported one | 14 / 15 | 1 / 12 |
| Pairs both right | 1 | 10 |

It reproduces the smoke check on contradicted claims (14 of 15). It reproduces it less well on supported ones:
11 of 15 against 14, with three supported prompts now `none` (`claim_012`, `claim_022`, `claim_026`).

<details><summary>Every prompt, d4</summary>

| Prompt | Facts | Expected | 4B off | 4B on |
|---|---|---|---|---|
| `claim_001` wolf | contradict | none | attack_target wolf (wrong) | none |
| `claim_002` wolf | support | attack_target wolf | attack_target wolf | attack_target wolf |
| `claim_003` | contradict | none | move_to bridge (wrong) | none |
| `claim_004` | support | move_to bridge | move_to bridge | move_to bridge |
| `claim_005` | contradict | none | move_to village_gate (wrong) | none |
| `claim_006` | support | move_to village_gate | move_to village_gate | move_to village_gate |
| `claim_007` | contradict | none | move_to well (wrong) | none |
| `claim_008` | support | move_to well | move_to well | move_to well |
| `claim_009` | contradict | none | move_to north_tower (wrong) | none |
| `claim_010` | support | move_to north_tower | move_to north_tower | move_to north_tower |
| `claim_011` | contradict | none | none | none |
| `claim_012` | support | move_to docks | move_to docks | none (wrong) |
| `claim_013` | contradict | none | move_to shelter (wrong) | move_to shelter (wrong) |
| `claim_014` | support | move_to shelter | move_to shelter | move_to shelter |
| `claim_015` | contradict | none | move_to eastern_road (wrong) | none |
| `claim_016` | support | move_to eastern_road | move_to eastern_road | move_to eastern_road |
| `claim_017` wolf | contradict | none | attack_target wolf (wrong) | none |
| `claim_018` wolf | support | attack_target wolf | attack_target wolf | attack_target wolf |
| `claim_019` | contradict | none | move_to shelter (wrong) | none |
| `claim_020` | support | move_to campfire | move_to shelter (wrong) | move_to campfire |
| `claim_021` | contradict | none | move_to market (wrong) | none |
| `claim_022` | support | move_to market | move_to market | none (wrong) |
| `claim_023` | contradict | none | move_to north_tower (wrong) | none |
| `claim_024` | support | move_to north_tower | move_to north_tower | move_to north_tower |
| `claim_025` | contradict | none | move_to bridge (wrong) | none |
| `claim_026` | support | move_to bridge | move_to bridge | none (wrong) |
| `claim_027` | contradict | none | move_to well (wrong) | none |
| `claim_028` | support | move_to well | move_to well | move_to well |
| `claim_029` wolf | contradict | none | move_to shelter (wrong) | none |
| `claim_030` wolf | support | attack_target wolf | move_to shelter (wrong) | move_to shelter (wrong) |

</details>

## 2. Does it cost? The 209 set

The arm turns the switch on for every prompt, game-triggered ones included, which no game would do. So the harm
check is the 91 `Player`-labelled prompts. The noise floor is `10 × sqrt(50 / n)`.

| Scope | n | 4B off | 4B on | B − A | Floor | 2B off | 2B on | B − A |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| all | 209 | 184 | 176 | −3.8 | 4.9 | 149 | 76 | **−34.9** |
| **Player four** | 91 | 86 | 84 | −2.2 | 7.4 | 77 | 47 | **−33.0** |
| simple_command | 24 | 23 | 22 | −4.2 | 14.4 | 20 | 10 | **−41.7** |
| command_with_target | 35 | 32 | 31 | −2.9 | 12.0 | 30 | 8 | **−62.9** |
| impossible_request | 15 | 14 | 15 | +6.7 | 18.3 | 12 | 15 | **+20.0** |
| conversation | 17 | 17 | 16 | −5.9 | 17.1 | 15 | 14 | −5.9 |
| situation | 58 | 47 | 47 | 0.0 | 9.3 | 36 | 17 | **−32.8** |
| event | 60 | 51 | 45 | **−10.0** | 9.1 | 36 | 12 | **−40.0** |

| | 4B off | 4B on | 2B off | 2B on |
|---|---:|---:|---:|---:|
| Wrong `none` (all / Player four) | 3 / 0 | 12 / 4 | 21 / 6 | 124 / 41 |
| Prompt tokens a decision, mean | 466 | 529 | 466 | 529 |
| Seconds a decision, mean | 0.78 | 0.74 | 0.50 | 0.46 |

- **4B, within noise on the Player four** (−2.2 against a floor of 7.4) and overall (−3.8 against 4.9).
- **It does refuse four plain requests** it answered without the line, each now `none`:
  - `simple_001` "Come with me." → "I will not follow you."
  - `target_015` "Don't go to the gate, go to the market instead."
  - `target_021` "Go and cover whichever way in nobody is watching."
  - `target_032` "If you're not busy, the market could use a guard." → "I am busy guarding the village."

  Events fall 10.0 points, just past their 9.1 floor, mostly to `none` on lines like "The campfire has gone out."
  and "Lights were seen in the north tower."; events are not the switch's use.
- **2B, far beyond noise everywhere it acts:** 124 wrong `none` against 21. Its rise on impossible requests (+20)
  is the same refusal, scored right.

## What it suggests, for the tech lead to decide

- **For the 4B, the line does what DR-031 asks** on the one set that decides: it sets aside every claim its facts
  contradict, where without it the model set aside none. It keeps acting on 13 of 15 supported claims. On the
  Player four of the 209 set it costs nothing beyond noise, though four plain requests now come back `none`. Both
  30-prompt sets are small and both were written by an AI; it is one block, one model and one run (hard rule 2).
  That makes it a strong pattern, not a settled result.
- **For the 2B, the line makes the model do nothing**, on claims and on commands alike (Player four −33 points). A
  game on the 2B should not set it.
- **Not a default either way:** the switch is per call (`DecideOptions`), and a game sets it only on calls that
  carry someone's word. Nothing here argues for turning it on by default, and no default was changed.
- **Not measured:** a claim with no facts either way (left out on purpose); another wording for the 2B; a game's
  own agent.
