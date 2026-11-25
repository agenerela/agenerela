using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Agenerela.Providers.SdCpp
{
    /// <summary>
    /// An <see cref="IImageProvider"/> that runs an image model inside the game with
    /// stable-diffusion.cpp, with no server (DR-028): it loads the model once, then makes and edits
    /// pictures on a worker thread of its own, one at a time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Isolated</b> in its own package, <c>com.agenerela.sdcpp</c> (assembly
    /// <c>Agenerela.SdCpp</c>), so the framework's core keeps Newtonsoft as its only dependency
    /// (hard rule 6). The native library and the model files are downloaded on the developer's
    /// choice and live under <c>StreamingAssets</c>; neither is in the package.
    /// </para>
    /// <para>
    /// <b>Threads.</b> Call it from the main thread. Input pictures are decoded there before the
    /// request is queued; the model runs on the worker; the PNG is encoded on the worker; the result
    /// comes back on the main thread. The main thread never waits on the model.
    /// </para>
    /// <para>
    /// <b>The card.</b> The model loads on the first request or on <see cref="WarmUpAsync"/>, and
    /// <see cref="UnloadAsync"/> gives its memory back. On an 8 GB card it does not fit beside a loaded
    /// language model: unload that first (DR-028, Q8). While a picture is made the card is shared
    /// with Unity's rendering, so frames slow down; make pictures behind a loading screen or a pause.
    /// </para>
    /// <para>
    /// <b>Edits</b> follow <see cref="SdCppSettings.EditMode"/>: a repaint of <c>Images[0]</c>,
    /// with the mask and <c>strength</c>, or an instruction edit with every picture a reference.
    /// </para>
    /// <para>
    /// <b>Failures</b> are results: no native library is <see cref="ProviderErrorKind.Unreachable"/>,
    /// a model file missing or broken is <see cref="ProviderErrorKind.ModelNotFound"/>, an option
    /// sd.cpp will not take is <see cref="ProviderErrorKind.Rejected"/>, and running out of memory
    /// or a failed generation is <see cref="ProviderErrorKind.ServerError"/>, each with what to do.
    /// Cancelling the token stops a picture being made (<c>sd_cancel_generation</c>) and throws
    /// <see cref="OperationCanceledException"/>.
    /// </para>
    /// </remarks>
    public sealed class SdCppImageProvider : IImageProvider, IModelLifecycle, IDisposable
    {
        private readonly SdCppSettings settings;
        private readonly ISdCppEngine engine;
        private readonly BlockingCollection<Action> work = new BlockingCollection<Action>();
        private readonly Thread worker;
        private readonly System.Random seeds = new System.Random();
        private readonly Func<RgbImage, byte[]> encode;
        private volatile bool disposed;

        /// <summary>A provider for the model <paramref name="settings"/> name. Nothing loads until the first request or <see cref="WarmUpAsync"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
        public SdCppImageProvider(SdCppSettings settings)
            : this(settings, null, null)
        {
        }

        // The seam EditMode tests use: a scripted engine, and an encoder that needs no Unity.
        internal SdCppImageProvider(SdCppSettings settings, ISdCppEngine engine, Func<RgbImage, byte[]> encode)
        {
            this.settings = (settings ?? throw new ArgumentNullException(nameof(settings))).Copy();
            // Resolved here, on the main thread: Application.streamingAssetsPath is not for other threads.
            this.engine = engine ?? new SdCppNativeEngine(this.settings, SdCppLibrary.Candidates(this.settings.LibraryFolder, Application.streamingAssetsPath));
            this.encode = encode ?? SdCppPixels.EncodePng;
            Capabilities = new ImageCapabilities
            {
                SizeStep = 16,
                MaxSide = 2048,
                HonoursSeed = true,
                Edits = this.settings.EditMode != SdCppEditMode.None,
                Masks = this.settings.EditMode == SdCppEditMode.Repaint,
                MaxImages = this.settings.EditMode == SdCppEditMode.Instruction ? Math.Max(1, this.settings.MaxReferenceImages) : 1,
            };

            worker = new Thread(Work) { IsBackground = true, Name = "Agenerela sd.cpp" };
            worker.Start();
            AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
        }

        /// <summary><c>"sdcpp"</c>.</summary>
        public string Name => "sdcpp";

        /// <summary>Sides a multiple of 16 up to 2048, seeds honoured; edits as <see cref="SdCppSettings.EditMode"/> says.</summary>
        public ImageCapabilities Capabilities { get; }

        /// <summary>True while the model is in memory.</summary>
        public bool IsLoaded => engine.IsLoaded;

        /// <summary>Seconds the last load took, or null before one.</summary>
        public float? LoadSeconds { get; private set; }

        /// <summary>Makes, or edits, one picture for <paramref name="request"/>.</summary>
        /// <exception cref="ArgumentException">What <see cref="ImageCapabilities.WhyNot"/> reports, or an input picture that is not a PNG.</exception>
        /// <exception cref="ObjectDisposedException">The provider was disposed.</exception>
        public Awaitable<ImageResult> GenerateAsync(ImageRequest request, CancellationToken ct)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (disposed)
            {
                throw new ObjectDisposedException(nameof(SdCppImageProvider));
            }

            string why = Capabilities.WhyNot(request);
            if (why != null)
            {
                throw new ArgumentException(why, nameof(request));
            }

            // Decoded now, on the main thread, so a picture that will not decode fails at the call.
            var job = Build(request);
            return Run(job, ct);
        }

        /// <summary>Loads the model now, so the first picture does not wait for it.</summary>
        public async Awaitable WarmUpAsync(CancellationToken ct)
        {
            await Queue(() =>
            {
                Load();
                return true;
            }, ct);
        }

        /// <summary>Frees the model and its memory on the card, once any picture being made is done. True once it is out.</summary>
        public async Awaitable<bool> UnloadAsync(CancellationToken ct)
        {
            return await Queue(() =>
            {
                engine.Unload();
                return true;
            }, ct);
        }

        /// <summary>The model files this provider loads, with their sizes on disk.</summary>
        public Awaitable<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct)
        {
            var source = new AwaitableCompletionSource<IReadOnlyList<ModelInfo>>();
            var files = new[] { settings.DiffusionModel, settings.TextEncoder, settings.Vae, settings.Checkpoint }
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(p => new ModelInfo(Path.GetFileName(p), File.Exists(p) ? new FileInfo(p).Length : 0L))
                .ToList();
            source.SetResult(files);
            return source.Awaitable;
        }

        /// <summary>Stops the worker and frees the model. Pictures still queued end cancelled.</summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            AppDomain.CurrentDomain.DomainUnload -= OnDomainUnload;
            engine.Cancel();
            work.Add(engine.Dispose);
            work.CompleteAdding();
        }

        // ---- The request ---------------------------------------------------------------------------

        private SdCppJob Build(ImageRequest request)
        {
            var job = new SdCppJob
            {
                Prompt = request.Prompt ?? "",
                Width = request.Width,
                Height = request.Height,
                Seed = request.Seed ?? NextSeed(),
            };

            foreach (var pair in settings.DefaultParameters.Where(d => request.Parameters == null || !request.Parameters.ContainsKey(d.Key)))
            {
                job.Options[pair.Key] = pair.Value;
            }

            foreach (var pair in request.Parameters ?? new Dictionary<string, string>())
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Key == "prompt" || pair.Key == "width" || pair.Key == "height" || pair.Key == "seed")
                {
                    throw new ArgumentException($"The parameter '{pair.Key}' is set through ImageRequest's own fields (Prompt, Width, Height, Seed).", nameof(request));
                }

                job.Options[pair.Key] = pair.Value;
            }

            if (!request.IsEdit)
            {
                return job;
            }

            job.Edit = settings.EditMode;
            if (settings.EditMode == SdCppEditMode.Repaint)
            {
                job.InitImage = SdCppPixels.Decode(request.Images[0], request.Width, request.Height, out _);
                if (request.Mask != null)
                {
                    SdCppPixels.Decode(request.Mask, request.Width, request.Height, out byte[] alpha);
                    job.Mask = SdCppPixels.MaskFromAlpha(alpha);
                }
            }
            else
            {
                // References keep their own size: an edit model reads them as they are.
                foreach (byte[] png in request.Images)
                {
                    var texture = new Texture2D(2, 2);
                    int w, h;
                    try
                    {
                        texture.LoadImage(png, false);
                        w = texture.width;
                        h = texture.height;
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(texture);
                    }

                    job.References.Add(SdCppPixels.Decode(png, w, h, out _));
                }
            }

            return job;
        }

        private long NextSeed()
        {
            lock (seeds)
            {
                return seeds.Next(0, int.MaxValue);
            }
        }

        // ---- The worker ----------------------------------------------------------------------------

        private async Awaitable<ImageResult> Run(SdCppJob job, CancellationToken ct)
        {
            var clock = Stopwatch.StartNew();
            float? generateSeconds = null;
            byte[] png;
            try
            {
                png = await Queue(() =>
                {
                    Load();
                    var made = Stopwatch.StartNew();
                    RgbImage image = engine.Generate(job);
                    generateSeconds = (float)made.Elapsed.TotalSeconds;
                    return encode(image);
                }, ct);
            }
            catch (SdCppFailure f)
            {
                return ImageResult.Failed(f.Kind, Name, f.Message, (float)clock.Elapsed.TotalSeconds);
            }
            catch (Exception e) when (!(e is OperationCanceledException) && !(e is ObjectDisposedException))
            {
                return ImageResult.Failed(ProviderErrorKind.ServerError, Name, $"stable-diffusion.cpp failed: {e.GetType().Name}: {e.Message}", (float)clock.Elapsed.TotalSeconds);
            }

            if (!ImageResult.IsPng(png))
            {
                return ImageResult.Failed(ProviderErrorKind.MalformedResponse, Name, "The picture could not be encoded as a PNG.", (float)clock.Elapsed.TotalSeconds);
            }

            return ImageResult.Made(png, Name, (float)clock.Elapsed.TotalSeconds, job.Seed, generateSeconds);
        }

        private void Load()
        {
            if (engine.IsLoaded)
            {
                return;
            }

            var clock = Stopwatch.StartNew();
            engine.Load();
            LoadSeconds = (float)clock.Elapsed.TotalSeconds;
        }

        // Runs step on the worker; the await resumes on the main thread. The caller's token takes a
        // job off the queue before it starts, or stops the one running.
        private async Awaitable<T> Queue<T>(Func<T> step, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(SdCppImageProvider));
            }

            var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            int started = 0;
            using (ct.Register(() =>
                   {
                       if (Interlocked.CompareExchange(ref started, 2, 0) == 0)
                       {
                           done.TrySetCanceled(ct);
                       }
                       else if (Volatile.Read(ref started) == 1)
                       {
                           engine.Cancel();
                       }
                   }))
            {
                try
                {
                    work.Add(() =>
                    {
                        if (Interlocked.CompareExchange(ref started, 1, 0) != 0)
                        {
                            return; // cancelled while it waited
                        }

                        try
                        {
                            T value = step();
                            if (ct.IsCancellationRequested)
                            {
                                done.TrySetCanceled(ct);
                            }
                            else
                            {
                                done.TrySetResult(value);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            done.TrySetCanceled(ct);
                        }
                        catch (Exception e)
                        {
                            done.TrySetException(e);
                        }
                    });
                }
                catch (InvalidOperationException)
                {
                    throw new ObjectDisposedException(nameof(SdCppImageProvider));
                }

                try
                {
                    T value = await done.Task;
                    await Awaitable.MainThreadAsync();
                    return value;
                }
                catch (OperationCanceledException e)
                {
                    await Awaitable.MainThreadAsync();
                    throw new OperationCanceledException("The caller cancelled the picture.", e, ct);
                }
                catch (Exception)
                {
                    await Awaitable.MainThreadAsync();
                    throw;
                }
            }
        }

        private void Work()
        {
            foreach (Action step in work.GetConsumingEnumerable())
            {
                try
                {
                    step();
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }

        private void OnDomainUnload(object sender, EventArgs e)
        {
            // A domain reload in the editor: free the model now, or its memory on the card is lost
            // until the editor closes. Waits up to 10 s for a picture being made to stop.
            engine.Cancel();
            Dispose();
            worker.Join(TimeSpan.FromSeconds(10));
            SdCppLibrary.BeforeReload();
        }
    }
}
