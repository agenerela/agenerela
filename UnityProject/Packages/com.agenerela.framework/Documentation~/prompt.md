# The prompt — what the model reads, and three switches that change it

`PromptBuilder` assembles every request an agent sends: the system prompt, the few-shot
examples, the observations, the history and the stimulus. The schema travels with it. This
note is about one gap and the three switches that close it. All three are off until an A/B run
shows they help (hard rule 2, #151).

**The gap.** A model served through Ollama never reads the schema. Ollama uses it only to
constrain sampling (findings, 10 October 2026), so the action list, the descriptions and the
target ids in it reach no local model. The system prompt says "Choose exactly one action from
the allowed list", but by default it never shows that list. A local model learns the actions
only from the few-shot examples, and learns the targets only from what the game writes into
observations. Living Vale and GreyBoxVillage both wrote every action and target into their
observations by hand (#190).

## Listing the actions and targets

```csharp
var result = await agent.DecideAsync(line, new DecideOptions { ListActionsAndTargets = true });
```

The system prompt then gains a list, right after the instruction line:

```
Choose exactly one action from the allowed list. Use no_target when none of the listed targets applies.
Allowed actions:
- quote_purchase: Quote a price for something the customer wants to buy.
- offer_quest: Offer a quest.
- none: Take no action.
Targets:
- stock_mara_bread_one: One loaf of bread, 3 gold.
- north_tower
```

- **The actions** are exactly the ones the schema offers, in its order. An action whose
  availability check fails is not listed, and `none` comes last (build plan §2.2, rule 3). Each
  `Description` is put on one line and ends with a full stop. An action with no description is
  listed by its id alone. Keep descriptions to one short clause of similar length: a long,
  emphatic one biases a small model toward that action (Appendix A).
- **The targets** are the schema's, in its order, without `no_target`, which the instruction line
  already names. A target object can say what it is by implementing `IDescribedTarget`:

  ```csharp
  sealed class StockEntry : IDescribedTarget
  {
      public string Description => $"{Quantity} {Item}, {Price} gold.";
      // ...
  }
  shop.Add("stock_mara_bread_one", breadEntry);   // an ExplicitTargetSource
  ```

  Any other target is listed by its id. A scene target is registered as its `Transform`, so it is
  listed by id, and its `Targetable.Description` reaches the model as an observation through
  `ProximityObservationSource` instead (build plan §2.8).
- **The budget.** `MaxListedTargets`, 12 by default, caps the target lines. The first targets in
  schema order are kept, so a source that offers the nearest first, as `ProximityTargetSource`
  does, loses the far ones. A target past the cap can still be chosen, but it gets no line. Every
  action is always listed: the developer decides how many there are, and dropping one would hide
  it. 0 lists no targets. A negative value throws at the call.
- **With the target rule.** On a provider without token probabilities, the target rule
  (`PromptOptions.TargetRule`, #107) comes after the list, as the last line.

The list's form follows the "Around you" list that DR-016's probe measured, which listed
targets the same way, but no actions. It has never been measured on its own.

## The idle example on an agent built in code

The few-shot block ends with an idle example, a line answered with `none`, so the model sees
that not every line asks for something. An agent built from an `AgentProfile` gets the
profile's `IdleExampleStimulus`. An agent built in code gets none, unless it is given one:

```csharp
var agent = new Agent(identity, actions, provider)
{
    IdleExampleStimulus = AgentProfile.DefaultIdleExampleStimulus,   // "Nothing has changed since last time."
};
```

The one used for a call is the first of these that is set: the call's
`DecideOptions.IdleExampleStimulus`, then the agent's `IdleExampleStimulus`, then the profile's.
An empty string at any level leaves the example out. `Agent.IdleExampleStimulus` is null by
default, so it changes nothing until someone sets it. Whether the idle example helps at all is
still unmeasured (build plan §2.3).

## Counter-examples

A counter-example is a line that touches an action's subject but asks for nothing, shown
answered with `none`. Living Vale's shopkeepers quoted prices nobody had asked for. One line
answered with `none`, sent as the idle example, cut those unasked offers from 6/13 to 0/13 on
one small model and from 10/13 to 0/13 on another, and asked-for trades held. That was measured
in the game, on its branch `69-living-vale` (its findings, 5 November: "Unasked offers"), not
on the evaluation set. A counter-example is the same idea tied to one action, and is itself
unmeasured.

```csharp
quote.CounterExampleStimulus = "Any rumours about the wagon?";   // an ActionDefinition or its asset
```

```csharp
[AgentAction("quote_purchase", "Quote a price for something the customer wants to buy.", RequiresTarget = true)]
[Example("How much for the {0}?", "bread")]
[CounterExample("That {0} smells good.")]
public void QuotePurchase(AgentContext ctx, StockEntry entry) { /* ... */ }
```

`FewShotBuilder` puts it right after the action's own example, and only while the action is
offered:

```
Player: "How much for the Bread?" -> action: quote_purchase, target: bread
Player: "That Bread smells good." -> action: none, target: no_target
```

A `{0}` is filled the way the example's is, with the first registered target in
`PreferredExampleTargets`. If none of them is registered, the counter-example is left out, so a
placeholder never reaches the model. The field is empty by default. Writing one is the opt-in,
and `DecideOptions.CounterExamples = false` leaves them all out of a call.

**Keep it apart from the evaluation set** (hard rule 4). A counter-example is a few-shot example
like any other. `PromptSetChecks` flags a prompt that matches one, so `EvalRunner` refuses to
run that set. A counter-example also carries a risk: it pushes the model toward `none`, and in
the prototype a prompt biased toward `none` scored below the baseline (Appendix A). So the A/B
has to check that real commands still score, as well as whether unasked actions go down.

## Measuring them

Each switch is an option an `EvalArm` carries, so one run in one session compares an arm with it
against an arm without it. `report.json` records each arm's `listActionsAndTargets`,
`maxListedTargets`, `counterExamples` and `idleExampleStimulus`.
[`evaluation.md`](evaluation.md#running-an-ab) explains how to run an A/B. Until one has been
run, quote no number for any of these on the evaluation set.
