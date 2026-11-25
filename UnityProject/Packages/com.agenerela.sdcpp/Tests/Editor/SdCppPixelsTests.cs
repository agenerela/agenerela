using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Agenerela.Providers.SdCpp.Tests
{
    // The pixels sd.cpp takes and gives (top row first, RGB), the mask's convention, the recipe's
    // paths and the library's search (DR-028). No native library.
    public sealed class SdCppPixelsTests
    {
        [Test]
        public void APngDecodesTopRowFirstAndEncodesBackTheSameWayUp()
        {
            // 3x2, top-left red, top-right green, the bottom row blue.
            var rgb = new byte[]
            {
                255, 0, 0, 0, 255, 0, 0, 255, 0,
                0, 0, 255, 0, 0, 255, 0, 0, 255,
            };
            byte[] png = SdCppPixels.EncodePng(new RgbImage(3, 2, rgb));

            Assert.That(ImageResult_IsPng(png), Is.True);
            var texture = new Texture2D(2, 2);
            texture.LoadImage(png);
            Assert.That((Color32)texture.GetPixel(0, 1), Is.EqualTo(new Color32(255, 0, 0, 255)), "The first row is the picture's top.");
            Object.DestroyImmediate(texture);

            var back = SdCppPixels.Decode(png, 3, 2, out byte[] alpha);
            Assert.That(back.Rgb, Is.EqualTo(rgb));
            Assert.That(alpha, Has.All.EqualTo((byte)255));
        }

        [Test]
        public void TransparentPixelsAreTheOnesToChange()
        {
            Assert.That(SdCppPixels.MaskFromAlpha(new byte[] { 0, 1, 128, 255 }), Is.EqualTo(new byte[] { 255, 0, 0, 0 }));
        }

        [Test]
        public void ScalingKeepsAFlatColourAndPicksTheNearestMaskPixel()
        {
            var flat = Enumerable.Repeat((byte)77, 2 * 2 * 3).ToArray();
            Assert.That(SdCppPixels.ScaleBilinear(flat, 2, 2, 5, 3), Has.All.EqualTo((byte)77));
            Assert.That(SdCppPixels.ScaleNearest(new byte[] { 0, 255 }, 2, 1, 1, 4, 1), Is.EqualTo(new byte[] { 0, 0, 255, 255 }));
        }

        [Test]
        public void TheZImageRecipeNamesItsFilesUnderStreamingAssets()
        {
            var model = ScriptableObject.CreateInstance<SdCppModel>();
            model.UseZImageTurbo();
            var settings = model.ToSettings();

            Assert.That(settings.DiffusionModel, Is.EqualTo(Path.Combine(Application.streamingAssetsPath, "Agenerela/models/z_image_turbo-Q4_K.gguf")));
            Assert.That(settings.Steps, Is.EqualTo(8));
            Assert.That(settings.Cfg, Is.EqualTo(1f));
            Assert.That(settings.TextEncoderOnCpu && settings.VaeTiling, Is.True, "Sized for an 8 GB card.");
            Assert.That(model.KnownFiles.Select(f => f.Slot), Is.EquivalentTo(new[] { "diffusion", "text_encoder", "vae" }));
            Assert.That(model.KnownFiles.Select(f => f.Url), Has.All.StartsWith("https://huggingface.co/"));
            Assert.That(model.KnownFiles.Select(f => f.Sha256), Has.All.Match("^[0-9a-f]{64}$"));
            Assert.That(SdCppModel.Resolve("C:/models/a.gguf"), Is.EqualTo("C:/models/a.gguf"), "An absolute path stays as it is.");
            Object.DestroyImmediate(model);
        }

        [Test]
        public void TheLibraryIsLookedForInTheFolderGivenOrInEachInstalledBuildFastestFirst()
        {
            string root = Path.Combine(Path.GetTempPath(), "sdcpp-candidates-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, SdCppLibrary.StreamingFolder, "win-x64-cpu"));
                Directory.CreateDirectory(Path.Combine(root, SdCppLibrary.StreamingFolder, "win-x64-vulkan"));

                var found = SdCppLibrary.Candidates("", root).Select(Path.GetFileName).ToList();
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    Assert.That(found, Is.EqualTo(new[] { "win-x64-vulkan", "win-x64-cpu" }), "Vulkan before the CPU; CUDA is not installed.");
                }

                Assert.That(SdCppLibrary.Candidates(root, root), Is.EqualTo(new[] { Path.GetFullPath(root) }), "A folder given is the only one tried.");
                Assert.That(SdCppLibrary.Candidates("", Path.Combine(root, "none")), Is.Empty);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void AGpuIsAnyDeviceButTheCpu()
        {
            // sd_list_devices' own output on the RTX 4060 Laptop, and on a machine whose GPU backend did not load.
            Assert.That(Native.SdCppApi.HasGpu("Vulkan0\tAMD Radeon(TM) 890M Graphics\nVulkan1\tNVIDIA GeForce RTX 4060 Laptop GPU\nCPU\tAMD Ryzen AI 9 HX 370\n"), Is.True);
            Assert.That(Native.SdCppApi.HasGpu("CPU\tAMD Ryzen AI 9 HX 370\n"), Is.False);
            Assert.That(Native.SdCppApi.HasGpu(""), Is.False);
        }

        [Test]
        public void EveryBuildOfThePinnedReleaseHasASizeAndAHash()
        {
            Assert.That(SdCppRelease.Builds.Select(b => b.Sha256), Has.All.Match("^[0-9a-f]{64}$"));
            Assert.That(SdCppRelease.Builds.Select(b => b.File), Has.All.Contain("79dc7ca"));
            Assert.That(SdCppRelease.Url(SdCppRelease.Builds[1]), Does.StartWith("https://github.com/leejet/stable-diffusion.cpp/releases/download/" + SdCppRelease.Tag + "/"));
        }

        private static bool ImageResult_IsPng(byte[] bytes) => ImageResult.IsPng(bytes);
    }
}
