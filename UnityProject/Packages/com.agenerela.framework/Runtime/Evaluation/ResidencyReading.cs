using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// Which models a provider's server held in memory at one moment, and how much of each was in
    /// GPU memory: hard rule 3 and build plan §4's VRAM check, recorded rather than assumed.
    /// </summary>
    /// <remarks>
    /// Read from Ollama's <c>/api/ps</c>. For any other provider nothing is read,
    /// <see cref="Checked"/> is false, and <see cref="Note"/> says so.
    /// </remarks>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class ResidencyReading
    {
        /// <summary>When it was read: before the warm-up, after it, or at the arm's end.</summary>
        [JsonProperty("when")] public string When;

        /// <summary>False when the provider cannot say what is resident.</summary>
        [JsonProperty("checked")] public bool Checked;

        /// <summary>Why it was not checked, or what was wrong with what it found; null when nothing.</summary>
        [JsonProperty("note")] public string Note;

        [JsonProperty("models")] public List<ResidentModel> Models = new List<ResidentModel>();

        /// <summary>The resident model named <paramref name="model"/>, or null.</summary>
        public ResidentModel Find(string model) =>
            Models.FirstOrDefault(m => Providers.OllamaProvider.SameModel(m.Name, model));
    }
}
