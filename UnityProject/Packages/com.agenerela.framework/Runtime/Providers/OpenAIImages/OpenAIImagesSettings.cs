using System;
using System.Collections.Generic;

namespace Agenerela.Providers.OpenAIImages
{
    /// <summary>
    /// Where an <see cref="OpenAIImagesProvider"/> sends its requests and what that server can do
    /// (DR-028). Plain data: build it in code, or from an <see cref="OpenAIImagesProviderConfig"/>.
    /// </summary>
    public sealed class OpenAIImagesSettings
    {
        /// <summary>The variable <see cref="FromEnvironment"/> reads the key from unless told another.</summary>
        public const string DefaultKeyVariable = "AGENERELA_IMAGES_API_KEY";

        /// <summary>
        /// The API's base address, up to and including its version, such as
        /// <c>http://127.0.0.1:1234/v1</c> for a local <c>sd-server</c> or
        /// <c>https://api.openai.com/v1</c>. <c>images/generations</c> and <c>images/edits</c> are added to it.
        /// </summary>
        public string Url = "http://127.0.0.1:1234/v1";

        /// <summary>The key, sent as <c>Authorization: Bearer</c>. Empty for a server that wants none. Never written to a message (hard rule 1).</summary>
        public string ApiKey = "";

        /// <summary>The <c>model</c> field, such as <c>gpt-image-1</c>. Empty sends none, which <c>sd-server</c> wants: it runs the model it was started with.</summary>
        public string Model = "";

        /// <summary>
        /// Write the seed, <c>strength</c>, <c>negative_prompt</c> and every <c>extra.</c> parameter into a
        /// <c>&lt;sd_cpp_extra_args&gt;</c> block at the end of the prompt, which <c>sd-server</c> reads
        /// and removes. On for <c>sd-server</c>; off for any other server, which would draw the block.
        /// </summary>
        public bool SdCppExtraArgs;

        /// <summary>Whether the server edits pictures (<c>/v1/images/edits</c>). LocalAI does not; <c>sd-server</c> and OpenAI do.</summary>
        public bool Edits = true;

        /// <summary>Whether an edit may carry a mask.</summary>
        public bool Masks = true;

        /// <summary>The most input pictures one edit takes.</summary>
        public int MaxImages = 16;

        /// <summary>Width and height must be multiples of this. 16 suits the diffusion models <c>sd-server</c> runs; 1 for no rule.</summary>
        public int SizeStep = 16;

        /// <summary>The longest side the server makes, in pixels.</summary>
        public int MaxSide = 2048;

        /// <summary>
        /// Fields every request gets unless it sets them, by the same names as
        /// <see cref="ImageRequest.Parameters"/>: a body field such as <c>quality</c>, or
        /// <c>extra.sample_params.sample_steps</c> for <c>sd-server</c>.
        /// </summary>
        public Dictionary<string, string> DefaultParameters = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Seconds one attempt may take before it is a timeout. A first picture on a cold model takes longest.</summary>
        public float TimeoutSeconds = 300f;

        /// <summary>How long a busy server (HTTP 429 or 503) is waited out (DR-026). Three sends within 10 s unless set.</summary>
        public RetryPolicy Retry = new RetryPolicy();

        /// <summary>
        /// Settings whose key comes from the variable <paramref name="keyVariable"/>, at
        /// <paramref name="url"/>, or at the address in <paramref name="urlVariable"/> when that is set.
        /// </summary>
        /// <remarks>
        /// When <paramref name="urlVariable"/> is named but not set, the settings fall back to
        /// <paramref name="url"/> and send <b>no key</b>, so a key meant for one server never reaches
        /// another (DR-026).
        /// </remarks>
        public static OpenAIImagesSettings FromEnvironment(string url, string urlVariable = "", string keyVariable = DefaultKeyVariable)
        {
            var settings = new OpenAIImagesSettings { Url = (url ?? "").Trim() };
            bool sendKey = true;
            if (!string.IsNullOrWhiteSpace(urlVariable))
            {
                string fromVariable = Environment.GetEnvironmentVariable(urlVariable.Trim());
                if (string.IsNullOrWhiteSpace(fromVariable))
                {
                    sendKey = false;
                }
                else
                {
                    settings.Url = fromVariable.Trim();
                }
            }

            if (sendKey && !string.IsNullOrWhiteSpace(keyVariable))
            {
                settings.ApiKey = (Environment.GetEnvironmentVariable(keyVariable.Trim()) ?? "").Trim();
            }

            return settings;
        }
    }
}
