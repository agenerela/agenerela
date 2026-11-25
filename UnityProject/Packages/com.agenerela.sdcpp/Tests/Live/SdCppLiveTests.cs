using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Providers.SdCpp.Tests
{
    // The in-process provider on a real model, in the player loop (DR-028, step 3). Runs only when
    // AGENERELA_SDCPP_MODELS names a folder holding z_image_turbo-Q4_K.gguf,
    // Qwen3-4B-Instruct-2507-Q4_K_M.gguf and ae.safetensors, and AGENERELA_SDCPP_LIBRARY names the
    // unpacked Vulkan build. Writes each picture and report.json to AGENERELA_SDCPP_OUT.
    [Category("RequiresSdCpp")]
    public sealed class SdCppLiveTests
    {
        private const int Width = 768;
        private const int Height = 896;
        private const string Prompt =
            "Painted illustration in the manner of a late-medieval chronicle, bust portrait of an old king with a grey beard " +
            "and a red cloak, plain dark background, soft light";

        private static string Variable(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                Assert.Ignore($"{name} is not set, so there is no model to run.");
            }

            return value.Trim();
        }

        private sealed class Frames
        {
            public readonly List<float> Seconds = new List<float>();

            public string Summary()
            {
                if (Seconds.Count == 0)
                {
                    return "{\"frames\":0}";
                }

                var sorted = Seconds.OrderBy(s => s).ToList();
                float P(double q) => sorted[Math.Min(sorted.Count - 1, (int)(q * sorted.Count))] * 1000f;
                return string.Format(CultureInfo.InvariantCulture, "{{\"frames\":{0},\"median_ms\":{1:0.0},\"p95_ms\":{2:0.0},\"max_ms\":{3:0.0}}}",
                    Seconds.Count, P(0.5), P(0.95), sorted[sorted.Count - 1] * 1000f);
            }
        }

        // Waits for the awaitable while the player loop runs and a camera renders, recording each frame's time.
        private static IEnumerator Await<T>(Awaitable<T> awaitable, Action<T> done, Frames frames, Action<Exception> failed = null)
        {
            bool finished = false;
            Exception error = null;
            T value = default;

            async void Watch()
            {
                try
                {
                    value = await awaitable;
                }
                catch (Exception e)
                {
                    error = e;
                }

                finished = true;
            }

            Watch();
            while (!finished)
            {
                yield return null;
                frames?.Seconds.Add(Time.unscaledDeltaTime);
            }

            if (error != null)
            {
                if (failed == null)
                {
                    throw error;
                }

                failed(error);
                yield break;
            }

            done(value);
        }

        [UnityTest]
        [Timeout(1800000)]
        public IEnumerator ZImageTurboMakesAndEditsAPictureInTheEditor()
        {
            string models = Variable("AGENERELA_SDCPP_MODELS");
            string library = Variable("AGENERELA_SDCPP_LIBRARY");
            string output = Environment.GetEnvironmentVariable("AGENERELA_SDCPP_OUT") ?? Path.Combine(Path.GetTempPath(), "agenerela-sdcpp");
            Directory.CreateDirectory(output);

            // Something for the card to render every frame, as a game would.
            var target = new RenderTexture(1920, 1080, 24);
            var cameraObject = new GameObject("Live camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = target;
            camera.transform.position = new Vector3(0, 0, -10);
            var cubes = Enumerable.Range(0, 200).Select(i =>
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.position = new Vector3((i % 20) - 10, (i / 20) - 5, (i % 7));
                return cube;
            }).ToList();

            var settings = new SdCppSettings
            {
                DiffusionModel = Path.Combine(models, "z_image_turbo-Q4_K.gguf"),
                TextEncoder = Path.Combine(models, "Qwen3-4B-Instruct-2507-Q4_K_M.gguf"),
                Vae = Path.Combine(models, "ae.safetensors"),
                LibraryFolder = library,
                Steps = 8,
                Cfg = 1f,
                // AGENERELA_SDCPP_TE=gpu keeps the text encoder on the card, for a machine whose RAM is short.
                TextEncoderOnCpu = !string.Equals(Environment.GetEnvironmentVariable("AGENERELA_SDCPP_TE"), "gpu", StringComparison.OrdinalIgnoreCase),
            };
            var report = new StringBuilder("{");
            void Add(string key, string json) => report.Append(report.Length > 1 ? "," : "").Append('"').Append(key).Append("\":").Append(json);
            string F(float? f) => f.HasValue ? f.Value.ToString("0.000", CultureInfo.InvariantCulture) : "null";

            var idle = new Frames();
            for (int i = 0; i < 120; i++)
            {
                yield return null;
                idle.Seconds.Add(Time.unscaledDeltaTime);
            }

            Add("idle", idle.Summary());
            Add("text_encoder", settings.TextEncoderOnCpu ? "\"cpu\"" : "\"gpu\"");

            using (var provider = new SdCppImageProvider(settings))
            {
                var warm = new Frames();
                var clock = System.Diagnostics.Stopwatch.StartNew();
                yield return Await(provider.WarmUpAsync(CancellationToken.None).AsBool(), _ => { }, warm);
                Add("load_s", F((float)clock.Elapsed.TotalSeconds));
                Add("load_frames", warm.Summary());
                Assert.That(provider.IsLoaded, Is.True);

                var made = new List<ImageResult>();
                for (int i = 0; i < 3; i++)
                {
                    var frames = new Frames();
                    ImageResult result = null;
                    yield return Await(provider.GenerateAsync(new ImageRequest { Prompt = Prompt, Width = Width, Height = Height, Seed = 1000 + i }, CancellationToken.None), r => result = r, frames);
                    Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
                    File.WriteAllBytes(Path.Combine(output, $"portrait-{i}.png"), result.Png);
                    made.Add(result);
                    Add($"picture_{i}", $"{{\"seconds\":{F(result.Seconds)},\"generate_seconds\":{F(result.GenerateSeconds)},\"bytes\":{result.Png.Length},\"frames\":{frames.Summary()}}}");
                }

                // The same seed again: the same picture.
                ImageResult again = null;
                yield return Await(provider.GenerateAsync(new ImageRequest { Prompt = Prompt, Width = Width, Height = Height, Seed = 1000 }, CancellationToken.None), r => again = r, null);
                Add("same_seed_same_bytes", again.Png.SequenceEqual(made[0].Png) ? "true" : "false");

                // A repaint: the top half kept (opaque), the bottom half changed (transparent).
                var mask = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                var pixels = new Color32[Width * Height];
                for (int y = 0; y < Height; y++)
                {
                    for (int x = 0; x < Width; x++)
                    {
                        // A texture's row 0 is the picture's bottom row.
                        pixels[y * Width + x] = y < Height / 2 ? new Color32(0, 0, 0, 0) : new Color32(0, 0, 0, 255);
                    }
                }

                mask.SetPixels32(pixels);
                mask.Apply();
                var edit = new ImageRequest { Prompt = Prompt + ", wearing a tall golden crown and a white fur collar", Width = Width, Height = Height, Seed = 7 };
                edit.Images.Add(made[0].Png);
                edit.Mask = mask.EncodeToPNG();
                edit.Parameters["strength"] = "0.75";
                UnityEngine.Object.DestroyImmediate(mask);
                var editFrames = new Frames();
                ImageResult edited = null;
                yield return Await(provider.GenerateAsync(edit, CancellationToken.None), r => edited = r, editFrames);
                Assert.That(edited.Succeeded, Is.True, edited.ErrorMessage);
                File.WriteAllBytes(Path.Combine(output, "portrait-0-edited.png"), edited.Png);
                Add("edit", $"{{\"seconds\":{F(edited.Seconds)},\"generate_seconds\":{F(edited.GenerateSeconds)},\"frames\":{editFrames.Summary()}}}");

                // Cancelling a picture one second in.
                using (var cts = new CancellationTokenSource())
                {
                    var started = System.Diagnostics.Stopwatch.StartNew();
                    var pending = provider.GenerateAsync(new ImageRequest { Prompt = Prompt, Width = Width, Height = Height, Seed = 99 }, cts.Token);
                    cts.CancelAfter(1000);
                    bool cancelled = false;
                    yield return Await(pending, _ => { }, null, e => cancelled = e is OperationCanceledException);
                    Add("cancel", $"{{\"cancelled\":{(cancelled ? "true" : "false")},\"ended_after_s\":{F((float)started.Elapsed.TotalSeconds)}}}");
                    Assert.That(cancelled, Is.True, "The cancelled picture ended as cancelled.");
                }

                bool unloaded = false;
                yield return Await(provider.UnloadAsync(CancellationToken.None), u => unloaded = u, null);
                Add("unloaded", unloaded ? "true" : "false");
                Assert.That(provider.IsLoaded, Is.False);
                yield return new WaitForSecondsRealtime(3f); // a moment for the VRAM sampler to see it freed
            }

            Add("library", "\"" + (SdCppLibrary.LoadedFrom ?? "").Replace("\\", "/") + "\"");
            File.WriteAllText(Path.Combine(output, "report.json"), report.Append('}').ToString());
            Debug.Log("[sdcpp live] " + report);

            foreach (var cube in cubes)
            {
                UnityEngine.Object.Destroy(cube);
            }

            UnityEngine.Object.Destroy(cameraObject);
            target.Release();
        }
    }

    internal static class AwaitableBool
    {
        // WarmUpAsync returns a plain Awaitable; this gives it a value so one helper waits on both.
        internal static async Awaitable<bool> AsBool(this Awaitable awaitable)
        {
            await awaitable;
            return true;
        }
    }
}
