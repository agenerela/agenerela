using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Agenerela.Providers.SdCpp.Tests
{
    // A domain reload while sd.cpp is making a picture (#414 review, items 2 and 3). The reload must
    // wait for the worker to leave native code, and the native log callback must not outlive the old
    // domain: either failure ends the editor process, so a run that finishes is the check. Then a
    // fresh provider in the new domain must load and log as before. Runs only with
    // AGENERELA_SDCPP_MODELS and AGENERELA_SDCPP_LIBRARY set, as SdCppLiveTests does.
    [Category("RequiresSdCpp")]
    public sealed class SdCppReloadTests
    {
        // Locals do not survive the reload (only the iterator's position does), so everything the
        // second half needs is read again from the environment.
        private static SdCppSettings Settings()
        {
            string models = Environment.GetEnvironmentVariable("AGENERELA_SDCPP_MODELS");
            string library = Environment.GetEnvironmentVariable("AGENERELA_SDCPP_LIBRARY");
            if (string.IsNullOrWhiteSpace(models) || string.IsNullOrWhiteSpace(library))
            {
                Assert.Ignore("AGENERELA_SDCPP_MODELS and AGENERELA_SDCPP_LIBRARY are not set, so there is no model to run.");
            }

            return new SdCppSettings
            {
                DiffusionModel = Path.Combine(models, "z_image_turbo-Q4_K.gguf"),
                TextEncoder = Path.Combine(models, "Qwen3-4B-Instruct-2507-Q4_K_M.gguf"),
                Vae = Path.Combine(models, "ae.safetensors"),
                LibraryFolder = library,
                Steps = 8,
                Cfg = 1f,
                TextEncoderOnCpu = !string.Equals(Environment.GetEnvironmentVariable("AGENERELA_SDCPP_TE"), "gpu", StringComparison.OrdinalIgnoreCase),
            };
        }

        private static string Output()
        {
            string output = Environment.GetEnvironmentVariable("AGENERELA_SDCPP_OUT") ?? Path.Combine(Path.GetTempPath(), "agenerela-sdcpp");
            Directory.CreateDirectory(output);
            return output;
        }

        private static string Now() => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        [UnityTest]
        [Timeout(1800000)]
        public IEnumerator AReloadDuringAPictureWaitsForTheWorkerAndTheNextDomainPaintsAgain()
        {
            var provider = new SdCppImageProvider(Settings());
            _ = provider.GenerateAsync(new ImageRequest { Prompt = "a painted castle on a hill", Width = 768, Height = 896, Seed = 3 }, CancellationToken.None);
            while (!provider.IsLoaded)
            {
                yield return null;
            }

            // Well into the picture's sampling, which takes about 20 s here.
            var until = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < until)
            {
                yield return null;
            }

            File.WriteAllText(Path.Combine(Output(), "reload-requested.txt"), Now());
            EditorUtility.RequestScriptReload();
            yield return new WaitForDomainReload();

            // A new domain: the provider above is gone, its worker stopped before the reload.
            File.WriteAllText(Path.Combine(Output(), "reload-done.txt"), Now());
            Assert.That(SdCppLibrary.LoadedFrom, Is.Null, "A fresh domain has loaded nothing yet.");

            // A separate iterator: a closure in this method would be allocated before the reload and
            // be null after it, since only the iterator's position survives.
            yield return PaintAgain();
        }

        private static IEnumerator PaintAgain()
        {
            var again = new SdCppImageProvider(Settings());
            var awaitable = again.GenerateAsync(new ImageRequest { Prompt = "a painted castle on a hill", Width = 512, Height = 512, Seed = 4 }, CancellationToken.None);
            ImageResult result = null;
            Exception error = null;
            bool finished = false;

            async void Watch()
            {
                try
                {
                    result = await awaitable;
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
            }

            again.Dispose();
            Assert.That(error, Is.Null, error?.ToString());
            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            File.WriteAllBytes(Path.Combine(Output(), "after-reload.png"), result.Png);
            File.WriteAllText(Path.Combine(Output(), "after-reload.txt"), $"{Now()} {result.Seconds:0.0} s, library {SdCppLibrary.LoadedFrom}");
        }
    }
}
