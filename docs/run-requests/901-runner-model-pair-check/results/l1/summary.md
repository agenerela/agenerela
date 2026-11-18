# GreyBoxStrategy series: personas: qwen3.5:4b (default), 1 games

Seeds 1 to 1. Seats rotate every game, so each kind plays every country equally often.

| Player | Seats | Wins | Win rate | Mean provinces at the end | Knocked out |
|---|---:|---:|---:|---:|---:|
| qwen3.5:4b opportunist | 1 | 1 | 100% | 8.0 | 0 |
| qwen3.5:4b builder | 1 | 0 | 0% | 1.0 | 0 |
| qwen3.5:4b merchant | 1 | 0 | 0% | 5.0 | 0 |
| qwen3.5:4b proud | 1 | 0 | 0% | 5.0 | 0 |
| qwen3.5:4b warmonger | 1 | 0 | 0% | 6.0 | 0 |

| Ending | Games |
|---|---:|
| domination (15 provinces) | 0 |
| last country standing | 0 |
| turn limit, most provinces | 0 |
| unfinished at the turn cap | 1 |

| Measure | Value |
|---|---:|
| Game length, turns (mean / median / min / max) | 40.0 / 40.0 / 40 / 40 |
| Provinces changing hands per turn | 0.5 |
| Turns where no province changed hands | 68% |
| Battles per game | 100.0 |
| Peace offered / made | 0 / 7 |
| Moves refused as stale at resolution | 36 |

| Wins by country | valen | orsk | merrow | kesh | ilvar |
|---|---:|---:|---:|---:|---:|
| | 0 | 0 | 1 | 0 | 0 |

Provinces changing hands, by turn (mean over the games still running):

| Turns | 1-5 | 6-10 | 11-15 | 16-20 | 21-25 | 26-30 | 31-35 | 36-40 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Changes per turn | 1.6 | 0.6 | 0.0 | 0.2 | 0.0 | 0.2 | 0.8 | 0.2 |
| Games still running | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 |

## The model seat: qwen3.5:4b

| Measure | Value |
|---|---:|
| Decisions (council / envoy) | 592 (261 / 21) |
| Decisions per game | 592.0 |
| Median / 90th percentile latency per decision, s | 0.50 / 0.59 |
| Mean prompt tokens | 1399 |
| Council answers not offered, before the guards | 0 |
| Contained by a guard (rewritten to none) | 12 |
| Of those, on a legal answer / a legal attack | 12 / 0 |
| Attacks landed / won | 0 / 0 |
| Rejected by a guard | 0 |
| Pipeline errors (no decision) | 0 |
| Move not among those offered | 0 |
| Target named for a move that takes none (dropped) | 0 |
| Refused by Agent.Execute at landing | 0 |
| Refused at resolution, all causes | 36 |
| Peace accepted / envoys answered | 9 / 21 |

| Guard | Firings |
|---|---:|
| TargetGroundingGuard | 12 |

| Council move | Share |
|---|---:|
| recruit | 0% (0) |
| fortify | 2% (5) |
| raise_taxes | 22% (57) |
| declare_war | 6% (15) |
| attack | 0% (0) |
| offer_peace | 0% (0) |
| none | 3% (9) |

Wall time: 320 s. Arm: default. Personas: on.
