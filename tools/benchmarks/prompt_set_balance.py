"""Counts the evaluation prompt set's balance, for the README's "Category balance" (#147).

    python tools/benchmarks/prompt_set_balance.py [path/to/agent.json]

Prints the section's table and lines as Markdown, from the files alone, so the README is never
counted by hand. Defaults to UnityProject/Assets/Evaluation/agent.json. No Unity needed.

"Found only in the observations" is a word match: none of the expected target's names (below) is in
the stimulus, and one is in an observation. A history turn's words do not count as the stimulus.
"""
import json
import os
import re
import sys
from collections import Counter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DEFAULT = os.path.join(ROOT, "UnityProject", "Assets", "Evaluation", "agent.json")
GAME_TRIGGERED = ("situation", "event", "turn")
AI = "claude (tech lead session)"

# The words a target goes by in a prompt. A target not listed goes by its id, underscores as spaces.
NAMES = {
    "village_gate": ["gate"],
    "campfire": ["campfire", "camp fire"],
    "north_tower": ["tower"],
    "market": ["market", "stalls?"],
    "docks": ["docks?"],
    "eastern_road": ["eastern road", "road east"],
    "locked_cave": ["cave"],
    "wolf": ["wolf", "wolves"],
}


def names(target):
    return NAMES.get(target, [target.replace("_", " ")])


def mentions(text, target):
    return any(re.search(r"\b" + n + r"\b", text, re.I) for n in names(target))


def main(path):
    folder = os.path.dirname(path)
    with open(path, encoding="utf-8") as f:
        agent = json.load(f)

    rows, prompts = [], []
    for category in agent["categories"]:
        file = os.path.join(folder, category["file"])
        found = []
        if os.path.exists(file):
            with open(file, encoding="utf-8") as f:
                found = json.load(f)["prompts"]
        rows.append((category["id"], found))
        prompts += [(category["id"], p) for p in found]
    if not any(c == "turn" for c, _ in rows):
        rows.append(("turn", []))

    total = len(prompts)
    double = lambda ps: sum(1 for p in ps if len(p.get("labelledBy", [])) > 1 or p.get("labels"))
    multi = lambda ps: sum(1 for p in ps if p.get("history"))
    ai = lambda ps: sum(1 for p in ps if AI in p.get("labelledBy", []))

    out = ["| Category | Prompts | Written by the AI session | Multi-turn | Doubly labelled |", "|---|---|---|---|---|"]
    for cid, ps in rows:
        out.append(f"| `{cid}` | {len(ps)} | {ai(ps)} | {multi(ps)} | {double(ps)} |")
    every = [p for _, p in prompts]
    out.append(f"| **All** | **{total}** | **{ai(every)}** | **{multi(every)}** | **{double(every)}** |")
    out.append("")

    game = sum(1 for c, _ in prompts if c in GAME_TRIGGERED)
    actions = Counter(p["expectedAction"] for p in every)
    none = actions.get("none", 0)
    targets = lambda p: p["expectedTarget"] if isinstance(p["expectedTarget"], list) else [p["expectedTarget"]]
    only_obs = [p["id"] for p in every
                if "no_target" not in targets(p)
                and not any(mentions(p["stimulus"], t) for t in targets(p))
                and any(mentions(o, t) for o in p.get("observations", []) for t in targets(p))]
    open_choice = [p["id"] for p in every if isinstance(p["expectedTarget"], list) and len(p["expectedTarget"]) > 1]
    unchecked = [p["id"] for p in every if p.get("checkTarget") is False]
    noted = sum(1 for p in every if p.get("note"))

    pct = lambda n: f"{round(100 * n / total)}%" if total else "0%"
    out.append(f"- **Game-triggered** (`situation`, `event`, `turn`): {game} of {total}, {pct(game)}.")
    out.append("- **Expected actions:** " + ", ".join(f"`{a}` {n}" for a, n in actions.most_common())
               + f". A model that never acts would score {pct(none)}.")
    out.append(f"- **Targets found only in the observations:** {len(only_obs)}, by word match.")
    out.append(f"- **Open choices:** {len(open_choice)}. **`checkTarget: false`:** {len(unchecked)}.")
    out.append(f"- **Multi-turn** (with a `history`): {multi(every)}.")
    out.append(f"- **With a `note`:** {noted}.")
    print("\n".join(out))


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else DEFAULT)
