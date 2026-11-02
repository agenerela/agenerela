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
| **Provider** | This agent's own `ProviderConfig`. Empty uses the project default. Telemetry records which one resolved |
| **Find Targets Nearby**, **Targets** | On by default. Offers the `Targetable`s around this agent as targets, through a `ProximityTargetSource`. An empty **Origin** means this transform |
| **Notice Nearby**, **Observations** | On by default. Tells the agent the `Description` of the `Targetable`s around it, through a `ProximityObservationSource`. A `Targetable` with no description adds nothing. An empty **Origin** means this transform. The two sources have separate settings, so an agent can notice farther than it can name |
| **Handlers** | Pairs of an action id and an `ActionHandlerBehaviour`, for actions the profile lists that no method on this GameObject runs. The handler can be on another GameObject |

The fields are read once, when the agent is built. After that, change the agent itself:
`guard.Agent` is the plain `Agent`, with its `Execute`, `State`, `Guards`, `Memory`,
`TargetSources` and `MaxObservations`.

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
does not leave the request running. A decision still in the provider's queue leaves it and is
never sent; one already sent has its request stopped. Either way the `await` throws
`OperationCanceledException` and there is no result, which is why the trigger above catches it.
A call made after the destruction ends the same way.

One exception, which is Unity's: it raises that token only for a component that woke. A decision
asked of a guard whose GameObject has never been active is not cancelled by destroying it, so
activate the guard first or pass your own token to `guard.Agent.DecideAsync`.

To also cancel for a reason of the game's own, call
`guard.Agent.DecideAsync(stimulus, options, token)` with a token linked to
`guard.destroyCancellationToken`.

## An agent with no GameObject

The same `Agent`, owned by the game's own class, with nothing in the scene:

```csharp
var country = new Agent(profile, ProviderConfig.Resolve().Provider);
country.Actions.RegisterMethods(handlers);
country.TargetSources.Add(new ExplicitTargetSource().Add("north_border", northBorder));
```

Nothing cancels it when a scene unloads, so pass your own token to `DecideAsync`.
