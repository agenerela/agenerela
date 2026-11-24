"""A stand-in for a Unity block, for the kit's tests: python fake_block.py <out> <name> <seconds> [say <text>] [exit <code>].

Appends "<name> start" and "<name> end" to <out>/plays.txt, sleeps, and writes <out>/<name>/summary.md.
"""
import sys
import time
from pathlib import Path

out, name, seconds = Path(sys.argv[1]), sys.argv[2], float(sys.argv[3])
rest = sys.argv[4:]
with open(out / "plays.txt", "a", encoding="utf-8") as f:
    f.write(f"{name} start\n")
(out / name).mkdir(parents=True, exist_ok=True)
(out / name / "partial.txt").write_text("half a game\n", encoding="utf-8")
time.sleep(seconds)
if "say" in rest:
    print(rest[rest.index("say") + 1], flush=True)
if "exit" in rest:
    sys.exit(int(rest[rest.index("exit") + 1]))
(out / name / "summary.md").write_text(f"# {name}\n", encoding="utf-8")
with open(out / "plays.txt", "a", encoding="utf-8") as f:
    f.write(f"{name} end\n")
