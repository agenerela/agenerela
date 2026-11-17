using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// Everything a run's numbers depend on, written beside them, because a number whose conditions
    /// are not recorded cannot be compared with another (build plan §4).
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class EvalConditions
    {
        /// <summary>When the run started and finished, in UTC, ISO 8601.</summary>
        [JsonProperty("startedUtc")] public string StartedUtc;
        [JsonProperty("finishedUtc")] public string FinishedUtc;

        [JsonProperty("frameworkVersion")] public string FrameworkVersion;

        [JsonProperty("order"), JsonConverter(typeof(StringEnumConverter))] public EvalArmOrder Order;

        /// <summary>The set file's full path, its agent and how many prompts it holds.</summary>
        [JsonProperty("setPath")] public string SetPath;
        [JsonProperty("setAgent")] public string SetAgent;
        [JsonProperty("prompts")] public int Prompts;

        /// <summary>The last commit that changed the set's folder; null when it is not in git.</summary>
        [JsonProperty("setCommit")] public string SetCommit;

        /// <summary>True when the set's folder has changes git has not committed; null when it is not in git.</summary>
        [JsonProperty("setUncommitted")] public bool? SetUncommitted;

        /// <summary>The <see cref="AgentProfile"/> asset's name.</summary>
        [JsonProperty("profile")] public string Profile;

        /// <summary>The graphics device Unity reports, and its memory in MB. Not necessarily the one the model is on.</summary>
        [JsonProperty("graphicsDevice")] public string GraphicsDevice;
        [JsonProperty("graphicsMemoryMB")] public int GraphicsMemoryMB;

        /// <summary>How the set's targets reached the agent and the model.</summary>
        [JsonProperty("targets")] public string Targets;

        /// <summary>How each prompt's state was told to the model.</summary>
        [JsonProperty("state")] public string State;

        [JsonProperty("arms")] public List<EvalArmConditions> Arms = new List<EvalArmConditions>();

        /// <summary>
        /// Every condition that did not hold as it should, such as a model not fully on the GPU.
        /// Empty for a clean run.
        /// </summary>
        [JsonProperty("warnings")] public List<string> Warnings = new List<string>();
    }
}
