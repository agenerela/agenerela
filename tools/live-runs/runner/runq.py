"""The run queue: play live runs on the RTX 5080 machine, resumably, in an order people can change.

    runq.py serve                      on the RTX 5080 machine: play the queue, forever
    runq.py run <request folder>       anywhere: play one request, resumable
    runq.py status                     anywhere: the queue, each request's progress, whether the card is in use
    runq.py pause "<why>" | resume     change QUEUE.json on test in a commit (asks before pushing)
    runq.py front <request id>         move a request to the front of the order (asks before pushing)
    runq.py check <request folder>     check a run.json without running it

DESIGN.md is the design; README.md is for a person, AGENT_SETUP.md for the agent on the RTX 5080
machine, AGENT_USAGE.md for a session on a laptop. Standard library only, Python 3.8 or newer.
"""
import argparse
import json
import os
import socket
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from runner import gitio, queuefile  # noqa: E402
from runner.card import Card, NullCard, main_checkout  # noqa: E402
from runner.common import REPO, RunnerError, parse_time, read_json, say  # noqa: E402
from runner.manifest import Manifest  # noqa: E402
from runner.play import TIMEOUT_FACTOR, Player  # noqa: E402
from runner.request import Request  # noqa: E402
from runner.serve import Server  # noqa: E402
from runner.status import Status  # noqa: E402


def work_dir(args):
    if getattr(args, "work", None):
        return Path(args.work).resolve()
    if os.environ.get("AGENERELA_RUNNER_WORK"):
        return Path(os.environ["AGENERELA_RUNNER_WORK"]).resolve()
    return main_checkout() / ".scratch" / "runner"


def describe_runner(kind, request=None):
    host = socket.gethostname()
    what = "command" if request is not None and request.command else "unity-editor-batch"
    if kind == "server":
        return f"{what} on the RTX 5080 machine ({host}), gateway on 127.0.0.1"
    if kind == "laptop":
        return f"{what} on {host}, the RTX 5080's Ollama over the network"
    return f"{what} on {host}, no model"


# ---- check ---------------------------------------------------------------------------------

def check_request(request, repo=REPO, fetch=True):
    """(problems, notes): problems stop a run; notes are said and do not."""
    problems = request.problems()
    notes = []
    if problems:
        return problems, notes
    if fetch:
        try:
            gitio.fetch(repo)
        except RunnerError as e:
            notes.append(f"could not fetch, checking against what is here: {e}")
    sha = request.commit
    if not gitio.exists(repo, f"{sha}^{{commit}}"):
        problems.append(f"commit {sha[:12]} is not in this repository (pushed? fetched?)")
        return problems, notes
    if not gitio.is_ancestor(repo, sha, "origin/test"):
        notes.append(f"commit {sha[:12]} is not on test: fine for a branch's run, but say so in the request")
    if request.project:
        if not gitio.exists(repo, f"{sha}:{request.project}/ProjectSettings/ProjectVersion.txt"):
            problems.append(f"{request.project} is not a Unity project at {sha[:12]}")
        else:
            method = request.method.rsplit(".", 1)[-1]
            found = gitio.git(repo, "grep", "-l", f"void {method}(", sha, "--", request.project, check=False)
            if not found.strip():
                problems.append(f"no method {method} in {request.project} at {sha[:12]}")
    total = request.total_minutes()
    notes.append(f"{len(request.blocks)} block{'' if len(request.blocks) == 1 else 's'}, {total:g} min estimated, at most {TIMEOUT_FACTOR * total:g} min "
                 f"if every block ran to its limit"
                 + (f"; smallest slice {', '.join(request.smallest_slice)} first" if request.smallest_slice else ""))
    return problems, notes


def cmd_check(args):
    request = Request.load(args.request)
    problems, notes = check_request(request)
    for n in notes:
        say(f"note: {n}")
    for p in problems:
        say(f"problem: {p}")
    if not problems:
        say(f"{request.id}: fine")
    return 1 if problems else 0


# ---- run -----------------------------------------------------------------------------------

def queue_claims_card(repo):
    """Why the runner on the RTX 5080 machine owns the card now, or None."""
    try:
        gitio.fetch(repo)
    except RunnerError as e:
        say(f"warning: could not fetch test to read the queue: {e}")
    queue = queuefile.parse(gitio.queue_text(repo))
    if not queue["paused"] and [i for i in queue["order"] if i not in queue["hold"]]:
        return ("the queue is running, so the card is the runner's: pause it first "
                "(runq.py pause \"<why>\"), and resume it when you are done")
    return None


