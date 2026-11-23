"""Report on Painted Realm's economy live check (#20, run E3): free orders with and without `Economy`.

    python tools/live-runs/economy/report.py <out> [--control x] [--limit 1600]

<out> holds one folder a block, as `StrategyTableMenu.RunInterleavedFromCommandLine` writes them, named
<arm><seed>: x1, e1, x2, e2, ... Each block's `model.csv` and `games.csv` are read:

- illegal answers, all requests and court requests;
- prompt tokens a court request, mean and max, and how many went over the limit (1,600 by default);
- calls and model seconds a turn, each arm against the control arm on the same seed and turn;
- per persona and per game, each building bought (kind, province, turn) and each tax change (level, turn), as a
  timeline, then each persona's totals over the games.

A building or a tax order counts only when it was carried out (`execution` Done). Tax levels start at normal, so a
`taxes_normal` order on a realm already at normal is listed as kept, not as a change. A seat's persona is read from
`games.csv` (`<country>_kind`, such as "qwen3.5:4b merchant").

Standard library only. Numbers are printed, never compared with another machine's (AGENTS.md, hard rule 2).
"""
import argparse
import collections
import csv
import os
import re
import statistics as st

BLOCK = re.compile(r"^([a-z]+?)(\d+)$")
BUILDS = {"build_market": "market", "build_barracks": "barracks", "build_farm": "farm"}
TAXES = {"taxes_low": "low", "taxes_normal": "normal", "taxes_high": "high"}


def rows(folder, name):
    p = os.path.join(folder, name)
    if not os.path.exists(p):
        return []
    with open(p, encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def mean(xs, default=0.0):
    xs = list(xs)
    return st.mean(xs) if xs else default


def personas(folder):
    """{country: persona} from games.csv's <country>_kind columns ('qwen3.5:4b merchant' -> 'merchant')."""
    out = {}
    for r in rows(folder, "games.csv")[:1]:
        for k, v in r.items():
            if k.endswith("_kind") and k != "winner_kind":
                out[k[:-5]] = v.split()[-1] if v else "?"
    return out


def arm_figures(data, arm, blocks, limit):
    per_turn = collections.defaultdict(lambda: [0, 0.0])
    total = illegal = 0
    court_tokens = []
    court_illegal = 0
    for b, seed in blocks:
        for r in rows(os.path.join(data, b), "model.csv"):
            t = int(r["turn"])
            per_turn[(seed, t)][0] += 1
            per_turn[(seed, t)][1] += float(r["latency_s"] or 0)
            bad = r["model_answer_legal"] == "False"
            total += 1
            illegal += bad
            if r["moment"] == "Council":
                court_illegal += bad
                if r.get("prompt_tokens"):
                    court_tokens.append(int(r["prompt_tokens"]))
    print(f"\n## arm {arm} ({', '.join(b for b, _ in blocks)})")
    if not per_turn:
        print("  no model.csv")
        return {}
    n_court = len(court_tokens)
    print(f"  decisions {total}, illegal answers {illegal} ({100 * illegal / max(1, total):.1f}%); "
          f"court requests {n_court}, illegal {court_illegal} ({100 * court_illegal / max(1, n_court):.1f}%)")
    if court_tokens:
        over = sum(t > limit for t in court_tokens)
        print(f"  prompt tokens a court request: mean {mean(court_tokens):.0f}, max {max(court_tokens)} against {limit}; "
              f"over the limit {over} of {n_court}")
    print(f"  calls a turn {mean(v[0] for v in per_turn.values()):.1f} (max {max(v[0] for v in per_turn.values())}); "
          f"model s a turn {mean(v[1] for v in per_turn.values()):.1f} (max {max(v[1] for v in per_turn.values()):.1f})")
    return dict(per_turn)


def timelines(data, arm, blocks):
    """Prints each game's purchases and tax changes by persona; returns per-persona totals."""
    totals = collections.defaultdict(collections.Counter)
    first = collections.defaultdict(list)  # persona -> first turn it bought anything, a game
    print(f"\n## arm {arm}: buildings and taxes, by persona, as a timeline")
    for b, seed in blocks:
        folder = os.path.join(data, b)
        who = personas(folder)
        events = collections.defaultdict(list)
        level = collections.defaultdict(lambda: "normal")
        for r in rows(folder, "model.csv"):
            if r["moment"] != "Council" or r.get("execution") != "Done":
                continue
            c, t, act = r["country"], int(r["turn"]), r["action"]
            p = who.get(c, "?")
            if act in BUILDS:
                events[c].append((t, f"t{t} {BUILDS[act]} {r['target']}"))
                totals[p][BUILDS[act]] += 1
            elif act in TAXES:
                new = TAXES[act]
                if new == level[c]:
                    events[c].append((t, f"t{t} taxes kept {new}"))
                    totals[p]["taxes kept"] += 1
                else:
                    events[c].append((t, f"t{t} taxes {level[c]}->{new}"))
                    totals[p][f"taxes {new}"] += 1
                    level[c] = new
        print(f"  {b} (seed {seed}):")
        for c in sorted(who, key=lambda c: who[c]) or sorted(events):
            ev = [e for _, e in sorted(events.get(c, []))]
            bought = [t for t, e in events.get(c, []) if "taxes" not in e]
            if bought:
                first[who.get(c, "?")].append(min(bought))
            print(f"    {who.get(c, '?'):12} {c:8} {', '.join(ev) if ev else 'nothing bought, taxes never changed'}"
                  f"{f'  (ends at {level[c]})' if level[c] != 'normal' else ''}")
    if totals:
        print(f"\n## arm {arm}: per persona over {len(blocks)} game{'s' if len(blocks) != 1 else ''}")
        for p in sorted(totals):
            parts = ", ".join(f"{k} {v}" for k, v in sorted(totals[p].items()))
            fb = f"; first purchase on turn {sorted(first[p])}" if first[p] else ""
            print(f"  {p:12} {parts}{fb}")
    return totals


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("out", help="the run's output folder, one subfolder a block")
    ap.add_argument("--control", default="x", help="the control arm's block prefix (default x, free orders without Economy)")
    ap.add_argument("--limit", type=int, default=1600, help="the court prompt's token limit to report against")
    a = ap.parse_args()

    arms = collections.defaultdict(list)
    for b in sorted(os.listdir(a.out)):
        m = BLOCK.match(b)
        if m and os.path.exists(os.path.join(a.out, b, "model.csv")):
            arms[m.group(1)].append((b, m.group(2)))
    order = sorted(arms, key=lambda k: (k != a.control, k))

    results = {arm: arm_figures(a.out, arm, arms[arm], a.limit) for arm in order}
    ctl = results.get(a.control)
    if ctl:
        for arm in order:
            if arm == a.control:
                continue
            pairs = [(ctl[k], results[arm][k]) for k in ctl if k in results[arm]]
            if pairs:
                print(f"\n## {arm} against {a.control}, same seed and turn ({len(pairs)} turns): "
                      f"calls a turn {mean(y[0] - x[0] for x, y in pairs):+.2f}, model s a turn {mean(y[1] - x[1] for x, y in pairs):+.2f}")
    for arm in order:
        if arm != a.control:
            timelines(a.out, arm, arms[arm])


if __name__ == "__main__":
    main()
