using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Agenerela.Providers.SdCpp.Tests
{
    // SdCppImageProvider with a scripted engine (DR-028): the queue on its worker thread, edits,
    // cancellation, unloading and failures, with no native library and no model.
    public sealed class SdCppImageProviderTests
    {
        private static readonly byte[] Transparent = ScriptedImageProvider.OnePixelPng;

        private static SdCppSettings Settings(Action<SdCppSettings> change = null)
        {
            var settings = new SdCppSettings { DiffusionModel = "z.gguf", Steps = 8, Cfg = 1f };
            change?.Invoke(settings);
            return settings;
        }

        private static SdCppImageProvider Provider(FakeEngine engine, Action<SdCppSettings> change = null)
        {
            return new SdCppImageProvider(Settings(change), engine, image =>
            {
                engine.Encoded.Add(image);
                return Transparent;
            });
        }

        private static ImageRequest Request(long? seed = 42) => new ImageRequest { Prompt = "a painted king", Width = 64, Height = 48, Seed = seed };

        private static async Task<T> NotCancelled<T>(Awaitable<T> awaitable)
        {
            try
            {
                return await awaitable;
            }
            catch (OperationCanceledException e)
            {
                Assert.Fail($"It ended cancelled, though nothing cancelled it: {e.Message}");
                return default;
            }
        }

        private static byte[] OpaquePng(Color32 topLeft, Color32 rest, int w = 2, int h = 2)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = Enumerable.Repeat(rest, w * h).ToArray();
            pixels[(h - 1) * w] = topLeft; // a texture's last row is the picture's top row
            t.SetPixels32(pixels);
            t.Apply();
            byte[] png = t.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(t);
            return png;
        }

        // ---- A new picture -------------------------------------------------------------------------

        [Test]
        public async Task APictureIsMadeOnTheWorkerAndTheModelLoadsOnce()
        {
            var engine = new FakeEngine();
            using (var provider = Provider(engine, s => s.DefaultParameters["negative_prompt"] = "text"))
            {
                int mainThread = Thread.CurrentThread.ManagedThreadId;
                var request = Request();
                request.Parameters["sample.steps"] = "6";

                var first = await NotCancelled(provider.GenerateAsync(request, CancellationToken.None));
                Assert.That(Thread.CurrentThread.ManagedThreadId, Is.EqualTo(mainThread), "The result comes back on the main thread.");
                var second = await NotCancelled(provider.GenerateAsync(Request(), CancellationToken.None));

                Assert.That(first.Succeeded, Is.True, first.ErrorMessage);
                Assert.That(first.ProviderName, Is.EqualTo("sdcpp"));
                Assert.That(first.Seed, Is.EqualTo(42));
                Assert.That(first.GenerateSeconds, Is.Not.Null);
                Assert.That(second.Succeeded, Is.True);
                Assert.That(engine.Loads, Is.EqualTo(1), "Loaded once, kept for the next picture.");
                Assert.That(engine.Threads.Distinct().Single(), Is.Not.EqualTo(mainThread), "The model runs off the main thread.");

                var job = engine.Jobs[0];
                Assert.That(job.Prompt, Is.EqualTo("a painted king"));
                Assert.That((job.Width, job.Height, job.Seed), Is.EqualTo((64, 48, 42L)));
                Assert.That(job.Options["sample.steps"], Is.EqualTo("6"));
                Assert.That(job.Options["negative_prompt"], Is.EqualTo("text"), "A default parameter.");
                Assert.That(job.Edit, Is.EqualTo(SdCppEditMode.None));
                Assert.That(provider.IsLoaded, Is.True);
                Assert.That(provider.LoadSeconds, Is.Not.Null);
            }
        }

        [Test]
        public async Task WithNoSeedOneIsChosenAndReported()
        {
            var engine = new FakeEngine();
            using (var provider = Provider(engine))
            {
                var result = await NotCancelled(provider.GenerateAsync(Request(seed: null), CancellationToken.None));

                Assert.That(result.Seed, Is.Not.Null);
                Assert.That(engine.Jobs[0].Seed, Is.EqualTo(result.Seed.Value));
                Assert.That(provider.Capabilities.HonoursSeed, Is.True);
            }
        }

        [Test]
        public void ARequestThatCannotBeSentFailsAtTheCallAndQueuesNothing()
        {
            var engine = new FakeEngine();
            using (var provider = Provider(engine))
            {
                var seed = Request();
                seed.Parameters["seed"] = "1";
                var odd = Request();
                odd.Width = 65;

                Assert.Throws<ArgumentException>(() => provider.GenerateAsync(seed, CancellationToken.None));
                Assert.Throws<ArgumentException>(() => provider.GenerateAsync(odd, CancellationToken.None));
                Assert.That(engine.Jobs, Is.Empty);
                Assert.That(engine.Loads, Is.EqualTo(0));
            }
        }

        // ---- Edits ---------------------------------------------------------------------------------

        [Test]
        public async Task ARepaintDecodesThePictureAtTheRequestSizeAndTurnsTheMaskAround()
        {
            var engine = new FakeEngine();
            using (var provider = Provider(engine))
            {
                var edit = Request();
                edit.Images.Add(OpaquePng(new Color32(255, 0, 0, 255), new Color32(0, 0, 255, 255)));
                // ScriptedImageProvider.OnePixelPng is half transparent (alpha 127), so a clear one is made here.
                edit.Mask = OpaquePng(new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0));
                edit.Parameters["strength"] = "0.6";
                var keep = Request();
                keep.Images.Add(Transparent);
                keep.Mask = OpaquePng(new Color32(0, 0, 0, 255), new Color32(0, 0, 0, 255));

                Assert.That(provider.Capabilities.Edits && provider.Capabilities.Masks, Is.True);
                await NotCancelled(provider.GenerateAsync(edit, CancellationToken.None));
                await NotCancelled(provider.GenerateAsync(keep, CancellationToken.None));

                var job = engine.Jobs[0];
                Assert.That(job.Edit, Is.EqualTo(SdCppEditMode.Repaint));
                Assert.That((job.InitImage.Width, job.InitImage.Height), Is.EqualTo((64, 48)), "Scaled to the request's size.");
                Assert.That(job.InitImage.Rgb.Take(3), Is.EqualTo(new byte[] { 255, 0, 0 }), "The top-left pixel comes first.");
                Assert.That(job.Mask, Has.Length.EqualTo(64 * 48));
                Assert.That(job.Mask, Has.All.EqualTo((byte)255), "A fully transparent mask changes everything.");
                Assert.That(job.Options["strength"], Is.EqualTo("0.6"));
                Assert.That(engine.Jobs[1].Mask, Has.All.EqualTo((byte)0), "An opaque mask keeps everything.");
            }
        }

        [Test]
        public async Task AnInstructionEditSendsEveryPictureAsAReferenceAndTakesNoMask()
        {
            var engine = new FakeEngine();
            using (var provider = Provider(engine, s =>
                   {
                       s.EditMode = SdCppEditMode.Instruction;
                       s.MaxReferenceImages = 2;
                   }))
            {
                var edit = Request();
                edit.Images.Add(OpaquePng(new Color32(1, 2, 3, 255), new Color32(1, 2, 3, 255), 4, 2));
                edit.Images.Add(Transparent);

                await NotCancelled(provider.GenerateAsync(edit, CancellationToken.None));

                var job = engine.Jobs[0];
                Assert.That(job.Edit, Is.EqualTo(SdCppEditMode.Instruction));
                Assert.That(job.References.Select(r => (r.Width, r.Height)), Is.EqualTo(new[] { (4, 2), (1, 1) }), "References keep their own size.");
                Assert.That(job.InitImage, Is.Null);
                Assert.That(provider.Capabilities.Masks, Is.False);

                edit.Mask = Transparent;
                Assert.Throws<ArgumentException>(() => provider.GenerateAsync(edit, CancellationToken.None));
                edit.Mask = null;
                edit.Images.Add(Transparent);
                Assert.Throws<ArgumentException>(() => provider.GenerateAsync(edit, CancellationToken.None), "Three pictures, two allowed.");
            }
        }

        [Test]
        public void AModelThatDoesNotEditRefusesAnEdit()
        {
            var engine = new FakeEngine();
            using (var provider = Provider(engine, s => s.EditMode = SdCppEditMode.None))
            {
                var edit = Request();
                edit.Images.Add(Transparent);

                Assert.That(provider.Capabilities.Edits, Is.False);
                Assert.Throws<ArgumentException>(() => provider.GenerateAsync(edit, CancellationToken.None));
            }
        }

        // ---- Failures are results ------------------------------------------------------------------

        [Test]
        public async Task AFailedLoadOrGenerationIsAResult()
        {
            var engine = new FakeEngine { LoadFails = new SdCppFailure(ProviderErrorKind.ModelNotFound, "no z.gguf") };
            using (var provider = Provider(engine))
            {
                var result = await NotCancelled(provider.GenerateAsync(Request(), CancellationToken.None));
                Assert.That(result.Error, Is.EqualTo(ProviderErrorKind.ModelNotFound));
                Assert.That(result.ErrorMessage, Is.EqualTo("no z.gguf"));

                engine.LoadFails = null;
                engine.GenerateFails = new SdCppFailure(ProviderErrorKind.ServerError, "out of memory");
                result = await NotCancelled(provider.GenerateAsync(Request(), CancellationToken.None));
                Assert.That(result.Error, Is.EqualTo(ProviderErrorKind.ServerError));

                engine.GenerateFails = new InvalidOperationException("boom");
                result = await NotCancelled(provider.GenerateAsync(Request(), CancellationToken.None));
                Assert.That(result.Error, Is.EqualTo(ProviderErrorKind.ServerError), "Anything else the engine throws is a result too.");

                engine.GenerateFails = null;
                Assert.That((await NotCancelled(provider.GenerateAsync(Request(), CancellationToken.None))).Succeeded, Is.True, "It recovers.");
            }
        }

        [Test]
        public async Task WithNoNativeLibraryTheResultSaysHowToInstallOne()
        {
            if (SdCppLibrary.LoadedFrom != null)
            {
                Assert.Ignore("A live test loaded the native library earlier in this run, so there is no missing library to report.");
            }

            string model = Path.GetTempFileName();
            try
            {
                var settings = Settings(s => s.DiffusionModel = model);
                using (var provider = new SdCppImageProvider(settings, new SdCppNativeEngine(settings, new string[0]), null))
                {
                    var result = await NotCancelled(provider.GenerateAsync(Request(), CancellationToken.None));

                    Assert.That(result.Error, Is.EqualTo(ProviderErrorKind.Unreachable));
                    Assert.That(result.ErrorMessage, Does.Contain("Local Image Models"));
                }

                var missing = Settings(s => s.DiffusionModel = model + ".nope");
                using (var provider = new SdCppImageProvider(missing, new SdCppNativeEngine(missing, new string[0]), null))
                {
                    var result = await NotCancelled(provider.GenerateAsync(Request(), CancellationToken.None));

                    Assert.That(result.Error, Is.EqualTo(ProviderErrorKind.ModelNotFound));
                    Assert.That(result.ErrorMessage, Does.Contain(".nope"));
                }
            }
            finally
            {
                File.Delete(model);
            }
        }

        // ---- Cancellation and lifetime -------------------------------------------------------------

        [Test]
        public async Task CancellingStopsThePictureBeingMadeAndOneStillQueued()
        {
            var engine = new FakeEngine { Hold = true };
            using (var provider = Provider(engine))
            using (var cts = new CancellationTokenSource())
            {
                var running = provider.GenerateAsync(Request(), cts.Token);
                var queued = provider.GenerateAsync(Request(seed: 7), cts.Token);
                Assert.That(engine.Started.Wait(5000), Is.True, "The first picture started.");

                cts.Cancel();
                int cancelled = 0;
                foreach (var awaitable in new[] { running, queued })
                {
                    try
                    {
                        await awaitable;
                    }
                    catch (OperationCanceledException)
                    {
                        cancelled++;
                    }
                }

                Assert.That(cancelled, Is.EqualTo(2));
                Assert.That(engine.Cancels, Is.GreaterThanOrEqualTo(1), "The running picture was told to stop.");
                Assert.That(engine.Jobs.Select(j => j.Seed), Is.EqualTo(new[] { 42L }), "The queued one never started.");

                engine.Hold = false;
                Assert.That((await NotCancelled(provider.GenerateAsync(Request(), CancellationToken.None))).Succeeded, Is.True, "The next one runs.");
            }
        }

        [Test]
        public async Task ACancelWhileTheModelLoadsStopsThePictureBeforeItStarts()
        {
            // sd.cpp cannot stop a load, and ignores a cancel before a picture starts executing, so
            // the provider must check the token itself once the load is done (review item 4).
            var engine = new FakeEngine { HoldLoad = true };
            using (var provider = Provider(engine))
            using (var cts = new CancellationTokenSource())
            {
                var pending = provider.GenerateAsync(Request(), cts.Token);
                Assert.That(engine.LoadStarted.Wait(5000), Is.True, "The load started.");
                cts.Cancel();
                engine.ReleaseLoad.Set();

                bool cancelled = false;
                try
                {
                    await pending;
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }

                Assert.That(cancelled, Is.True);
                Assert.That(engine.Jobs, Is.Empty, "No picture was started after the cancelled load.");
                Assert.That(provider.IsLoaded, Is.True, "The load itself finished, for the next picture.");
            }
        }

        [Test]
        public async Task DisposeCancelsThePictureRunningAndEveryOneStillQueued()
        {
            var engine = new FakeEngine { Hold = true };
            var provider = Provider(engine);
            var running = provider.GenerateAsync(Request(), CancellationToken.None);
            var queued = provider.GenerateAsync(Request(seed: 7), CancellationToken.None);
            var unload = provider.UnloadAsync(CancellationToken.None);
            Assert.That(engine.Started.Wait(5000), Is.True, "The first picture started.");

            provider.Dispose();
            int cancelled = 0;
            foreach (var awaitable in new[] { running, queued })
            {
                try
                {
                    await awaitable;
                }
                catch (OperationCanceledException)
                {
                    cancelled++;
                }
            }

            try
            {
                await unload;
            }
            catch (OperationCanceledException)
            {
                cancelled++;
            }

            Assert.That(cancelled, Is.EqualTo(3), "The running picture, the queued one and the queued unload all end cancelled.");
            Assert.That(engine.Jobs.Select(j => j.Seed), Is.EqualTo(new[] { 42L }), "The queued picture never ran.");
            Assert.That(engine.Unloads, Is.EqualTo(0), "The queued unload never ran; Dispose frees the model instead.");
            Assert.That(engine.Disposed.Wait(5000), Is.True);
        }

        [Test]
        public async Task WarmUpLoadsAndUnloadFreesTheModel()
        {
            var engine = new FakeEngine();
            using (var provider = Provider(engine))
            {
                IModelLifecycle lifecycle = provider;
                await lifecycle.WarmUpAsync(CancellationToken.None);
                Assert.That(provider.IsLoaded, Is.True);

                Assert.That(await NotCancelled(lifecycle.UnloadAsync(CancellationToken.None)), Is.True);
                Assert.That(provider.IsLoaded, Is.False);
                Assert.That(engine.Unloads, Is.EqualTo(1));

                await NotCancelled(provider.GenerateAsync(Request(), CancellationToken.None));
                Assert.That(engine.Loads, Is.EqualTo(2), "The next picture loads it again.");
            }
        }

        [Test]
        public async Task ListModelsNamesTheFilesItLoads()
        {
            using (var provider = Provider(new FakeEngine(), s =>
                   {
                       s.DiffusionModel = "/models/z_image_turbo-Q4_K.gguf";
                       s.TextEncoder = "/models/qwen.gguf";
                   }))
            {
                var models = await NotCancelled(((IModelLifecycle)provider).ListModelsAsync(CancellationToken.None));

                Assert.That(models.Select(m => m.Name), Is.EqualTo(new[] { "z_image_turbo-Q4_K.gguf", "qwen.gguf" }));
            }
        }

        [Test]
        public void ADisposedProviderTakesNoRequestAndFreesTheModel()
        {
            var engine = new FakeEngine();
            var provider = Provider(engine);
            provider.Dispose();

            Assert.Throws<ObjectDisposedException>(() => provider.GenerateAsync(Request(), CancellationToken.None));
            Assert.That(engine.Disposed.Wait(5000), Is.True);
        }

        // ---- The scripted engine -------------------------------------------------------------------

        private sealed class FakeEngine : ISdCppEngine
        {
            public readonly List<SdCppJob> Jobs = new List<SdCppJob>();
            public readonly List<int> Threads = new List<int>();
            public readonly List<RgbImage> Encoded = new List<RgbImage>();
            public readonly ManualResetEventSlim Started = new ManualResetEventSlim();
            public readonly ManualResetEventSlim Disposed = new ManualResetEventSlim();
            public readonly ManualResetEventSlim LoadStarted = new ManualResetEventSlim();
            public readonly ManualResetEventSlim ReleaseLoad = new ManualResetEventSlim();
            public volatile bool HoldLoad;
            private readonly ManualResetEventSlim cancelled = new ManualResetEventSlim();
            public volatile bool Hold;
            public Exception LoadFails;
            public Exception GenerateFails;
            public int Loads, Unloads, Cancels;
            private bool loaded;

            public bool IsLoaded => loaded;

            public void Load()
            {
                if (LoadFails != null)
                {
                    throw LoadFails;
                }

                LoadStarted.Set();
                if (HoldLoad && !ReleaseLoad.Wait(10000))
                {
                    throw new SdCppFailure(ProviderErrorKind.Timeout, "the load was never released");
                }

                Loads++;
                loaded = true;
            }

            public RgbImage Generate(SdCppJob job)
            {
                lock (Jobs)
                {
                    Jobs.Add(job);
                    Threads.Add(Thread.CurrentThread.ManagedThreadId);
                }

                Started.Set();
                if (GenerateFails != null)
                {
                    throw GenerateFails;
                }

                if (Hold)
                {
                    if (!cancelled.Wait(10000))
                    {
                        throw new SdCppFailure(ProviderErrorKind.Timeout, "never cancelled");
                    }

                    cancelled.Reset();
                    throw new OperationCanceledException();
                }

                return new RgbImage(job.Width, job.Height, new byte[job.Width * job.Height * 3]);
            }

            public void Cancel()
            {
                Interlocked.Increment(ref Cancels);
                cancelled.Set();
            }

            public void Unload()
            {
                Unloads++;
                loaded = false;
            }

            public void Dispose() => Disposed.Set();
        }
    }
}
