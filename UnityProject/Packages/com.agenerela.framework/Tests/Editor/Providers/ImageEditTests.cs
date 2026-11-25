using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Providers;
using NUnit.Framework;

namespace Agenerela.Tests
{
    // Editing in the image request (DR-028): the input pictures and the mask in the key, what a
    // backend's capabilities refuse, and the cache keeping one edit apart from another.
    public sealed class ImageEditTests
    {
        private static readonly byte[] Png = ScriptedImageProvider.OnePixelPng;
        private static readonly byte[] OtherPng = Png.Concat(new byte[] { 1, 2, 3 }).ToArray();

        private static ImageRequest Plain()
        {
            var request = new ImageRequest { Prompt = "a ruler", Width = 512, Height = 640, Seed = 7 };
            request.Parameters["steps"] = "8";
            return request;
        }

        private static ImageRequest Edit(params byte[][] images)
        {
            var request = Plain();
            request.Prompt = "give him a crown";
            request.Images.AddRange(images.Length == 0 ? new[] { Png } : images);
            return request;
        }

        private static ImageCapabilities Editing() => new ImageCapabilities { Edits = true, Masks = true, MaxImages = 3 };

        // ---- The key -------------------------------------------------------------------------------

        [Test]
        public void APlainRequestKeepsTheKeyItHadBeforeEdits()
        {
            // Computed from the key's definition before DR-028 (length-prefixed fields, SHA-256), so a
            // cache made then still serves the same pictures.
            Assert.That(Plain().Key("comfyui"), Is.EqualTo("7e54c4393524786a59bc24c610aa48c7a224ce36b2e0629c1bdc5a07bdf23807"));
        }

        [Test]
        public void ThePicturesTheirOrderAndTheMaskAllChangeTheKey()
        {
            var plain = Plain();
            plain.Prompt = "give him a crown";
            var one = Edit(Png);
            var other = Edit(OtherPng);
            var both = Edit(Png, OtherPng);
            var swapped = Edit(OtherPng, Png);
            var masked = Edit(Png);
            masked.Mask = Png;
            var otherMask = Edit(Png);
            otherMask.Mask = OtherPng;

            var keys = new[] { plain, one, other, both, swapped, masked, otherMask }.Select(r => r.Key("openai-images")).ToList();

            Assert.That(keys.Distinct().Count(), Is.EqualTo(keys.Count), "Each of the seven is a different picture to make.");
            Assert.That(Edit(Png).Key("openai-images"), Is.EqualTo(one.Key("openai-images")), "The same bytes, the same key.");
        }

        // ---- What a backend refuses ----------------------------------------------------------------

        [Test]
        public void ABackendThatDoesNotEditSaysSoAndAPlainRequestPasses()
        {
            var caps = new ImageCapabilities();

            Assert.That(caps.Edits, Is.False, "Off unless a backend says otherwise.");
            Assert.That(caps.WhyNot(Plain()), Is.Null);
            Assert.That(caps.WhyNot(Edit()), Does.Contain("does not edit"));
        }

        [Test]
        public void WhyNotNamesEachProblemWithAnEdit()
        {
            var caps = Editing();
            var ok = Edit(Png, OtherPng);
            ok.Mask = Png;
            Assert.That(caps.WhyNot(ok), Is.Null);

            var maskOnly = Plain();
            maskOnly.Mask = Png;
            Assert.That(caps.WhyNot(maskOnly), Does.Contain("Mask but no Images"));

            Assert.That(caps.WhyNot(Edit(Png, Png, Png, Png)), Does.Contain("at most 3"));
            Assert.That(caps.WhyNot(Edit(Png, new byte[] { 1, 2, 3 })), Does.Contain("Images[1] is not a PNG"));

            var nullImage = Edit(Png);
            nullImage.Images.Add(null);
            Assert.That(caps.WhyNot(nullImage), Does.Contain("Images[1] is not a PNG"));

            var badMask = Edit(Png);
            badMask.Mask = new byte[] { 0 };
            Assert.That(caps.WhyNot(badMask), Does.Contain("Mask is not a PNG"));

            var noMasks = Editing();
            noMasks.Masks = false;
            var masked = Edit(Png);
            masked.Mask = Png;
            Assert.That(noMasks.WhyNot(masked), Does.Contain("takes no Mask"));

            var big = Edit(Png);
            big.Width = 4096;
            Assert.That(caps.WhyNot(big), Does.Contain("too large"), "The size is checked first, as before.");
        }

        [Test]
        public void TheScriptedProviderRefusesWhatItsCapabilitiesRefuse()
        {
            var backend = ScriptedImageProvider.Always();
            backend.Capabilities.Edits = false;

            Assert.Throws<ArgumentException>(() => backend.GenerateAsync(Edit(), CancellationToken.None));
            Assert.That(backend.Requests, Is.Empty);
        }

        // ---- The cache -----------------------------------------------------------------------------

        [Test]
        public async Task TheCacheNeverHandsOneEditOutForAnother()
        {
            int made = 0;
            var backend = new ScriptedImageProvider(r =>
            {
                made++;
                return ImageResult.Made(r.Images[0] == Png ? Png : OtherPng, "scripted", 0f);
            });
            var cached = new CachedImageProvider(backend, new MemoryCache());

            var first = await Fixtures.NotCancelled(cached.GenerateAsync(Edit(Png), CancellationToken.None));
            var second = await Fixtures.NotCancelled(cached.GenerateAsync(Edit(OtherPng), CancellationToken.None));
            var again = await Fixtures.NotCancelled(cached.GenerateAsync(Edit(Png), CancellationToken.None));

            Assert.That(first.FromCache, Is.False);
            Assert.That(second.FromCache, Is.False, "A different input picture is a different edit.");
            Assert.That(second.Png, Is.EqualTo(OtherPng));
            Assert.That(again.FromCache, Is.True);
            Assert.That(again.Png, Is.EqualTo(Png));
            Assert.That(made, Is.EqualTo(2));
            Assert.That(backend.Requests[0].Images[0], Is.SameAs(Png), "The edit reached the backend as given.");
        }

        private sealed class MemoryCache : IImageCache
        {
            private readonly Dictionary<string, byte[]> stored = new Dictionary<string, byte[]>();

            public bool TryGet(string key, out byte[] png) => stored.TryGetValue(key, out png);

            public void Put(string key, byte[] png) => stored[key] = png;
        }
    }
}
