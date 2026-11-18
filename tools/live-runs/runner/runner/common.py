"""Small helpers every module shares: the repository's paths, the time, atomic JSON files."""
import datetime
import json
import os
import subprocess
import sys
import tempfile
import time
from pathlib import Path

KIT = Path(__file__).resolve().parent.parent          # tools/live-runs/runner
REPO = KIT.parent.parent.parent                       # the checkout this kit runs from
REQUESTS = Path("docs") / "run-requests"              # relative to a checkout
QUEUE_FILE = REQUESTS / "QUEUE.json"

# Variables a child process other than Unity never receives, so a report script or git cannot
# leak them into a log.
SECRET_VARIABLES = ("AGENERELA_OLLAMA_API_KEY", "COMFY_API_TOKEN")


class RunnerError(Exception):
    """A failure the runner reports in one line and stops on."""


def now():
    """Local time to the second, ISO 8601 with its offset, as every manifest and status file writes it."""
    return datetime.datetime.now().astimezone().replace(microsecond=0).isoformat()


def parse_time(text):
    return datetime.datetime.fromisoformat(text) if text else None


def say(message):
    print(message, flush=True)


def write_json(path, data):
    """Write JSON through a temporary file and a rename, so a crash never leaves half a file."""
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, temp = tempfile.mkstemp(prefix=path.name + ".", suffix=".tmp", dir=str(path.parent))
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
            f.write("\n")
        for attempt in range(50):  # on Windows a reader holding the file open blocks the rename for a moment
            try:
                os.replace(temp, str(path))
                break
            except PermissionError:
                if attempt == 49:
                    raise
                time.sleep(0.1)
    except BaseException:
        try:
            os.unlink(temp)
        except OSError:
            pass
        raise


def read_json(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def clean_env(extra=None):
    """The environment for a child that is not Unity: this one without the keys."""
    env = {k: v for k, v in os.environ.items() if k not in SECRET_VARIABLES}
    env.update(extra or {})
    return env


def run(args, cwd=None, check=True, capture=True, env=None, input_text=None):
    """Run a command with the keys stripped from its environment; return its stdout."""
    done = subprocess.run([str(a) for a in args], cwd=None if cwd is None else str(cwd),
                          env=env if env is not None else clean_env(), input=input_text,
                          stdout=subprocess.PIPE if capture else None,
                          stderr=subprocess.PIPE if capture else None,
                          universal_newlines=True, encoding="utf-8", errors="replace")
    if check and done.returncode != 0:
        detail = (done.stderr or done.stdout or "").strip().splitlines()
        raise RunnerError(f"{' '.join(str(a) for a in args[:3])} ... failed ({done.returncode}): "
                          f"{detail[-1] if detail else 'no output'}")
    return done.stdout if capture else ""


def python():
    return sys.executable or "python"
