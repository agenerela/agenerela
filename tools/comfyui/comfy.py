"""Run workflows on the Agenerela ComfyUI server from the command line.

Standard library only, so it runs on any Python 3.10+ with nothing to install.
People, Claude Code and Codex all use it the same way:

    python tools/comfyui/comfy.py check
    python tools/comfyui/comfy.py list
    python tools/comfyui/comfy.py validate
    python tools/comfyui/comfy.py run image --set "prompt=a wooden barrel, ..."
    python tools/comfyui/comfy.py run mesh --set image=tools/comfyui/out/image/x.png
    python tools/comfyui/comfy.py run sfx --set "prompt=..." --set seconds=3
    python tools/comfyui/comfy.py free

Configuration comes from the environment, or from tools/comfyui/.env:

    COMFY_URL        the server's gateway, e.g. http://192.168.1.50:8289
    COMFY_API_TOKEN  the token from the server's .env

The token travels in the Authorization header only, never in a URL, and is
redacted from every error this script prints.
"""

import argparse
import json
import mimetypes
import os
import random
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from pathlib import Path

HERE = Path(__file__).resolve().parent
WORKFLOWS = HERE / "workflows"
DEFAULT_OUT = HERE / "out"
MODEL_SUFFIXES = (".safetensors", ".ckpt", ".pt", ".pth", ".bin", ".gguf")


class ComfyError(Exception):
    pass


def log(message):
    print(message, file=sys.stderr, flush=True)


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


class Client:
    def __init__(self, url, token):
        self.url = url.rstrip("/")
        self.token = token

    def redact(self, text):
        return text.replace(self.token, "<redacted>")

    def request(self, method, path, body=None, headers=None, timeout=60):
        req = urllib.request.Request(self.url + path, data=body, method=method)
        req.add_header("Authorization", "Bearer " + self.token)
        for name, value in (headers or {}).items():
            req.add_header(name, value)
        try:
            with urllib.request.urlopen(req, timeout=timeout) as resp:
                return resp.read()
        except urllib.error.HTTPError as e:
            detail = e.read().decode("utf-8", "replace")
            raise ComfyError(self.redact(explain_status(e.code, path, detail))) from None
        except (urllib.error.URLError, TimeoutError, ConnectionError) as e:
            reason = getattr(e, "reason", e)
            raise ComfyError(self.redact(
                f"cannot reach {self.url} ({reason}). Is the RTX 5080 machine on, is the stack "
                "up there (docker compose ps), and is COMFY_URL right?")) from None

    def get_json(self, path, timeout=60):
        return json.loads(self.request("GET", path, timeout=timeout))

    def post_json(self, path, payload, timeout=60):
        raw = self.request("POST", path, json.dumps(payload).encode("utf-8"),
                           {"Content-Type": "application/json"}, timeout)
        return json.loads(raw) if raw.strip() else None

    def upload_image(self, path):
        """Upload a local file into the server's input folder; returns the name LoadImage takes."""
        boundary = uuid.uuid4().hex
        name = f"{uuid.uuid4().hex[:8]}_{path.name}"
        content_type = mimetypes.guess_type(path.name)[0] or "application/octet-stream"
        head = (
            f"--{boundary}\r\nContent-Disposition: form-data; name=\"type\"\r\n\r\ninput\r\n"
            f"--{boundary}\r\nContent-Disposition: form-data; name=\"image\"; filename=\"{name}\"\r\n"
            f"Content-Type: {content_type}\r\n\r\n"
        ).encode("utf-8")
        body = head + path.read_bytes() + f"\r\n--{boundary}--\r\n".encode("utf-8")
        info = json.loads(self.request("POST", "/upload/image", body,
                                       {"Content-Type": f"multipart/form-data; boundary={boundary}"}, 300))
        return f"{info['subfolder']}/{info['name']}" if info.get("subfolder") else info["name"]

    def download(self, item, out_dir):
        query = urllib.parse.urlencode({
            "filename": item["filename"],
            "subfolder": item.get("subfolder", ""),
            "type": item.get("type", "output"),
        })
        data = self.request("GET", "/view?" + query, timeout=600)
        out_dir.mkdir(parents=True, exist_ok=True)
        target = out_dir / Path(item["filename"]).name  # never trust a server path
        target.write_bytes(data)
        return target.resolve()


def explain_status(code, path, detail):
    if code == 401:
        return "the gateway rejected the token: COMFY_API_TOKEN here must match the one in the server's .env"
    if code == 403:
        return f"{path} is not exposed through the gateway (see Caddyfile)"
    if code == 502:
        return ("the gateway is up but ComfyUI is not answering; it may still be starting. "
                "On the server: docker compose logs comfyui")
    if code == 400 and path == "/prompt":
        return "the server refused the workflow:\n" + describe_prompt_error(detail)
    return f"HTTP {code} from {path}: {detail[:500]}"


