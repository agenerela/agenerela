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
        /// <summary>
        /// The arm's <see cref="DecideOptions.TargetGroundingThreshold"/> (#244); null in a report
        /// written before it existed, which ran <see cref="TargetGroundingGuard.DefaultThreshold"/>.
        /// </summary>
        [JsonProperty("targetGroundingThreshold", NullValueHandling = NullValueHandling.Ignore)] public float? TargetGroundingThreshold;
        /// <summary>
        /// How many alternatives an Ollama arm asked for at each token
        /// (<see cref="Agenerela.Providers.OllamaSettings.TokenAlternatives"/>, #244); null for another
        /// provider, and in a report written before it existed, which asked for 5.
        /// </summary>
        [JsonProperty("tokenAlternatives", NullValueHandling = NullValueHandling.Ignore)] public int? TokenAlternatives;
        [JsonProperty("askForRefusalLine")] public bool AskForRefusalLine;
        [JsonProperty("idleExampleStimulus")] public string IdleExampleStimulus;
        [JsonProperty("listActionsAndTargets")] public bool ListActionsAndTargets;
        [JsonProperty("maxListedTargets")] public int MaxListedTargets;
        [JsonProperty("counterExamples")] public bool CounterExamples;
        [JsonProperty("idleExampleStatement")] public string IdleExampleStatement;
        [JsonProperty("exampleStatements")] public bool ExampleStatements;
        [JsonProperty("exampleTargetFallback")] public bool ExampleTargetFallback;
        [JsonProperty("pairTargetsWithActions")] public bool PairTargetsWithActions;
        /// <summary>
        /// How the arm's prompt said which targets each action may take, the name of a
        /// <see cref="Agenerela.LegalTargetListing"/> value (#151); null in a report written before it
        /// existed, which listed none.
        /// </summary>
        [JsonProperty("legalTargets")] public string LegalTargets;
        /// <summary>
        /// How the arm's prompt introduced the identity's statement rules, the name of a
        /// <see cref="Agenerela.StatementRulesWording"/> value (#244 item 8); null in a report written
        /// before it existed, which used <c>YourStatement</c>.
        /// </summary>
        [JsonProperty("statementRulesWording")] public string StatementRulesWording;
        /// <summary>The arm's own <see cref="EvalArm.StatementRules"/>, in place of the profile's; null when it kept the profile's.</summary>
        [JsonProperty("statementRules", NullValueHandling = NullValueHandling.Ignore)] public string StatementRules;
        /// <summary>The arm's own target legality (<see cref="EvalArm.Legality"/>, #151); null when it ran the set as it is.</summary>
        [JsonProperty("legality", NullValueHandling = NullValueHandling.Ignore)] public EvalLegality Legality;
        /// <summary>
        /// Where the arm put each prompt's own observations, the name of a
        /// <see cref="Agenerela.ObservationPlacement"/> value (#151); null in a report written before
        /// it existed, which ran <c>AfterSources</c>.
        /// </summary>
        [JsonProperty("observationPlacement")] public string ObservationPlacement;
        /// <summary>
        /// Whether the arm sent its standing lines, the arm's, the state's and the targets', as the
        /// call's own observations rather than through an observation source
        /// (<see cref="EvalArm.StandingLinesAsCallObservations"/>, #151); false in a report written
        /// before it existed.
        /// </summary>
        [JsonProperty("standingLinesAsCallObservations")] public bool StandingLinesAsCallObservations;
        [JsonProperty("priority")] public string Priority;
        [JsonProperty("observations")] public List<string> Observations = new List<string>();
        /// <summary>The arm's <see cref="EvalArm.Knowledge"/>, added to the profile's; empty in a report written before it existed.</summary>
        [JsonProperty("knowledge")] public List<string> Knowledge = new List<string>();

        /// <summary>
        /// The arm's <see cref="EvalArm.MaxStatementLength"/>, above which a statement is
        /// <see cref="ReplyFlag.TooLong"/>; 0 in a report written before reply checks existed.
        /// </summary>
        [JsonProperty("maxStatementLength")] public int MaxStatementLength;

        /// <summary>The throwaway decisions sent before the arm's first prompt, which no count includes.</summary>
        [JsonProperty("warmUpRequests")] public int WarmUpRequests;

        /// <summary>What was resident before the warm-up, after it, and when the arm's last prompt was answered.</summary>
        [JsonProperty("residency")] public List<ResidencyReading> Residency = new List<ResidencyReading>();

        /// <summary>Whether the model was unloaded after the arm, as a sequential run does; null when nobody tried.</summary>
        [JsonProperty("unloaded")] public bool? Unloaded;
    }
}
