"""docs/run-requests/QUEUE.json: the runs owed, in order, and whether the runner may play them.

    {"paused": false, "pause_reason": "", "order": ["007-...", "008-..."], "hold": ["010-..."]}

order is what plays next, first to last; hold lists requests that must not run yet. The runner
only reads it (from origin/test). pause, resume and front change it in a commit on test, which
asks before pushing, so every pause and reorder is in the history with its reason.
"""
import json
import shutil
import tempfile
from pathlib import Path

from . import gitio
from .common import QUEUE_FILE, RunnerError, say

EMPTY = {"paused": False, "pause_reason": "", "order": [], "hold": []}


def parse(text):
    """The queue from its text, checked; an absent file is an empty queue."""
    if not text:
        return dict(EMPTY)
    try:
        data = json.loads(text)
    except ValueError as e:
        raise RunnerError(f"{QUEUE_FILE.as_posix()} is not valid JSON: {e}")
    problems = check(data)
    if problems:
        raise RunnerError(f"{QUEUE_FILE.as_posix()}: " + "; ".join(problems))
    out = dict(EMPTY)
    out.update(data)
    return out


def check(data):
    p = []
    if not isinstance(data, dict):
        return ["it must be an object"]
    for key in sorted(set(data) - set(EMPTY)):
        p.append(f"unknown field {key!r}")
    if not isinstance(data.get("paused", False), bool):
        p.append("paused must be true or false")
    for key in ("order", "hold"):
        value = data.get(key, [])
        if not isinstance(value, list) or not all(isinstance(x, str) for x in value):
            p.append(f"{key} must be a list of request ids")
        elif len(set(value)) != len(value):
            p.append(f"{key} names a request twice")
    both = set(data.get("order") or []) & set(data.get("hold") or [])
    if both:
        p.append(f"{', '.join(sorted(both))} is in both order and hold")
    return p


def pick(queue, playable):
    """The first request in order that still has a block to play; playable(id) says whether one has."""
    for request_id in queue["order"]:
        if request_id not in queue["hold"] and playable(request_id):
            return request_id
    return None


# ---- changing it on test --------------------------------------------------------------------

def pause(queue, why):
    queue["paused"], queue["pause_reason"] = True, why
    return f"Run queue: pause ({why})"


def resume(queue, why=""):
    queue["paused"], queue["pause_reason"] = False, ""
    return "Run queue: resume" + (f" ({why})" if why else "")


def front(queue, request_id, why=""):
    if request_id in queue["hold"]:
        queue["hold"].remove(request_id)
    if request_id in queue["order"]:
        queue["order"].remove(request_id)
    queue["order"].insert(0, request_id)
    return f"Run queue: {request_id} to the front" + (f" ({why})" if why else "")


def change(repo, edit, yes=False, ask=input):
    """Apply edit(queue) -> commit subject on origin/test's QUEUE.json, show it, and push to test on a yes.

    Works in a throwaway worktree, so the caller's own checkout and branch are untouched. If test moved
    meanwhile, it fetches and tries once more.
    """
    for attempt in (1, 2):
        gitio.fetch(repo)
        temp = Path(tempfile.mkdtemp(prefix="runq-queue-"))
        path = temp / "wt"
        try:
            gitio.git(repo, "worktree", "add", "--quiet", "--detach", str(path), "origin/test")
            file = path / QUEUE_FILE
            queue = parse(file.read_text(encoding="utf-8") if file.is_file() else "")
            subject = edit(queue)
            file.parent.mkdir(parents=True, exist_ok=True)
            file.write_text(json.dumps(queue, indent=2) + "\n", encoding="utf-8", newline="\n")
            if not gitio.git(path, "status", "--porcelain").strip():
                say("the queue is already like that; nothing to push")
                return False
            gitio.git(path, "add", QUEUE_FILE.as_posix())
            gitio.git(path, "commit", "--quiet", "-m", subject)
            say(gitio.git(path, "show", "--stat", "--format=%s", "HEAD").rstrip())
            say(gitio.git(path, "diff", "HEAD~1", "--", QUEUE_FILE.as_posix()).rstrip())
            if not yes:
                try:
                    answer = ask("push this to test? [y/N] ")
                except EOFError:
                    answer = ""
                if answer.strip().lower() not in ("y", "yes"):
                    say("not pushed")
                    return False
            try:
                gitio.git(path, "push", "--quiet", "origin", "HEAD:refs/heads/test")
                say("pushed to test")
                return True
            except RunnerError:
                if attempt == 2:
                    raise
                say("test moved meanwhile; trying again on the new head")
        finally:
            gitio.git(repo, "worktree", "remove", "--force", str(path), check=False)
            shutil.rmtree(str(temp), ignore_errors=True)
    return False