def describe_prompt_error(detail):
    try:
        data = json.loads(detail)
    except ValueError:
        return detail[:500]
    lines = []
    error = data.get("error") or {}
    if error:
        lines.append(f"  {error.get('message', '')} {error.get('details', '')}".rstrip())
    for node_id, info in (data.get("node_errors") or {}).items():
        for e in info.get("errors", []):
            lines.append(f"  node {node_id} ({info.get('class_type')}): {e.get('message')} {e.get('details', '')}".rstrip())
    return "\n".join(lines) or detail[:500]


# --- Workflows ---------------------------------------------------------------
#
# A workflow file is ComfyUI's "Export (API)" format wrapped with named params:
#   {"description": ..., "params": {name: {"targets": [[node_id, input]], ...}}, "prompt": {API graph}}
# A param may also set "required", "random" (a fresh seed unless given) or
# "upload" (the value is a local file, uploaded and replaced by its server name).
# A bare API export without the wrapper works too; set its inputs as node_id.input.
#
# Workflows still being tried live in workflows/experimental/. They run by name like the
# others, `list` shows them under their own heading, and `validate` reports on them without
# failing, since their models are an optional download.

EXPERIMENTAL = WORKFLOWS / "experimental"


def load_workflow(name):
    if name.endswith(".json"):
        path = Path(name)
    else:
        path = WORKFLOWS / f"{name}.json"
        if not path.is_file() and "/" not in name:
            path = EXPERIMENTAL / f"{name}.json"
    if not path.is_file():
        raise ComfyError(f"no workflow {name!r}; `list` shows the available ones")
    data = json.loads(path.read_text(encoding="utf-8"))
    if "prompt" not in data:
        data = {"description": "(bare API export)", "params": {}, "prompt": data}
    return path.stem, data


def coerce(raw, current):
    if isinstance(current, bool):
        if raw.lower() in ("1", "true", "yes", "on"):
            return True
        if raw.lower() in ("0", "false", "no", "off"):
            return False
        raise ComfyError(f"expected true or false, got {raw!r}")
    try:
        if isinstance(current, int):
            return int(raw)
        if isinstance(current, float):
            return float(raw)
    except ValueError:
        raise ComfyError(f"expected a number, got {raw!r}") from None
    return raw


def apply_params(client, workflow, assignments):
    """Write --set values into the graph. Returns what was set, for the log and the summary."""
    prompt, params = workflow["prompt"], workflow.get("params", {})
    parsed = []
    for item in assignments:
        if "=" not in item:
            raise ComfyError(f"--set expects name=value, got {item!r}")
        name, raw = item.split("=", 1)
        if name in params:
            spec = params[name]
        elif "." in name:  # node_id.input, for anything not declared as a param
            spec = {"targets": [name.split(".", 1)]}
        else:
            raise ComfyError(f"unknown parameter {name!r}; this workflow takes: {', '.join(params) or 'node_id.input only'}")
        parsed.append((name, raw, spec))

    given = {name for name, _, _ in parsed}
    for name, spec in params.items():
        if spec.get("required") and name not in given:
            raise ComfyError(f"{name} is required: --set {name}=...  ({spec.get('help', '')})")

    applied = {}
    for name, raw, spec in parsed:
        for node_id, input_name in spec["targets"]:
            node = prompt.get(node_id)
            if node is None or input_name not in node["inputs"]:
                raise ComfyError(f"{name}: the workflow has no input {node_id}.{input_name}")
            if isinstance(node["inputs"][input_name], list):
                raise ComfyError(f"{name}: input {node_id}.{input_name} is wired to another node and cannot be set")
        if spec.get("upload"):
            path = Path(raw).expanduser()
            if not path.is_file():
                raise ComfyError(f"{name}: no such file {raw}")
            value = client.upload_image(path)
        else:
            node_id, input_name = spec["targets"][0]
            value = coerce(raw, prompt[node_id]["inputs"][input_name])
        for node_id, input_name in spec["targets"]:
            prompt[node_id]["inputs"][input_name] = value
        applied[name] = value

    for name, spec in params.items():
        if spec.get("random") and name not in applied:
            seed = random.randint(0, 2**50)
            for node_id, input_name in spec["targets"]:
                prompt[node_id]["inputs"][input_name] = seed
            applied[name] = seed
    return applied


def output_items(entry):
    """Every saved file in a finished job's history, whatever node produced it."""
    for outputs in (entry.get("outputs") or {}).values():
        for items in outputs.values():
            if not isinstance(items, list):
                continue
            for item in items:
                if isinstance(item, dict) and "filename" in item and item.get("type") == "output":
                    yield item


