using System;
using System.Collections.Generic;

namespace Agenerela.Providers.ComfyUI
{
    /// <summary>
    /// Where <see cref="ComfyUIProvider"/> finds its server, what it runs there and how long it
    /// waits (DR-025). A plain class, so a provider can be built in code;
    /// <see cref="ComfyUIProviderConfig"/> holds the same as an asset.
    /// </summary>
    /// <remarks>The provider copies these when it is built: change them, build another.</remarks>
    public sealed class ComfyUISettings
    {
        /// <summary>The environment variable <see cref="FromEnvironment"/> reads the server from unless told otherwise.</summary>
        public const string DefaultUrlVariable = "COMFY_URL";

        /// <summary>The environment variable <see cref="FromEnvironment"/> reads the key from unless told otherwise.</summary>
        public const string DefaultKeyVariable = "COMFY_API_TOKEN";

        /// <summary>The server's base address, such as <c>http://192.168.1.4:8289</c>: ComfyUI itself or a gateway in front of it.</summary>
        public string Url = "http://127.0.0.1:8188";

        /// <summary>
        /// A key for a gateway that wants one, sent as <c>Authorization: Bearer &lt;key&gt;</c> on every
        /// request, or empty for a ComfyUI that wants none. Read it from the environment, never from a
        /// committed file (hard rule 1); no message the provider writes contains it.
        /// </summary>
        public string ApiKey = "";

        /// <summary>
        /// The workflow, as JSON: either ComfyUI's "Export (API)" graph, or that graph wrapped with
        /// named parameters as <c>tools/comfyui/workflows/*.json</c> are,
        /// <c>{"params": {"prompt": {"targets": [["27", "text"]]}, ...}, "prompt": {graph}}</c>.
        /// The request's prompt, seed, width and height go into the params named <c>prompt</c>,
        /// <c>seed</c>, <c>width</c> and <c>height</c>; a bare graph takes them only as
        /// <c>node.input</c> parameters.
        /// </summary>
        public string WorkflowJson = "";

        /// <summary>
        /// Parameters every request gets unless it sets the same name itself: a workflow param's name
        /// or <c>node.input</c>. For a server that names a model differently, such as
        /// <c>28.unet_name</c> on the home server.
        /// </summary>
        public Dictionary<string, string> DefaultParameters = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Seconds one picture may take, waiting in the server's queue included, before it fails with
        /// <see cref="ProviderErrorKind.Timeout"/>. 180 unless set: a busy shared card can hold a job
        /// for a couple of minutes.
        /// </summary>
        public float TimeoutSeconds = 180f;

        /// <summary>Seconds between two looks at whether the job is done. 1 unless set.</summary>
        public float PollSeconds = 1f;

        /// <summary>
        /// Whether the provider asks the server to unload its models after each picture, giving the
        /// card back to whatever else uses it. True unless set; for a batch, set false and call
        /// <see cref="ComfyUIProvider.FreeAsync"/> once at the end.
        /// </summary>
        public bool FreeAfterEach = true;

        /// <summary>
        /// Settings with the server and key read from the environment variables
        /// <paramref name="urlVariable"/> and <paramref name="keyVariable"/>.
        /// </summary>
        /// <exception cref="ArgumentException">The URL variable is not set. A key variable that is empty or not set means no key.</exception>
        public static ComfyUISettings FromEnvironment(string workflowJson,
            string urlVariable = DefaultUrlVariable, string keyVariable = DefaultKeyVariable)
        {
            urlVariable = string.IsNullOrWhiteSpace(urlVariable) ? DefaultUrlVariable : urlVariable.Trim();
            string url = Environment.GetEnvironmentVariable(urlVariable);
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException(
                    $"{urlVariable} is not set in the environment this process started in. Set it to the server's address, " +
                    "and the key's variable if it wants one, then restart Unity and Unity Hub.");
            }

            return new ComfyUISettings
            {
                Url = url.Trim(),
                ApiKey = string.IsNullOrWhiteSpace(keyVariable) ? "" : (Environment.GetEnvironmentVariable(keyVariable.Trim()) ?? "").Trim(),
                WorkflowJson = workflowJson ?? "",
            };
        }
    }
}
