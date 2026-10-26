using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Agenerela;
using Agenerela.Providers;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// The Phase 2 sandbox (#94): a village guard a person can watch decide. Type a stimulus, choose
/// its label and priority, press Decide, read the decision, the statement and the telemetry, then
/// press Execute to carry it out.
/// </summary>
/// <remarks>
/// <para>
/// The agent is built in code (pattern B), with the same five actions and three targets as the
/// <c>RequiresOllama</c> PlayMode tests, so the scene needs neither a provider asset (#92) nor an
/// <c>AgentBehaviour</c> (#93). The handlers only move this capsule and log what they did.
/// </para>
/// <para>
/// The model and endpoint are the fields below, or, when a field is empty, the
/// <c>AGENERELA_OLLAMA_MODEL</c> and <c>AGENERELA_OLLAMA_URL</c> environment variables the tests
/// read, then <c>qwen3.5:2b</c> at <c>http://127.0.0.1:11434</c>. Dev project only: never shipped.
/// </para>
/// </remarks>
public sealed class SandboxGuard : MonoBehaviour
{
    [Serializable]
    public sealed class Place
    {
        public string Id;
        public Transform Transform;
    }

    [Tooltip("Empty uses AGENERELA_OLLAMA_MODEL, then qwen3.5:2b.")]
    [SerializeField] private string model = "";

    [Tooltip("Empty uses AGENERELA_OLLAMA_URL, then http://127.0.0.1:11434. Never localhost: on Windows it adds about 2 s a request.")]
    [SerializeField] private string endpoint = "";

    [Tooltip("The targets the guard can be told about, by id.")]
    [SerializeField] private List<Place> places = new List<Place>();

    [Tooltip("Where follow_player walks to: the player's stand-in.")]
    [SerializeField] private Transform player;

    private Agent guard;
    private string stimulus = "Follow me.";
    private string label = "Player";
    private int priority;
    private bool thinking;
    private DecisionResult result;
    private double wallSeconds;
    private string executed = "";
    private Vector2 scroll;

