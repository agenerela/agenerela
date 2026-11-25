"""Second thinking probe (24 November 2026): does qwen3.5:4b's thinking end on its own?

    python tools/benchmarks/think_end_probe.py qwen3.5:4b <out.jsonl>

Two of think_probe.py's own stimuli, think true, full thinking text kept:
  fmt-16k    the schema in format, num_ctx 16384
  nofmt-4k   no format at all, num_ctx 4096 (the model free to answer in prose)
Untimed, no lease.
"""
import importlib.util, json, os, sys, time
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
_REMOTE = Path(os.environ.get("AGENERELA_OLLAMA_REMOTE") or ROOT / "tools/ollama-server/ollama_remote.py")
_spec = importlib.util.spec_from_file_location("ollama_remote", _REMOTE)
ollama_remote = importlib.util.module_from_spec(_spec); _spec.loader.exec_module(ollama_remote)
STIMULI = ["Put an arrow in that grey beast before it gets near anyone.",
           "Your boots are muddy. Did you sleep outside again?"]
def main(argv):
    model, out_path = argv[1], argv[2]
    ollama_remote.load_env()
    server = ollama_remote.Server(os.environ["AGENERELA_OLLAMA_URL"], os.environ["AGENERELA_OLLAMA_API_KEY"])
    base = json.load(open(ROOT / "tools/live-runs/model-sweep/framework_request.json", encoding="utf-8"))["request"]
    with open(out_path, "w", encoding="utf-8") as out:
        for arm in ("fmt-16k", "nofmt-4k"):
            for stimulus in STIMULI:
                body = json.loads(json.dumps(base)); body["model"] = model; body["think"] = True
                body["messages"].append({"role": "user", "content": stimulus})
                if arm == "fmt-16k": body["options"]["num_ctx"] = 16384
                else: body.pop("format")
                for attempt in range(3):
                    t = time.time()
                    try:
                        r = server.post("/api/chat", body, timeout=900); break
                    except Exception as e:  # noqa: BLE001
                        print(arm, "retry", str(e)[:120], flush=True); time.sleep(30); r = None
                if r is None: continue
                m = r.get("message", {})
                row = {"arm": arm, "stimulus": stimulus, "wall": round(time.time() - t, 2), "eval_count": r.get("eval_count"),
                       "done_reason": r.get("done_reason"), "eval_duration_s": round(r.get("eval_duration", 0) / 1e9, 2),
                       "content": m.get("content", ""), "thinking": m.get("thinking", ""),
                       "logprob_tokens": len(r.get("logprobs") or [])}
                out.write(json.dumps(row) + "\n"); out.flush()
                print(arm, row["wall"], row["eval_count"], row["done_reason"], len(row["thinking"]), repr(row["content"][:150]), flush=True)
if __name__ == "__main__":
    sys.exit(main(sys.argv))
