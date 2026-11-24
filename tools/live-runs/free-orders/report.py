"""Report on Painted Realm's free-orders live runs (#20): run 1 (the court's prompt A/B) and run 2 (the played check).

    python tools/live-runs/free-orders/report.py <out> [--control c]

<out> holds one folder a block, as `StrategyTableMenu.RunInterleavedFromCommandLine` and
`FreeOrdersLiveCheck.RunFromCommandLine` write them:

- Watch-mode blocks (a model table playing alone) are named <arm><seed>, such as c1, t1, n1, and read from
  `model.csv`: illegal answers, court orders a turn by their number in the turn, calls and model seconds a turn,
  each arm against the control arm on the same seed and turn, and what a viewer of a 12 s playback would wait.
- Played blocks are named p<plan seconds>s<seed>, such as p12s1, and read from `turns.csv`: how long End turn waited.
  A turn with failed calls (`errors` > 0) measures the network, not the model, and is left out.

Standard library only. Numbers are printed, never compared with another machine's: the RTX 5080's runs and the home
server's are different machines (AGENTS.md, hard rule 2).
"""
import argparse
import collections
import csv
import os
import re
import statistics as st

PHASES = [("early", 1, 13), ("mid", 14, 26), ("late", 27, 40)]
WATCH = re.compile(r"^([a-z]+?)(\d+)$")
PLAYED = re.compile(r"^p(\d+)s(\d+)$")


