# Getting started

From an empty scene to an agent that decides, with the New Agent wizard or by hand.

## Before you start

- **Ollama running on this machine**, from [ollama.com](https://ollama.com).
- **The model pulled:** `ollama pull qwen3.5:4b`. It is the one the wizard sets up. Without it,
  the first decision fails with `ModelNotFound`.

## With the wizard

1. In the Hierarchy, select the GameObject that will be the agent, or nothing for a new one.
2. **Assets → Create → Agenerela → New Agent.**
3. Give it a name, a role and a folder. Tick the actions it needs, and add your own for anything
   else.
4. Press **Create agent**. Once Unity has compiled, the GameObject has your script and
   **Agent Behaviour**, with the profile filled in.

What it writes:

| File | What it is |
|---|---|
| `GateGuard.cs` | One method for each action. Fill in each TODO with what it does in your game |
| `GateGuardProfile.asset` | Who the agent is: its name and role |
| `Assets/Agenerela/OllamaLocal.asset` | Only when the project had no AI model. It becomes the project default, for every agent |
| `Actions/move_to.asset`, … | Only with **Also save every action as an Action asset**. An action's wording in a file, which can change without a recompile. The script still runs it |

The profile's **Actions** list is empty unless you ticked that box. That is normal: the actions
are in the script.

## Without the wizard

1. **Create → Agenerela → Agent Profile**, and fill in its **Identity**.
2. Write a script with an `[AgentAction]` method for each action:

   ```csharp
   using Agenerela;
   using UnityEngine;

   public class GateGuard : MonoBehaviour
   {
       [AgentAction("speak_only", "Answer without moving.", TakesNoTarget = true)]
       void SpeakOnly(AgentDecision decision) => Debug.Log($"{name}: speak_only", this);
   }
   ```

3. Add **Agent Behaviour** and the script to the GameObject, and drag the profile into
   **Profile**.
4. **Create → Agenerela → Ollama Provider**, and set it as **Default Provider** in
   **Project Settings → Agenerela**.

## Ask it to decide

Add this to the same GameObject and press Play:

```csharp
using Agenerela;
using UnityEngine;

public class AskOnPlay : MonoBehaviour
{
    async void Start()
    {
        AgentBehaviour agent = GetComponent<AgentBehaviour>();
        DecisionResult result = await agent.DecideAsync("Anything happening today?");
        agent.Agent.Execute(result);
    }
}
```

The Console shows the GameObject's name and the method the agent chose, such as
`Gate guard: speak_only, target no_target`. In
a game, your own code decides when to ask: a player's line, an event, a turn
([agents-in-a-scene.md](agents-in-a-scene.md)).

## Give it something to act on

Actions such as `move_to` need a target. Add a **Targetable** component to each object the agent
may name, with an **Id** such as `bridge`. Agent Behaviour offers the ones nearby
([targets.md](targets.md)).

## Which AI model it uses

An agent with no **Provider** of its own uses the active settings profile's (Development in the
Editor), and otherwise the **Default Provider** in **Project Settings → Agenerela**
([providers.md](providers.md#settings-profiles-development-and-production)).
