# Agents in a scene — `AgentBehaviour`

A guard, a shopkeeper or a companion is a thing in the world, so Unity should own its agent.
`AgentBehaviour` is the component that does that: put it on the GameObject, assign a profile, and
ask it to decide. There is no manager object in the scene, ever.

An agent that is not a thing in the world, such as a country, a colony or a director, has no
Transform to put a component on. Its own class owns a plain `Agent`: see the end of this page.

## Setting one up

1. Create the profile: **Create → Agenerela → Agent Profile**, with its identity and the action
   assets it lists.
2. Add **AgentBehaviour** to the GameObject and drag the profile into **Profile**.
3. Leave **Provider** empty to use the default in **Project Settings → Agenerela**, or assign a
   config for this agent alone ([providers.md](providers.md)).
4. Give the actions something to run them, on the same GameObject:

```csharp
public class GuardActions : MonoBehaviour
{
    [AgentAction("move_to", "Walk to a named place.", RequiresTarget = true)]
    void MoveTo(Transform place) => GetComponent<NavMeshAgent>().SetDestination(place.position);
}
```

5. Put a `Targetable`, with an id, on each object the guard may name.

The only code left is the developer's own trigger:

```csharp
public class GuardEars : MonoBehaviour
{
    AgentBehaviour guard;

    void Awake() => guard = GetComponent<AgentBehaviour>();

    public async void OnPlayerSaid(string line)
    {
        try
        {
            DecisionResult result = await guard.DecideAsync(line);
            guard.Agent.Execute(result);
        }
        catch (OperationCanceledException)
        {
            // The guard was destroyed while the model was thinking.
        }
    }
}
```

## What each field does

| Field | What it does |
|---|---|
| **Profile** | Who this agent is and what it can do. Ten guards can share one |
| **Provider** | This agent's own `ProviderConfig`. Empty uses the project default. Telemetry records which one resolved. Read once, when the agent is built: to switch at runtime, set `Agent.Provider` ([providers.md](providers.md#switching-an-agents-provider)) |
| **Find Targets Nearby**, **Targets** | On by default. Offers the `Targetable`s around this agent as targets, through a `ProximityTargetSource`. An empty **Origin** means this transform |
| **Notice Nearby**, **Observations** | On by default. Tells the agent the `Description` of the `Targetable`s around it, through a `ProximityObservationSource`. A `Targetable` with no description adds nothing. An empty **Origin** means this transform. The two sources have separate settings, so an agent can notice farther than it can name |
| **Handlers** | Pairs of an action id and an `ActionHandlerBehaviour`, for actions the profile lists that no method on this GameObject runs. The handler can be on another GameObject |
| **Only Actions** | The action ids this agent registers, of the methods on this GameObject and the rows of **Handlers**. Empty, the default, registers every one, so a body class several agents share can carry actions only some of them take ([targets.md](targets.md)) |
| **Target Categories** | The only target categories this agent is offered, from any source (`Agent.TargetCategories`). Empty, the default, offers every target. When **Targets** names no categories of its own it is given these, so its cap counts only what can be offered ([targets.md](targets.md)) |

The fields are read once, when the agent is built. After that, change the agent itself:
`guard.Agent` is the plain `Agent`, with its `Execute`, `State`, `Guards`, `Memory`,
`TargetSources`, `TargetCategories`, `ObservationSources` and `MaxObservations`.

## Where an action's handler comes from

When the agent is built, every component on this GameObject with `[AgentAction]` methods is
registered through `ActionRegistry.RegisterMethods`, with the profile's action assets. So an
asset with the same id as a method wins the action's wording, and the method still runs it
(DR-011). Then each row of **Handlers** is bound with `Agent.Bind`.

An action has exactly one handler. A row for an action a method already runs stops the agent
being built, and so does a row naming an action the profile does not list.

An action the profile lists that nothing runs is never offered to the model, because nothing
could carry it out.

## What the Inspector warns about

In the Console, each time the component changes outside Play mode:

- **Profile is empty**.
- **No handler for** the profile's actions that have none, by name.
- A row of **Handlers** with no handler, naming an action the profile does not list, or binding
  an action that already has a handler.
- A malformed `[AgentAction]` method on this GameObject.
- Two components on this GameObject declaring one action.
- An id in **Only Actions** that no method on this GameObject and no row of **Handlers**
  provides, and a row of **Handlers** that **Only Actions** leaves out. An action the profile lists
  but **Only Actions** leaves out needs no handler.

`AgentBehaviour.Validate(out string problem)` runs the same checks from code. An empty
**Provider** is not one of them: it is the normal case.

