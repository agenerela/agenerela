# Guards — checking the model's answer before the game acts on it

Every decision the model returns passes through an ordered list of guards before any handler
runs (build plan §2.5). The framework ships two. A game adds its own after them, for rules only
the game knows: the wolf is 26 m away, the player cannot be seen from the gate, the bell rang
ten seconds ago.

```csharp
agent.Guards.Add(new DelegateGuard("bell_cooldown", input => …));
```

This note says what the built-in guards check, what a guard can answer, what the agent says
after one stops it, when to write one at all, and gives two worked examples. Both examples run
as tests in
`Tests/Editor/Validation/DeveloperGuardTests.cs`, so the code here cannot quietly stop working.

## The order guards run in

Inside `Agent.DecideAsync`, after the reply has been parsed:

1. **`SchemaLegalityGuard`.** The decision is one the agent could legally make: the action is
   registered and was offered in this request; the target is registered or `no_target`; an
   action that needs a target got a real one; and against a paired schema
   ([`targets.md`](targets.md#the-paired-schema)), the action was offered with that target. The schema's grammar should already have
   enforced all of this. It is checked again because a provider is untrusted. Every refusal is
   a `Reject`.
2. **`TargetGroundingGuard`.** Did the model want a target that is not on the list? From the
   reply's token probabilities, it reads how much of the model's probability could still lead
   to a listed target, at every token of the target it chose, and records it in the telemetry.
   Below 0.8, for an action that requires a target, it rewrites the decision to `none`:
   "Attack the scarecrow." with no scarecrow listed is refused rather than turned into an
   attack on the training dummy. An action whose target is optional is scored but never
   rewritten. A provider that reports no probabilities
   cannot be checked, and the guard records that and passes. `DecideOptions.CheckTarget =
   false` turns it off for one call. DR-016 has the measurements.

   **When several targets are right at once** (#244). The share counts every listed target, so a
   model torn between two good ones keeps its probability on the list. What it cannot count is
   probability the provider did not report, which counts as off the list. Ollama reports
   `OllamaSettings.TokenAlternatives` alternatives a token, 20 by default since 11 November 2026
   (5 before): with five, a model spread over a strategy game's provinces read below 0.8 without
   wanting anything absent, and twenty cut GreyBoxStrategy's containments of legal moves from 72
   to 19 on `qwen3.5:4b` and from 94 to 58 on `qwen3.5:2b`. On the evaluation sets it left the
   correct answers and the impossible requests handled as they were; it did let four wrong
   answers the guard had contained pass, where an action was expected.

   What is left is real probability on strings that are no target, beside a legal spread: at
   "declare war", 0.365 on `k(esh)`, 0.315 on `mer(row)`, 0.11 on `orsk`, and 0.07 on `kers`, which
   continues no id. The guard counts that as designed and contains it at 0.8. A call can lower
   `DecideOptions.TargetGroundingThreshold` (0.8 unless set, from 0 to 1) instead of switching the
   guard off, but know the trade. At 0.6, GreyBoxStrategy's remaining legal containments fell to
   2 to 5 in 30 games and the model attacked as often as with the guard off, while on the
   389-prompt interim set `qwen3.5:2b` carried out 17 impossible requests instead of 15 (a guess
   at a target that is not there) and `qwen3.5:4b` none more. So a lower threshold suits a game
   with many legal targets a turn, not one where refusing the impossible matters most: an NPC
   told to fetch what is not there should keep 0.8.
3. **`TargetCategoryGuard`.** The target is a kind the action takes. An action that names
   `TargetCategories` is held to them: a target in another category, or in none, is rewritten to
   `none`, so `complete_quest` aimed at the village gate does nothing. An action that names none
   always passes, so this guard changes nothing until a developer declares a category. DR-020, and
   [targets.md](targets.md).
4. **`TargetAvailabilityGuard`.** The action can be done to this target right now, as the
   handler's per-target check (`[AvailableFor]`, `DelegateActionHandler.IsAvailableFor` or
   `ITargetAvailability`) says. A pair it rejects is rewritten to `none`, with the check's reason.
   An action whose handler has no check always passes. [targets.md](targets.md#conditions-per-target).
5. **`Agent.Guards`**, the game's own, in list order.

The first guard whose verdict is not `Pass` stops the rest, so a decision fires at most one
guard. The built-in guards are not in `Agent.Guards` and cannot be removed from it.

A reply that could not be read, or a provider that failed, is a pipeline error and never
reaches a guard.

## The four verdicts

| Verdict | Means | The agent | `Execute` returns | Logged |
|---|---|---|---|---|
| `GuardVerdict.Pass` | Nothing wrong; the next guard looks at it | does what the model chose | whether the handler ran and did not report `Failed` | no |
| `GuardVerdict.RewriteToNone(reason)` | The answer is legal, but wrong for the game right now | does `none` with `no_target` | `true` | no |
| `GuardVerdict.Reject(reason)` | The provider broke the contract | does `none` with `no_target` | `false` | a warning |
| `GuardVerdict.ReplaceStatement(reason, statement)` | The move is right; its statement is not | does what the model chose, saying `statement` (empty drops it) | as for `Pass` | no |

Use **`RewriteToNone`** for a game rule. A model that picks a legal action at the wrong moment
has not failed; the guard contained it. That is a contained refusal, and the evaluation harness
counts it apart from a wrong action carried out.

Use **`Reject`** only when the answer should never have been possible, the kind of thing
`SchemaLegalityGuard` catches. A game rarely needs it.

Use **`ReplaceStatement`** for a fair move with a bad line (#266 item 4): a storyteller's event
that strikes the right country but whose story invents a battle, a ruler's grievance the facts do
not hold. Prefer it to containment whenever the move would be right on its own; prefer it to
patching the line in the handler, which telemetry never sees. It does not stop the pipeline: the
later guards judge the same move with the new statement, and one of them may still stop it, which
leaves the statement empty as any stop does. The result's `Verdict` stays null,
`ModelDecision` keeps the model's words, and each replacement is recorded in `GuardsFired` and
`DecisionTelemetry.StatementsReplaced` (the guard, its reason, the text replaced and the new one),
and in the decision log's `statementsReplaced`. Memory remembers the line the agent said. No refusal
line is asked for. On a call that asked for no statement (`AskForStatement = false`) the verdict
counts as a pass and is recorded nowhere.

```csharp
agent.Guards.Add(new DelegateGuard("HistoryCheck", input =>
    InventsABattle(input.Decision.Statement)
        ? GuardVerdict.ReplaceStatement("the story invents a battle.", TemplateLine(input.Decision))
        : GuardVerdict.Pass));
```

Either way, the result keeps the model's own answer in `DecisionResult.ModelDecision` and the
verdict in `DecisionResult.Verdict`, and the exchange it came from in `DecisionResult.Request`
and `DecisionResult.Reply`: the model's original reply, not the refusal line's (#142). The telemetry records the guard's name in `GuardsFired`,
the verdict's kind in `Verdict` and its reason in `GuardReason`, for a game's guard exactly as
for a built-in one. Write the reason for a developer reading telemetry: what was wrong, not
what the player should hear. The one exception is below: a call that asks the model for a line
shows it your reason.

## What the agent says when a guard stops it

The model's statement goes with the action it was written for. An agent must not say "To arms!"
while doing nothing, so `DecisionResult.Decision.Statement` is **empty** after a rewrite or a
reject, and the model's words stay on `DecisionResult.ModelDecision`. Two things can supply a line
instead, and the first that gives one wins.

**Your own line.** `Agent.RefusalStatement` is a `Func<GuardVerdict, AgentContext, string>`, null
by default. The verdict names the guard and its reason, so a game can answer each differently:

```csharp
agent.RefusalStatement = (verdict, ctx) =>
    verdict.Guard == "TargetGroundingGuard" ? "There's no such thing here."
    : verdict.Guard == "bell_cooldown" ? "I just rang it."
    : null;
```

A null return gives no line. An empty string is a line that says nothing, so no request follows
it. A callback that throws is logged as a warning and counts as a null return; the decision still
stands.

**The model's line.** `DecideOptions.AskForRefusalLine`, off by default, sends one more short
request when a guard fired and your callback gave no line. It is per call, because only the caller
knows whether anyone is listening. A decision that passed never costs a second request.

- It carries the same system prompt as the decision, one user turn saying what was asked and why
  it will not be done, and a schema with only `statement`. No examples, observations or history.
- It goes through the same queue at the call's `Priority`, so it waits behind whatever is ahead
  of it. Agents on a busy provider wait for the line as well as the decision.
- The model reads the guard's reason, so a game's guard used with this option needs a reason that
  makes sense to the model. The grounding guard's reason holds probabilities, so the model reads
  "nothing like that is here." instead, and telemetry keeps the numbers.
- Its time and tokens are recorded in `RefusalLineSeconds`, `RefusalLinePromptTokens` and
  `RefusalLineCompletionTokens`, never added to the decision's own: `LatencySeconds` is still the
  decision alone. Its wait in the queue is not recorded.
- If it fails, through a provider error or a reply with no readable `statement`, a warning is
  logged, the kind goes in `RefusalLineError`, and the decision stands with an empty statement. It
  is never a pipeline error. Cancelling the call during it cancels the decision.
- No guard judges the line, and it cannot change the action: an action in the reply is ignored.

The wording of this request is new and unmeasured. It affects only what is said after a refusal,
never which action runs.

**Memory** holds what happened: `none`, with the line actually said or an empty statement. It
never holds the model's original words.

`Execute`'s own re-check is different: it refuses a decision that passed, at the moment of acting,
and never rewrites it, so that decision keeps its statement.

**A call that asked for no statement** (`DecideOptions.AskForStatement = false`, #266) gets none
here either: neither your callback nor a refusal line is asked, and the stopped decision's statement
stays empty. A guard of your own that judges the statement should pass such a call, which it can
tell from `input.Options.AskForStatement`; `input.Decision.Statement` is empty there.

## A guard, or availability?

There are two places to stop an action, and they are not interchangeable.

**Availability** (`IActionHandler.IsAvailable`) takes an option off the menu before the model is
asked. The action is absent from the schema, so the model cannot choose it, and nothing has to
be refused afterwards. That is a settled rule of this framework (§2.2): an illegal option absent
from the schema beats one forbidden in prose, and it beats one refused after the fact.

**A guard** judges the answer after the model has given it. It sees what availability cannot:
which target was chosen, what the statement says, what the model was told, and the reply's
token probabilities.

So:

- When the rule depends only on the agent and the world, use availability. "The bell cannot be
  rung for 30 seconds after it rang" is known before the model is asked: hide `sound_alarm`.
- When the rule depends on the answer, write a guard. "The bell cannot be rung again for the
  *same* threat within 30 seconds" depends on the target the model picked. Availability cannot
  express it without hiding the bell from every other threat too.

The wolf from the 25 October demo shows both. A wolf 26 m away drew `sound_alarm` while the
model's own statement said the wolf was too far away to be a threat. If the bell is only for a
threat that is near, and that is known before asking, hide `sound_alarm` while nothing is near.
If one threat may be near and another far, a guard can refuse the bell for the far one, reading
the chosen target's distance.

### Not re-checked at `Execute`

`Agent.Execute` checks the decision again before running the handler, because the world can
change while the model thinks. That re-check is `SchemaLegalityGuard`, then
`TargetCategoryGuard`, then `TargetAvailabilityGuard`, which asks a per-target check again,
against the targets as they are then. A game's guards judged the answer
once, inside `DecideAsync`, and are not run again.

If a game rule must still hold at the moment of acting, put it in the handler, in the action's
`IsAvailable`, or, when it is about one target, in its per-target check; `Execute` asks both
again. A handler can say why it did not act by returning
`ActionResult.Failed(reason)`, which `Execute(result, out ActionResult)` hands back and the
telemetry records ([executing-and-memory.md](executing-and-memory.md)).

## Writing one

A guard implements `IDecisionGuard`: a `Name`, which telemetry records, and
`Inspect(GuardInput)`, which returns a verdict. `DelegateGuard` makes one from a name and a
function, as `DelegateActionHandler` does for an action.

`GuardInput` holds everything a guard may read: the `Context` the decision was made from (state,
targets, stimulus, observations, and the call's `Correlation`), the `Request` sent, the provider's `Reply`, the parsed
`Decision`, the call's `Options` and the `Telemetry` the result will carry. A guard may write its
own numbers into the telemetry. It never changes the decision itself.

A guard runs on the main thread, once per decision. Keep it quick: it runs on every decision
the agent makes.

**A guard that throws** is not a refusal. It is the game's own code failing, so the decision
becomes a pipeline error: `DecisionResult.Decision` is null, `Telemetry.Outcome` is
`PipelineError` and `Telemetry.ErrorKind` is `GuardFailed`, with a warning naming the guard and
its place in the list. The guards after it do not run, nothing is remembered, and `Execute`
returns `false`. A null entry in `Agent.Guards` fails the same way.

## Example: a cooldown for each threat

The bell may ring for a new threat at any time, but not again for the same one within 30
seconds. The game writes `rangAt[target]` when the bell actually rings.

```csharp
// When the bell last rang for each threat, by target id.
var rangAt = new Dictionary<string, float>();

agent.Guards.Add(new DelegateGuard("bell_cooldown", input =>
{
    var d = input.Decision;
    return d.ActionId == "sound_alarm"
           && rangAt.TryGetValue(d.TargetId, out float at)
           && Time.time - at < 30f
        ? GuardVerdict.RewriteToNone($"the bell rang for {d.TargetId} less than 30 seconds ago.")
        : GuardVerdict.Pass;
}));
```

A wolf that set the bell off 15 seconds ago gets `none`, and `GuardsFired` says
`bell_cooldown`. A bandit on the road still rings it.

## Example: the name check

Before DR-016, the plan was a built-in check that the chosen target's name appears in what the
player said. It was measured and dropped. A game that wants strict wording can add it back:

```csharp
// Only for what the player says, and only for a decision with a target. Ids are words joined
// by "_", so "training_dummy" must appear as "training dummy".
agent.Guards.Add(new DelegateGuard("name_check", input =>
{
    var d = input.Decision;
    if (input.Options.StimulusLabel != "Player" || d.TargetId == TargetRegistry.NoTarget)
    {
        return GuardVerdict.Pass;
    }

    string name = d.TargetId.Replace('_', ' ');
    return input.Context.Stimulus.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0
        ? GuardVerdict.Pass
        : GuardVerdict.RewriteToNone($"the player never named '{name}'.");
}));
```

The label is the game's own: it decided which calls are the player speaking (DR-008), so the
guard may read it. The framework never does.

**What it cost when measured.** It refused 15 to 29 good requests per model: paraphrases such as
"Hit the mannequin." and pronouns such as "Attack it." On one model it scored below no check at
all (DR-016, build plan §2.5). The built-in grounding guard catches the same failure, a model
swapping a missing target for a listed one, without word lists, and lets a paraphrase through.
Add this one only if strict wording matters more to the game than those refusals.
