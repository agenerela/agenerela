"""Pre-checks for one model before its sweep run (tools/live-runs/model-sweep/README.md).

Sends the framework's own /api/chat request (framework_request.json: schema in the order action,
target, statement, think false, temperature 0, num_ctx 4096, logprobs) with a few stimuli and
records: the reply's key order, any thinking that came back, eval_count, timing, and size and
size_vram from /api/ps. Leaves the model loaded; the caller unloads it.

Usage: python probe.py <model> <out.json> [requests]   (requests: 1 to 6, default 6)
The server is OLLAMA_URL, http://127.0.0.1:11434 when unset. Never use localhost on Windows.
A server behind tools/ollama-server/'s gateway also needs AGENERELA_OLLAMA_API_KEY, sent as a
Bearer header and never printed.
Standard library only.
"""
import json
import os
import sys
import time
import urllib.error
import urllib.request

BASE = os.environ.get("OLLAMA_URL", "http://127.0.0.1:11434").rstrip("/")
KEY = os.environ.get("AGENERELA_OLLAMA_API_KEY", "").strip()
AUTH = {"Authorization": "Bearer " + KEY} if KEY else {}
HERE = os.path.dirname(os.path.abspath(__file__))
STIMULI = ["Meet me over at the bridge.", "Drive off that wolf!", "Good evening.",
           "Walk with me a while.", "Bring me the king's crown.", "Go and wait by the well."]


def post(path, body, timeout=300):
    req = urllib.request.Request(BASE + path, data=json.dumps(body).encode(), headers={"Content-Type": "application/json", **AUTH})
    return json.load(urllib.request.urlopen(req, timeout=timeout))


def get(path):
    return json.load(urllib.request.urlopen(urllib.request.Request(BASE + path, headers=AUTH), timeout=10))


def main():
    model, out = sys.argv[1], sys.argv[2]
    stimuli = STIMULI[:int(sys.argv[3])] if len(sys.argv) > 3 else STIMULI
    template = json.load(open(os.path.join(HERE, "framework_request.json"), encoding="utf-8"))["request"]
    assert template["think"] is False
    replies = []
    for s in stimuli:
        body = dict(template)
        body["model"] = model
        body["messages"] = [template["messages"][0], {"role": "user", "content": f'Player: "{s}"'}]
        t = time.time()
        try:
            r = post("/api/chat", body)
        except urllib.error.HTTPError as e:
            replies.append({"stimulus": s, "error": f"HTTP {e.code}: {e.read().decode(errors='replace')[:300]}"})
            print(model, "|", s, "| ERROR", replies[-1]["error"])
            continue
        content = r["message"].get("content", "")
        thinking = r["message"].get("thinking") or ""
        try:
            keys = list(json.loads(content).keys())
        except ValueError:
            keys = None
        rec = {"stimulus": s, "keys": keys, "in_order": keys == ["action", "target", "statement"], "content": content,
               "thinking_chars": len(thinking), "think_tag": "<think" in content, "eval_count": r.get("eval_count"),
               "total_ms": round(r.get("total_duration", 0) / 1e6), "wall_s": round(time.time() - t, 2)}
        replies.append(rec)
        print(model, "|", s, "|", keys, "| thinking", rec["thinking_chars"], "| eval", rec["eval_count"], "|", rec["wall_s"], "s")
    resident = [{"name": m["name"], "size_mb": m["size"] // 1048576, "size_vram_mb": m["size_vram"] // 1048576,
                 "context_length": m.get("context_length")} for m in get("/api/ps")["models"]]
    ok = [x for x in replies if "error" not in x]
    summary = {"model": model, "server": BASE, "version": get("/api/version")["version"],
               "in_order": sum(x["in_order"] for x in ok), "requests": len(stimuli), "errors": len(replies) - len(ok),
               "thinking_replies": sum(1 for x in ok if x["thinking_chars"] or x["think_tag"]),
               "eval_counts": [x["eval_count"] for x in ok], "resident": resident, "replies": replies}
    json.dump(summary, open(out, "w", encoding="utf-8"), indent=1)
    print(f"SUMMARY {model}: in order {summary['in_order']}/{len(stimuli)}, errors {summary['errors']}, "
          f"thinking {summary['thinking_replies']}, resident {resident}")


if __name__ == "__main__":
    main()
