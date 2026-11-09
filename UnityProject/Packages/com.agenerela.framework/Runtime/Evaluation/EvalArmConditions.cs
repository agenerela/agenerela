using System.Collections.Generic;
using Newtonsoft.Json;

namespace Agenerela.Evaluation
{
    /// <summary>What one arm ran with: its config, provider, model and options, and what was resident.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class EvalArmConditions
    {
        [JsonProperty("name")] public string Name;

        /// <summary>The provider config asset's name, such as <c>OllamaLocal</c>.</summary>
        [JsonProperty("providerConfig")] public string ProviderConfig;

        /// <summary>The provider's own name, such as <c>ollama</c>.</summary>
        [JsonProperty("provider")] public string Provider;

        /// <summary>The model the provider asks for, when it can say; otherwise the first one that answered.</summary>
        [JsonProperty("model")] public string Model;

        /// <summary>Every model the provider reported answering, in the order first seen.</summary>
        [JsonProperty("modelsAnswered")] public List<string> ModelsAnswered = new List<string>();

        /// <summary>The server's version, such as Ollama's <c>0.40.14</c>; "not checked" for a provider that cannot say.</summary>
        [JsonProperty("providerVersion")] public string ProviderVersion;

        // The arm's options, as every call started from them.
        [JsonProperty("checkTarget")] public bool CheckTarget;
        [JsonProperty("askForRefusalLine")] public bool AskForRefusalLine;
        [JsonProperty("idleExampleStimulus")] public string IdleExampleStimulus;
        [JsonProperty("listActionsAndTargets")] public bool ListActionsAndTargets;
        [JsonProperty("maxListedTargets")] public int MaxListedTargets;
        [JsonProperty("counterExamples")] public bool CounterExamples;
        [JsonProperty("pairTargetsWithActions")] public bool PairTargetsWithActions;
        [JsonProperty("priority")] public string Priority;
        [JsonProperty("observations")] public List<string> Observations = new List<string>();

        /// <summary>The throwaway decisions sent before the arm's first prompt, which no count includes.</summary>
        [JsonProperty("warmUpRequests")] public int WarmUpRequests;

        /// <summary>What was resident before the warm-up, after it, and when the arm's last prompt was answered.</summary>
        [JsonProperty("residency")] public List<ResidencyReading> Residency = new List<ResidencyReading>();

        /// <summary>Whether the model was unloaded after the arm, as a sequential run does; null when nobody tried.</summary>
        [JsonProperty("unloaded")] public bool? Unloaded;
    }
}
