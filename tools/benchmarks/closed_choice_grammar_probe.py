"""Does Ollama's grammar keep a closed choice beside action and target? (#266 item 2's probe)

Before any design for typed parameters, this asks the server, with no framework code:

  1. An enum field beside `action` and `target` (a strength, `minor` or `major`): does every
     reply hold one of the enum's values, and in which key order does it come, when the field is
     placed after `target` and when it is placed after `statement`?
  2. An array of target ids with `minItems` and `maxItems` (an event on one or two countries): is
     every reply an array of allowed ids within the bounds? And with `uniqueItems`?

on both of Ollama's chat endpoints, `/api/chat` with `format` and the OpenAI-compatible
`/v1/chat/completions` with `response_format`. Which of Ollama's two internal chat paths a model
takes (the rendered path, which keeps the schema's key order, or the native one, which sorts it;
findings, 5 November 2026) depends on the model, so the probe records the reply's key order for
each request and the caller says which model it ran.

Stimuli push against the constraints: asking for three countries, for a strength the enum does
not have, for no country at all. Each reply is checked in code: parsed, key order, enum values,
array length and items, duplicates. A refused schema (HTTP 4xx) is recorded, not retried.

    python tools/benchmarks/closed_choice_grammar_probe.py <model> <out.jsonl>

Reads AGENERELA_OLLAMA_URL and AGENERELA_OLLAMA_API_KEY as tools/ollama-server/ollama_remote.py
does, from the environment or tools/ollama-server/.env, and never prints the key. For a local
Ollama, set AGENERELA_OLLAMA_URL=http://127.0.0.1:11434 and any non-empty key; never localhost.
Standard library only. It leaves the model loaded: the caller unloads it.
"""
import importlib.util
import json
import os
import sys
import time
from pathlib import Path

# tools/ollama-server/ollama_remote.py, loaded by its path: its .env reading, its gateway client
# and its error messages, so the key is handled in one place and never printed.
# AGENERELA_OLLAMA_REMOTE names another checkout's copy, for a worktree whose own has no .env
# beside it; the .env is never copied.
_REMOTE = Path(os.environ.get("AGENERELA_OLLAMA_REMOTE") or
               Path(__file__).resolve().parents[1] / "ollama-server" / "ollama_remote.py")
_spec = importlib.util.spec_from_file_location("ollama_remote", _REMOTE)
ollama_remote = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ollama_remote)

COUNTRIES = ["orsk", "kesh", "fenmark", "westmarch"]
STRENGTHS = ["minor", "major"]
SEED = 7
TEMPERATURE = 0.7

SYSTEM = (
    "You are the storyteller of a realm of four countries: orsk, kesh, fenmark and westmarch. "
    "Each turn you choose an event, the countries it strikes and how hard. "
    "Events: famine, windfall, none. Strengths: minor, major. "
    "Answer with the event, then what the schema asks for, then one sentence telling what happened."
)

STIMULI = [
    "Tell this turn's event.",
    "Strike orsk, kesh and fenmark all at once.",
    "Make it catastrophic, worse than anything before.",
    "A small blessing for one country.",
    "Strike every country in the realm.",
    "Nothing should happen to anyone this turn.",
]

ACTION = {"type": "string", "enum": ["famine", "windfall", "none"]}
TARGET = {"type": "string", "enum": ["no_target"] + COUNTRIES}
STRENGTH = {"type": "string", "enum": STRENGTHS}
STATEMENT = {"type": "string"}


def obj(fields):
    """An object schema with these fields, in this order, all required, as JsonSchemaSerializer writes one."""
    names = [name for name, _ in fields]
    return {
        "type": "object",
        "properties": {name: schema for name, schema in fields},
        "required": names,
        "propertyOrdering": names,
        "additionalProperties": False,
    }


def targets_array(unique):
    schema = {"type": "array", "items": {"type": "string", "enum": COUNTRIES}, "minItems": 1, "maxItems": 2}
    if unique:
        schema["uniqueItems"] = True
    return schema


ARMS = [
    ("enum_after_target", obj([("action", ACTION), ("target", TARGET), ("strength", STRENGTH), ("statement", STATEMENT)])),
    ("enum_after_statement", obj([("action", ACTION), ("target", TARGET), ("statement", STATEMENT), ("strength", STRENGTH)])),
    ("array_1_to_2", obj([("action", ACTION), ("targets", targets_array(False)), ("statement", STATEMENT)])),
    ("array_1_to_2_unique", obj([("action", ACTION), ("targets", targets_array(True)), ("statement", STATEMENT)])),
]


def messages(stimulus):
    return [{"role": "system", "content": SYSTEM}, {"role": "user", "content": f'Report: "{stimulus}"'}]


def native(server, model, schema, stimulus):
    body = {"model": model, "messages": messages(stimulus), "format": schema, "stream": False, "think": False,
            "options": {"temperature": TEMPERATURE, "seed": SEED, "num_ctx": 4096, "num_predict": 200}}
    reply = server.post("/api/chat", body, timeout=300)
    return reply["message"].get("content", "")


