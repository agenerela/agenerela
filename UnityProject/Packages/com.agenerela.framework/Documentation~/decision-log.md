# The Decision Log window

**Window → Agenerela → Decision Log** answers "why did my agent do that?" (#172, screen 3 of
[the designs](../../../../docs/design/README.md)). Every decision any agent makes in Play mode
becomes a row, newest first. Click one to see its whole story: what the agent was asked, what the
model was offered and answered, what each guard did, what came of it, and the telemetry.

It reads the [decision feed](agents-in-a-scene.md#watching-an-agent-from-a-tool), so it covers
every agent, one on a GameObject or a plain `Agent` built in code, and the game passes it nothing.
It listens only while the window is open and **Recording** is on: with the window closed the feed
has no subscriber from it, and the game pays a null check.

## The four outcomes, and a fifth

"The agent did nothing" has several causes that look the same in the game and need different
fixes. The icon on each row says which it was:

| Icon | Outcome | What happened | Where to look |
|---|---|---|---|
| ✓ | **Executed** | The handler ran. A handler that reported it could not is still this, marked *handler failed* | Your handler, if the world did not change as expected |
| ○ | **No action** | The model chose `none` | The prompt: section 1 says what the agent knew, section 3 what it answered |
| ⚠ | **Contained** | A guard stopped or rewrote the model's choice, or `Execute`'s re-check refused it | Section 4: the guard, its verdict and its reason. This is the safety layer working, not a bug |
| ✕ | **Error** | No decision: the provider failed, the reply could not be read, or a guard threw | Section 5: the error's kind and message |
| ⏱ | **Not executed yet** | Decided, but nothing has called `Agent.Execute` on it | Your code, if you expected it to run |

The outcome comes from the decision itself, never from `DecisionTelemetry.Outcome`, which only
the evaluation runner sets.

## Reading a row

```
⚠  14:02:18  Guard (Gate)   Player: “Attack Godzilla.”                 1.40 s
                            attack_target · training_dummy ⇒ none
```

- **Time and agent.** The agent's GameObject name, or for an agent with no GameObject its
  identity's name.
- **Asked.** The stimulus label and the stimulus, as the game passed them.
- **Result.** The action and target the game got. When a guard changed the move, the model's own
  choice comes first and an arrow leads to what the game got: here the model reached for the
  training dummy, which was not what the player named, and the grounding guard rewrote it to
  `none`.
- **Latency.** The provider's own time; `n/a` for an error.

A decision raises two feed entries, one when `DecideAsync` returns and one when `Execute` does.
They are one row: the second updates the first with what `Execute` did.

## The detail, section by section

1. **Asked**: label, stimulus, the observations sent, and how much memory went with it.
2. **Offered to the model**: the actions and targets the schema allowed, the model's choice in
   bold, and any pair a per-target check left out with its reason.
3. **Model answered**: the raw reply, exactly as the provider returned it, and what the game got.
4. **Guards, in order**: every guard the decision's pipeline held, built-in first and then
   `Agent.Guards`, each **Passed**, **New statement**, **Stopped** (with the verdict and its reason),
   **Threw** or **Not run** (a guard before it stopped the decision). The grounding guard shows the
   share of the model's choice that stayed on the target list (DR-016). A refusal by `Execute`'s
   re-check comes last. Your own guards appear by their `Name` with no change to the window.
5. **Result**: one sentence on what came of it, and the refusal line the agent said, if it said
   one (#109).
6. **Telemetry**: latency, queue and backlog waits, tokens, prompt size and budget, the provider,
   the **provider config** and which of DR-013's three places it came from (the agent's own field,
   the project default, or code and scene overrides), the model, the framework version, memory
   turns sent and dropped, and targets and observations left out.

## The toolbar

| Control | What it does |
|---|---|
| **Recording** | On, the window listens to the feed; off, it does not, and keeps what it has |
| **Clear** | Removes every row |
| **Clear on Play** | Clears the log each time Play mode starts, as the Console's toggle does |
| **All agents** | Shows one agent's rows |
| Outcome toggles | Show or hide each outcome. The numbers count every row that matches the agent and the search, shown or not |
| Search | Finds rows whose stimulus or label contains the text, ignoring case |
| **Export .jsonl** | Saves the rows shown, oldest first, as a JSON-lines file in the [decision log format](logging-decisions.md) |

On a selected row, **Select agent in scene** selects its GameObject, while it still exists, and
**Copy as JSON** copies the row's log line.

## What it keeps

At most 1,000 decisions; past that the oldest go first. The log survives leaving Play mode, and the
domain reload that comes with it, but not an Editor restart: to keep a session, export it, or have
the game write its own file with [`DecisionLog`](logging-decisions.md). Each row is one
`DecisionLogLine` with the prompt included, so an export reads back with `DecisionLogReader`.

Not yet: reading a log back into the window, or a player build's log, and **Add to evaluation
prompts**, which will send a row to the Prompt Set window.
