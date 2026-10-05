"""Scores target_swap_results.jsonl from target_swap_probe.py: every arm, every check, both
scenes pooled, per model.

    python tools/benchmarks/target_swap_score.py              summary tables
    python tools/benchmarks/target_swap_score.py -v           plus every miss
    python tools/benchmarks/target_swap_score.py --in=PATH    another results file
"""
import json, math, sys
from collections import defaultdict
import target_swap_probe as probe

CASE = {(c[0], c[3]): c for c in probe.CASES}
PATH = next((a[5:] for a in sys.argv[1:] if a.startswith("--in=")), "target_swap_results.jsonl")
rows = [json.loads(l) for l in open(PATH, encoding="utf-8") if l.strip()]
errors = [r for r in rows if "error" in r]
by = defaultdict(dict)                                    # (model, arm) -> (scene, stim) -> row
for r in rows:
    if "error" not in r:
        by[(r["model"], r["arm"])][(r["scene"], r["stim"])] = r

def legal_values(scene):
    return [v.lower() for v in probe.SCENES[scene]["targets"]] + ["no_target"]

def signals(r):
    """The three confidence readings for the chosen target. Lower means less sure."""
    top = r.get("top")
    if not top:
        return None
    alts, (tok, lp) = top[:-1], top[-1]
    vals = legal_values(r["scene"])
    def legal_start(t):
        t = t.strip().strip('"').lower()
        return bool(t) and any(v.startswith(t) for v in vals)
    illegal = sum(math.exp(l) for t, l in alts if not legal_start(t))
    return {"prod": r.get("p_target") or 0.0, "first": math.exp(lp), "legal": 1.0 - illegal}

def decide(r, check=None, thr=None, everywhere=False):
    """Final (action, target) after the arm's own resolution and an optional check in code."""
    c = CASE[(r["scene"], r["stim"])]
    scene, m = r["scene"], r["raw"]
    nt = probe.needs_target(scene)
    if r["arm"] == "A5":
        t = probe.resolve(scene, m.get("target", ""))
        a = m.get("action")
        a, t = ((a, t) if t in probe.SCENES[scene]["targets"] else ("none", "no_target")) if a in nt else (a, "no_target")
    else:
        a, t = probe.legal(scene, m.get("action"), m.get("target"))
    if a not in nt or check is None:
        return a, t
    player = c[1] in probe.PLAYER_CATS
    rewrite = False
    if check == "name":
        rewrite = player and not probe.name_in(scene, c[3], t)
    elif check == "verify":
        rewrite = player and r.get("verify") == "no"
    elif check == "mention":
        rewrite = not probe.mention_check(scene, m.get("requested", ""), t)
    elif check in ("prod", "first", "legal"):
        s = signals(r)
        rewrite = (player or everywhere) and s is not None and s[check] < thr
    return ("none", "no_target") if rewrite else (a, t)

CATS = ["imp_far", "imp_near", "imp_pron", "ok_exact", "ok_para", "ok_pron", "game", "chat"]

def score(model, arm, check=None, thr=None, everywhere=False):
    src = by.get((model, arm), {})
    res = defaultdict(lambda: [0, 0]); per_scene = defaultdict(lambda: [0, 0])
    wrong = falseref = 0; secs = []; misses = []
    for c in probe.CASES:
        r = src.get((c[0], c[3]))
        if not r:
            continue
        a, t = decide(r, check, thr, everywhere)
        g = probe.grade(c, a, t)
        ok = g == "correct"
        res[c[1]][0] += ok; res[c[1]][1] += 1
        per_scene[c[0]][0] += ok; per_scene[c[0]][1] += 1
        wrong += g == "wrong_legal"; falseref += g == "false_refusal"
        secs.append(r["sec"] + (r.get("verify_sec", 0) if check == "verify" else 0))
        if not ok:
            misses.append((c[0], c[1], c[3], f"{a}/{t}", g))
    return res, per_scene, wrong, falseref, secs, misses