def execution_error(entry):
    status = entry.get("status") or {}
    if status.get("status_str") != "error":
        return None
    for kind, data in status.get("messages", []):
        if kind == "execution_error":
            return (f"node {data.get('node_id')} ({data.get('node_type')}) failed: "
                    f"{data.get('exception_type', '')}: {str(data.get('exception_message', '')).strip()}")
        if kind == "execution_interrupted":
            return "the job was interrupted on the server"
    return "the job failed without an error message"


def queue_note(client, prompt_id):
    try:
        queue = client.get_json("/queue")
    except ComfyError:
        return ""
    pending = [job[1] for job in queue.get("queue_pending", [])]
    if prompt_id in pending:
        return f", waiting behind {pending.index(prompt_id) + len(queue.get('queue_running', []))} job(s)"
    return ", running"


def wait_for(client, prompt_id, timeout):
    started = time.monotonic()
    next_note = started + 30
    while True:
        entry = client.get_json(f"/history/{prompt_id}").get(prompt_id)
        if entry:  # ComfyUI writes the history entry once the job has finished or failed
            return entry
        now = time.monotonic()
        if now - started > timeout:
            raise ComfyError(f"gave up after {timeout} s, but the job is still on the server "
                             f"(prompt {prompt_id}); cancel it in the web UI if it is stuck")
        if now >= next_note:
            log(f"  ... {int(now - started)} s{queue_note(client, prompt_id)}")
            next_note = now + 30
        time.sleep(2)


# --- Commands ----------------------------------------------------------------

def cmd_check(client, args):
    stats = client.get_json("/system_stats")
    system = stats.get("system", {})
    print(f"ComfyUI {system.get('comfyui_version', '?')}, Python {str(system.get('python_version', '?')).split()[0]}, "
          f"PyTorch {system.get('pytorch_version', '?')}")
    for device in stats.get("devices", []):
        gib = 2**30
        print(f"  {device.get('name')}: {device.get('vram_free', 0) / gib:.1f} of "
              f"{device.get('vram_total', 0) / gib:.1f} GiB VRAM free")
    return 0


def cmd_list(args):
    def show(path):
        name, workflow = load_workflow(str(path))
        print(f"{name}: {workflow.get('description', '')}")
        for pname, spec in workflow.get("params", {}).items():
            node_id, input_name = spec["targets"][0]
            if spec.get("required"):
                default = "(required)"
            elif spec.get("random"):
                default = "(random)"
            else:
                default = json.dumps(workflow["prompt"][node_id]["inputs"][input_name])
            print(f"    {pname:<18} {default:<14} {spec.get('help', '')}")

    for path in sorted(WORKFLOWS.glob("*.json")):
        show(path)
    experimental = sorted(EXPERIMENTAL.glob("*.json"))
    if experimental:
        print("\nExperimental (run them by name like the others; tools/comfyui/AGENT_USAGE.md says when):")
        for path in experimental:
            show(path)
    return 0


def combo_options(spec):
    """The allowed values of an /object_info input spec, or None if it is not a choice."""
    kind = spec[0] if spec else None
    extra = spec[1] if len(spec) > 1 and isinstance(spec[1], dict) else {}
    if isinstance(kind, list):
        return kind
    if kind == "COMBO":
        return extra.get("options")
    if isinstance(kind, str) and "DYNAMICCOMBO" in kind.upper():
        return [o.get("key") for o in extra.get("options", []) if isinstance(o, dict)]
    return None


def check_workflow(workflow, object_info):
    prompt = workflow["prompt"]
    uploaded = {tuple(t) for spec in workflow.get("params", {}).values() if spec.get("upload") for t in spec["targets"]}
    for node_id, node in prompt.items():
        cls = node.get("class_type")
        info = object_info.get(cls)
        if info is None:
            yield f"node {node_id}: {cls} does not exist on this server"
            continue
        required = info.get("input", {}).get("required", {})
        declared = {**required, **info.get("input", {}).get("optional", {})}
        for input_name, value in node["inputs"].items():
            base = input_name.split(".", 1)[0]  # a dynamic combo's sub-inputs are sent as combo.input
            if base not in declared:
                yield f"node {node_id} ({cls}): no input named {input_name!r}"
            elif isinstance(value, list):
                if str(value[0]) not in prompt:
                    yield f"node {node_id} ({cls}): {input_name} is wired to missing node {value[0]}"
            elif input_name == base and (node_id, input_name) not in uploaded:
                options = combo_options(declared[base])
                if options is not None and value not in options:
                    hint = ("not on the server; is the model downloaded?" if str(value).endswith(MODEL_SUFFIXES)
                            else f"not one of {', '.join(map(str, options[:10]))}")
                    yield f"node {node_id} ({cls}): {input_name}={value!r} {hint}"
        for input_name in required:
            if input_name not in node["inputs"]:
                yield f"node {node_id} ({cls}): required input {input_name!r} is missing"
    for pname, spec in workflow.get("params", {}).items():
        for node_id, input_name in spec["targets"]:
            if input_name not in prompt.get(node_id, {}).get("inputs", {}):
                yield f"param {pname}: the workflow has no input {node_id}.{input_name}"