def rows(folder, name):
    p = os.path.join(folder, name)
    if not os.path.exists(p):
        return []
    with open(p, encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def mean(xs, default=0.0):
    xs = list(xs)
    return st.mean(xs) if xs else default


def watch_arm(data, arm, blocks):
    """Prints one arm's watch-mode figures; returns {(seed, turn): [calls, model seconds]}."""
    per_turn = collections.defaultdict(lambda: [0, 0.0])
    court_orders = collections.defaultdict(int)  # (seed, turn, country) -> court orders given (not none)
    total = illegal = council = council_illegal = 0
    moments = collections.Counter()
    for b, seed in blocks:
        for r in rows(os.path.join(data, b), "model.csv"):
            t = int(r["turn"])
            per_turn[(seed, t)][0] += 1
            per_turn[(seed, t)][1] += float(r["latency_s"] or 0)
            bad = r["model_answer_legal"] == "False"
            total += 1
            illegal += bad
            moments[r["moment"]] += 1
            if r["moment"] == "Council":
                council += 1
                council_illegal += bad
                court_orders[(seed, t, r["country"])] += r["action"] not in ("none", "")
    names = ", ".join(b for b, _ in blocks)
    print(f"\n## arm {arm} ({names})")
    if not per_turn:
        print("  no model.csv")
        return {}
    print(f"  turns {len(per_turn)}, decisions {total}, illegal answers {illegal} ({100 * illegal / max(1, total):.1f}%); "
          f"court requests {council}, illegal {council_illegal} ({100 * council_illegal / max(1, council):.1f}%)")
    print(f"  decisions by moment: {dict(sorted(moments.items()))}")
    # Court orders a realm gave a turn, over the realm-turns with at least one court request ('none' only counts 0).
    realm_turns = collections.Counter(court_orders.values())
    if realm_turns:
        print("  court orders a realm a turn (realm-turns with at least one request): "
              + ", ".join(f"{k}: {realm_turns[k]}" for k in sorted(realm_turns))
              + f"; mean {mean(court_orders.values()):.2f}")
    for name, lo, hi in PHASES:
        ts = [v for (s, t), v in per_turn.items() if lo <= t <= hi]
        if ts:
            print(f"  {name:5} turns {lo}-{hi}: {len(ts)} turns; calls a turn {mean(v[0] for v in ts):.1f} (max {max(v[0] for v in ts)}); "
                  f"model s a turn {mean(v[1] for v in ts):.1f} (max {max(v[1] for v in ts):.1f})")
    print(f"  all: calls a turn {mean(v[0] for v in per_turn.values()):.1f}; model s a turn {mean(v[1] for v in per_turn.values()):.1f}")
    return dict(per_turn)


def viewer_waits(per_turn, seed, playback):
    """Watch mode as a viewer sees it: the next turn is decided under the playback of the last. With a playback of P
    seconds, turn T's playback starts at max(the last playback's end, T's resolution), so a viewer waits only for
    deciding that runs past the playback, and time banked carries on."""
    turns = sorted(t for (s, t) in per_turn if s == seed)
    resolved = last_end = 0.0
    waits = []
    for t in turns:
        resolved += per_turn[(seed, t)][1]
        start = max(last_end, resolved)
        if t > 1:
            waits.append(start - last_end)
        last_end = start + playback
    return waits


def played(data, blocks):
    print("\n## played (rule mode, a stand-in player)")
    for b in blocks:
        rs = rows(os.path.join(data, b), "turns.csv")
        dropped = [r["turn"] for r in rs if int(r.get("errors") or 0) > 0]
        rs = [r for r in rs if int(r.get("errors") or 0) == 0]
        if not rs:
            print(f"  {b}: no turns.csv, or every turn had failed calls")
            continue
        wait = [float(r["end_turn_wait_s"]) for r in rs]
        treaty = [float(r["treaty_wait_s"]) for r in rs]
        print(f"  {b}: plan {rs[0]['plan_s']} s, {len(rs)} turns{f' (left out, failed calls: turns {dropped})' if dropped else ''}; "
              f"End turn waited mean {mean(wait):.2f} s, max {max(wait):.2f}; "
              f"treaties {sum(treaty):.2f} of {sum(wait):.2f} s ({100 * sum(treaty) / max(0.001, sum(wait)):.0f}%); "
              f"rulers deciding at End turn {[int(r['deciding_at_end_turn']) for r in rs]}; "
              f"calls a turn {mean(int(r['calls']) for r in rs):.1f}; model s a turn {mean(float(r['model_s']) for r in rs):.1f}; "
              f"your court orders a turn {mean(int(r['your_court_orders']) for r in rs):.1f}")
    by_plan = collections.defaultdict(list)
    for b in blocks:
        for r in rows(os.path.join(data, b), "turns.csv"):
            if int(r.get("errors") or 0) == 0:
                by_plan[PLAYED.match(b).group(1)].append(float(r["end_turn_wait_s"]))
    for plan, ws in sorted(by_plan.items(), key=lambda kv: int(kv[0])):
        print(f"  plan {plan} s, all seeds: {len(ws)} turns, End turn waited mean {mean(ws):.2f} s, max {max(ws):.2f}")


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("out", help="the run's output folder, one subfolder a block")
    ap.add_argument("--control", default="c", help="the control arm's block prefix (default c)")
    ap.add_argument("--playback", type=float, default=12.0, help="playback seconds for the viewer-wait section")
    a = ap.parse_args()

    names = sorted(b for b in os.listdir(a.out) if os.path.isdir(os.path.join(a.out, b)))
    arms = collections.defaultdict(list)
    plays = []
    for b in names:
        if PLAYED.match(b):
            plays.append(b)
        elif (m := WATCH.match(b)) and os.path.exists(os.path.join(a.out, b, "model.csv")):
            arms[m.group(1)].append((b, m.group(2)))

    results = {arm: watch_arm(a.out, arm, blocks) for arm, blocks in sorted(arms.items(), key=lambda kv: (kv[0] != a.control, kv[0]))}

    ctl = results.get(a.control)
    if ctl:
        for arm, d in results.items():
            if arm == a.control:
                continue
            pairs = [(ctl[k], d[k]) for k in ctl if k in d]
            if pairs:
                print(f"\n## {arm} against {a.control}, same seed and turn ({len(pairs)} turns): "
                      f"calls a turn {mean(y[0] - x[0] for x, y in pairs):+.2f}, model s a turn {mean(y[1] - x[1] for x, y in pairs):+.2f}")

    if results:
        print(f"\n## watch mode with a {a.playback:g} s playback: seconds a viewer waits between one playback and the next")
        for arm, d in results.items():
            allw = []
            for seed in sorted({s for s, _ in d}, key=int):
                ws = viewer_waits(d, seed, a.playback)
                allw += ws
                if ws:
                    print(f"  {arm} seed {seed}: mean {mean(ws):.1f} s, max {max(ws):.1f}, turns with any wait {sum(x > 0.05 for x in ws)} of {len(ws)}")
            if allw:
                print(f"  {arm} all: mean {mean(allw):.1f} s a turn")

    if plays:
        played(a.out, plays)


if __name__ == "__main__":
    main()
