using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Agenerela.Evaluation
{
    /// <summary>How every file of a prompt set is read and written, so they all look alike on disk.</summary>
    internal static class PromptSetJson
    {
        // DateParseHandling.None: a stimulus or a state value that looks like a date stays the
        // string it was, so a file round-trips unchanged.
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Decimal,
        };

        // No byte-order mark, so a diff of a saved file shows only what changed.
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        internal static T Read<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, Settings);
        }

        /// <summary>Indented two spaces, with LF line ends and a final newline on every machine.</summary>
        internal static string Write(object value)
        {
            var text = new StringWriter { NewLine = "\n" };
            using (var writer = new JsonTextWriter(text) { Formatting = Formatting.Indented, Indentation = 2 })
            {
                JsonSerializer.Create(Settings).Serialize(writer, value);
            }

            return text + "\n";
        }

        internal static string ReadFile(string path) => File.ReadAllText(path, Utf8);

        internal static void WriteFile(string path, object value)
        {
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(path, Write(value), Utf8);
        }

        /// <summary>A category file: <c>{ "category": ..., "prompts": [ ... ] }</c>.</summary>
        [JsonObject(MemberSerialization.OptIn)]
        internal sealed class CategoryFile
        {
            [JsonProperty("category")] public string Category;
            [JsonProperty("prompts")] public List<EvalPrompt> Prompts = new List<EvalPrompt>();
            [JsonExtensionData] public IDictionary<string, JToken> Extra = new Dictionary<string, JToken>();
        }
    }
}