def cmd_run(args):
    request = Request.load(args.request)
    problems, notes = check_request(request)
    for n in notes:
        say(f"note: {n}")
    if problems:
        for p in problems:
            say(f"problem: {p}")
        return 1
    kind = "server" if args.on_server else "laptop"
    if not request.model:
        kind = "none"
    if kind != "none" and not args.ignore_queue:
        why = queue_claims_card(REPO)
        if why:
            raise RunnerError(why)
    work = work_dir(args)
    checkout = Path(args.checkout).resolve() if args.checkout else gitio.worktree(REPO, work, request.commit)
    if args.checkout:
        head = gitio.git(checkout, "rev-parse", "HEAD").strip()
        if head != request.commit:
            raise RunnerError(f"{checkout} is at {head[:12]}, but the request pins {request.commit[:12]}")
    pause_file = work / "pause"
    card = NullCard(pause_file) if kind == "none" else Card(kind, pause_file=pause_file)
    out = work / "results" / request.id
    status = Status(work / "status.json", describe_runner(kind, request))
    player = Player(request, out, checkout, card, describe_runner(kind, request), status)
    say(f"{request.id}: playing at {request.commit[:12]} in {checkout}, results in {out}")
    result, why = player.play()
    if args.push:
        if gitio.publish(REPO, work, request, out, [status.path]):
            say(f"results pushed to runs/{request.id}")
    if result == "stopped":
        say(f"{request.id}: stopped ({why}); run the same command again to continue")
    return 0


# ---- serve ---------------------------------------------------------------------------------

def cmd_serve(args):
    work = work_dir(args)
    work.mkdir(parents=True, exist_ok=True)
    if args.log:
        log = open(args.log, "a", encoding="utf-8", buffering=1)
        sys.stdout = sys.stderr = log
    card = Card("server", pause_file=work / "pause", parent_pid=args.parent_pid)
    runner = describe_runner("server")
    server = Server(REPO, work, card, runner, Status(work / "status.json", runner),
                    push=not args.no_push and not args.local, local=args.local)
    server.serve()
    return 0


def cmd_where(args):
    """Where this machine's runner keeps its files, as JSON (tray.ps1 reads it)."""
    work = work_dir(args)
    print(json.dumps({"work": str(work), "repo": str(REPO), "status": str(work / "status.json"),
                      "results": str(work / "results"), "python": sys.executable}))
    return 0


# ---- status --------------------------------------------------------------------------------

