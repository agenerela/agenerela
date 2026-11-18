"""manifest.json: a request's blocks with a status each, so a run stops after any block and resumes.

A status is one of planned, running, done, "invalid: <why>" or "skipped: <why>". Resuming is
starting at the first block, in play order, that is planned, or invalid and tried only once: an
invalid block is retried once, then left for a person. A done block is never played again unless a
person deletes its entry, which puts it back to planned.
"""
import shutil
import time
from pathlib import Path

from .common import RunnerError, now, read_json, write_json

MAX_TRIES = 2


class Manifest:
    def __init__(self, path, data):
        self.path = Path(path)
        self.data = data

    # ---- loading ---------------------------------------------------------------------------

    @classmethod
    def open(cls, path, request, runner):
        """The request's manifest, made if it is new; refuses one written for another commit."""
        path = Path(path)
        if path.is_file():
            data = read_json(path)
            if data.get("id") != request.id:
                raise RunnerError(f"{path} belongs to request {data.get('id')!r}, not {request.id!r}")
            if data.get("commit") != request.commit:
                raise RunnerError(f"the request pins {request.commit[:12]} but its manifest was played at "
                                  f"{str(data.get('commit'))[:12]}: a request is never edited, so make a new "
                                  "one, or move the old results folder aside")
            known = {b["name"] for b in data.get("blocks", [])}
            for b in request.blocks:
                if b.name not in known:  # a person deleted its entry: play it again
                    data["blocks"].append({"name": b.name, "status": "planned"})
            wanted = {b.name for b in request.blocks}
            data["blocks"] = [b for b in data["blocks"] if b["name"] in wanted]
            data["runner"] = runner
        else:
            data = {"id": request.id, "commit": request.commit, "runner": runner, "ollama": {},
                    "created": now(), "blocks": [{"name": b.name, "status": "planned"} for b in request.blocks]}
        m = cls(path, data)
        m.save()
        return m

    def save(self):
        self.data["updated"] = now()
        write_json(self.path, self.data)

    # ---- reading ---------------------------------------------------------------------------

    def entry(self, name):
        for b in self.data["blocks"]:
            if b["name"] == name:
                return b
        raise RunnerError(f"no block {name!r} in {self.path}")

    def status(self, name):
        return self.entry(name).get("status", "planned")

    def playable(self, name):
        e = self.entry(name)
        s = e.get("status", "planned")
        return s == "planned" or (s.startswith("invalid") and e.get("tries", 0) < MAX_TRIES)

    def next_block(self, request):
        for b in request.play_order():
            if self.playable(b.name):
                return b
        return None

    def counts(self):
        out = {"done": 0, "invalid": 0, "skipped": 0, "running": 0, "planned": 0}
        for b in self.data["blocks"]:
            key = b.get("status", "planned").split(":")[0]
            out[key] = out.get(key, 0) + 1
        return out

    def minutes_left(self, request):
        return sum(b.minutes or 0 for b in request.blocks if self.playable(b.name) or self.status(b.name) == "running")

    def slice_done(self, request):
        return all(not self.playable(n) and self.status(n) != "running" for n in request.smallest_slice)

    # ---- changing --------------------------------------------------------------------------

    def start(self, name, pid=None, exe=None):
        e = self.entry(name)
        e.update(status="running", started=now(), tries=e.get("tries", 0) + 1)
        for key in ("ended", "minutes", "why"):
            e.pop(key, None)
        if pid is not None:
            e["pid"], e["exe"] = pid, exe
        self.save()

    def set_pid(self, name, pid, exe):
        self.entry(name).update(pid=pid, exe=exe)
        self.save()

    def finish(self, name, status, started_clock=None):
        e = self.entry(name)
        e["status"] = status
        e["ended"] = now()
        if started_clock is not None:
            e["minutes"] = round((time.monotonic() - started_clock) / 60, 2)
        e.pop("pid", None)
        e.pop("exe", None)
        self.save()

    def back_to_planned(self, name, why):
        e = self.entry(name)
        e["status"] = "planned"
        e["tries"] = max(0, e.get("tries", 1) - 1)  # an interrupted try is not a failed one
        e["interrupted"] = e.get("interrupted", []) + [f"{now()}: {why}"]
        for key in ("pid", "exe", "started", "ended", "minutes"):
            e.pop(key, None)
        self.save()

    def recover(self, out, alive, kill):
        """Blocks a stopped run left running: kill what is left of them, set their output aside, plan them again."""
        found = []
        for e in self.data["blocks"]:
            if e.get("status") != "running":
                continue
            pid, exe = e.get("pid"), e.get("exe")
            if pid and alive(pid, exe):
                kill(pid)
            set_aside(out, e["name"])
            self.back_to_planned(e["name"], "found running when the runner started")
            found.append(e["name"])
        return found


def set_aside(out, name):
    """Move a block's partial output and log to <out>/_aside/<name>-<time>/, so the next try starts clean."""
    out = Path(out)
    items = [p for p in (out / name, out / f"{name}.log") if p.exists()]
    if not items:
        return None
    aside = out / "_aside" / f"{name}-{time.strftime('%Y%m%d-%H%M%S')}"
    aside.mkdir(parents=True, exist_ok=True)
    for p in items:
        for attempt in range(20):  # a process just killed can hold its log open for a moment
            try:
                shutil.move(str(p), str(aside / p.name))
                break
            except PermissionError:
                if attempt == 19:
                    raise
                time.sleep(0.5)
    return aside
