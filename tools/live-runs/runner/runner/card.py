"""The RTX 5080 and who is using it: Ollama's resident models, the asset server's queue, the laptop's lock.

Two places run a request (DESIGN.md, Part 1 and "The same script on a laptop"):

- on the RTX 5080 machine itself ("server"): the gateway at http://127.0.0.1:11436, and the card is
  quiet when Ollama holds no other model, the asset server's queue is empty, and nothing else (the
  lead's own ComfyUI) holds much of the card's memory;
- on the lead's laptop ("laptop"): the gateway from tools/ollama-server/.env, the laptop's lock
  folder .scratch/ollama-5080-lock/held taken per block, its pause file and its battery checked
  between blocks.

The key is read by ollama_remote.load_env() into this process and handed to Unity's environment
only. Nothing here prints it.
"""
import ctypes
import importlib.util
import os
import socket
import subprocess
import sys
import time
from pathlib import Path

from .common import REPO, RunnerError, clean_env, now, run

SERVER_URL = "http://127.0.0.1:11436"
WAIT_SECONDS = 60


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, str(path))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main_checkout(start=REPO):
    """The main checkout of the repository this kit is in (a worktree's .scratch is not the shared one)."""
    try:
        common = run(["git", "rev-parse", "--path-format=absolute", "--git-common-dir"], cwd=start).strip()
        return Path(common).parent
    except RunnerError:
        return Path(start)


class NullCard:
    """No model and no lock: for a request whose blocks need no GPU, and for the kit's tests."""
    kind = "none"

    def __init__(self, pause_file=None):
        self.pause_file = Path(pause_file) if pause_file else None
        self.waiting = ""
        self.in_use = False

    def wait_quiet(self, model, tick=None):
        return None

    def take(self, request, block):
        self.in_use = True

    def release(self, model):
        self.in_use = False

    def ollama_info(self, model):
        return {}

    def unity_env(self):
        return clean_env()

    def stop_reason(self):
        if self.pause_file and self.pause_file.exists():
            return f"paused by {self.pause_file}"
        return None


