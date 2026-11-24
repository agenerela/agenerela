using System;
using System.Collections.Generic;
using UnityEngine;

namespace Agenerela.Providers.ComfyUI
{
    /// <summary>
    /// A <see cref="ComfyUIProvider"/> as an asset (DR-025): the workflow, the names of the two
    /// environment variables that hold the server and the key, the timings and any default
    /// parameters. Made with <b>Create → Agenerela → ComfyUI Image Provider</b>.
    /// </summary>
    /// <remarks>
    /// Nothing here is secret: the asset stores where to read the server and key from, never the
    /// key (hard rule 1). The Editor reads its environment once, when it starts, so set the variables
    /// before starting Unity and Unity Hub.
    /// </remarks>
    [CreateAssetMenu(menuName = "Agenerela/ComfyUI Image Provider", fileName = "ComfyUIImages")]
    public sealed class ComfyUIProviderConfig : ScriptableObject
    {
        private static readonly ComfyUISettings Defaults = new ComfyUISettings();

        /// <summary>One default parameter: a workflow param's name or <c>node.input</c>, and its value.</summary>
        [Serializable]
        public struct Parameter
        {
            [Tooltip("A workflow param's name, or node.input such as 28.unet_name.")]
            public string Name;

            [Tooltip("Its value, written as the workflow's own value there is typed.")]
            public string Value;
        }

        [Tooltip("The workflow: ComfyUI's Export (API) JSON, or one wrapped with named params as tools/comfyui/workflows/*.json are.")]
        public TextAsset Workflow;

        [Tooltip("The environment variable holding the server's address, such as http://192.168.1.4:8289.")]
        public string UrlVariable = ComfyUISettings.DefaultUrlVariable;

        [Tooltip("The environment variable holding the key, sent in the Authorization header. Empty for a ComfyUI that wants none. The key is never stored here.")]
        public string KeyVariable = ComfyUISettings.DefaultKeyVariable;

        [Min(1f)]
        [Tooltip("Seconds one picture may take, waiting in the server's queue included.")]
        public float TimeoutSeconds = Defaults.TimeoutSeconds;

        [Range(0f, 10f)]
        [Tooltip("Seconds between two looks at whether the job is done.")]
        public float PollSeconds = Defaults.PollSeconds;

        [Tooltip("Unload the server's models after each picture, giving the card back. Off for a batch, which then calls FreeAsync once.")]
        public bool FreeAfterEach = Defaults.FreeAfterEach;

        [Tooltip("Parameters every request gets unless it sets them, such as 28.unet_name for a server whose model file is named differently.")]
        public List<Parameter> DefaultParameters = new List<Parameter>();

        /// <summary>This asset's values as settings, the server and key read from the environment now.</summary>
        /// <exception cref="ArgumentException">There is no workflow, or <see cref="UrlVariable"/> is not set.</exception>
        public ComfyUISettings ToSettings()
        {
            if (Workflow == null)
            {
                throw new ArgumentException($"ComfyUI image provider '{name}' has no workflow. Set its Workflow to the JSON file.");
            }

            var settings = ComfyUISettings.FromEnvironment(Workflow.text, UrlVariable, KeyVariable ?? "");

            settings.TimeoutSeconds = TimeoutSeconds;
            settings.PollSeconds = PollSeconds;
            settings.FreeAfterEach = FreeAfterEach;
            foreach (var p in DefaultParameters)
            {
                if (!string.IsNullOrWhiteSpace(p.Name))
                {
                    settings.DefaultParameters[p.Name.Trim()] = p.Value ?? "";
                }
            }

            return settings;
        }

        /// <summary>A new provider from <see cref="ToSettings"/>.</summary>
        /// <exception cref="ArgumentException">As <see cref="ToSettings"/>, or a setting <see cref="ComfyUIProvider"/> refuses.</exception>
        public ComfyUIProvider CreateProvider() => new ComfyUIProvider(ToSettings());
    }
}
