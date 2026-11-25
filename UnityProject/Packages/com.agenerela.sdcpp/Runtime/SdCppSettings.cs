using System;
using System.Collections.Generic;

namespace Agenerela.Providers.SdCpp
{
    /// <summary>How a model changes a picture it is given (DR-028, Q6).</summary>
    public enum SdCppEditMode
    {
        /// <summary>It makes new pictures only.</summary>
        None,

        /// <summary>
        /// A repaint, which any model does: the picture is the starting point (sd.cpp's
        /// <c>init_image</c>), a mask limits the change (<c>mask_image</c>), and <c>strength</c>
        /// says how far it may move from the picture.
        /// </summary>
        Repaint,

        /// <summary>
        /// An instruction edit, for an edit model such as Qwen-Image-Edit or Flux Kontext: the
        /// pictures are references (<c>ref_images</c>) and the prompt says what to change. No mask.
        /// </summary>
        Instruction,
    }

    /// <summary>
    /// What an <see cref="SdCppImageProvider"/> loads and how (DR-028). Plain data: build it in code,
    /// or from an <see cref="SdCppModel"/> asset. Paths are absolute, or relative to
    /// <c>StreamingAssets</c>.
    /// </summary>
    public sealed class SdCppSettings
    {
        /// <summary>The diffusion model file (sd.cpp's <c>diffusion_model_path</c>), such as <c>z_image_turbo-Q4_K.gguf</c>.</summary>
        public string DiffusionModel = "";

        /// <summary>The text encoder (<c>llm_path</c>), such as <c>Qwen3-4B-Instruct-2507-Q4_K_M.gguf</c> for Z-Image. Empty for a model that has none apart.</summary>
        public string TextEncoder = "";

        /// <summary>The VAE (<c>vae_path</c>), such as <c>ae.safetensors</c>.</summary>
        public string Vae = "";

        /// <summary>A whole checkpoint in one file (<c>model_path</c>), for a model shipped that way (SDXL). Empty otherwise.</summary>
        public string Checkpoint = "";

        /// <summary>The native library's folder, or empty to find an installed build under <c>StreamingAssets</c> (<see cref="SdCppLibrary"/>).</summary>
        public string LibraryFolder = "";

        /// <summary>Sampling steps (<c>sample.steps</c>); 0 leaves sd.cpp's default. Z-Image Turbo: 8.</summary>
        public int Steps;

        /// <summary>Text guidance (<c>sample.guidance.txt_cfg</c>); a negative value leaves sd.cpp's default. Z-Image Turbo: 1.</summary>
        public float Cfg = -1f;

        /// <summary>The sampler (<c>sample.method</c>), such as <c>euler</c>; empty for the model's own.</summary>
        public string SampleMethod = "";

        /// <summary>The scheduler (<c>sample.scheduler</c>); empty for the model's own.</summary>
        public string Scheduler = "";

        /// <summary>Keep the text encoder on the CPU (<c>backend = te=cpu</c>), which leaves the card for the diffusion model. On by default: it is the biggest saving on an 8 GB card (DR-028, Q8).</summary>
        public bool TextEncoderOnCpu = true;

        /// <summary>Keep the VAE on the CPU (<c>vae=cpu</c>). Slower decoding, a little less memory on the card.</summary>
        public bool VaeOnCpu;

        /// <summary>Keep every weight in system memory and stream it to the card as needed (<c>params_backend = *=cpu</c>), for a card that is too small for the model. Slower.</summary>
        public bool OffloadToCpu;

        /// <summary>Decode the picture in tiles (<c>vae_tiling.enabled</c>), which caps the decoder's memory on a large picture. On by default.</summary>
        public bool VaeTiling = true;

        /// <summary>Flash attention where the backend supports it (sd.cpp's default, <c>auto</c>): less memory, usually faster. Off sets <c>flash_attn_mode = off</c>.</summary>
        public bool FlashAttention = true;

        /// <summary>CPU threads (<c>n_threads</c>); 0 for the number of physical cores.</summary>
        public int Threads;

        /// <summary>How this model edits a picture; <see cref="SdCppEditMode.None"/> refuses edits.</summary>
        public SdCppEditMode EditMode = SdCppEditMode.Repaint;

        /// <summary>For an instruction edit, the most reference pictures one request takes.</summary>
        public int MaxReferenceImages = 3;

        /// <summary>
        /// Context options beyond those above, by sd.cpp's own key (its <c>docs/guides/c_api.md</c>),
        /// such as <c>max_vram</c> = <c>6G</c> or <c>model_args</c>. The value is tried as an integer,
        /// a number, a flag and then text, whichever the key takes.
        /// </summary>
        public Dictionary<string, string> ContextOptions = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Generation options every request gets unless it sets them, by sd.cpp's key, such as <c>negative_prompt</c>.</summary>
        public Dictionary<string, string> DefaultParameters = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Forward sd.cpp's own log lines (warnings and errors) to the Unity console.</summary>
        public bool LogNative = true;

        /// <summary>A copy, so a provider never sees a change made after it was built.</summary>
        public SdCppSettings Copy()
        {
            var copy = (SdCppSettings)MemberwiseClone();
            copy.ContextOptions = new Dictionary<string, string>(ContextOptions ?? new Dictionary<string, string>(), StringComparer.Ordinal);
            copy.DefaultParameters = new Dictionary<string, string>(DefaultParameters ?? new Dictionary<string, string>(), StringComparer.Ordinal);
            return copy;
        }
    }
}
