using System;
using System.Collections.Generic;
using UnityEngine;

namespace Agenerela.Providers.OpenAIImages
{
    /// <summary>
    /// An <see cref="OpenAIImagesProvider"/>'s settings as an asset (DR-028), so Development and
    /// Production can point at different servers. It stores the <i>names</i> of the environment
    /// variables that hold the address and the key, never the key (hard rule 1).
    /// </summary>
    [CreateAssetMenu(menuName = "Agenerela/OpenAI-compatible Image Provider", fileName = "OpenAIImages")]
    public sealed class OpenAIImagesProviderConfig : ScriptableObject
    {
        private static readonly OpenAIImagesSettings Defaults = new OpenAIImagesSettings();

        /// <summary>One default parameter: a name and its value.</summary>
        [Serializable]
        public struct Parameter
        {
            [Tooltip("A body field such as quality, or extra.<path> such as extra.sample_params.sample_steps for sd-server.")]
            public string Name;

            [Tooltip("Its value: true or false, a number, or text.")]
            public string Value;
        }

        [Tooltip("The API's base address up to and including /v1, used when the URL variable below is empty or not set. " +
                 "sd-server: http://127.0.0.1:1234/v1 (its --listen-port).")]
        public string Url = Defaults.Url;

        [Tooltip("An environment variable holding the address instead, such as AGENERELA_IMAGES_URL. Named but not set, " +
                 "the provider uses Url above and sends no key, so a key never reaches a server it was not meant for.")]
        public string UrlVariable = "";

        [Tooltip("The environment variable holding the key, sent in the Authorization header. Empty for a server that wants none.")]
        public string KeyVariable = OpenAIImagesSettings.DefaultKeyVariable;

        [Tooltip("The model field, such as gpt-image-1. Empty for sd-server, which runs the model it was started with.")]
        public string Model = "";

        [Tooltip("Write the seed, strength, negative_prompt and extra.* parameters into sd-server's <sd_cpp_extra_args> block. " +
                 "On for sd-server only.")]
        public bool SdCppExtraArgs;

        [Tooltip("The server edits pictures through /images/edits. Off for LocalAI.")]
        public bool Edits = Defaults.Edits;

        [Tooltip("An edit may carry a mask.")]
        public bool Masks = Defaults.Masks;

        [Min(1)]
        [Tooltip("The most input pictures one edit takes.")]
        public int MaxImages = Defaults.MaxImages;

        [Min(1)]
        [Tooltip("Width and height must be multiples of this.")]
        public int SizeStep = Defaults.SizeStep;

        [Min(1)]
        [Tooltip("The longest side the server makes.")]
        public int MaxSide = Defaults.MaxSide;

        [Min(1f)]
        [Tooltip("Seconds one attempt may take.")]
        public float TimeoutSeconds = Defaults.TimeoutSeconds;

        [Tooltip("How long a busy server (HTTP 429 or 503) is waited out.")]
        public RetryPolicy Retry = new RetryPolicy();

        [Tooltip("Parameters every request gets unless it sets them.")]
        public List<Parameter> DefaultParameters = new List<Parameter>();

        /// <summary>The settings, with the address and key read from the environment now.</summary>
        public OpenAIImagesSettings ToSettings()
        {
            var settings = OpenAIImagesSettings.FromEnvironment(Url, UrlVariable, KeyVariable ?? "");
            settings.Model = Model ?? "";
            settings.SdCppExtraArgs = SdCppExtraArgs;
            settings.Edits = Edits;
            settings.Masks = Masks;
            settings.MaxImages = MaxImages;
            settings.SizeStep = SizeStep;
            settings.MaxSide = MaxSide;
            settings.TimeoutSeconds = TimeoutSeconds;
            settings.Retry = (Retry ?? new RetryPolicy()).Copy();
            foreach (var p in DefaultParameters)
            {
                if (!string.IsNullOrWhiteSpace(p.Name))
                {
                    settings.DefaultParameters[p.Name.Trim()] = p.Value ?? "";
                }
            }

            return settings;
        }

        /// <summary>A provider for these settings.</summary>
        /// <exception cref="ArgumentException">As <see cref="OpenAIImagesProvider(OpenAIImagesSettings)"/>.</exception>
        public OpenAIImagesProvider CreateProvider() => new OpenAIImagesProvider(ToSettings());
    }
}