def line(name, model, arm, check=None, thr=None, everywhere=False):
    res, ps, wrong, fr, secs, _ = score(model, arm, check, thr, everywhere)
    if not secs:
        return
    tot = sum(v[0] for v in res.values()), sum(v[1] for v in res.values())
    print(f"{name:<22}" + "".join(f"{res[k][0]:>4}/{res[k][1]:<3}" for k in CATS)
          + f"{wrong:>6}{fr:>6}{tot[0]:>6}/{tot[1]:<4}" + "".join(f"{ps[s][0]:>4}/{ps[s][1]:<3}" for s in probe.SCENES)
          + f"{sum(secs) / len(secs):>6.2f}")

def auroc(model, arm, key):
    pos, neg = [], []
    for c in probe.CASES:
        r = by.get((model, arm), {}).get((c[0], c[3]))
        if not r or c[1] not in probe.PLAYER_CATS:
            continue
        a, t = decide(r)
        if a not in probe.needs_target(c[0]):
            continue
        s = signals(r)
        if s is None:
            continue
        (neg if probe.grade(c, a, t) == "correct" else pos).append(s[key])
    if not pos or not neg:
        return None, len(pos), len(neg)
    wins = sum((p < n) + 0.5 * (p == n) for p in pos for n in neg)
    return round(wins / (len(pos) * len(neg)), 3), len(pos), len(neg)

for model in sorted({m for m, _ in by}):
    print(f"\n=== {model} ===   refused / acted correctly, by category; wrong = a wrong action carried out")
    print(f"{'':<22}" + "".join(f"{k:>8}" for k in CATS) + f"{'wrong':>6}{'fref':>6}{'total':>11}"
          + "".join(f"{s:>8}" for s in probe.SCENES) + f"{'sec':>6}")
    line("A0 control", model, "A0")
    line("A0 + name check", model, "A0", "name")
    line("A0 + verifier", model, "A0", "verify")
    line("A0 + conf legal 0.8 all", model, "A0", "legal", 0.8, True)  # the setting DR-016 adopts
    line("A0 + conf first 0.8", model, "A0", "first", 0.8)
    for k in ("prod", "first", "legal"):
        line(f"A0 + conf {k} 0.5", model, "A0", k, 0.5)
    line("A0 + conf first 0.8 all", model, "A0", "first", 0.8, True)
    line("A2 near-miss example", model, "A2")
    line("A2 + conf legal 0.5", model, "A2", "legal", 0.5)
    line("A3 explicit rule", model, "A3")
    line("A3 + conf legal 0.5", model, "A3", "legal", 0.5)
    line("A3 + name check", model, "A3", "name")
    line("A4 mention field", model, "A4")
    line("A4 + mention check", model, "A4", "mention")
    line("A4 + conf legal 0.5", model, "A4", "legal", 0.5)
    line("A5 free-text target", model, "A5")
    line("A0n (no logprobs)", model, "A0n")

    print("\nconfidence sweep (player requests only); columns as above")
    for arm in ("A0", "A3"):
        for k in ("prod", "first", "legal"):
            for thr in (0.2, 0.3, 0.5, 0.7, 0.8, 0.9):
                line(f"{arm} {k} <{thr}", model, arm, k, thr)
    for arm in ("A0", "A2", "A3", "A4"):
        print(f"AUROC {arm}: " + ", ".join(f"{k} {auroc(model, arm, k)}" for k in ("prod", "first", "legal"))
              + "   (value, wrong n, right n)")

    a0, a0n = by.get((model, "A0"), {}), by.get((model, "A0n"), {})
    diff = [k for k in a0 if k in a0n and (a0[k]["raw"].get("action"), a0[k]["raw"].get("target"))
            != (a0n[k]["raw"].get("action"), a0n[k]["raw"].get("target"))]
    vs = [r["verify_sec"] for r in a0.values() if "verify_sec" in r]
    print(f"A0 vs A0n decisions that differ: {len(diff)} {diff[:5]}")
    if vs:
        print(f"verifier: {len(vs)} extra requests, mean {sum(vs) / len(vs):.2f}s each")

    if "-v" in sys.argv:
        for name, arm, check, thr in (("A0", "A0", None, None), ("A0+first0.8", "A0", "first", 0.8),
                                      ("A3", "A3", None, None), ("A0+name", "A0", "name", None)):
            print(f"\n  {model} {name} misses:")
            for m in score(model, arm, check, thr)[5]:
                print("   ", m)
if errors:
    print(f"\n{len(errors)} request errors:", [(e['model'], e['arm'], e['stim'], e['error'][:60]) for e in errors[:10]])
