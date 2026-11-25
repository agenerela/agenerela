using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Providers;
using Agenerela.Providers.OpenAIImages;
using NUnit.Framework;

namespace Agenerela.Tests
{
    // OpenAIImagesProvider against a real sd-server (DR-028). Runs only when AGENERELA_IMAGES_URL names
    // one, such as http://127.0.0.1:1234/v1, started with Z-Image Turbo; writes its pictures to
    // AGENERELA_IMAGES_OUT when that is set.
    [Category("RequiresImageServer")]
    public sealed class OpenAIImagesLiveTests
    {
        private static OpenAIImagesProvider Provider()
        {
            string url = Environment.GetEnvironmentVariable("AGENERELA_IMAGES_URL");
            if (string.IsNullOrWhiteSpace(url))
            {
                Assert.Ignore("AGENERELA_IMAGES_URL is not set, so there is no images server to ask.");
            }

            var settings = new OpenAIImagesSettings { Url = url.Trim(), SdCppExtraArgs = true, TimeoutSeconds = 600f };
            settings.DefaultParameters["extra.sample_params.sample_steps"] = "8";
            settings.DefaultParameters["extra.sample_params.guidance.txt_cfg"] = "1";
            return new OpenAIImagesProvider(settings);
        }

        [Test]
        public async Task SdServerMakesThePictureAndTheSeedMakesItAgain()
        {
            var provider = Provider();
            var request = new ImageRequest
            {
                Prompt = "Painted illustration in the manner of a late-medieval chronicle, a heraldic banner, a gold lion on red",
                Width = 512,
                Height = 640,
                Seed = 21,
            };

            var first = await provider.GenerateAsync(request, CancellationToken.None);
            var again = await provider.GenerateAsync(request, CancellationToken.None);

            Assert.That(first.Succeeded, Is.True, first.ErrorMessage);
            Assert.That(again.Succeeded, Is.True, again.ErrorMessage);
            Assert.That(first.Seed, Is.EqualTo(21));
            Assert.That(again.Png, Is.EqualTo(first.Png), "The seed reached sd-server through <sd_cpp_extra_args>.");

            string output = Environment.GetEnvironmentVariable("AGENERELA_IMAGES_OUT");
            if (!string.IsNullOrWhiteSpace(output))
            {
                Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, "sd-server-banner.png"), first.Png);
                File.WriteAllText(Path.Combine(output, "sd-server-report.txt"), $"first {first.Seconds:0.0} s, again {again.Seconds:0.0} s");
            }

            var other = await provider.GenerateAsync(new ImageRequest { Prompt = request.Prompt, Width = 512, Height = 640, Seed = 22 }, CancellationToken.None);
            Assert.That(other.Succeeded, Is.True, other.ErrorMessage);
            Assert.That(other.Png, Is.Not.EqualTo(first.Png), "Another seed, another picture.");
        }
    }
}
