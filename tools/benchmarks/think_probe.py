"""Native thinking probe (24 November 2026): what think:true does to the framework's request.

    python tools/benchmarks/think_probe.py qwen3.5:4b <out.jsonl>

Sends OllamaProvider's evaluation-set request (tools/live-runs/model-sweep/framework_request.json)
with our own stimuli (none from the evaluation sets, hard rule 4) under five arms:
  off          think false (today's request)
  on           think true
  on-cap256    think true, num_predict 256
  on-low       think "low" (a level; Ollama documents levels for gpt-oss only)
  on-nolp      think true, no logprobs
Untimed: no lease, numbers are indicative only. Reads the server from tools/ollama-server/.env
through ollama_remote.py and never prints the key.
"""
import importlib.util
import json
import os
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
_REMOTE = Path(os.environ.get("AGENERELA_OLLAMA_REMOTE") or ROOT / "tools/ollama-server/ollama_remote.py")
_spec = importlib.util.spec_from_file_location("ollama_remote", _REMOTE)
ollama_remote = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ollama_remote)

STIMULI = [
    "The chimney smoke looks wrong from here. Climb up the north side lookout and watch.",
    "Put an arrow in that grey beast before it gets near anyone.",
    "I lost a silver ring somewhere by the old lake.",
    "Your boots are muddy. Did you sleep outside again?",
    "Walk with me, I'm heading out and want a second pair of eyes.",
    "Lightning just split the oak! Get yourself indoors where it's safe.",
]

ARMS = [
    ("off", {"think": False}, {}),
    ("on", {"think": True}, {}),
    ("on-cap256", {"think": True}, {"num_predict": 256}),
    ("on-low", {"think": "low"}, {}),
    ("on-nolp", {"think": True, "_nolp": True}, {}),
]


def main(argv):
    model, out_path = argv[1], argv[2]
    ollama_remote.load_env()
    server = ollama_remote.Server(os.environ["AGENERELA_OLLAMA_URL"], os.environ["AGENERELA_OLLAMA_API_KEY"])
    version = server.get("/api/version").get("version")
    base = json.load(open(ROOT / "tools/live-runs/model-sweep/framework_request.json", encoding="utf-8"))["request"]
    print(f"Ollama {version}, {model}", flush=True)
    with open(out_path, "w", encoding="utf-8") as out:
        for stimulus in STIMULI:
            for arm, top, options in ARMS:
                body = json.loads(json.dumps(base))
                body["model"] = model
                body["messages"].append({"role": "user", "content": stimulus})
                body["think"] = top["think"]
                body["options"].update(options)
                if top.get("_nolp"):
                    body.pop("logprobs", None)
                    body.pop("top_logprobs", None)
                row = {"ollama": version, "model": model, "arm": arm, "stimulus": stimulus}
                started = time.time()
                try:
                    reply = server.post("/api/chat", body, timeout=600)
                except Exception as e:  # noqa: BLE001 - a probe records every failure
                    row["error"] = str(e)[:300]
                    print(arm, "ERROR", row["error"], flush=True)
                    out.write(json.dumps(row) + "\n")
                    continue
                row["wall"] = round(time.time() - started, 2)
                msg = reply.get("message", {})
                content = msg.get("content", "")
                thinking = msg.get("thinking")
                row["content"] = content
                row["thinking_chars"] = len(thinking) if thinking else 0
                row["thinking_head"] = (thinking or "")[:300]
                row["think_tag_in_content"] = "<think>" in content or "</think>" in content
                try:
                    parsed = json.loads(content)
                    row["valid_json"] = True
                    row["keys"] = list(parsed.keys())
                    row["action"] = parsed.get("action")
                    row["target"] = parsed.get("target")
                except ValueError:
                    row["valid_json"] = False
                for k in ("eval_count", "prompt_eval_count", "done_reason"):
                    row[k] = reply.get(k)
                for k in ("total_duration", "eval_duration", "prompt_eval_duration", "load_duration"):
                    if reply.get(k) is not None:
                        row[k + "_s"] = round(reply[k] / 1e9, 2)
                lp = reply.get("logprobs") or []
                row["logprob_tokens"] = len(lp)
                joined = "".join(e.get("token", "") for e in lp)
                row["logprob_text_head"] = joined[:120]
                row["logprob_text_equals_content"] = joined == content
                row["logprob_text_starts_with_thinking"] = bool(thinking) and joined.startswith(thinking[:40])
                print(f"{arm:10} {row['wall']:6}s eval={row['eval_count']} think={row['thinking_chars']}ch "
                      f"lp={row['logprob_tokens']} lp==content={row['logprob_text_equals_content']} "
                      f"json={row['valid_json']} {row.get('action')}/{row.get('target')} {row.get('done_reason')}",
                      flush=True)
                out.write(json.dumps(row) + "\n")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
