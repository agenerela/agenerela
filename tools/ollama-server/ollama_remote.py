"""Talk to the Agenerela Ollama server through its gateway (tools/ollama-server/README.md).

    python tools/ollama-server/ollama_remote.py check           version, models pulled, models resident
    python tools/ollama-server/ollama_remote.py ps              models resident now, and how much is on the GPU
    python tools/ollama-server/ollama_remote.py unload <model>  unload one model and wait until it is gone
    python tools/ollama-server/ollama_remote.py unload --all    unload every resident model
    python tools/ollama-server/ollama_remote.py warm <model>    load a model now and say how long it took
    python tools/ollama-server/ollama_remote.py pull <model>    have the server download a model
    python tools/ollama-server/ollama_remote.py systemone <model>  ask a decision model one question
                                                    on /v1/systemone, to check the route works

Configuration comes from the environment, or from tools/ollama-server/.env:
    AGENERELA_OLLAMA_URL      the gateway, such as http://192.168.1.50:11436
    AGENERELA_OLLAMA_API_KEY  the key from the server's .env

The key goes in the Authorization header only. This script never prints it.
Standard library only, Python 3.8 or newer.
"""
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent


def load_env():
    """Fill os.environ from .env next to this script, without overriding real variables."""
    path = HERE / ".env"
    if not path.is_file():
        return
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, value = line.split("=", 1)
        os.environ.setdefault(key.strip(), value.strip().strip("\"'"))


def pinned_version():
    """The Ollama version docker-compose.yml pins, so check can say when the server differs."""
    try:
        text = (HERE / "docker-compose.yml").read_text(encoding="utf-8")
    except OSError:
        return None
    match = re.search(r"image:\s*ollama/ollama:(\S+)", text)
    return match.group(1) if match else None


class Failure(Exception):
    pass


class Server:
    def __init__(self, url, key):
        self.url = url.rstrip("/")
        self.key = key

    def _open(self, path, body=None, timeout=30):
        data = None if body is None else json.dumps(body).encode()
        request = urllib.request.Request(self.url + path, data=data, method="GET" if body is None else "POST")
        request.add_header("Authorization", "Bearer " + self.key)
        if data is not None:
            request.add_header("Content-Type", "application/json")
        try:
            return urllib.request.urlopen(request, timeout=timeout)
        except urllib.error.HTTPError as e:
            raise Failure(self._explain(e.code, e.read().decode("utf-8", "replace"), path))
        except (urllib.error.URLError, OSError) as e:
            reason = getattr(e, "reason", e)
            raise Failure(f"cannot reach {self.url}: {reason}. Is the RTX 5080 machine on, the stack up "
                          f"and, away from home, the VPN connected?")

    def _explain(self, status, text, path):
        if status == 401:
            return ("the gateway rejected the key: AGENERELA_OLLAMA_API_KEY here must match the one in "
                    "the server's tools/ollama-server/.env")
        if status == 403:
            return f"{path} is not exposed through the gateway"
        if status in (502, 503, 504):
            return "the gateway is up but Ollama is not answering: it may still be starting. Try again in a minute"
        try:
            said = json.loads(text).get("error", text)
        except ValueError:
            said = text
        return f"HTTP {status} from {path}: {said.strip()[:300]}"

    def get(self, path):
        with self._open(path) as response:
            return json.load(response)

    def post(self, path, body, timeout=300):
        with self._open(path, body, timeout) as response:
            return json.load(response)

    def stream(self, path, body):
        with self._open(path, body, timeout=3600) as response:
            for line in response:
                line = line.strip()
                if line:
                    yield json.loads(line)


def gigabytes(n):
    return f"{n / 1e9:.1f} GB"


def resident(server):
    return server.get("/api/ps").get("models") or []


def describe(model):
    size, vram = model.get("size", 0), model.get("size_vram", 0)
    share = f"{100 * vram / size:.0f}% on the GPU" if size else "size unknown"
    return f"{model.get('name')}  {gigabytes(size)}, {share}"


def check(server):
    version = server.get("/api/version").get("version")
    pinned = pinned_version()
    print(f"server    {server.url}")
    note = "" if not pinned or version == pinned else f"   (docker-compose.yml pins {pinned}: results are not comparable)"
    print(f"ollama    {version}{note}")
    models = server.get("/api/tags").get("models") or []
    names = ", ".join(sorted(m.get("name", "?") for m in models)) or "none"
    print(f"pulled    {len(models)}: {names}")
    loaded = resident(server)
    print("resident  " + ("none" if not loaded else "; ".join(describe(m) for m in loaded)))


