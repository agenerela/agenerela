"""Summarise a model sweep's folder into results.json and print one line per model
(tools/live-runs/model-sweep/README.md).

Usage: python aggregate.py <sweep folder> [--baseline qwen3.5:2b] [--compare other/results.json]
                           [--describe "what the set, server and machine were"]

Reads probe-<model>.json, template-<model>.txt (optional, the server log's "template selection"
result) and report-<model>/report.json for every model in the folder. --compare puts another
sweep's scores for the same models beside these (the curated set's beside the interim set's, or
one machine's beside another's). Standard library only.
"""
import argparse
import glob
import json
import math
import os
import statistics


def floor(n):
    return 10 * math.sqrt(50 / n)


def p90(xs):
    xs = sorted(xs)
    return xs[min(len(xs) - 1, math.ceil(0.9 * len(xs)) - 1)]


def arm_stats(answers, replies):
    n = len(answers)
    outcomes = {}
    for a in answers:
        outcomes[a["outcome"]] = outcomes.get(a["outcome"], 0) + 1
    correct = outcomes.get("Correct", 0)
    cats = {}
    for a in answers:
        c = cats.setdefault(a["category"], [0, 0])
        c[1] += 1
        c[0] += a["outcome"] == "Correct"
    stop = [a for a in answers if a["expectedAction"] == "stop_following"]
    none_expected = [a for a in answers if a["expectedAction"] == "none"]
    act_expected = [a for a in answers if a["expectedAction"] != "none"]
    lat = [a["latencySeconds"] for a in answers if a["outcome"] != "PipelineError"]
    return {
        "n": n, "correct": correct, "accuracy": round(100 * correct / n, 1), "raw": 100 * correct / n, "outcomes": outcomes,
        "categories": {k: {"correct": v[0], "n": v[1], "pct": round(100 * v[0] / v[1], 1), "raw": 100 * v[0] / v[1]}
                       for k, v in sorted(cats.items())},
        "stop_following": {"correct": sum(a["outcome"] == "Correct" for a in stop), "n": len(stop)},
        "unasked_actions": {"count": sum(a["modelAction"] not in (None, "none") for a in none_expected), "n": len(none_expected)},
        "missed_requests": {"count": sum(a["modelAction"] == "none" for a in act_expected), "n": len(act_expected)},
        "replies": replies,
        "reply_flagged": [{"prompt": a["promptId"], "statement": a["modelStatement"],
                           "flags": [(f["flag"] + (": " + f["detail"] if f.get("detail") else "")) for f in a["replyFlags"]]}
                          for a in answers if a.get("replyFlags")],
        "latency_s": {"median": round(statistics.median(lat), 3) if lat else None, "p90": round(p90(lat), 3) if lat else None},
        "pipeline_errors": [f'{a["promptId"]}: {a["errorKind"]}: {(a["errorMessage"] or "")[:120]}'
                            for a in answers if a["outcome"] == "PipelineError"],
    }


