"""runq.py serve: the loop on the RTX 5080 machine (DESIGN.md, Part 3 "The runner's loop").

    forever:
      git fetch; read QUEUE.json and the requests' run.json at origin/test
      if paused, or order is empty: write status; wait a minute
      pick the first request in order that has a block not done
      play its next block
      after the block: write the manifest and the status; push results if the request finished
                       or thirty minutes have passed since the last push; switch if the order changed
"""
import json
import time
from pathlib import Path

from . import gitio, queuefile
from .common import RunnerError, read_json, say
from .manifest import Manifest
from .play import Player
from .request import Request

PUSH_EVERY = 30 * 60
IDLE_SECONDS = 60


class Server:
    def __init__(self, repo, work, card, runner, status, push=True, idle=IDLE_SECONDS, once=False, local=None):
        self.repo = Path(repo)
        self.work = Path(work)
        self.card = card
        self.runner = runner
        self.status = status
        self.push = push
        self.idle = idle
        self.once = once           # tests: return when there is nothing to do instead of waiting
        self.last_push = {}
        self.pause_file = self.work / "pause"
        if card.pause_file is None:  # the machine's own controls always live in the work folder
            card.pause_file, card.stop_file = self.pause_file, self.work / "stop"
        self.local = Path(local) if local else None  # read the queue and requests here, not from origin/test

    # ---- reading what is owed --------------------------------------------------------------

    def read(self):
        """(queue, {id: Request or an error string}) at origin/test, after a fetch."""
        if self.local is None:
            try:
                gitio.fetch(self.repo)
            except RunnerError as e:
                say(f"warning: fetch failed, using what is here: {e}")

        def text_of(relative):
            if self.local is None:
                return gitio.show(self.repo, "origin/test", Path("docs/run-requests") / relative)
            path = self.local / relative
            return path.read_text(encoding="utf-8") if path.is_file() else None

        queue = queuefile.parse(text_of("QUEUE.json"))
        requests = {}
        for request_id in queue["order"]:
            text = text_of(Path(request_id) / "run.json")
            if not text:
                requests[request_id] = f"no docs/run-requests/{request_id}/run.json on test"
                continue
            try:
                r = Request(json.loads(text), Path(request_id))
            except ValueError as e:
                requests[request_id] = f"run.json is not valid JSON: {e}"
                continue
            problems = r.problems()
            requests[request_id] = r if not problems else "; ".join(problems)
        return queue, requests

    def out(self, request_id):
        return self.work / "results" / request_id

    def playable(self, request):
        path = self.out(request.id) / "manifest.json"
        if not path.is_file():
            return True
        data = read_json(path)
        if data.get("commit") != request.commit:
            return False  # Manifest.open would refuse it; say so once, in last_error
        m = Manifest(path, data)
        known = {b["name"] for b in data.get("blocks", [])}
        return any(b.name not in known for b in request.blocks) or \
            m.next_block(request) is not None or any(b.get("status") == "running" for b in data.get("blocks", []))

    def choose(self, queue, requests):
        errors = [f"{i}: {e}" for i, e in requests.items() if isinstance(e, str)]
        good = {i: r for i, r in requests.items() if not isinstance(r, str)}
        chosen = queuefile.pick(queue, lambda i: i in good and self.playable(good[i]))
        return (good[chosen] if chosen else None), errors

    def stop_reason(self, queue=None):
        card_says = self.card.stop_reason()
        if card_says:
            return card_says
        if queue and queue["paused"]:
            return "paused in QUEUE.json" + (f": {queue['pause_reason']}" if queue["pause_reason"] else "")
        return None

    # ---- the loop --------------------------------------------------------------------------

    def serve(self):
        say(f"runner: serving the queue from {self.local or self.repo}, work in {self.work}")
        self.card.clear_stop()  # a Stop now left from before this start must not kill the first block
        while True:
            if self.card.parent_gone():
                self.status.write(state="stopped", request=None, block=None, waiting="the tray app closed", card_in_use=False)
                say("runner: the tray app closed; stopping")
                return
            try:
                did = self.step()
            except RunnerError as e:
                say(f"error: {e}")
                self.status.write(state="error", last_error=str(e), card_in_use=False)
                did = False
            if not did:
                if self.once:
                    return
                self.nap()

    def nap(self):
        """Wait up to idle seconds, waking early when a local pause is lifted or the tray closes."""
        paused = self.pause_file.exists()
        deadline = time.monotonic() + self.idle
        while time.monotonic() < deadline:
            if self.card.parent_gone() or (paused and not self.pause_file.exists()):
                return
            time.sleep(min(2, self.idle))

    def step(self):
        """Play the chosen request until it finishes, pauses or is switched. False when there was nothing to do."""
        queue, requests = self.read()
        why = self.stop_reason(queue)
        request, errors = self.choose(queue, requests)
        if why or request is None:
            self.status.write(state="paused" if why else "idle", request=None, block=None,
                              waiting=why or ("nothing owed" if not errors else "every request in order has a problem"),
                              last_error="; ".join(errors), card_in_use=False)
            return False
        checkout = gitio.worktree(self.repo, self.work, request.commit)
        player = Player(request, self.out(request.id), checkout, self.card, self.runner, self.status)
        self.last_push.setdefault(request.id, time.monotonic())

        def after_block(p):
            if time.monotonic() - self.last_push[request.id] > PUSH_EVERY:
                self.publish(request)
            q, rs = self.read()
            stop = self.stop_reason(q)
            if stop:
                return stop
            nxt, _ = self.choose(q, rs)
            if nxt is not None and nxt.id != request.id:
                say(f"runner: the queue now puts {nxt.id} first; leaving {request.id} after block, to finish later")
                return f"switched to {nxt.id}"
            return None

        result, why = player.play(should_stop=lambda: self.stop_reason(), after_block=after_block)
        self.publish(request)
        if result == "stopped":
            say(f"runner: {request.id} stopped: {why}")
        return result != "stopped" or not self.once

    def publish(self, request):
        self.last_push[request.id] = time.monotonic()
        if not self.push:
            return
        try:
            if gitio.publish(self.repo, self.work, request, self.out(request.id), [self.status.path]):
                say(f"runner: results of {request.id} pushed to runs/{request.id}")
        except RunnerError as e:
            say(f"warning: could not push results of {request.id}: {e}")
            self.status.write(last_error=f"push of {request.id}: {e}")