    private void Awake()
    {
        var settings = new OllamaSettings
        {
            Model = Pick(model, "AGENERELA_OLLAMA_MODEL", "qwen3.5:2b"),
            Endpoint = Pick(endpoint, "AGENERELA_OLLAMA_URL", "http://127.0.0.1:11434"),
        };

        guard = new Agent(new AgentIdentity
        {
            Name = "Village Guard",
            Role = "a guard at the town gate",
            Personality = "Loyal, brief, a little suspicious of strangers.",
            Goals = "keep the gate safe; help the player when asked.",
        }, new List<ActionDefinition>
        {
            Action("follow_player", "Walk alongside the player.", "Accompany me on my rounds.", false),
            Action("stop_following", "Stop walking alongside the player.", "You can return to your post now.", false),
            Action("move_to", "Walk to a named place.", "Proceed to the {0}.", true, "tower"),
            Action("attack_target", "Attack a named target.", "Engage the {0}.", true, "training_dummy"),
            Action("pick_up_item", "Pick up a named item.", "Fetch the {0} for me.", true, "sword"),
        }, new OllamaProvider(settings));

        guard.State["isFollowing"] = false;
        guard.Bind("follow_player", new DelegateActionHandler(ctx => !Following(ctx), (ctx, d) =>
        {
            guard.State["isFollowing"] = true;
            if (player != null)
            {
                WalkTo(player.position, "the player");
            }

            Debug.Log("[Sandbox] follow_player: the guard is now following the player.");
        }));
        guard.Bind("stop_following", new DelegateActionHandler(Following, (ctx, d) =>
        {
            guard.State["isFollowing"] = false;
            Debug.Log("[Sandbox] stop_following: the guard stays where it is.");
        }));
        guard.Bind("move_to", new DelegateActionHandler(ctx => true, (ctx, d) =>
            Debug.Log($"[Sandbox] move_to: walked to {WalkTo(ctx, d)}.")));
        guard.Bind("attack_target", new DelegateActionHandler(ctx => true, (ctx, d) =>
            Debug.Log($"[Sandbox] attack_target: walked to {WalkTo(ctx, d)} and attacked it.")));
        guard.Bind("pick_up_item", new DelegateActionHandler(ctx => true, (ctx, d) =>
            Debug.Log($"[Sandbox] pick_up_item: walked to {WalkTo(ctx, d)} and picked it up.")));

        foreach (var place in places)
        {
            if (place != null && !string.IsNullOrWhiteSpace(place.Id) && place.Transform != null)
            {
                guard.Targets.Register(place.Id, place.Transform);
            }
        }

        Debug.Log($"[Sandbox] Guard asks {settings.Model} at {settings.Endpoint}, with targets: "
                + string.Join(", ", guard.Targets.Ids) + ".");
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 420, Screen.height - 20), GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);

        GUILayout.Label("Stimulus");
        stimulus = GUILayout.TextField(stimulus);
        GUILayout.Label("Label");
        label = GUILayout.TextField(label);
        GUILayout.Label("Priority");
        priority = GUILayout.Toolbar(priority, Enum.GetNames(typeof(DecisionPriority)));

        GUI.enabled = !thinking && !string.IsNullOrWhiteSpace(label);
        if (GUILayout.Button(thinking ? "Deciding..." : "Decide"))
        {
            Decide();
        }

        GUI.enabled = !thinking && result != null && result.Decision != null;
        if (GUILayout.Button("Execute"))
        {
            executed = guard.Execute(result) ? "Carried out." : "Refused: see the console for why.";
        }

        GUI.enabled = true;
        GUILayout.Label($"Following: {guard.State["isFollowing"]}    {executed}");
        GUILayout.Space(8);
        GUILayout.Label(Describe(), GUI.skin.textArea);

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private async void Decide()
    {
        thinking = true;
        executed = "";
        var options = new DecideOptions
        {
            StimulusLabel = label,
            IdleExampleStimulus = "You seem well rested today.",
            Priority = (DecisionPriority)priority,
        };

        try
        {
            var clock = Stopwatch.StartNew();
            result = await guard.DecideAsync(stimulus, options, destroyCancellationToken);
            wallSeconds = clock.Elapsed.TotalSeconds;
        }
        catch (OperationCanceledException)
        {
            // The scene is closing.
        }
        finally
        {
            thinking = false;
        }
    }

    private string Describe()
    {
        if (result == null)
        {
            return "No decision yet.";
        }

        var t = result.Telemetry;
        var text = new StringBuilder();
        if (result.Decision != null)
        {
            text.AppendLine($"action:     {result.Decision.ActionId}");
            text.AppendLine($"target:     {result.Decision.TargetId}");
            text.AppendLine($"statement:  {result.Decision.Statement}");
        }
        else
        {
            text.AppendLine($"No decision: {t.ErrorKind}");
            text.AppendLine(t.ErrorMessage);
        }

        text.AppendLine();
        text.AppendLine($"wall clock:      {wallSeconds:F3} s");
        text.AppendLine($"latency:         {t.LatencySeconds:F3} s");
        text.AppendLine($"queue wait:      {t.QueueWaitSeconds:F3} s ({t.QueuedAhead} ahead)");
        text.AppendLine($"priority:        {t.Priority}");
        text.AppendLine($"provider:        {t.ProviderName}");
        text.AppendLine($"model:           {t.Model}");
        text.AppendLine($"framework:       {t.FrameworkVersion}");
        text.AppendLine($"tokens:          {t.PromptTokens} prompt, {t.CompletionTokens} completion");
        text.AppendLine($"history:         {t.HistoryTurnsSent} sent, {t.HistoryTurnsDropped} dropped, {t.HistoryTurnsForgotten} forgotten");
        text.AppendLine($"targets dropped: {(t.TargetsDropped.Count == 0 ? "none" : string.Join(", ", t.TargetsDropped))}");
        text.Append($"outcome:         {(t.Outcome == null ? "decision" : t.Outcome.ToString())}");
        return text.ToString();
    }

    private string WalkTo(AgentContext ctx, AgentDecision decision)
    {
        if (ctx.Targets.TryGet(decision.TargetId, out var target) && target is Transform place)
        {
            return WalkTo(place.position, decision.TargetId);
        }

        return $"nowhere ({decision.TargetId} has no place in the scene)";
    }

    // Stops a little short, on the guard's side, so the capsule stands next to the target.
    private string WalkTo(Vector3 position, string name)
    {
        var from = transform.position;
        var flat = new Vector3(position.x, from.y, position.z);
        var offset = from - flat;
        offset = offset.sqrMagnitude < 0.01f ? Vector3.back : offset.normalized;
        transform.position = flat + offset * 1.5f;
        return name;
    }

    private static bool Following(AgentContext ctx)
    {
        return (bool)ctx.State["isFollowing"];
    }

    private static string Pick(string field, string variable, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(field))
        {
            return field.Trim();
        }

        string value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static ActionDefinition Action(string id, string description, string exampleStimulus,
                                           bool requiresTarget, params string[] preferredTargets)
    {
        return new ActionDefinition
        {
            Id = id,
            Description = description,
            RequiresTarget = requiresTarget,
            ExampleStimulus = exampleStimulus,
            PreferredExampleTargets = preferredTargets,
        };
    }
}
