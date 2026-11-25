# Parameters: a closed choice, or a short free text, beside the target

An action can ask the model for more than a target: how hard an event strikes, how many potions,
which of three prices. Each is a **parameter**, a closed list of values the action declares. The
model picks one, the framework checks it, and the handler receives it (DR-023, first step).

Off unless you declare one. An agent whose actions declare no parameter sends exactly the request
it sent before, and a test holds the evaluation set's schema to that.

**Not a default, and not a way to make a small model more accurate.** Declaring a parameter changes
what the model is sent (below). Two A/Bs on `qwen3.5:4b`, both in `docs/llm-wiki/findings.md`
(16 November 2026), say what it costs: one harmless parameter on the evaluation sets ("One declared
parameter on the evaluation sets") lost 5.7 points on the interim set, and GreyBoxStrategy's
storyteller, the measurement DR-023 names ("The storyteller's strength as a parameter against bundled
ids"), found the parameter form does nothing the bundled ids do not, for about a tenth more time and
output tokens; a per-value check cannot see the target. So **on a small model prefer bundled ids**
(`blight_minor`, `blight_major`), and use a parameter to keep a long action list short. No default
changed, and `also_targets` is not scheduled.

## Declaring one

In code, on the action's own method:

```csharp
[AgentAction("blight", "A country falls sick.", RequiresTarget = true)]
[AgentParameter("strength", "how hard it strikes", "minor", "major")]
void Blight(Country country, [Argument("strength")] string strength)
{
    country.Sicken(strength == "major" ? 3 : 1);
}
```

On an asset, fill **Parameters** on the `ActionDefinition`. In a definition built in code:

```csharp
new ActionDefinition
{
    Id = "quote_purchase", Description = "Offer to sell an item.", RequiresTarget = true,
    Parameters = new[]
    {
        new ActionParameter { Name = "quantity", Description = "how many", Values = new[] { "1", "2", "3" }, Default = "1" },
    },
};
```

| Field | What it is |
|---|---|
| `Name` | snake_case, the key the model writes. Not `action`, `target`, `statement` or `also_targets` |
| `Description` | Optional. Shown beside the values in the prompt |
| `Values` | The closed list. At least one, none empty (Gemini rejects `""` in an enum), none twice. No comma, no control character and no whitespace at either end, since the prompt lists them as `a, b or c` |
| `Default` | Optional. The value used when the reply leaves the parameter out. Only then: a value the model wrote is never replaced |
| `ExampleValue` | Optional. The value the action's example shows; else `Default`, else the first offered |

Two kinds, both a list:

- **A closed set of words**: `minor`, `major`.
- **A number from a list**: `1`, `2`, `3`. When every value is a whole number, a method may take
  it as an `int` (`[Argument("quantity")] int quantity`), and `AgentDecision.TryGetNumber` reads it.

There is no open number range: a small model invents numbers. Compute a short list instead, such as
three prices.

- **A short free text** (DR-030): give `MaxLength` and no values, and the model writes the value
  itself, such as a name. `[AgentParameter("world_name", "the world's name", MaxLength = 24)]`, or
  `new ActionParameter { Name = "world_name", MaxLength = 24 }`. The schema sends `maxLength`
  (Ollama's grammar holds it), the action list says "world_name (the world's name): free text, at
  most 24 characters.", the example shows `ExampleValue` in quotes (or leaves the field out when
  there is none), and `SchemaLegalityGuard` refuses a longer value or one with a control character
  other than a line break. Nothing else is checked: the text is data, and the game judges it as it
  judges a statement. Painted Realm's loremaster names a world and its realms this way, in one
  decision (`Demos/GreyBoxStrategy`, `WorldLoremaster`). For one extra text beside the statement,
  the recipe in [prompt.md](prompt.md#two-texts-in-one-statement-a-title-and-a-story) still works.

**An asset wins over the method, parameters included.** When `RegisterMethods` is given an asset
with the action's id, the asset's `Parameters` replace the method's `[AgentParameter]`s (DR-011).
The method must then agree with the asset: every `[Argument]` and `[AvailableValue]` names a
parameter the asset declares, and an `int` argument a number list, or registration throws.

**`[Argument]` is not optional.** A method parameter without it receives the target. One named
like a declared parameter and missing the attribute is refused at registration.

With two parameters on one method, give each an `Order` (`[AgentParameter(..., Order = 1)]`): C#
promises no order for several attributes on one method, and the order is the order the reply gives
them in. Registration refuses a parameter that could not be sent, and a method whose `[Argument]`
names no parameter or asks for an `int` from words.

## What the model is sent

A call that offers an action with a parameter differs from the same call without it in three ways.

1. **The schema is paired** (`DecideOptions.PairTargetsWithActions`, DR-020), whether or not the
   call asked: a parameter belongs to one action, so only that action's branch can ask for it.
   Each branch is a complete object. An action with a parameter never shares a branch with one
   that has none.
2. **The parameter comes after `target` and before `statement`**: `action`, `target`,
   `strength`, `statement`. Placed after the statement, a value was written into the statement in
   3 replies of 6; placed before, in none (findings, 14 November 2026).
3. **The prompt lists the actions and targets**, as `ListActionsAndTargets` does, whatever that
   option says, with each parameter's values on its action's line:

   ```text
   Allowed actions:
   - blight: A country falls sick. strength (how hard it strikes): minor or major.
   - bless: A country prospers.
   - none: Take no action.
   ```

   A model served through Ollama never reads the schema, so this line is the only place it can
   learn the values. The action's example shows one too:
   `-> action: blight, target: orsk, strength: minor, statement: "..."`.

With no target registered, the schema is still paired and has no `target` field.

**Key order on some models.** Ollama's native chat path sorts a schema's keys alphabetically
(findings, 5 November 2026), which would put `strength` after `statement`. The `qwen3.5` family
takes the path that keeps the order. A reply whose fields came in another order than the schema's
is **flagged, not refused**: `DecisionTelemetry.OrderLost` is true and the decision log's line
carries `orderLost`. The values are still legal, since the grammar held them, so the decision
stands; what is lost is that the statement was written before the value. If you see the flag on a
model, expect statements that do not match the parameter, and prefer a model that keeps the order.
`tools/benchmarks/closed_choice_grammar_probe.py` checks a model before you rely on it.

**A provider that cannot take a paired schema.** `ProviderCapabilities.SupportsPairedSchema` is
true by default and for Ollama. On a provider that says false, an agent refuses an action with a
parameter when it is set up, with an `InvalidOperationException` that names the action and the
provider: at construction, when the action is registered or bound, and when `Agent.Provider` is
set. Never at the first decision.

## Values that depend on the game

A value can be unavailable right now: a quantity the stall cannot cover, a strength a country
struck last turn should not take again. Give the handler a per-value check, and a value it
rejects is **absent from the schema and the prompt** for that decision, not forbidden in prose.

```csharp
[AvailableValue("quote_purchase", "quantity")]
Availability CanSell(AgentContext ctx, string quantity) =>
    int.Parse(quantity) <= stock ? Availability.Yes : Availability.No($"only {stock} left");
```

- `[AvailableValue(actionId, parameter)]` on a method taking the value as a string, alone or after
  an `AgentContext`, returning `bool` or `Availability`.
- `DelegateActionHandler.IsAvailableValue`, a `Func<AgentContext, string, string, Availability>`
  given the parameter's name and the value.
- `IValueAvailability` on a handler of your own.

Each rejected value is recorded with its reason (below). An action with a parameter that has no
value left is left out of the decision, as an action with no legal target is. So is an action
with a parameter that needs a target and has none it can take right now, in every call: only a
branch of its own could offer it, and that branch would have no target. The check runs once
per value per decision, and again at `Execute`; count nothing in it
([guards.md](guards.md#recipe-fairness-and-cooldowns)).

## What comes back

`AgentDecision.Arguments` holds the chosen value of each parameter by name, in declared order;
empty, never null, for an action that declares none.

```csharp
if (result.Decision.TryGetArgument("strength", out string strength)) { ... }
if (result.Decision.TryGetNumber("quantity", out int quantity)) { ... }
```

The framework checks the values itself; the model's reply is never trusted (hard rule 5):

| The reply | What happens |
|---|---|
| A value the schema offered | Kept |
| A parameter left out (or `null`), and the parameter has a `Default` that is offered now | The default is used, and `DecisionTelemetry.ArgumentsDefaulted` names the parameter. `DecisionResult.ModelDecision` keeps what the model wrote |
| A parameter left out, with no default | `SchemaLegalityGuard` rejects the decision: it becomes `none`, and nothing runs |
| A string the parameter does not declare, or one not offered for this decision | Rejected the same way, default or none: the default covers only a missing value |
| A whole number written bare (`3` for `"3"`) | Read as its text |
| Any other type: a float (`3.0`), a boolean, an array, an integer past 64 bits | The reply is unreadable: an `UnreadableReply` pipeline error, even when the parameter has a default |

`Agent.Execute` runs the same guard again against the game as it is then: every argument must be a
declared value of a declared parameter, none may be missing, and the per-value check is asked
again. A value gone stale is refused with the check's reason in `ActionResult.AvailabilityReason`,
and no handler runs. `Execute` never applies a `Default`: a decision you build yourself must carry
every value, or it is refused. A guard of your own reads `input.Decision.Arguments`;
`GuardVerdict.ReplaceStatement` keeps them.

## Where it is recorded

| Where | What |
|---|---|
| `DecisionTelemetry.Arguments` | The decision's values by name; empty for a stopped decision |
| `DecisionTelemetry.ArgumentsDefaulted` | The parameters that took their default |
| `DecisionTelemetry.ValuesUnavailable` | Each value a per-value check rejected, with its reason |
| `DecisionTelemetry.OrderLost` | Whether the reply's fields came in another order than the schema's; `orderLost` in the log, left out when the order held |
| The decision log | `arguments` inside `decision` and `modelDecision`; `argumentsDefaulted` and `valuesUnavailable` on the line; all left out when empty ([logging-decisions.md](logging-decisions.md)) |
| `AgentPreview` | `ValuesUnavailable`, an action left out for a parameter with no value in `LeftOut`, and each branch's parameters with the values offered now in `Request.Schema.Pairs[i].Parameters` |
| Memory | A remembered decision keeps its arguments before the statement, and `RollingHistory.Remembers = Move` keeps them with the move |

## Parameters, choices and targets

- **A choice among moves** ("hire them, send them away, or seize their arms") is
  `DecideOptions.Choices` ([prompt.md](prompt.md#a-choice-among-options-given-with-the-call)): each
  option is an action value. A choice takes no parameter.
- **A choice about a move** (how hard, how many) is a parameter.
- **A value that is a game object with its own description**, such as a stock bundle the shop
  actually holds, is still better as a target ([targets.md](targets.md)).
- **Several targets** in one decision are not built: use a group as a target
  ([targets.md](targets.md#a-group-as-one-target)). `target` plus `also_targets` is DR-023's second
  step.

## Not built

- An open number range.
- A free text's cap in the GBNF grammar: the in-process provider does not exist yet, and the guard holds the cap.
- `target` plus `also_targets`.
- Another schema shape for a provider that cannot take a paired one: such a provider cannot be
  given parameters at all yet. Only Ollama exists; Gemini takes `anyOf`, OpenAI's strict mode
  refuses a root `anyOf`.
- A parameter on a `Choice`.

## For an A/B

`EvalLegality.ActionParameters` gives an evaluation arm's copy of an action a parameter, without
touching the set, its labels or the profile; the labels are scored on action and target alone
([evaluation.md](evaluation.md)).
