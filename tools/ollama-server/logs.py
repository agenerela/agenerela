"""The Ollama server's two logs as one short timeline. Run it on the RTX 5080 machine.

    python tools/ollama-server/logs.py                the last hour
    python tools/ollama-server/logs.py --since 6h     further back (10m, 2h, 1d, or a date Docker takes)
    python tools/ollama-server/logs.py -f             keep printing as things happen
    python tools/ollama-server/logs.py --problems     only what went wrong
    python tools/ollama-server/logs.py --verbose      Ollama's own lines too, without llama.cpp's

One line per event, in this machine's local time:
    request   status, duration, method and path, from the gateway (so refused requests show too)
    load      a model loaded: which, how long it took, its template, free GPU memory before it
    pull      a model download starting, part by part
    start     the server starting: its version and GPU
    problem   any warning or error from Ollama or llama.cpp, a 401, 403 or 5xx

Ollama writes about 25 lines of llama.cpp detail per request, which `docker logs agenerela-ollama`
still shows in full. Standard library only; it calls `docker logs` and changes nothing.
"""
import argparse
import json
import queue
import re
import subprocess
import sys
import threading
from datetime import datetime, timezone

OLLAMA, GATEWAY = "agenerela-ollama", "agenerela-ollama-gateway"
ANSI = re.compile(r"\x1b\[[0-9;]*m")
STAMP = re.compile(r"^(\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d)(\.\d+)?Z ")
GO_LINE = re.compile(r'^time=\S+ level=(?P<level>[A-Z]+) source=\S+ msg="(?P<msg>(?:[^"\\]|\\.)*)"(?P<rest>.*)$')
FIELD = re.compile(r'(\w+)=("(?:[^"\\]|\\.)*"|\S+)')
GIN = re.compile(r'^\[GIN\] [\d/]+ - [\d:]+ \| (?P<status>\d+) \| +(?P<took>\S+) \| +(?P<ip>\S+) \| (?P<method>\S+) +"(?P<path>[^"]*)"')
CADDY = re.compile(r"^\S+ \S+\t(?P<level>[A-Z]+)\t\S+\thandled request\t(?P<json>\{.*\})\s*$")
TROUBLE = re.compile(r"error|fail|out of memory|cudaMalloc|abort|panic", re.I)
# Requests the health check makes from inside the container, every few minutes.
HEALTH = {("HEAD", "/"), ("GET", "/api/tags")}


def fields(rest):
    return {k: v.strip('"') for k, v in FIELD.findall(rest)}


GO_UNITS = {"ns": 1e-9, "us": 1e-6, "µs": 1e-6, "μs": 1e-6, "ms": 1e-3, "s": 1.0, "m": 60.0, "h": 3600.0}


def go_seconds(text):
    """A Go duration as Caddy and Ollama print it ("957.664µs", "1.9s", "1m2.5s"), in seconds."""
    parts = re.findall(r"([\d.]+)(ns|us|µs|μs|ms|s|m|h)", text or "")
    if not parts or "".join(n + u for n, u in parts) != text:
        return None
    return sum(float(n) * GO_UNITS[u] for n, u in parts)


def short_model(name):
    return re.sub(r"^registry\.ollama\.ai/library/", "", name or "?")


