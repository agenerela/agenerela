# GreyBoxStrategy series: personas: qwen3.5:4b (default), 1 games

Seeds 1 to 1. Seats rotate every game, so each kind plays every country equally often.

| Player | Seats | Wins | Win rate | Mean provinces at the end | Knocked out |
|---|---:|---:|---:|---:|---:|
| qwen3.5:4b merchant | 1 | 1 | 100% | 6.0 | 0 |
| qwen3.5:4b builder | 1 | 0 | 0% | 3.0 | 0 |
| qwen3.5:4b opportunist | 1 | 0 | 0% | 6.0 | 0 |
| qwen3.5:4b proud | 1 | 0 | 0% | 5.0 | 0 |
| qwen3.5:4b warmonger | 1 | 0 | 0% | 5.0 | 0 |

| Ending | Games |
|---|---:|
| domination (15 provinces) | 0 |
| last country standing | 0 |
| turn limit, most provinces | 0 |
| unfinished at the turn cap | 1 |

| Measure | Value |
|---|---:|
| Game length, turns (mean / median / min / max) | 40.0 / 40.0 / 40 / 40 |
| Provinces changing hands per turn | 0.3 |
| Turns where no province changed hands | 83% |
| Battles per game | 95.0 |
| Peace offered / made | 0 / 3 |
| Moves refused as stale at resolution | 14 |

| Wins by country | valen | orsk | merrow | kesh | ilvar |
|---|---:|---:|---:|---:|---:|
| | 0 | 0 | 0 | 1 | 0 |

Provinces changing hands, by turn (mean over the games still running):

| Turns | 1-5 | 6-10 | 11-15 | 16-20 | 21-25 | 26-30 | 31-35 | 36-40 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Changes per turn | 1.6 | 0.4 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 | 0.0 |
| Games still running | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 |

## The model seat: qwen3.5:4b

| Measure | Value |
|---|---:|
| Decisions (council / envoy) | 538 (232 / 10) |
| Decisions per game | 538.0 |
| Median / 90th percentile latency per decision, s | 0.54 / 0.61 |
| Mean prompt tokens | 1431 |
| Council answers not offered, before the guards | 0 |
| Contained by a guard (rewritten to none) | 3 |
| Of those, on a legal answer / a legal attack | 3 / 0 |
| Attacks landed / won | 0 / 0 |
| Rejected by a guard | 0 |
| Pipeline errors (no decision) | 0 |
| Move not among those offered | 0 |
| Target named for a move that takes none (dropped) | 0 |
| Refused by Agent.Execute at landing | 0 |
| Refused at resolution, all causes | 14 |
| Peace accepted / envoys answered | 3 / 10 |

| Guard | Firings |
|---|---:|
| TargetGroundingGuard | 3 |

| Council move | Share |
|---|---:|
| recruit | 0% (0) |
| fortify | 2% (4) |
| raise_taxes | 14% (33) |
| declare_war | 6% (13) |
| attack | 0% (0) |
| offer_peace | 0% (0) |
| none | 16% (37) |

Wall time: 305 s. Arm: default. Personas: on.