def openai(server, model, schema, stimulus):
    body = {"model": model, "messages": messages(stimulus), "temperature": TEMPERATURE, "seed": SEED,
            "max_tokens": 200, "stream": False, "reasoning_effort": "none",
            "response_format": {"type": "json_schema", "json_schema": {"name": "decision", "schema": schema, "strict": True}}}
    reply = server.post("/v1/chat/completions", body, timeout=300)
    return reply["choices"][0]["message"].get("content", "")


def judge(arm, schema, text):
    """What the reply did with the constraints, read in code."""
    expected = list(schema["properties"])
    out = {"parsed": False}
    try:
        answer = json.loads(text, object_pairs_hook=lambda pairs: pairs)
    except ValueError:
        return out
    if not isinstance(answer, list):
        return out
    keys = [k for k, _ in answer]
    values = dict(answer)
    out.update(parsed=True, keys=keys, order_kept=keys == expected, sorted_order=keys == sorted(expected),
               fields_ok=set(keys) == set(expected))
    out["action_ok"] = values.get("action") in ACTION["enum"]
    if "strength" in schema["properties"]:
        out["enum_ok"] = values.get("strength") in STRENGTHS
        out["strength"] = values.get("strength")
    if "targets" in schema["properties"]:
        t = values.get("targets")
        is_list = isinstance(t, list)
        out["targets"] = t
        out["array_ok"] = is_list
        out["count_ok"] = is_list and 1 <= len(t) <= 2
        out["items_ok"] = is_list and all(isinstance(x, str) and x in COUNTRIES for x in t)
        out["unique_ok"] = is_list and len(set(map(str, t))) == len(t)
    return out


def main(argv):
    if len(argv) != 3:
        print(__doc__.strip().splitlines()[0])
        print("usage: closed_choice_grammar_probe.py <model> <out.jsonl>")
        return 2
    model, out_path = argv[1], argv[2]
    ollama_remote.load_env()
    url = os.environ.get("AGENERELA_OLLAMA_URL", "").strip()
    key = os.environ.get("AGENERELA_OLLAMA_API_KEY", "").strip()
    if not url or not key:
        print("error: set AGENERELA_OLLAMA_URL and AGENERELA_OLLAMA_API_KEY (tools/ollama-server/.env)", file=sys.stderr)
        return 2
    server = ollama_remote.Server(url, key)
    version = server.get("/api/version").get("version")
    print(f"Ollama {version}, model {model}, temperature {TEMPERATURE}, seed {SEED}", flush=True)

    paths = [("api_chat", native), ("v1_chat_completions", openai)]
    rows = []
    total = len(paths) * len(ARMS) * len(STIMULI)
    done = 0
    with open(out_path, "w", encoding="utf-8") as out:
        for path_name, send in paths:
            for arm, schema in ARMS:
                for stimulus in STIMULI:
                    done += 1
                    row = {"ollama": version, "model": model, "path": path_name, "arm": arm, "stimulus": stimulus}
                    started = time.time()
                    try:
                        text = send(server, model, schema, stimulus)
                        row["reply"] = text
                        row.update(judge(arm, schema, text))
                    except ollama_remote.Failure as e:
                        row["error"] = str(e)
                    row["seconds"] = round(time.time() - started, 2)
                    rows.append(row)
                    out.write(json.dumps(row) + "\n")
                    out.flush()
                    status = row.get("error") or row.get("reply", "")[:90]
                    print(f"[{done}/{total}] {path_name} {arm} | {stimulus[:30]:30s} | {status}", flush=True)
                    if "error" in row and len(rows) >= 2 and all("error" in r for r in rows[-2:]):
                        print("two failures in a row: stopping this arm", flush=True)
                        break

    print()
    print(f"{'path':22s} {'arm':22s} {'n':>2s} {'parsed':>6s} {'order':>5s} {'sorted':>6s} {'enum':>4s} {'array':>5s} {'count':>5s} {'items':>5s} {'unique':>6s} {'error':>5s}")
    for path_name, _ in paths:
        for arm, _ in ARMS:
            group = [r for r in rows if r["path"] == path_name and r["arm"] == arm]
            if not group:
                continue

            def count(field):
                hits = [r for r in group if field in r]
                return f"{sum(1 for r in hits if r[field])}/{len(hits)}" if hits else "-"

            print(f"{path_name:22s} {arm:22s} {len(group):>2d} {count('parsed'):>6s} {count('order_kept'):>5s} "
                  f"{count('sorted_order'):>6s} {count('enum_ok'):>4s} {count('array_ok'):>5s} {count('count_ok'):>5s} "
                  f"{count('items_ok'):>5s} {count('unique_ok'):>6s} {sum(1 for r in group if 'error' in r):>5d}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
