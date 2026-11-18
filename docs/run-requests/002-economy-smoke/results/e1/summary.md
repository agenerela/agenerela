# GreyBoxStrategy series: personas: qwen3.5:4b (default), 1 games

Seeds 1 to 1. Seats rotate every game, so each kind plays every country equally often.

| Player | Seats | Wins | Win rate | Mean provinces at the end | Knocked out |
|---|---:|---:|---:|---:|---:|
| qwen3.5:4b merchant | 1 | 1 | 100% | 9.0 | 0 |
| qwen3.5:4b builder | 1 | 0 | 0% | 1.0 | 0 |
| qwen3.5:4b opportunist | 1 | 0 | 0% | 9.0 | 0 |
| qwen3.5:4b proud | 1 | 0 | 0% | 0.0 | 1 |
| qwen3.5:4b warmonger | 1 | 0 | 0% | 4.0 | 0 |

| Ending | Games |
|---|---:|
| domination (15 provinces) | 0 |
| last country standing | 0 |
| turn limit, most provinces | 0 |
| unfinished at the turn cap | 1 |

| Measure | Value |
|---|---:|
| Game length, turns (mean / median / min / max) | 40.0 / 40.0 / 40 / 40 |
| Provinces changing hands per turn | 0.9 |
| Turns where no province changed hands | 48% |
| Battles per game | 79.0 |
| Peace offered / made | 0 / 23 |
| Moves refused as stale at resolution | 55 |

| Wins by country | valen | orsk | merrow | kesh | ilvar |
|---|---:|---:|---:|---:|---:|
| | 0 | 0 | 0 | 1 | 0 |

Provinces changing hands, by turn (mean over the games still running):

| Turns | 1-5 | 6-10 | 11-15 | 16-20 | 21-25 | 26-30 | 31-35 | 36-40 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Changes per turn | 1.6 | 0.8 | 0.4 | 1.4 | 1.0 | 0.8 | 0.4 | 1.0 |
| Games still running | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 |

## The model seat: qwen3.5:4b

| Measure | Value |
|---|---:|
| Decisions (council / envoy) | 1208 (448 / 71) |
| Decisions per game | 1208.0 |
| Median / 90th percentile latency per decision, s | 0.49 / 0.54 |
| Mean prompt tokens | 1513 |
| Council answers not offered, before the guards | 0 |
| Contained by a guard (rewritten to none) | 4 |
| Of those, on a legal answer / a legal attack | 4 / 0 |
| Attacks landed / won | 0 / 0 |
| Rejected by a guard | 0 |
| Pipeline errors (no decision) | 0 |
| Move not among those offered | 0 |
| Target named for a move that takes none (dropped) | 0 |
| Refused by Agent.Execute at landing | 0 |
| Refused at resolution, all causes | 55 |
| Peace accepted / envoys answered | 23 / 71 |

| Guard | Firings |
|---|---:|
| TargetGroundingGuard | 4 |

| Council move | Share |
|---|---:|
| recruit | 0% (0) |
| fortify | 3% (13) |
| raise_taxes | 0% (0) |
| declare_war | 10% (45) |
| attack | 0% (0) |
| offer_peace | 0% (0) |
| none | 23% (103) |

Wall time: 639 s. Arm: default. Personas: on.