## When the agent is built

In `Awake`, or by the first read of `Agent` or call of `DecideAsync` if one comes first. So a
sibling's `Awake` can use the agent whatever the script order, and code can add the component and
then set the profile:

```csharp
var guard = gameObject.AddComponent<AgentBehaviour>();
guard.Profile = profile;
await guard.DecideAsync("Halt!");
```

If the agent cannot be built, say with no provider anywhere, `Awake` logs the reason as an error,
naming the GameObject. Reading `Agent` and calling `DecideAsync` throw
`InvalidOperationException` with the same reason. Nothing is kept, so the next read tries again.
A missing profile is the one thing `Awake` stays silent about, because code may be about to set
it; the Inspector warns about it, and reading `Agent` throws.

## When the GameObject is destroyed

`DecideAsync` passes the component's `destroyCancellationToken`, so a guard destroyed mid-request
does not leave the request running. A decision still in the provider's queue, or waiting in the
agent's `Backlog`, leaves it and is never sent; one already sent has its request stopped. Either way the `await` throws
`OperationCanceledException` and there is no result, which is why the trigger above catches it.
A call made after the destruction ends the same way.

One exception, which is Unity's: it raises that token only for a component that woke. A decision
asked of a guard whose GameObject has never been active is not cancelled by destroying it, so
activate the guard first or pass your own token to `guard.Agent.DecideAsync`.

To also cancel for a reason of the game's own, call
`guard.Agent.DecideAsync(stimulus, options, token)` with a token linked to
`guard.destroyCancellationToken`.

## Starting the agent afresh

To start a guard over without destroying anything, after a scene event or a new stage, call
`guard.ResetAgent()`. It cancels every decision the agent has waiting or under way, and no other
agent's, then drops the decisions held for memory and clears its memory. The GameObject, the
component and the agent itself stay, with the guards, sources and state the game added to it.
Awaiting a cancelled decision throws `OperationCanceledException`, as for a destruction.
[executing-and-memory.md](executing-and-memory.md#starting-an-agent-afresh-reset) has the whole
of it, including how to tell a reset from your own cancellation.

## An agent with no GameObject

The same `Agent`, owned by the game's own class, with nothing in the scene:

```csharp
var country = new Agent(profile, ProviderConfig.Resolve().Provider);
country.Actions.RegisterMethods(handlers, profile.Actions);   // the assets' wording wins, as above
country.TargetSources.Add(new ExplicitTargetSource().Add("north_border", northBorder));
```

Nothing cancels it when a scene unloads, so pass your own token to `DecideAsync`.

## Watching an agent from a tool

Two seams let a tool, such as the Decision Log or one of your own, see an agent without the game
passing anything on (#166).

**The decision feed.** `DecisionFeed.Recorded` is raised once when `DecideAsync` returns a result,
pipeline errors and guard-stopped decisions included, and once each time `Execute` returns, saying
whether the handler ran or the re-check refused. Each `DecisionFeedEntry` carries the `Agent`, the
`DecisionResult`, the `Stage`, for `Executed` the `Execution` it reported, the time in UTC and a
`Sequence` number. It covers every agent, a country with no GameObject as well as a guard. With no
subscriber it costs a null check, and a subscriber that throws is logged and changes nothing.

```csharp
DecisionFeed.Recorded += entry =>
{
    var guard = AgentBehaviour.Find(entry.Agent);          // null for an agent with no GameObject
    Debug.Log($"{entry.Sequence} {entry.Stage} {(guard != null ? guard.name : entry.Agent.Identity.Name)}");
};
```

A call that ends cancelled throws instead of returning, so it is never in the feed, and neither is a
call that `ResetAgent` cancels. A decision held for memory that the reset drops already has its
`Decided` entry, and gets no other. An `Execute` whose handler throws has no entry. With the domain
reload off, every subscriber is removed and the sequence starts again as Play mode starts, so
subscribe again then.

`AgentBehaviour.Find(agent)` gives the component that built an agent, or null. It never builds one;
`guard.HasAgent` says whether the component has built its agent yet, since reading `guard.Agent`
builds it.

**The preview.** `agent.Preview(stimulus, options)` returns what `DecideAsync` would send next with
the same stimulus and options, without sending it: the actions offered and why the others are not,
the targets of each source with what its cap dropped, the observation lines of each source with
what was left out, the history the memory would send, and the `DecisionRequest` itself. It runs
the same code as a decision, so it never drifts from it. It never calls the provider or remembers
anything, but every source, availability check and `IMemoryStrategy.Recall` runs once per preview:
a source that costs something, such as one searching the scene, costs it again each time.
