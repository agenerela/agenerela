"""Exports one game's decisions from a table run into a recording the screen can play back.

    python export_recording.py <run folder> <seed> <out.json>

A table run (Tables.Named, StrategyTableMenu) writes decisions.jsonl: every framework decision,
with its telemetry, keyed by chainId seed-<n>-turn-<t>-<council|envoys>. The rules are
deterministic per seed, so these choices are all the screen needs to rebuild the game exactly.

Each decision keeps its "Decided" record, which is what the game received, plus the
"execution" of its "Executed" record when the move reached Agent.Execute. The request keeps the
facts sent, the history and the offered actions, and drops the system prompt and the full target
list, which are long and the same every turn. Personas are read from the run's game-<n>.md
header.
"""
import glob
import json
import os
import re
import sys


def main(run, seed, out):
    rows = []
    for f in sorted(glob.glob(os.path.join(run, "decisions*.jsonl"))):
        for line in open(f, encoding="utf-8"):
            r = json.loads(line)
            if (r.get("chainId") or "").startswith(f"seed-{seed}-turn-"):
                rows.append(r)
    if not rows:
        sys.exit(f"no decisions for seed {seed} in {run}")
    executed = {}
    for r in rows:
        if r["stage"] == "Executed":
            executed[(r["chainId"], r["agentId"])] = r.get("execution")
    decisions = []
    for r in rows:
        if r["stage"] != "Decided":
            continue
        d = {k: v for k, v in r.items() if k not in ("request", "stage", "time")}
        req = r.get("request") or {}
        d["request"] = {k: req.get(k) for k in ("observations", "history", "stimulusTurn", "actions")}
        d["execution"] = executed.get((r["chainId"], r["agentId"]))
        m = re.match(r"seed-\d+-turn-(\d+)-(\w+)", r["chainId"])
        d["turn"], d["moment"] = int(m.group(1)), m.group(2)
        decisions.append(d)
    decisions.sort(key=lambda d: (d["turn"], d["moment"] != "council", d["sequence"]))

    header = open(os.path.join(run, f"game-{seed:04d}.md"), encoding="utf-8").read().split("\n## Turn 1", 1)[0]
    players = {}
    for m in re.finditer(r"^\| (\w+) \| ([^|]+?) \|", header, re.M):
        if m.group(1) != "Country":
            parts = m.group(2).split()
            players[m.group(1)] = {"model": parts[0], "persona": parts[1] if len(parts) > 1 else None}
    title = header.splitlines()[0].lstrip("# ").strip()

    rec = {
        "format": 1,
        "source": os.path.basename(os.path.normpath(run)),
        "seed": seed,
        "title": title,
        "players": players,
        "decisions": decisions,
    }
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        json.dump(rec, f, ensure_ascii=False, indent=0)
    print(out, len(decisions), "decisions", players)


if __name__ == "__main__":
    main(sys.argv[1], int(sys.argv[2]), sys.argv[3])