class Card(NullCard):
    def __init__(self, kind, pause_file=None, lock_dir=None, max_other_mb=None, owner=None):
        super().__init__(pause_file)
        self.kind = kind                                  # "server" or "laptop"
        self.lock_dir = Path(lock_dir) if lock_dir else main_checkout() / ".scratch" / "ollama-5080-lock"
        self.max_other_mb = int(max_other_mb or os.environ.get("AGENERELA_RUNNER_MAX_OTHER_MB", "6000"))
        self.owner = owner or f"runq.py on {socket.gethostname()}"
        self.ollama = load_module("ollama_remote", REPO / "tools" / "ollama-server" / "ollama_remote.py")
        if kind == "server":
            os.environ["AGENERELA_OLLAMA_URL"] = SERVER_URL  # a real variable wins over .env
        self.ollama.load_env()
        url = os.environ.get("AGENERELA_OLLAMA_URL", "").strip()
        key = os.environ.get("AGENERELA_OLLAMA_API_KEY", "").strip()
        if not url or not key:
            raise RunnerError("set AGENERELA_OLLAMA_URL and AGENERELA_OLLAMA_API_KEY in tools/ollama-server/.env "
                              "(tools/ollama-server/AGENT_USAGE.md)")
        self.url, self._key = url, key
        self.server = self.ollama.Server(url, key)
        self.comfy = None
        try:
            comfy = load_module("comfy", REPO / "tools" / "comfyui" / "comfy.py")
            comfy.load_env()
            curl, token = os.environ.get("COMFY_URL", "").strip(), os.environ.get("COMFY_API_TOKEN", "").strip()
            if curl and token:
                self.comfy = (comfy, comfy.Client(curl, token))
        except Exception:  # the asset server is optional; without its .env its queue cannot be seen
            self.comfy = None
        if self.comfy is None:
            print("note: COMFY_URL and COMFY_API_TOKEN are not set (tools/comfyui/.env), so the asset "
                  "server's queue is not checked before a block", flush=True)
        self.held = False

    # ---- what is on the card ---------------------------------------------------------------

    def resident(self):
        return self.ollama.resident(self.server)

    def busy_reason(self, model):
        """Why the card is not free for this model now, or None."""
        try:
            others = [m.get("name") for m in self.resident() if not self.ollama.same_model(m.get("name"), model)]
        except self.ollama.Failure as e:
            return f"Ollama: {e}"
        if others:
            return f"another client has {', '.join(others)} loaded"
        if self.comfy:
            comfy, client = self.comfy
            try:
                queue = client.get_json("/queue")
                if queue.get("queue_running") or queue.get("queue_pending"):
                    return "the asset server has jobs queued"
                client.post_json("/free", {"unload_models": True, "free_memory": True})
            except comfy.ComfyError as e:
                return f"the asset server: {e}"
        if self.kind == "server":
            other = self.other_memory_mb()
            if other is not None and other > self.max_other_mb:
                return (f"{other} MB of the card is used by something other than Ollama (the lead's own ComfyUI?), "
                        f"more than AGENERELA_RUNNER_MAX_OTHER_MB={self.max_other_mb}")
        return None

    def other_memory_mb(self):
        """Card memory in use beyond what Ollama reports for its models, from nvidia-smi; None if unknown."""
        try:
            out = subprocess.run(["nvidia-smi", "--query-gpu=memory.used", "--format=csv,noheader,nounits"],
                                 stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, universal_newlines=True, timeout=30)
            used = int(out.stdout.strip().splitlines()[0])
        except (OSError, ValueError, IndexError, subprocess.TimeoutExpired):
            return None
        try:
            ollama = sum(m.get("size_vram", 0) for m in self.resident()) // (1024 * 1024)
        except self.ollama.Failure:
            ollama = 0
        return used - ollama

    def wait_quiet(self, model, tick=None):
        """Block until the card is free for this model, saying why it waits."""
        while True:
            reason = self.busy_reason(model)
            if reason is None:
                self.waiting = ""
                return None
            if self.waiting != reason:
                print(f"waiting: {reason}", flush=True)
            self.waiting = reason
            if tick:
                tick()
            stop = self.stop_reason()
            if stop:
                return stop
            time.sleep(WAIT_SECONDS)

    # ---- per block -------------------------------------------------------------------------

    def take(self, request, block):
        if self.kind == "laptop":
            self.take_lock(f"{request.id} block {block.name}", block.minutes)
        self.in_use = True

    def release(self, model):
        try:
            names = [m.get("name") for m in self.resident()]
            if names:
                self.ollama.unload(self.server, names)
        except self.ollama.Failure as e:
            print(f"warning: could not unload: {e}", flush=True)
        finally:
            self.in_use = False
            if self.held:
                self.release_lock()

    def ollama_info(self, model):
        info = {"model": model}
        try:
            info["version"] = self.server.get("/api/version").get("version")
            for m in self.resident():
                if self.ollama.same_model(m.get("name"), model):
                    info["size_vram"] = m.get("size_vram")
                    info["size"] = m.get("size")
        except self.ollama.Failure as e:
            info["error"] = str(e)
        return info

    def unity_env(self):
        env = clean_env()
        env["AGENERELA_OLLAMA_URL"] = self.url
        env["AGENERELA_OLLAMA_API_KEY"] = self._key
        return env

    def stop_reason(self):
        reason = super().stop_reason()
        if reason:
            return reason
        if self.kind == "laptop":
            if (self.lock_dir / "pause").exists():
                return f"paused by {self.lock_dir / 'pause'}"
            percent, on_battery = battery()
            if on_battery and percent is not None and percent < 25:
                return f"the battery is at {percent}%"
        return None

    # ---- the laptop's lock -----------------------------------------------------------------

    def take_lock(self, what, minutes):
        held = self.lock_dir / "held"
        said = False
        while True:
            try:
                self.lock_dir.mkdir(parents=True, exist_ok=True)
                held.mkdir()
                (held / "owner.txt").write_text(
                    f"owner: {self.owner}\npid: {os.getpid()} (runq.py)\nwhat: {what}\nsince: {now()}\n"
                    f"expect: {minutes} min\n", encoding="utf-8")
                self.held = True
                return
            except FileExistsError:
                if not said:
                    try:
                        who = (held / "owner.txt").read_text(encoding="utf-8").splitlines()[0]
                    except OSError:
                        who = "someone"
                    print(f"waiting: the laptop's lock is held ({who})", flush=True)
                    said = True
                time.sleep(WAIT_SECONDS)

    def release_lock(self):
        held = self.lock_dir / "held"
        try:
            text = (held / "owner.txt").read_text(encoding="utf-8")
        except OSError:
            text = ""
        if f"pid: {os.getpid()} (runq.py)" in text:
            for p in held.iterdir():
                p.unlink()
            held.rmdir()
        self.held = False


def battery():
    """(percent, on_battery) on Windows; (None, False) where it cannot be read."""
    if sys.platform != "win32":
        return None, False

    class Status(ctypes.Structure):
        _fields_ = [("ACLineStatus", ctypes.c_byte), ("BatteryFlag", ctypes.c_byte),
                    ("BatteryLifePercent", ctypes.c_byte), ("SystemStatusFlag", ctypes.c_byte),
                    ("BatteryLifeTime", ctypes.c_ulong), ("BatteryFullLifeTime", ctypes.c_ulong)]

    s = Status()
    if not ctypes.windll.kernel32.GetSystemPowerStatus(ctypes.byref(s)):
        return None, False
    percent = s.BatteryLifePercent & 0xFF
    no_battery = (s.BatteryFlag & 0xFF) in (128, 255)
    return (None if percent == 255 or no_battery else percent), (s.ACLineStatus == 0)
