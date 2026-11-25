using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Agenerela.Providers.SdCpp
{
    /// <summary>
    /// A model "recipe" (DR-028, Q6): which files make up an image model, where they are, and the
    /// model's own defaults. Files live outside the package, in <c>StreamingAssets</c> so a build
    /// carries them, or at an absolute path. The Inspector picks each file from disk or downloads a
    /// known one.
    /// </summary>
    [CreateAssetMenu(menuName = "Agenerela/Local Image Model (stable-diffusion.cpp)", fileName = "ImageModel")]
    public sealed class SdCppModel : ScriptableObject
    {
        private static readonly SdCppSettings Defaults = new SdCppSettings();

        /// <summary>A file the recipe may download: its official source, size, hash and licence.</summary>
        [Serializable]
        public struct KnownFile
        {
            [Tooltip("diffusion, text_encoder, vae or checkpoint.")]
            public string Slot;

            [Tooltip("The official Hugging Face URL it is downloaded from.")]
            public string Url;

            [Tooltip("Its size in bytes, checked after the download.")]
            public long Size;

            [Tooltip("Its SHA-256, lower-case hex; empty to skip the check.")]
            public string Sha256;

            [Tooltip("Its licence, shown before the download.")]
            public string Licence;
        }

        /// <summary>One sd.cpp option by key.</summary>
        [Serializable]
        public struct Option
        {
            public string Key;
            public string Value;
        }

        [Header("Files (absolute, or relative to StreamingAssets)")]
        [Tooltip("The diffusion model, such as Agenerela/models/z_image_turbo-Q4_K.gguf.")]
        public string DiffusionModel = "";

        [Tooltip("The text encoder, such as Qwen3-4B-Instruct-2507-Q4_K_M.gguf for Z-Image. Empty for a model with none apart.")]
        public string TextEncoder = "";

        [Tooltip("The VAE, such as ae.safetensors.")]
        public string Vae = "";

        [Tooltip("A whole checkpoint in one file, for a model shipped that way. Empty otherwise.")]
        public string Checkpoint = "";

        [Header("The model's defaults")]
        [Min(0)]
        [Tooltip("Sampling steps; 0 for sd.cpp's default. Z-Image Turbo: 8.")]
        public int Steps;

        [Tooltip("Text guidance; negative for sd.cpp's default. Z-Image Turbo: 1.")]
        public float Cfg = -1f;

        [Tooltip("The sampler, such as euler; empty for the model's own.")]
        public string SampleMethod = "";

        [Tooltip("The scheduler; empty for the model's own.")]
        public string Scheduler = "";

        [Tooltip("How this model edits a picture: Repaint for any model, Instruction for an edit model such as Qwen-Image-Edit.")]
        public SdCppEditMode EditMode = Defaults.EditMode;

        [Header("Memory (an 8 GB card: text encoder on the CPU, VAE tiling on)")]
        [Tooltip("Keep the text encoder on the CPU, leaving the card for the diffusion model.")]
        public bool TextEncoderOnCpu = Defaults.TextEncoderOnCpu;

        [Tooltip("Keep the VAE on the CPU.")]
        public bool VaeOnCpu = Defaults.VaeOnCpu;

        [Tooltip("Keep every weight in system memory and stream it to the card: for a card too small for the model. Slower.")]
        public bool OffloadToCpu = Defaults.OffloadToCpu;

        [Tooltip("Decode the picture in tiles, capping the decoder's memory.")]
        public bool VaeTiling = Defaults.VaeTiling;

        [Tooltip("Flash attention in the diffusion model: less memory, usually faster.")]
        public bool FlashAttention = Defaults.FlashAttention;

        [Tooltip("More context options by sd.cpp's key, such as max_vram = 6G.")]
        public List<Option> ContextOptions = new List<Option>();

        [Tooltip("Generation options every request gets unless it sets them, such as negative_prompt.")]
        public List<Option> DefaultParameters = new List<Option>();

        [Header("Downloads")]
        [Tooltip("Where the Inspector's Download buttons put files, relative to StreamingAssets.")]
        public string DownloadFolder = "Agenerela/models";

        [Tooltip("Files this recipe knows how to download.")]
        public List<KnownFile> KnownFiles = new List<KnownFile>();

        /// <summary>The settings, with every path made absolute. Call it on the main thread.</summary>
        public SdCppSettings ToSettings()
        {
            var settings = new SdCppSettings
            {
                DiffusionModel = Resolve(DiffusionModel),
                TextEncoder = Resolve(TextEncoder),
                Vae = Resolve(Vae),
                Checkpoint = Resolve(Checkpoint),
                Steps = Steps,
                Cfg = Cfg,
                SampleMethod = SampleMethod ?? "",
                Scheduler = Scheduler ?? "",
                EditMode = EditMode,
                TextEncoderOnCpu = TextEncoderOnCpu,
                VaeOnCpu = VaeOnCpu,
                OffloadToCpu = OffloadToCpu,
                VaeTiling = VaeTiling,
                FlashAttention = FlashAttention,
            };
            foreach (var o in ContextOptions)
            {
                if (!string.IsNullOrWhiteSpace(o.Key))
                {
                    settings.ContextOptions[o.Key.Trim()] = o.Value ?? "";
                }
            }

            foreach (var o in DefaultParameters)
            {
                if (!string.IsNullOrWhiteSpace(o.Key))
                {
                    settings.DefaultParameters[o.Key.Trim()] = o.Value ?? "";
                }
            }

            return settings;
        }

        /// <summary>A provider for this recipe.</summary>
        public SdCppImageProvider CreateProvider() => new SdCppImageProvider(ToSettings());

        /// <summary><paramref name="path"/> as an absolute path: itself when rooted, else under <c>StreamingAssets</c>. Empty stays empty.</summary>
        public static string Resolve(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "";
            }

            path = path.Trim();
            return Path.IsPathRooted(path) ? path : Path.Combine(Application.streamingAssetsPath, path);
        }

        /// <summary>A Z-Image Turbo recipe: the quantised files sd.cpp's documentation names, 8 steps, cfg 1, sized for an 8 GB card.</summary>
        public void UseZImageTurbo()
        {
            DiffusionModel = DownloadFolder + "/z_image_turbo-Q4_K.gguf";
            TextEncoder = DownloadFolder + "/Qwen3-4B-Instruct-2507-Q4_K_M.gguf";
            Vae = DownloadFolder + "/ae.safetensors";
            Checkpoint = "";
            Steps = 8;
            Cfg = 1f;
            EditMode = SdCppEditMode.Repaint;
            TextEncoderOnCpu = true;
            VaeTiling = true;
            FlashAttention = true;
            KnownFiles = new List<KnownFile>
            {
                new KnownFile
                {
                    Slot = "diffusion", Size = 3864250304, Licence = "Apache-2.0 (Tongyi-MAI Z-Image Turbo, quantised by leejet)",
                    Url = "https://huggingface.co/leejet/Z-Image-Turbo-GGUF/resolve/main/z_image_turbo-Q4_K.gguf",
                    Sha256 = "14b375ab4f226bc5378f68f37e899ef3c2242b8541e61e2bc1aff40976086fbd",
                },
                new KnownFile
                {
                    Slot = "text_encoder", Size = 2497281120, Licence = "Apache-2.0 (Qwen3-4B-Instruct-2507, quantised by unsloth)",
                    Url = "https://huggingface.co/unsloth/Qwen3-4B-Instruct-2507-GGUF/resolve/main/Qwen3-4B-Instruct-2507-Q4_K_M.gguf",
                    Sha256 = "3605803b982cb64aead44f6c1b2ae36e3acdb41d8e46c8a94c6533bc4c67e597",
                },
                new KnownFile
                {
                    Slot = "vae", Size = 335304388, Licence = "Apache-2.0 (FLUX.1-schnell autoencoder, from Comfy-Org)",
                    Url = "https://huggingface.co/Comfy-Org/z_image_turbo/resolve/main/split_files/vae/ae.safetensors",
                    Sha256 = "afc8e28272cd15db3919bacdb6918ce9c1ed22e96cb12c4d5ed0fba823529e38",
                },
            };
        }
    }
}
