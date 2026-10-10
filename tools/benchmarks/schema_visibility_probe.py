"""Does Ollama show the response schema to the model, or only constrain sampling with it?

Sends one short chat several times: without `format`, then with the decision schema whose
`action` description is one sentence, then with ~300 extra words in that description. If
the schema reached the prompt, `prompt_eval_count` would grow with the description. It
does not on Ollama 0.34.2 (docs/llm-wiki/findings.md, 10 October 2026). Re-run this after
upgrading Ollama: it takes seconds, and the answer decides whether text in the schema
(field and action descriptions) is ever read by a local model.

    python tools/benchmarks/schema_visibility_probe.py [model]

Standard library only. Talks to 127.0.0.1, never localhost (see README.md).
"""
import json
import sys
import urllib.request

URL = "http://127.0.0.1:11434/api/chat"
MODEL = sys.argv[1] if len(sys.argv) > 1 else "qwen3.5:2b"

SHORT = "The one action to take now, from the allowed list."
LONG = SHORT + " " + " ".join(f"filler_word_{i}" for i in range(300))

MESSAGES = [
    {"role": "system", "content": "You are Village Guard, a guard at the town gate."},
    {"role": "user", "content": 'Player: "Go to the tower."'},
]


def schema(action_description):
    return {
        "type": "object",
        "properties": {
            "action": {"type": "string", "description": action_description,
                       "enum": ["follow_player", "move_to", "none"]},
            "target": {"type": "string", "enum": ["no_target", "tower"]},
            "statement": {"type": "string"},
        },
        "required": ["action", "target", "statement"],
        "additionalProperties": False,
    }


def ask(fmt):
    body = {"model": MODEL, "messages": MESSAGES, "stream": False, "think": False,
            "options": {"temperature": 0, "num_predict": 40}}
    if fmt is not None:
        body["format"] = fmt
    request = urllib.request.Request(URL, json.dumps(body).encode(),
                                     {"Content-Type": "application/json"})
    return json.load(urllib.request.urlopen(request, timeout=300))


def main():
    arms = [
        ("no format", None),
        ("schema, one-sentence description", schema(SHORT)),
        ("schema, ~300-word description", schema(LONG)),
        ("schema, one-sentence, repeated", schema(SHORT)),
    ]
    with urllib.request.urlopen("http://127.0.0.1:11434/api/version", timeout=10) as r:
        print(f"Ollama {json.load(r)['version']}, model {MODEL}\n")
    counts = []
    for label, fmt in arms:
        reply = ask(fmt)
        counts.append(reply.get("prompt_eval_count"))
        print(f"{label:34s} prompt_eval_count={counts[-1]:<5} "
              f"reply={reply['message']['content'][:60]!r}")
    print()
    if len(set(counts)) == 1:
        print("Same prompt size in every arm: the schema is NOT shown to the model.")
    else:
        print("Prompt size changed with the schema: Ollama IS showing it to the model.")


if __name__ == "__main__":
    main()
