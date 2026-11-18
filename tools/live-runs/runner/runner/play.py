"""Playing a request's blocks, one at a time, resumably (DESIGN.md, Part 1 "How it runs a block", Part 2).

For each block: wait for a quiet card, take it, start the block, kill it at twice its estimate,
record Ollama's version and size_vram, unload the model, and mark the block done or invalid.
Between blocks: stop if paused (a pause never cuts a block), and let the caller switch requests.
"""
import time
from pathlib import Path

from . import process
from .common import RunnerError, clean_env, now, python, run, say
from .request import split
from .manifest import Manifest, set_aside

TIMEOUT_FACTOR = 2


class Player:
    def __init__(self, request, out, checkout, card, runner, status=None, poll=1.0):
        self.request = request
        self.out = Path(out)
        self.checkout = Path(checkout)
        self.card = card
        self.runner = runner
        self.status = status
        self.poll = poll
        self.out.mkdir(parents=True, exist_ok=True)
        self.manifest = Manifest.open(self.out / "manifest.json", request, runner)

    # ---- the request -----------------------------------------------------------------------

    def play(self, should_stop=None, after_block=None):
        """Play blocks until the request is finished, a pause, or after_block() returns a reason to stop.

        Returns ("finished", None), ("stopped", why) or ("left", why).
        """
        m, r = self.manifest, self.request
        found = m.recover(self.out, process.alive, process.kill)
        if found:
            say(f"{r.id}: {', '.join(found)} did not finish last time; set aside and planned again")
        sliced = m.slice_done(r) if r.smallest_slice else True
        while True:
            why = self.card.stop_reason() or (should_stop() if should_stop else None)
            if why:
                self.write_status(state="paused", block=None, waiting=why)
                return "stopped", why
            block = m.next_block(r)
            if block is None:
                return self.finish()
            outcome = self.play_block(block)
            if outcome == "stopped":
                why = self.card.stop_reason() or (should_stop() if should_stop else None) or "stopped"
                self.write_status(state="paused", block=None, waiting=why)
                return "stopped", why
            if not sliced and m.slice_done(r):
                sliced = True
                say(f"{r.id}: smallest slice ({', '.join(r.smallest_slice)}) played: "
                    + ", ".join(f"{n} {m.status(n)}" for n in r.smallest_slice))
                self.write_status(slice_done=True)
            if after_block:
                why = after_block(self)
                if why:
                    return "left", why

    def finish(self):
        m, r = self.manifest, self.request
        if not m.data.get("finished"):
            self.run_reports()
            m.data["finished"] = now()
            m.save()
        counts = m.counts()
        say(f"{r.id}: finished, {counts['done']} of {len(r.blocks)} blocks done"
            + (f", {counts['invalid']} invalid (left for a person)" if counts["invalid"] else ""))
        self.write_status(state="finished", block=None)
        return "finished", None

    def run_reports(self):
        for i, command in enumerate(self.request.report, 1):
            line = command.format(out=str(self.out), checkout=str(self.checkout), python=python())
            target = self.out / f"report-{i}.txt"
            try:
                text = run(split(line), cwd=self.checkout, check=False)
            except RunnerError as e:
                text = f"report failed: {e}\n"
            target.write_text(f"$ {command}\n\n{text}", encoding="utf-8")

    # ---- one block -------------------------------------------------------------------------

    def play_block(self, block):
        m, r, card = self.manifest, self.request, self.card
        say(f"{r.id}: block {block.name} (try {m.entry(block.name).get('tries', 0) + 1}, "
            f"estimate {block.minutes} min)")
        if r.model:
            self.write_status(state="waiting", block=block.name)
            why = card.wait_quiet(r.model, tick=lambda: self.write_status(state="waiting", block=block.name))
            if why:
                return "stopped"
        set_aside(self.out, block.name)  # a leftover from an earlier invalid try
        log = self.out / f"{block.name}.log"
        if r.command:
            args = r.command_for(block, self.out, self.checkout, python())
            stdout, env = log, clean_env()
        else:
            exe = process.unity_exe(self.checkout / r.project)
            args = r.command_for(block, self.out, self.checkout, python(), unity_exe=exe, log_file=log)
            stdout, env = None, None
        card.take(r, block)
        clock = time.monotonic()
        proc = None
        try:
            m.start(block.name)
            if env is None:
                env = card.unity_env()
            proc = process.start(args, env, self.checkout, stdout)
            env = None  # the key is only in the child now
            m.set_pid(block.name, proc.pid, None)  # at once, so a crash from here on can still find it
            m.set_pid(block.name, proc.pid, process.image(proc.pid))
            self.write_status(state="running", block=block.name, started=now())
            limit = TIMEOUT_FACTOR * 60 * float(block.minutes)
            kind, code = process.watch(proc, limit, poll=self.poll)
            if r.model:
                info = card.ollama_info(r.model)
                if info.get("size_vram"):
                    m.data["ollama"] = info
            card.release(r.model)
            status = self.judge(block, kind, code, log)
            m.finish(block.name, status, clock)
            say(f"{r.id}: block {block.name} {status} after {m.entry(block.name).get('minutes')} min")
            self.write_status(state="between blocks", block=None,
                              last_error="" if status == "done" else f"{block.name}: {status}")
            return status
        except BaseException:
            if proc is not None and proc.poll() is None:
                process.kill(proc.pid)
            if card.in_use:
                card.release(r.model)
            set_aside(self.out, block.name)
            m.back_to_planned(block.name, "the runner was stopped mid-block")
            raise

    def judge(self, block, kind, code, log):
        r = self.request
        if kind == "timeout":
            return "invalid: timeout"
        if code != 0:
            return f"invalid: exit {code}"
        if process.log_mentions(log, r.invalid_if_log):
            return "invalid: provider"
        if not r.done_path(block, self.out).exists():
            return f"invalid: no {r.done_when.format(block=block.name, name=block.name)}"
        return "done"

    def write_status(self, **fields):
        if self.status:
            self.status.progress(self.request, self.manifest, self.card, **fields)
