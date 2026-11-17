"""Does Ollama honour a paired schema (#190 B, DR-020)? Sends ten stimuli that tempt a wrong pair
(complete_quest on a place) with four schemas: the flat one and the root-anyOf one
JsonSchemaSerializer writes, and both again without none and no_target, so the model must act. For
each reply it records whether the pair is one the paired schema offers, and the order its keys came
back in. A grammar check, not an accuracy run: n is 10 per arm and the stimuli are made up.

Usage: python tools/benchmarks/paired_schema_probe.py <model> <out.jsonl>
Unload other models first (hard rule 3). Re-run it after upgrading Ollama."""
import json
import sys
import time
import urllib.request

OLLAMA = 'http://127.0.0.1:11434'
ACTION_DESC = 'The one action to take now, from the allowed list.'
TARGET_DESC = 'What the action applies to. Use no_target when none of the listed targets applies.'
STATEMENT_DESC = 'What the agent says or announces. One or two short sentences, or empty.'

PLACES = ['village_gate', 'well']
QUESTS = ['quest_wolf', 'quest_lantern']
TARGETS = PLACES + QUESTS
# move_to takes any target; complete_quest a quest; none nothing. As DecisionSchema.Build pairs them.
PAIRS = [(['move_to'], TARGETS), (['complete_quest'], QUESTS), (['none'], ['no_target'])]
ACTIONS = ['move_to', 'complete_quest', 'none']


def obj(actions, targets):
    return {
        'type': 'object',
        'properties': {
            'action': {'type': 'string', 'description': ACTION_DESC, 'enum': actions},
            'target': {'type': 'string', 'description': TARGET_DESC, 'enum': targets},
            'statement': {'type': 'string', 'description': STATEMENT_DESC},
        },
        'required': ['action', 'target', 'statement'],
        'propertyOrdering': ['action', 'target', 'statement'],
        'additionalProperties': False,
    }


FLAT = obj(ACTIONS, ['no_target'] + TARGETS)
PAIRED = {'anyOf': [obj(a, t) for a, t in PAIRS]}
# A check of the grammar alone, not a shape the framework sends: without none or no_target the model
# must act, so the flat arm can pair complete_quest with a place and the paired arm show it cannot.
FORCED_PAIRS = [(['move_to'], TARGETS), (['complete_quest'], QUESTS)]
FLAT_FORCED = obj(['move_to', 'complete_quest'], TARGETS)
PAIRED_FORCED = {'anyOf': [obj(a, t) for a, t in FORCED_PAIRS]}
ARMS = (('flat', FLAT), ('paired', PAIRED), ('flat_forced', FLAT_FORCED), ('paired_forced', PAIRED_FORCED))

SYSTEM = (
    'You are Odile, a merchant at the village market. Brief and practical.\n'
    'Choose exactly one action from the allowed list. Use no_target when none of the listed targets applies.\n'
    'Allowed actions:\n- move_to: Walk to a place.\n- complete_quest: Take what the player brought for a quest.\n- none: Take no action.\n'
    'Targets:\n- village_gate: The main gate.\n- well: The stone well.\n- quest_wolf: The wolf quest.\n- quest_lantern: The lantern quest.'
)
FEW_SHOT = (
    'Examples of correct decisions:\n'
    'Player: "Meet me by the well." -> action: move_to, target: well\n'
    'Player: "Here are the wolf pelts you wanted." -> action: complete_quest, target: quest_wolf\n'
    'Player: "Nice weather today." -> action: none, target: no_target'
)

# Lines that tempt complete_quest on a place, and a few that should be right either way. None of
# these is an evaluation prompt or one of the examples above (hard rule 4).
STIMULI = [
    'I finished the job at the village gate, take it.',
    'Complete it at the well, I left everything there.',
    'Done with the gate business, mark it complete.',
    'Wrap up the well task for me.',
    'The thing at the village gate is sorted, you can close it.',
    'Here is the lantern you asked for.',
    'I brought the wolf pelts.',
    'Walk over to the gate with me.',
    'Finish it at the gate.',
    'Mark the well as done.',
]


def chat(model, schema, stimulus):
    body = {
        'model': model, 'stream': False, 'format': schema, 'think': False,
        'options': {'temperature': 0.7, 'seed': 7, 'num_predict': 120},
        'messages': [
            # As OllamaProvider lays a request out: the system prompt and the examples in one system
            # message, a blank line between them, and the labelled stimulus as the user turn.
            {'role': 'system', 'content': SYSTEM + '\n\n' + FEW_SHOT},
            {'role': 'user', 'content': 'Player: "' + stimulus + '"'},
        ],
    }
    req = urllib.request.Request(OLLAMA + '/api/chat', data=json.dumps(body).encode(), headers={'Content-Type': 'application/json'})
    start = time.monotonic()
    with urllib.request.urlopen(req, timeout=120) as resp:
        reply = json.load(resp)
    return reply, time.monotonic() - start


def offered(action, target):
    return any(action in a and target in t for a, t in PAIRS)


def main():
    model, out = sys.argv[1], sys.argv[2]
    rows = []
    for stimulus in STIMULI:
        for arm, schema in ARMS:
            row = {'model': model, 'arm': arm, 'stimulus': stimulus}
            try:
                reply, seconds = chat(model, schema, stimulus)
                text = reply['message']['content']
                row['seconds'] = round(seconds, 2)
                row['text'] = text
                parsed = json.loads(text, object_pairs_hook=lambda pairs: pairs)
                keys = [k for k, _ in parsed]
                values = dict(parsed)
                row['keys'] = keys
                row['action'] = values.get('action')
                row['target'] = values.get('target')
                row['pairOffered'] = offered(row['action'], row['target'])
            except Exception as e:  # recorded, not raised: a refusal of the schema is a result too
                row['error'] = f'{type(e).__name__}: {e}'
            rows.append(row)
            print(json.dumps({k: row.get(k) for k in ('arm', 'stimulus', 'action', 'target', 'pairOffered', 'keys', 'error')}))
    with open(out, 'a', encoding='utf-8') as f:
        for row in rows:
            f.write(json.dumps(row) + '\n')
    for arm, _ in ARMS:
        mine = [r for r in rows if r['arm'] == arm]
        ok = [r for r in mine if 'error' not in r]
        print(f"{model} {arm}: {len(ok)}/{len(mine)} answered; pairs offered {sum(r['pairOffered'] for r in ok)}/{len(ok)}; "
              f"order action,target,statement {sum(r['keys'] == ['action', 'target', 'statement'] for r in ok)}/{len(ok)}")


if __name__ == '__main__':
    main()