def cmd_validate(client, args):
    object_info = client.get_json("/object_info", timeout=120)
    if args.workflows:
        names, experimental = args.workflows, []
    else:  # every workflow; the experimental ones are reported but do not fail the check
        names = [p.stem for p in sorted(WORKFLOWS.glob("*.json"))]
        experimental = [f"experimental/{p.stem}" for p in sorted(EXPERIMENTAL.glob("*.json"))]
    failed = 0
    for name in names + experimental:
        wf_name, workflow = load_workflow(name)
        problems = list(check_workflow(workflow, object_info))
        label = f"{wf_name} (experimental)" if name in experimental else wf_name
        print(f"{label}: {'ok' if not problems else f'{len(problems)} problem(s)'}")
        for problem in problems:
            print(f"  {problem}")
        if problems and name in experimental:
            print("  not counted as a failure; an experimental model may simply not be downloaded "
                  "(download_models.py --list names its group)")
        failed += bool(problems) and name not in experimental
    return 1 if failed else 0


def cmd_run(client, args):
    name, workflow = load_workflow(args.workflow)
    applied = apply_params(client, workflow, args.set or [])
    log(f"{name}: " + ", ".join(f"{k}={v}" for k, v in applied.items()))
    started = time.monotonic()
    try:
        queued = client.post_json("/prompt", {"prompt": workflow["prompt"], "client_id": uuid.uuid4().hex})
        prompt_id = queued["prompt_id"]
        log(f"  queued as {prompt_id}")
        entry = wait_for(client, prompt_id, args.timeout)
        error = execution_error(entry)
        if error:
            raise ComfyError(error)
        out_dir = args.out or DEFAULT_OUT / name
        files = [client.download(item, out_dir) for item in output_items(entry)]
    finally:
        if not args.keep_loaded:
            try:  # give the VRAM back, so another ComfyUI on the same GPU is not squeezed
                client.post_json("/free", {"unload_models": True, "free_memory": True})
            except ComfyError:
                pass
    seconds = round(time.monotonic() - started, 1)
    if not files:
        raise ComfyError("the job finished but saved no files")
    if args.json:
        print(json.dumps({"workflow": name, "prompt_id": prompt_id, "params": applied,
                          "seconds": seconds, "files": [str(f) for f in files]}, indent=2))
    else:
        log(f"  done in {seconds} s")
        for f in files:
            print(f)
    return 0


def cmd_free(client, args):
    client.post_json("/free", {"unload_models": True, "free_memory": True})
    print("models unloaded")
    return 0


def main(argv=None):
    for stream in (sys.stdout, sys.stderr):
        stream.reconfigure(errors="replace")
    load_env()
    parser = argparse.ArgumentParser(description="Run workflows on the Agenerela ComfyUI server. See tools/comfyui/README.md.")
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("check", help="confirm the server answers and the token works; show the GPU")
    sub.add_parser("list", help="list the workflows and their parameters (offline)")
    p = sub.add_parser("validate", help="check workflows against the server's nodes and models")
    p.add_argument("workflows", nargs="*", help="names or .json paths; default: all in workflows/")
    p = sub.add_parser("run", help="run a workflow and download what it saves")
    p.add_argument("workflow", help="a name from `list`, or a path to a .json file")
    p.add_argument("--set", action="append", metavar="NAME=VALUE", help="set a parameter; repeatable")
    p.add_argument("--out", type=Path, help=f"download folder (default: {DEFAULT_OUT}/<workflow>)")
    p.add_argument("--keep-loaded", action="store_true",
                   help="leave the models in VRAM afterwards; faster for a batch, but squeezes any other ComfyUI on that GPU")
    p.add_argument("--timeout", type=int, default=1800, help="seconds to wait for the job (default 1800)")
    p.add_argument("--json", action="store_true", help="print a JSON summary instead of bare file paths")
    sub.add_parser("free", help="unload models from the server's VRAM")
    args = parser.parse_args(argv)

    if args.command == "list":
        return cmd_list(args)
    url, token = os.environ.get("COMFY_URL", "").strip(), os.environ.get("COMFY_API_TOKEN", "").strip()
    if not url or not token:
        log("error: set COMFY_URL and COMFY_API_TOKEN in tools/comfyui/.env (copy .env.example)")
        return 2
    client = Client(url, token)
    commands = {"check": cmd_check, "validate": cmd_validate, "run": cmd_run, "free": cmd_free}
    try:
        return commands[args.command](client, args)
    except ComfyError as e:
        log(f"error: {e}")
        return 1


if __name__ == "__main__":
    sys.exit(main())
