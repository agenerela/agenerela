# Logging decisions to a file

`DecisionLog` writes every decision every agent makes to a JSON-lines file, one line per
[decision feed](agents-in-a-scene.md#watching-an-agent-from-a-tool) entry: one when `DecideAsync`
returns, and one each time `Execute` returns (#190, G7). It is plain C#, so it logs an agent with no
GameObject as well as one with. `DecisionLogReader` reads a file back, and is what the Decision Log
window and a replay read through, so the format has one definition.

```csharp
using Agenerela;
using UnityEngine;

public sealed class GameLog : MonoBehaviour
{
    private DecisionLog log;

    private void Awake() => log = DecisionLog.Start(DecisionLog.DefaultPath());
    private void OnDestroy() => log.Stop();
}

// Later, in a test or a tool:
foreach (var line in DecisionLogReader.Read(path))
{
    Debug.Log($"{line.Sequence} {line.AgentName}: {line.Stimulus} -> {line.Decision?.Action}");
}
```

## What a line holds

Each line is one JSON object, `DecisionLogLine`, with `"version": 1`. A later version only adds
fields, and the reader ignores fields it does not know. A field that is null is left out.

| Field | What it is |
|---|---|
| `version`, `time`, `sequence`, `stage` | The format version; when the feed raised the entry, in UTC; the feed's sequence number, which starts again each Play session; `Decided` or `Executed` |
| `agentId`, `agentName` | `Agent.Id`, when the game set one, and `AgentIdentity.Name` |
| `label`, `stimulus`, `priority` | The call's stimulus label and stimulus, as the game passed them, and its priority |
| `decision` | What the game got, after the guards: `action`, `target`, `statement`. Absent for a pipeline error |
| `modelDecision` | What the model answered, only when a guard changed it |
| `verdict`, `guardsFired` | The guard, kind and reason that stopped the model's answer; every guard that did not pass it, `Execute`'s re-check included |
| `execution` | For an `Executed` line, what `Execute` did: `status` and `reason` |
| `error` | A pipeline error's `kind` and `message` |
| `provider`, `providerConfig`, `modelName`, `frameworkVersion` | Who answered |
| `latencySeconds`, `queueWaitSeconds`, `backlogWaitSeconds`, `queuedAhead` | How long it took, and how long it waited |
| `promptTokens`, `completionTokens` | As the provider reported them |
| `targetGrounding`, `targetGroundingStatus`, `historyTurnsSent`, `historyTurnsDropped`, `targetsDropped`, `observationsDropped` | From the telemetry; `observationsDropped` is a count |
| `reply` | The model's reply exactly as the provider returned it, which a replay scripts with `ScriptedReply.Raw` |
| `request` | Only with `IncludePrompt` on: the system prompt, few-shot block, observations, history, stimulus turn, and the actions and targets offered |

The prompt is **off by default**: it is large, and observations and history may hold the player's
own words. Turn it on with `DecisionLog.Start(path, includePrompt: true)` or `log.IncludePrompt =
true`, which applies from the next entry. No key is ever written: telemetry holds none, and a URL
with a user and password in an error message is masked.

## How it behaves

- **It never blocks or throws into the game.** The entry is copied when it arrives, because a
  result's telemetry changes when it is executed, and written to the file from a worker thread. A
  write that fails is logged once as a warning; `LinesDropped` and `LastError` say what was lost,
  and later lines are still tried.
- **Several agents** share one log: their lines interleave in feed order, each whole, and
  `agentId` and `agentName` tell them apart. Use one log per file.
- **Play sessions.** The feed drops its subscribers as Play mode starts, so start the log again
  each Play session. Leaving Play mode or quitting stops it and writes what is still queued.
- **A game that dies mid-line** leaves a partial last line. The reader skips it, counting it in
  `Read(path, out skipped)`, and the next log on that file starts on a new line.
- **File growth.** Past `MaxFileBytes` (16 MiB by default), the file moves to
  `DecisionLog.RolloverPath(path)`, `decisions.1.jsonl` for `decisions.jsonl`, replacing the one
  there, and a new file starts. Read the rollover file first for the older lines.
- **A list in memory, no file.** `DecisionLogLine.From(entry)` makes the same line from a feed
  entry without writing it, for a history panel of a game's own. Call it as the entry arrives.
- **Stopping** unsubscribes from the feed and writes what is queued. `Stop` and `Dispose` are the
  same, and both are safe to call twice.