def ps(server):
    loaded = resident(server)
    if not loaded:
        print("none")
    for model in loaded:
        print(describe(model))


def unload(server, names):
    for name in names:
        try:
            server.post("/api/generate", {"model": name, "keep_alive": 0}, timeout=60)
        except Failure as e:
            # A decision model with a trained head (d1, strands-decider) has no generate; it is
            # unloaded on its own route, which the gateway passes once the Caddyfile allows it.
            if "does not support generate" not in str(e):
                raise
            server.post("/v1/systemone", {"model": name, "keep_alive": 0, "state": "unload",
                                          "questions": {"q": {"type": "noul", "instructions": "Unload."}}},
                        timeout=60)
    deadline = time.time() + 30
    while True:
        left = [m.get("name") for m in resident(server) if any(same_model(m.get("name"), n) for n in names)]
        if not left:
            print("unloaded " + ", ".join(names))
            return
        if time.time() > deadline:
            raise Failure("still resident after 30 s: " + ", ".join(left))
        time.sleep(1)


def same_model(a, b):
    def tagged(n):
        n = (n or "").strip()
        return n if ":" in n else n + ":latest"
    return tagged(a).lower() == tagged(b).lower()


def warm(server, name):
    # The context size every framework decision sends (OllamaSettings.ContextSize): Ollama reloads a
    # model asked for another size, so warming at the server's default would not help.
    started = time.time()
    server.post("/api/generate", {"model": name, "options": {"num_ctx": 4096}}, timeout=600)
    print(f"{name} loaded in {time.time() - started:.1f} s")


def systemone(server, name):
    """One choice question on /v1/systemone, the route decision models answer on (Ollama 0.35 and later)."""
    body = {"model": name, "state": 'Player: "Meet me at the bridge."',
            "questions": {"action": {"type": "choice",
                                     "instructions": "Which action should the guard take now?",
                                     "criteria": {"move_to": "Walk to a place.",
                                                  "follow_player": "Walk behind the player wherever they go.",
                                                  "none": "Take no action."}}}}
    started = time.time()
    reply = server.post("/v1/systemone", body, timeout=600)
    answer = (reply.get("answers") or {}).get("action") or {}
    print(f"{name}: choice {answer.get('choice')}, probabilities {answer.get('probabilities')}, "
          f"{time.time() - started:.2f} s (the first call includes loading)")


def pull(server, name):
    last = None
    for event in server.stream("/api/pull", {"model": name, "stream": True}):
        if "error" in event:
            raise Failure(f"pull failed: {event['error']}")
        status = event.get("status", "")
        total, done = event.get("total"), event.get("completed")
        if total and done is not None:
            step = int(10 * done / total)
            line = f"{status}  {10 * step}%"
        else:
            line = status
        if line != last:
            print(line, flush=True)
            last = line


def main(argv):
    load_env()
    if len(argv) < 2 or argv[1] in ("-h", "--help", "help"):
        print(__doc__.strip())
        return 0
    url = os.environ.get("AGENERELA_OLLAMA_URL", "").strip()
    key = os.environ.get("AGENERELA_OLLAMA_API_KEY", "").strip()
    if not url or not key:
        print("error: set AGENERELA_OLLAMA_URL and AGENERELA_OLLAMA_API_KEY in tools/ollama-server/.env "
              "(copy .env.example)", file=sys.stderr)
        return 2
    server = Server(url, key)
    command, rest = argv[1], argv[2:]
    try:
        if command == "check":
            check(server)
        elif command == "ps":
            ps(server)
        elif command == "unload" and rest == ["--all"]:
            names = [m.get("name") for m in resident(server)]
            if names:
                unload(server, names)
            else:
                print("nothing resident")
        elif command == "unload" and len(rest) == 1:
            unload(server, rest)
        elif command == "warm" and len(rest) == 1:
            warm(server, rest[0])
        elif command == "pull" and len(rest) == 1:
            pull(server, rest[0])
        elif command == "systemone" and len(rest) == 1:
            systemone(server, rest[0])
        else:
            print(__doc__.strip(), file=sys.stderr)
            return 2
    except Failure as e:
        print(f"error: {e}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
