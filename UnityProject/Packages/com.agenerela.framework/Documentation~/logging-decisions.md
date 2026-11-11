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

Each line is one JSON object, `DecisionLogLine`, with `"version": 2`. A later version only adds
fields, and the reader ignores fields it does not know, so version 1 lines, written before the
chain fields, still read, with no chain. A field that is null is left out.

| Field | What it is |
|---|---|
| `version`, `time`, `sequence`, `stage` | The format version; when the feed raised the entry, in UTC; the feed's sequence number, which starts again each Play session; `Decided` or `Executed` |
| `agentId`, `agentName` | `Agent.Id`, when the game set one, and `AgentIdentity.Name` |
| `chainId`, `chainHop`, `chainSource` | The chain of relayed decisions the line is in, its hop (0 for the chain's first decision) and the `Agent.Id` it was relayed from: see [Chains of relayed decisions](#chains-of-relayed-decisions). Absent for a decision in no chain |
| `label`, `stimulus`, `priority` | The call's stimulus label and stimulus, as the game passed them, and its priority |
| `decision` | What the game got, after the guards: `action`, `target`, `statement`. Absent for a pipeline error |
| `modelDecision` | What the model answered, only when a guard changed it or a stray target was dropped |
| `verdict`, `guardsFired` | The guard, kind and reason that stopped the model's answer; every guard that did not pass it, `Execute`'s re-check included |
| `execution` | For an `Executed` line, what `Execute` did: `status` and `reason` |
| `error` | A pipeline error's `kind` and `message` |
| `provider`, `providerConfig`, `modelName`, `frameworkVersion` | Who answered |
| `latencySeconds`, `queueWaitSeconds`, `backlogWaitSeconds`, `queuedAhead` | How long it took, and how long it waited |
| `promptTokens`, `completionTokens` | As the provider reported them |
| `targetGrounding`, `targetGroundingStatus`, `historyTurnsSent`, `historyTurnsDropped`, `targetsDropped`, `observationsDropped` | From the telemetry; `observationsDropped` is a count |
| `targetsUnavailable` | Each action and target pair a per-target check rejected, as `action`, `target` and `reason` (left out when the check gave none); the line has no such key when none was rejected ([targets.md](targets.md#conditions-per-target)) |
| `targetIgnored` | The target the model named for an action that takes none (`TakesNoTarget`), dropped before the guards; the line has no such key when none was ([targets.md](targets.md#an-action-that-takes-no-target)) |
| `reply` | The model's reply exactly as the provider returned it, which a replay scripts with `ScriptedReply.Raw` |
| `request` | Only with `IncludePrompt` on: the system prompt, few-shot block, observations, history, stimulus turn, and the actions and targets offered |

The prompt is **off by default**: it is large, and observations and history may hold the player's
own words. Turn it on with `DecisionLog.Start(path, includePrompt: true)` or `log.IncludePrompt =
true`, which applies from the next entry. No key is ever written: telemetry holds none, and a URL with a user and password anywhere in
the line, a reply or an error message alike, is masked, also when JSON escapes spell it. The
masking is a safety net for model and handler text, not a guarantee: a URL in some other encoding,
such as percent-encoded or base64, is not recognised.

## How it behaves

- **It never blocks or throws into the game.** The entry is copied when it arrives, because a
  result's telemetry changes when it is executed, and written to the file from a worker thread. A
  write that fails is logged once as a warning; `LinesDropped` and `LastError` say what was lost,
  and later lines are still tried.
- **Several agents** share one log: their lines interleave in feed order, each whole, and
  `agentId` and `agentName` tell them apart. Use one log per file.
- **Play sessions.** The feed drops its subscribers as Play mode starts, so start the log again
  each Play session. Leaving Play mode or quitting stops it and writes what is still queued.
- **A game that dies mid-line**, or a write that fails partway, leaves a partial line. The reader
  skips it, counting it in `Read(path, out skipped)`, and the next line written starts on a new line.
- **File growth.** Before a line would take the file past `MaxFileBytes` (16 MiB by default), it moves to
  `DecisionLog.RolloverPath(path)`, `decisions.1.jsonl` for `decisions.jsonl`, replacing the one
  there, and a new file starts. Read the rollover file first for the older lines.
- **A list in memory, no file.** `DecisionLogLine.From(entry)` makes the same line from a feed
  entry without writing it, for a history panel of a game's own. Call it as the entry arrives.
- **Stopping** unsubscribes from the feed and writes what is queued. `Stop` and `Dispose` are the
  same, and both are safe to call twice.

## Chains of relayed decisions

One agent's decision often becomes another's stimulus: Odile sees a thief and calls a guard, and
the guard is asked with her words. `RelayAsync` makes that call and puts both in one chain, so the
log shows the thief, Odile, Wren and the chase together (#190, G4):

```csharp
// In Odile's call_guard handler, or right after Execute:
var chase = await odile.RelayAsync(call, wren,
    "Odile shouts for you from the market: " + trouble + ".", "Merchant");
wren.Execute(chase);
```

It is `wren.DecideAsync` with that stimulus and label, plus `DecideOptions.Chain`: the chain
`call` was in, one hop further, from `odile.Id`, or a new chain at hop 1 when `call` was in none.
Pass the rest of the call's options as the sixth argument; their `Correlation` is kept and their
label replaced. The chain is recorded in the telemetry (`ChainId`, `ChainHop`, `ChainSource`), the
feed carries it, and every line of the receiving decision, its `Executed` line included, has it.

- **The model reads nothing new.** The chain rides beside the call and never reaches the prompt or
  the provider; the receiving agent is asked exactly the stimulus and label passed.
- **The first decision.** A chain starts at the first relay, so Odile's own decision, asked by the
  game, is in none. To put it in the chain, start it there:
  `new DecideOptions { Chain = DecisionChain.Start() }`, which is hop 0 with no source.
- **The hop cap.** A relay that would take the chain past `maxHops` (8 by default,
  `DecisionRelay.DefaultMaxHops`) is not sent: it returns a result with no decision, as
  `PipelineErrorKind.ChainTooLong`, logged nowhere and raised on the feed as the receiving agent's,
  so two agents relaying to each other stop, and the log shows where.
- **Not a scheduler.** The game still decides when to relay, to whom and with what words (DR-008).
  To tell two agents, relay twice.
- **A decision round is one chain.** `DecisionRound` (#216, [decision-rounds.md](decision-rounds.md))
  starts every call it makes that has no chain of its own at `DecisionChain.Start(round.Id)`, so a
  turn's decisions share one `chainId`, each at hop 0, and a decision relayed from one of them
  continues that chain.

Group a file by chain with `DecisionLogReader.Read(path).Where(l => l.ChainId != null).GroupBy(l =>
l.ChainId)`, and order each group by `sequence`; `chainSource` and `agentId` give each hop's two
ends. A chain id is unique per chain, not a counter, and a chain restored from a save is made with
`new DecisionChain(id, hop, sourceAgentId)`.
