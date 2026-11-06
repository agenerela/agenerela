# Targets — which agent may name what, and which action takes which

An agent's targets are worked out again on every decision (DR-014): the ones registered by hand
in `Agent.Targets`, then whatever each of `Agent.TargetSources` adds, in order. One flat list
serves every action, so by default a model can pair any action with any target. That is how a
quest giver ends up asked to `complete_quest` on the village gate, or a trader to `quote_sale` a
stock entry (#190, workstream B). This page covers the four tools that keep the pairs and the
lists right. DR-020 in the build plan has the reasoning.

| You want | Use |
|---|---|
| An action to refuse a target of the wrong kind | A **category** on each target, and `ActionDefinition.TargetCategories` on the action |
| An agent never to be offered some kinds of target, whichever source finds them | `Agent.TargetCategories`, or **Target Categories** on `AgentBehaviour` |
| Two sources that both offer the same object | Nothing: the same object twice is one target |
| An agent to have only some of the actions on its GameObject | **Only Actions** on `AgentBehaviour`, or `RegisterMethods(owner, assets, only)` |

None of them changes what the model is sent until you use it. With none set, every request is
byte-identical to what it was before, and a test holds the evaluation set's schema to that.

## Categories on targets

A category is a free string, such as `place`, `person`, `quest` or `ware`, registered with the
target. The model never reads it.

```csharp
// By hand, or in an ExplicitTargetSource.
agent.Targets.Register("quest_wolf", wolfQuest, "quest");
var stall = new ExplicitTargetSource()
    .Add("bread", breadEntry, "ware")
    .Add("cheese", cheeseEntry, "ware");

// In your own ITargetSource.
public void Collect(AgentContext ctx, TargetRegistry into)
{
    foreach (var quest in board.Open())
    {
        into.Offer(quest.Id, quest, "quest");
    }
}
```

A scene target gets its `Targetable.Category`: `ProximityTargetSource` registers each one in it.
`TargetRegistry.CategoryOf(id)` reads it back, and is null for a target registered without one. A
blank category is none. Categories compare ordinally, as `Targetable.Category` always has, so
`"Place"` and `"place"` are two categories.

## Which targets an action takes

An action names the categories it takes. Empty, the default, takes any target.

```csharp
// The code front door.
[AgentAction("complete_quest", "Take what the player brought for a quest.",
             RequiresTarget = true, TargetCategories = new[] { "quest" })]
private void Complete(Quest quest) => board.Complete(quest);

// An action asset, or a definition in code: the same field.
offerQuest.TargetCategories = new[] { "quest" };
```

**`TargetCategoryGuard`**, a built-in guard, holds each decision to it. It runs inside
`DecideAsync` after the grounding guard and before your own `Agent.Guards`
([guards.md](guards.md)):

- An action that names no category, `none` and `no_target` always pass.
- A target in one of the action's categories passes.
- A target in another category, or **in no category**, is a `RewriteToNone`, with a reason such
  as "'complete_quest' takes only targets in the category 'quest', and 'village_gate' is in the
  category 'place'." The model chose an action and a target that were both offered, so it broke no
  contract. The pair is wrong, so this is a contained refusal, and `Execute` returns true having
  done nothing.

`Agent.Execute` runs it again, right after `SchemaLegalityGuard`'s re-check, against the targets as
they are then, and refuses a mismatch with a warning. `Agent.RefusalStatement` can give the line
the agent says, keyed on `verdict.Guard == nameof(TargetCategoryGuard)`.

**By default the schema still offers every target with every action.** The guard catches a wrong
pair after the model chose it; it does not stop the model choosing it. The paired schema, below,
does.

## The paired schema

`DecideOptions.PairTargetsWithActions` makes a wrong pair unsayable: the schema offers each action
with only the targets it takes, so the sampling grammar cannot produce `complete_quest` on a place.

```csharp
var result = await agent.DecideAsync(line, new DecideOptions { PairTargetsWithActions = true });
```

It is **off by default and unmeasured**: it changes what the model is constrained to, and it stays
off until an A/B on the evaluation set shows it helps (hard rule 2, #151). What it does:

- Each action offered takes the targets its `TargetCategories` name, or every target when it names
  none, and `no_target` only if it does not require a target. `none` takes `no_target` alone.
- Actions that take the same targets share one branch (`DecisionSchema.Pairs`, one `ActionTargets`
  each).
- An action that requires a target and takes none of those registered is not offered at all, and
  its example is not shown, as an action that needs a target already is when nothing is registered.
- The system prompt, the action and target list, the target rule and the examples are what they
  would be without it. Only the schema changes, so an A/B of it measures the schema alone.
- `JsonSchemaSerializer` writes it as a root `anyOf` with one complete object per branch, the
  fields in the measured order. llama.cpp, and so Ollama, compiles that into one grammar.
- `SchemaLegalityGuard` rejects a reply that pairs an action with a target its branch did not
  offer: the grammar should have made it impossible, so the provider broke the contract.

On Ollama 0.40.14 no paired reply broke the pairing, on either of its chat paths (findings, 6
November 2026). That was a check of the grammar on ten lines, not of accuracy. Pairing makes the
wrong kind of target unsayable; the model can still pick the wrong target of the right kind, which
is what the grounding guard watches for. Give each action's `PreferredExampleTargets` a target of a
category it takes, or its example may show a pair the schema then forbids.

A cloud provider that cannot take a root `anyOf`, such as OpenAI's strict structured outputs, should
be sent the flat schema. None ships yet.

## Which categories an agent is offered

`Agent.TargetCategories` is an allowlist over everything the agent's sources found:

```csharp
guard.TargetCategories.Add("place");
guard.TargetCategories.Add("person");
guard.TargetCategories.Add("creature");
// The market's wares and the quests are never offered to this guard, whichever source adds them.
```

It is applied on every decision and at `Execute`'s re-check, after `Agent.Targets` and every
source have added theirs. A target in another category, **or in none**, is left out, as a
`Targetable` outside a proximity source's radius is, and is not recorded as dropped. Empty, the
default, offers every target.

A source's own cap is applied before this filter. A `ProximityTargetSource` capped at 8 that finds
3 wares nearby leaves 5 targets, not 8. Give the source the same categories so that it caps only
what can be offered. `AgentBehaviour` does that for you (below).

## The same target from two sources

Two sources may both offer one object: a proximity source finds a guard as a `person`, and a source
of the game's own lists the guards a merchant can call. **The same object under the same id is one
target**, as the first source registered it, its category included. **Another object under an
id already offered is still an error**, which makes the decision a pipeline error: two objects
answering to one id cannot both be offered.

"The same object" is reference equality. `ProximityTargetSource` registers each `Targetable`'s
`Transform`, so a source of your own that registers the same `Transform` matches it. A Targetable
an earlier source already offered still counts toward the proximity source's cap.

`ProximityTargetSource`, `ExplicitTargetSource` and `Agent.Targets` follow the rule. In your own
`ITargetSource`, call `into.Offer(id, target, category)`, which registers by it and returns
whether it added the target. `into.Register` still throws for any id already present, as it always
has, so a source written before this rule behaves as it did.

## Only some of a GameObject's actions

`AgentBehaviour` registers the `[AgentAction]` methods of every component on its GameObject, so a
body class every villager shares gives every villager its actions. **Only Actions** lists the ids
this agent registers; the rest are left out, the methods on its components and the rows of
**Handlers** alike. Empty, the default, registers every one.

For an agent built in code, `ActionRegistry.RegisterMethods(owner, assets, only)` registers only
the listed ids of one owner. Null registers all of them, as `RegisterMethods(owner, assets)` does;
an empty list registers none. Every method is still read and checked, so a malformed one throws
whether or not it is listed.

## On `AgentBehaviour`

| Field | What it does |
|---|---|
| **Only Actions** | The action ids this agent registers. Empty registers every one |
| **Target Categories** | The only target categories this agent is offered (`Agent.TargetCategories`). Empty offers every target. When **Targets** names no categories of its own, it is given these when the agent is built, so its cap counts only what can be offered |

The Inspector warns about an id in **Only Actions** that no method on the GameObject and no row of
**Handlers** provides, and about a row of **Handlers** that **Only Actions** leaves out. An action
the profile lists but **Only Actions** leaves out needs no handler.

## Extension points

- **The paired schema** (DR-020) is owed an A/B on the evaluation set before it becomes the default.
  Our set declares no categories, so on it pairing changes only the lists of actions that need a
  target and of `none`; measuring the pairing by category needs categories in the set first.
- **The Targets research area (#120)** owns target selection: a better ordering for the cap, a
  source by component type, and conditions per target (Living Vale's gap 19), which would sit
  beside the category as a second thing an action can require of a target.