def ago(text):
    import datetime
    t = parse_time(text)
    if not t:
        return "?"
    minutes = int((datetime.datetime.now().astimezone() - t).total_seconds() // 60)
    return f"{minutes} min ago" if minutes < 120 else f"{minutes // 60} h ago"


def cmd_status(args):
    work = work_dir(args)
    try:
        gitio.fetch(REPO, runs=True)
    except RunnerError as e:
        say(f"warning: could not fetch, showing what is here: {e}")
    queue = queuefile.parse(gitio.queue_text(REPO))
    branches = gitio.results_branches(REPO)

    statuses = []
    local_status = work / "status.json"
    if local_status.is_file():
        statuses.append(("this machine", read_json(local_status)))
    for b in branches:
        text = gitio.show(REPO, f"origin/runs/{b}", f"docs/run-requests/{b}/results/status.json")
        if text:
            statuses.append((f"runs/{b}", json.loads(text)))
    if statuses:
        where, s = max(statuses, key=lambda x: x[1].get("time") or "")
        hand = f" {s.get('request')}/{s.get('block')}" if s.get("block") else (f" {s['request']}" if s.get("request") else "")
        say(f"runner   {s.get('state')}{hand}, card {'in use' if s.get('card_in_use') else 'not in use'}"
            f"{', waiting: ' + s['waiting'] if s.get('waiting') else ''}; {ago(s.get('time'))} (from {where})")
        if s.get("last_error"):
            say(f"         last error: {s['last_error']}")
    else:
        say("runner   no status yet (no runs/* branch, and no runner on this machine)")
    say("queue    " + (f"PAUSED: {queue['pause_reason'] or 'no reason given'}" if queue["paused"] else "running")
        + f"; {len(queue['order'])} in order, {len(queue['hold'])} on hold")

    ids = queue["order"] + queue["hold"] + sorted(set(branches) - set(queue["order"]) - set(queue["hold"]))
    for n, request_id in enumerate(ids, 1):
        place = f"{n}." if request_id in queue["order"] else ("hold" if request_id in queue["hold"] else "off")
        manifest, where = None, ""
        local = work / "results" / request_id / "manifest.json"
        if local.is_file():
            manifest, where = read_json(local), "here"
        elif request_id in branches:
            text = gitio.show(REPO, f"origin/runs/{request_id}", f"docs/run-requests/{request_id}/results/manifest.json")
            manifest, where = (json.loads(text), f"runs/{request_id}") if text else (None, "")
        if manifest is None:
            say(f"  {place:<5} {request_id}  not started")
            continue
        blocks = manifest.get("blocks", [])
        done = sum(1 for b in blocks if b.get("status") == "done")
        bad = [b["name"] for b in blocks if str(b.get("status", "")).startswith("invalid")]
        running = [b["name"] for b in blocks if b.get("status") == "running"]
        say(f"  {place:<5} {request_id}  {done}/{len(blocks)} done"
            + (f", invalid: {', '.join(bad)}" if bad else "")
            + (f", playing {running[0]}" if running else "")
            + (", finished" if manifest.get("finished") else "")
            + f"  ({where}, {ago(manifest.get('updated'))})")
    return 0


# ---- pause, resume, front ------------------------------------------------------------------

def cmd_pause(args):
    queuefile.change(REPO, lambda q: queuefile.pause(q, args.why), yes=args.yes)
    say("the runner stops after the block in hand (runq.py status shows when)")
    return 0


def cmd_resume(args):
    queuefile.change(REPO, lambda q: queuefile.resume(q, args.why or ""), yes=args.yes)
    return 0


def cmd_front(args):
    text = gitio.run_json(REPO, args.request_id)
    if not text:
        say(f"warning: docs/run-requests/{args.request_id}/run.json is not on test yet; the runner will skip it until it is")
    queuefile.change(REPO, lambda q: queuefile.front(q, args.request_id, args.why or ""), yes=args.yes)
    return 0


def main(argv=None):
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(errors="replace")
        except AttributeError:
            pass
    parser = argparse.ArgumentParser(prog="runq.py", description=__doc__.strip().splitlines()[0])
    sub = parser.add_subparsers(dest="command", required=True)

    def with_work(p):
        p.add_argument("--work", help="the work folder (default: AGENERELA_RUNNER_WORK, else .scratch/runner "
                                      "in the main checkout)")
        return p

    p = with_work(sub.add_parser("serve", help="on the RTX 5080 machine: play the queue"))
    p.add_argument("--no-push", action="store_true", help="keep results on this machine")
    p.add_argument("--parent-pid", type=int, help="stop when this process ends (tray.ps1 passes its own)")
    p.add_argument("--log", help="write what serve prints to this file")
    p.add_argument("--local", help="read QUEUE.json and the requests from this folder instead of origin/test, "
                                   "and push nothing (for trying the runner out)")
    p = with_work(sub.add_parser("run", help="play one request, resumably"))
    p.add_argument("request", help="the request folder, such as docs/run-requests/007-name")
    p.add_argument("--on-server", action="store_true", help="this is the RTX 5080 machine: use the gateway on 127.0.0.1")
    p.add_argument("--push", action="store_true", help="push the results to runs/<request id> afterwards")
    p.add_argument("--ignore-queue", action="store_true", help="play although the queue is running (only with the lead's yes)")
    p.add_argument("--checkout", help="play in this checkout, which must be at the request's commit, instead of a worktree")
    with_work(sub.add_parser("status", help="the queue and each request's progress"))
    with_work(sub.add_parser("where", help="where this machine's runner keeps its files, as JSON"))
    p = sub.add_parser("check", help="check a run.json without running it")
    p.add_argument("request")
    p = sub.add_parser("pause", help="pause the queue after the block in hand (a commit on test)")
    p.add_argument("why")
    p.add_argument("--yes", action="store_true", help="push without asking (after the user said yes)")
    p = sub.add_parser("resume", help="resume the queue (a commit on test)")
    p.add_argument("why", nargs="?")
    p.add_argument("--yes", action="store_true")
    p = sub.add_parser("front", help="move a request to the front of the order (a commit on test)")
    p.add_argument("request_id")
    p.add_argument("why", nargs="?")
    p.add_argument("--yes", action="store_true")
    args = parser.parse_args(argv)
    commands = {"serve": cmd_serve, "run": cmd_run, "status": cmd_status, "check": cmd_check,
                "pause": cmd_pause, "resume": cmd_resume, "front": cmd_front, "where": cmd_where}
    try:
        return commands[args.command](args)
    except RunnerError as e:
        say(f"error: {e}")
        return 1
    except KeyboardInterrupt:
        say("stopped; the block in hand was set aside and is planned again")
        return 130


if __name__ == "__main__":
    sys.exit(main())