def verdict(a, b, n):
    d = round(b - a, 1)
    return {"delta": d, "floor": round(floor(n), 1), "verdict": "beyond noise" if abs(b - a) >= floor(n) else "within noise"}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("folder")
    ap.add_argument("--baseline", default="qwen3.5:2b")
    ap.add_argument("--compare")
    ap.add_argument("--describe", default="")
    args = ap.parse_args()
    here = os.path.abspath(args.folder)

    models = []
    for probe in sorted(glob.glob(os.path.join(here, "probe-*.json"))):
        p = json.load(open(probe, encoding="utf-8"))
        slug = os.path.basename(probe)[6:-5]
        tpl_file = os.path.join(here, f"template-{slug}.txt")
        tpl = open(tpl_file, encoding="utf-8").read().strip().replace("selected=", "") if os.path.exists(tpl_file) else ""
        entry = {"model": p["model"], "server": p.get("server"), "ollama_version": p["version"], "template": tpl,
                 "order_kept": f'{p["in_order"]}/{p["requests"]}', "order_ok": p["in_order"] == p["requests"],
                 "probe_errors": p["errors"], "thinking_replies": p["thinking_replies"],
                 "probe_eval_counts": p["eval_counts"], "probe_resident": p["resident"]}
        rep = os.path.join(here, f"report-{slug}", "report.json")
        if os.path.exists(rep):
            r = json.load(open(rep, encoding="utf-8"))
            arms = r["conditions"]["arms"]
            entry["residency"] = {a["name"]: [{"when": x["when"], "models": [{"name": m["name"], "size_mb": m["size"] // 1048576,
                                                                               "size_vram_mb": m["sizeVram"] // 1048576} for m in x["models"]]}
                                              for x in a["residency"]] for a in arms}
            entry["warnings"] = r["conditions"].get("warnings")
            entry["started_utc"], entry["finished_utc"] = r["conditions"]["startedUtc"], r["conditions"]["finishedUtc"]
            replies = {x.get("name"): x.get("replies") for x in r.get("arms", [])}
            by = {a["name"]: [x for x in r["answers"] if x["arm"] == a["name"]] for a in arms}
            on, off = by["idle-profile"], by["idle-none"]
            entry["idle_on"] = arm_stats(on, replies.get("idle-profile"))
            entry["idle_off"] = arm_stats(off, replies.get("idle-none"))
            entry["idle_off_minus_on"] = verdict(entry["idle_on"]["raw"], entry["idle_off"]["raw"], len(on))
            entry["idle_off_minus_on_by_category"] = {
                c: verdict(entry["idle_on"]["categories"][c]["raw"], entry["idle_off"]["categories"][c]["raw"],
                           entry["idle_on"]["categories"][c]["n"]) for c in entry["idle_on"]["categories"]}
            entry["answers"] = {name: {x["promptId"]: [x["modelAction"], x["modelTarget"], x["outcome"]] for x in by[name]}
                                for name in ("idle-profile", "idle-none")}
        models.append(entry)

    if args.compare and os.path.exists(args.compare):
        other = {m["model"]: m for m in json.load(open(args.compare, encoding="utf-8"))["models"]}
        for m in models:
            o = other.get(m["model"])
            if o and "idle_on" in o:
                m["compared"] = {k: {"correct": o[k]["correct"], "n": o[k]["n"], "accuracy": o[k]["accuracy"]}
                                 for k in ("idle_on", "idle_off")}

    base = next((m for m in models if m["model"] == args.baseline and "idle_on" in m), None)
    for m in models:
        if base and "idle_on" in m and m is not base:
            m["default_vs_baseline"] = verdict(base["idle_on"]["raw"], m["idle_on"]["raw"], m["idle_on"]["n"])
            m["default_vs_baseline_by_category"] = {
                c: verdict(base["idle_on"]["categories"][c]["raw"], m["idle_on"]["categories"][c]["raw"],
                           m["idle_on"]["categories"][c]["n"]) for c in m["idle_on"]["categories"]}

    out = {"describe": args.describe, "baseline": args.baseline, "compare": args.compare,
           "arms": {"idle-profile": "defaults (the profile's idle example)", "idle-none": "IdleExampleStimulus = ''"},
           "noise_floor": "10 x sqrt(50 / n) points", "models": models}
    json.dump(out, open(os.path.join(here, "results.json"), "w", encoding="utf-8"), indent=1)

    for m in models:
        if "idle_on" not in m:
            print(f'{m["model"]}: order {m["order_kept"]}, no A/B report')
            continue
        a, b = m["idle_on"], m["idle_off"]
        cmp_ = m.get("compared", {}).get("idle_on", {}).get("accuracy", "-")
        vb = m.get("default_vs_baseline", {})
        print(f'{m["model"]}: order {m["order_kept"]} {m["template"]} | on {a["correct"]}/{a["n"]} off {b["correct"]}/{b["n"]} '
              f'({m["idle_off_minus_on"]["delta"]:+} {m["idle_off_minus_on"]["verdict"]}) | '
              f'stop_following {a["stop_following"]["correct"]}/{a["stop_following"]["n"]}->{b["stop_following"]["correct"]}/{b["stop_following"]["n"]} | '
              f'unasked {a["unasked_actions"]["count"]}->{b["unasked_actions"]["count"]} missed {a["missed_requests"]["count"]}->{b["missed_requests"]["count"]} | '
              f'replies flagged {(a["replies"] or {}).get("flagged")}->{(b["replies"] or {}).get("flagged")} | '
              f'latency {a["latency_s"]["median"]}/{a["latency_s"]["p90"]} s | errors {len(a["pipeline_errors"])}/{len(b["pipeline_errors"])} | '
              f'compared {cmp_}% | vs baseline {vb.get("delta", "-")} {vb.get("verdict", "")}')


if __name__ == "__main__":
    main()