class Reader:
    """Turns raw lines into events, remembering what a model load has said so far."""

    def __init__(self, verbose, problems):
        self.verbose, self.problems = verbose, problems
        self.load = None

    def ollama(self, text):
        go = GO_LINE.match(text)
        if go:
            return self.go_line(go.group("level"), go.group("msg"), fields(go.group("rest")))
        gin = GIN.match(text)
        if gin:
            # The gateway logs every outside request already; only requests from inside the
            # container (docker exec ollama ...) are new here, minus the health check.
            if gin["ip"] in ("127.0.0.1", "::1") and (gin["method"], gin["path"]) not in HEALTH:
                return self.request(int(gin["status"]), gin["took"], gin["method"], gin["path"], "inside")
            return None
        if TROUBLE.search(text) and not text.lstrip().startswith(("print_info", "llama_model_loader")):
            return ("problem", "llama.cpp: " + text.strip())
        return None

    def go_line(self, level, msg, f):
        if level in ("WARN", "ERROR"):
            extra = " ".join(f"{k}={v}" for k, v in f.items() if k not in ("cmd", "env"))
            return ("problem", f"Ollama {level.lower()}: {msg} {extra}".strip())
        if msg == "loading model via llama-server":
            self.load = {"gpu": self.load and self.load.get("gpu")}
            return None
        if msg == "gpu memory":
            self.load = {"gpu": f.get("free")}
            return None
        started = re.match(r"llama-server started in ([\d.]+) seconds", msg)
        if started and self.load is not None and "took" not in self.load:
            self.load["took"] = float(started.group(1))
            return None
        if msg == "template selection" and self.load is not None:
            load, self.load = self.load, None
            took = f"{load['took']:.1f} s" if "took" in load else "?"
            gpu = f", {load['gpu']} GPU free before" if load.get("gpu") else ""
            return ("load", f"{short_model(f.get('model'))} in {took}, template {f.get('selected', '?')}{gpu}")
        if msg.startswith("Listening on"):
            version = re.search(r"version ([\w.\-]+)", msg)
            return ("start", f"Ollama {version.group(1) if version else '?'} listening")
        if msg == "inference compute":
            return ("start", f"GPU {f.get('description', '?')}, {f.get('total', '?')} total, {f.get('available', '?')} available")
        if msg.startswith("downloading "):
            return ("pull", msg.replace("downloading ", "part ", 1))
        if self.verbose and msg not in ("waiting for llama-server to start responding",
                                        "waiting for llama-server to become available", "server config"):
            extra = " ".join(f"{k}={v}" for k, v in f.items() if k not in ("cmd", "env", "model"))
            return ("ollama", f"{msg} {extra}".strip())
        return None

    def gateway(self, text):
        match = CADDY.match(text)
        if not match:
            if "error" in text.lower() and "handled request" not in text:
                return ("problem", "gateway: " + text.strip())
            return None
        entry = json.loads(match["json"])
        request = entry.get("request", {})
        # The address the client called: the LAN address for another machine, 127.0.0.1 here.
        host = request.get("host", "").rsplit(":", 1)[0]
        note = "" if host in ("", "127.0.0.1", "localhost", "[::1]") else "via " + host
        return self.request(entry.get("status", 0), entry.get("duration", "?"), request.get("method", "?"),
                            request.get("uri", "?"), note)

    def request(self, status, took, method, path, note):
        seconds = took if isinstance(took, (int, float)) else go_seconds(took)
        if seconds is not None:
            took = f"{seconds * 1000:.0f} ms" if seconds < 1 else f"{seconds:.1f} s"
        why = {401: "  wrong or missing key", 403: "  not exposed through the gateway",
               502: "  Ollama not answering"}.get(status, "")
        line = f"{status}  {took:>9}  {method:<5} {path}{why}{'  (' + note + ')' if note else ''}"
        if status in (401, 403) or status >= 500:
            return ("problem", line)
        return ("request", line)


def parse_stamp(line):
    match = STAMP.match(line)
    if not match:
        return None, line
    moment = datetime.strptime(match.group(1), "%Y-%m-%dT%H:%M:%S").replace(tzinfo=timezone.utc)
    fraction = float(match.group(2) or 0)
    return moment.timestamp() + fraction, line[match.end():]


def docker_logs(container, since, follow):
    command = ["docker", "logs", "--timestamps", "--since", since] + (["--follow"] if follow else []) + [container]
    try:
        return subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                text=True, encoding="utf-8", errors="replace")
    except FileNotFoundError:
        sys.exit("error: docker was not found. Run this on the RTX 5080 machine, where the stack runs.")


def show(reader, container, stamp, text, problems_only):
    text = ANSI.sub("", text)
    event = reader.ollama(text) if container == OLLAMA else reader.gateway(text)
    if not event or (problems_only and event[0] != "problem"):
        return
    when = datetime.fromtimestamp(stamp).strftime("%m-%d %H:%M:%S") if stamp else "?"
    print(f"{when}  {event[0]:<8} {event[1]}", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__.strip().splitlines()[0])
    parser.add_argument("--since", default="1h", help="how far back, as docker logs takes it (default 1h)")
    parser.add_argument("-f", "--follow", action="store_true", help="keep printing new events")
    parser.add_argument("--problems", action="store_true", help="only warnings, errors and refused requests")
    parser.add_argument("--verbose", action="store_true", help="Ollama's other lines too, not llama.cpp's")
    args = parser.parse_args()
    reader = Reader(args.verbose, args.problems)

    if not args.follow:
        lines = []
        for container in (OLLAMA, GATEWAY):
            process = docker_logs(container, args.since, False)
            out, _ = process.communicate()
            if process.returncode:
                sys.exit(f"error: docker logs {container} failed: {out.strip()[:300]}")
            for raw in out.splitlines():
                stamp, text = parse_stamp(raw)
                lines.append((stamp or 0, container, text))
        # Each container's lines keep their order; the two interleave by time.
        for stamp, container, text in sorted(lines, key=lambda line: line[0]):
            show(reader, container, stamp, text, args.problems)
        return

    events = queue.Queue()

    def pump(container):
        process = docker_logs(container, args.since, True)
        for raw in process.stdout:
            stamp, text = parse_stamp(raw.rstrip("\n"))
            events.put((stamp, container, text))
        events.put((None, container, None))

    for container in (OLLAMA, GATEWAY):
        threading.Thread(target=pump, args=(container,), daemon=True).start()
    ended = 0
    try:
        while ended < 2:
            stamp, container, text = events.get()
            if text is None:
                ended += 1
                continue
            show(reader, container, stamp, text, args.problems)
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
