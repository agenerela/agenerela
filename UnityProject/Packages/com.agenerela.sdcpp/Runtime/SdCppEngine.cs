using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Agenerela.Providers.SdCpp.Native;

namespace Agenerela.Providers.SdCpp
{
    /// <summary>One picture for the engine to make, everything decoded already.</summary>
    internal sealed class SdCppJob
    {
        internal string Prompt = "";
        internal int Width;
        internal int Height;
        internal long Seed;
        internal SdCppEditMode Edit = SdCppEditMode.None;
        internal RgbImage InitImage;
        internal byte[] Mask;
        internal List<RgbImage> References = new List<RgbImage>();
        internal Dictionary<string, string> Options = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>The caller's token: checked before the picture starts, and watched while it runs.</summary>
        internal CancellationToken Token;
    }

    /// <summary>A failure the provider reports as a result, with its kind.</summary>
    internal sealed class SdCppFailure : Exception
    {
        internal SdCppFailure(ProviderErrorKind kind, string message, Exception inner = null)
            : base(message, inner)
        {
            Kind = kind;
        }

        internal ProviderErrorKind Kind { get; }
    }

    /// <summary>
    /// What the provider's worker thread drives: load, generate, cancel and unload. The real one calls
    /// sd.cpp; EditMode tests give a scripted one, so the provider's queue, cancellation and
    /// failures are tested with no native library.
    /// </summary>
    internal interface ISdCppEngine : IDisposable
    {
        bool IsLoaded { get; }

        /// <summary>Loads the model. Throws <see cref="SdCppFailure"/>.</summary>
        void Load();

        /// <summary>
        /// Makes one picture. Throws <see cref="SdCppFailure"/>, or <see cref="OperationCanceledException"/>
        /// when <see cref="SdCppJob.Token"/> is cancelled, whether before it starts or while it runs.
        /// </summary>
        RgbImage Generate(SdCppJob job);

        /// <summary>Asks a running <see cref="Generate"/> to stop. Called from another thread.</summary>
        void Cancel();

        /// <summary>Frees the model and its memory on the card.</summary>
        void Unload();
    }

    /// <summary>The engine that calls stable-diffusion.cpp. Every member but <see cref="Cancel"/> runs on the provider's worker thread.</summary>
    internal sealed class SdCppNativeEngine : ISdCppEngine
    {
        private static readonly SdCppApi.LogCallback KeepLog = OnLog;
        private static volatile bool logging;

        // sd.cpp's first warning or error since the last load or picture began: its status codes say
        // little ("prompt encoding failed"), its log says why ("RAM weight capacity exceeded").
        private static volatile string firstProblem;

        private readonly SdCppSettings settings;
        private readonly IReadOnlyList<string> libraryCandidates;
        // Guards ctx: Cancel comes from other threads and must never use a context Unload is freeing.
        private readonly object ctxGate = new object();
        private SdCppApi api;
        private IntPtr ctx = IntPtr.Zero;

        internal SdCppNativeEngine(SdCppSettings settings, IReadOnlyList<string> libraryCandidates)
        {
            this.settings = settings;
            this.libraryCandidates = libraryCandidates;
        }

        public bool IsLoaded => ctx != IntPtr.Zero;

        public void Load()
        {
            if (IsLoaded)
            {
                return;
            }

            foreach (var (slot, path) in new[] { ("DiffusionModel", settings.DiffusionModel), ("TextEncoder", settings.TextEncoder), ("Vae", settings.Vae), ("Checkpoint", settings.Checkpoint) })
            {
                if (!string.IsNullOrEmpty(path) && !File.Exists(path))
                {
                    throw new SdCppFailure(ProviderErrorKind.ModelNotFound, $"The model file for {slot} is not there: {path}. Download it from the recipe's Inspector or fix the path.");
                }
            }

            if (string.IsNullOrEmpty(settings.DiffusionModel) && string.IsNullOrEmpty(settings.Checkpoint))
            {
                throw new SdCppFailure(ProviderErrorKind.ModelNotFound, "No model file is set: give SdCppSettings.DiffusionModel or Checkpoint.");
            }

            try
            {
                api = SdCppLibrary.Load(libraryCandidates);
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException)
            {
                throw new SdCppFailure(ProviderErrorKind.Unreachable, e.Message, e);
            }

            if (settings.LogNative && !logging)
            {
                logging = true;
                api.SetLogCallback(KeepLog, IntPtr.Zero);
            }

            firstProblem = null;
            IntPtr opt = api.CtxOptCreate();
            try
            {
                SetString(opt, "diffusion_model_path", settings.DiffusionModel);
                SetString(opt, "llm_path", settings.TextEncoder);
                SetString(opt, "vae_path", settings.Vae);
                SetString(opt, "model_path", settings.Checkpoint);
                Set(opt, "n_threads", (settings.Threads > 0 ? settings.Threads : Math.Max(1, Environment.ProcessorCount / 2)).ToString(CultureInfo.InvariantCulture), context: true);
                if (!settings.FlashAttention)
                {
                    // Left alone it is "auto", which sd.cpp turns on where the backend supports it.
                    SetString(opt, "flash_attn_mode", "off");
                }
                var placement = new List<string>();
                if (settings.TextEncoderOnCpu)
                {
                    placement.Add("te=cpu");
                }

                if (settings.VaeOnCpu)
                {
                    placement.Add("vae=cpu");
                }

                SetString(opt, "backend", string.Join(",", placement));
                SetString(opt, "params_backend", settings.OffloadToCpu ? "*=cpu" : "");
                foreach (var pair in settings.ContextOptions)
                {
                    Set(opt, pair.Key, pair.Value, context: true);
                }

                int status = api.ContextCreate(opt, out IntPtr created);
                if (status != SdCppApi.StatusOk || created == IntPtr.Zero)
                {
                    throw Failure(status, "load the model");
                }

                lock (ctxGate)
                {
                    ctx = created;
                }
            }
            finally
            {
                api.OptFree(opt);
            }
        }

        public RgbImage Generate(SdCppJob job)
        {
            Load();
            job.Token.ThrowIfCancellationRequested();
            firstProblem = null;
            api.CancelGeneration(ctx, SdCppApi.CancelReset);
            IntPtr opt = api.ImgGenOptCreate();
            var tensors = new List<IntPtr>();
            IntPtr result = IntPtr.Zero;
            try
            {
                SetString(opt, "prompt", job.Prompt);
                Set(opt, "width", job.Width.ToString(CultureInfo.InvariantCulture), context: false);
                Set(opt, "height", job.Height.ToString(CultureInfo.InvariantCulture), context: false);
                Set(opt, "seed", job.Seed.ToString(CultureInfo.InvariantCulture), context: false);
                if (settings.Steps > 0)
                {
                    Set(opt, "sample.steps", settings.Steps.ToString(CultureInfo.InvariantCulture), context: false);
                }

                if (settings.Cfg >= 0f)
                {
                    Set(opt, "sample.guidance.txt_cfg", settings.Cfg.ToString("R", CultureInfo.InvariantCulture), context: false);
                }

                if (!string.IsNullOrEmpty(settings.SampleMethod))
                {
                    SetString(opt, "sample.method", settings.SampleMethod);
                }

                if (!string.IsNullOrEmpty(settings.Scheduler))
                {
                    SetString(opt, "sample.scheduler", settings.Scheduler);
                }

                Set(opt, "vae_tiling.enabled", settings.VaeTiling ? "true" : "false", context: false);

                if (job.Edit == SdCppEditMode.Repaint && job.InitImage != null)
                {
                    IntPtr image = Tensor(job.InitImage.Rgb, 3, job.InitImage.Width, job.InitImage.Height);
                    tensors.Add(image);
                    Check(api.OptSetTensor(opt, Key("init_image", out IntPtr k1), image), "init_image", k1);
                    if (job.Mask != null)
                    {
                        IntPtr mask = Tensor(job.Mask, 1, job.Width, job.Height);
                        tensors.Add(mask);
                        Check(api.OptSetTensor(opt, Key("mask_image", out IntPtr k2), mask), "mask_image", k2);
                    }
                }
                else if (job.Edit == SdCppEditMode.Instruction && job.References.Count > 0)
                {
                    var refs = new IntPtr[job.References.Count];
                    for (int i = 0; i < refs.Length; i++)
                    {
                        // Kept as each is made, so one that fails cannot leak the ones before it.
                        refs[i] = Tensor(job.References[i].Rgb, 3, job.References[i].Width, job.References[i].Height);
                        tensors.Add(refs[i]);
                    }

                    Check(api.OptSetTensors(opt, Key("ref_images", out IntPtr k3), refs, (UIntPtr)refs.Length), "ref_images", k3);
                }

                foreach (var pair in job.Options)
                {
                    Set(opt, pair.Key, pair.Value, context: false);
                }

                // A cancel sent before sd.cpp starts executing is ignored, so it is checked once more
                // here and then sent again every 100 ms while the picture runs.
                job.Token.ThrowIfCancellationRequested();
                int status;
                using (var watch = new Timer(_ =>
                       {
                           if (job.Token.IsCancellationRequested)
                           {
                               Cancel();
                           }
                       }, null, 100, 100))
                {
                    status = api.GenerateImage(ctx, opt, out result);
                    using (var stopped = new ManualResetEvent(false))
                    {
                        if (watch.Dispose(stopped))
                        {
                            stopped.WaitOne();
                        }
                    }
                }

                if (status == SdCppApi.StatusOk && job.Token.IsCancellationRequested)
                {
                    status = SdCppApi.StatusCancelled;
                }

                if (status == SdCppApi.StatusCancelled)
                {
                    throw new OperationCanceledException("The picture was cancelled while stable-diffusion.cpp was making it.");
                }

                if (status != SdCppApi.StatusOk || result == IntPtr.Zero)
                {
                    throw Failure(status, "make the picture");
                }

                if ((ulong)api.ResultImageCount(result) < 1)
                {
                    throw new SdCppFailure(ProviderErrorKind.MalformedResponse, "stable-diffusion.cpp finished with no picture.");
                }

                return Read(api.ResultGetImage(result, UIntPtr.Zero));
            }
            finally
            {
                if (result != IntPtr.Zero)
                {
                    api.ResultFree(result);
                }

                foreach (IntPtr t in tensors)
                {
                    api.TensorFree(t);
                }

                api.OptFree(opt);
            }
        }

        public void Cancel()
        {
            lock (ctxGate)
            {
                if (api != null && ctx != IntPtr.Zero)
                {
                    api.CancelGeneration(ctx, SdCppApi.CancelAll);
                }
            }
        }

        public void Unload()
        {
            lock (ctxGate)
            {
                if (ctx != IntPtr.Zero)
                {
                    api.CtxFree(ctx);
                    ctx = IntPtr.Zero;
                }
            }
        }

        public void Dispose() => Unload();

        // ---- Options -------------------------------------------------------------------------------

        private void SetString(IntPtr opt, string key, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            IntPtr k = Key(key, out _);
            IntPtr v = SdCppApi.Utf8(value);
            try
            {
                Check(api.OptSetString(opt, k, v), key, IntPtr.Zero);
            }
            finally
            {
                Marshal.FreeHGlobal(k);
                Marshal.FreeHGlobal(v);
            }
        }

        // An open string set as the type the key takes: an integer, a number, a flag or text, in that
        // order, moving on only when sd.cpp says the type does not match.
        private void Set(IntPtr opt, string key, string raw, bool context)
        {
            raw = (raw ?? "").Trim();
            IntPtr k = Key(key, out _);
            try
            {
                int status = SdCppApi.StatusTypeMismatch;
                if (long.TryParse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long whole))
                {
                    status = api.OptSetInt(opt, k, whole);
                }

                if (status == SdCppApi.StatusTypeMismatch && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                {
                    status = api.OptSetFloat(opt, k, number);
                }

                if (status == SdCppApi.StatusTypeMismatch && (raw == "true" || raw == "false"))
                {
                    status = api.OptSetBool(opt, k, raw == "true");
                }

                if (status == SdCppApi.StatusTypeMismatch)
                {
                    IntPtr v = SdCppApi.Utf8(raw);
                    try
                    {
                        status = api.OptSetString(opt, k, v);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(v);
                    }
                }

                if (status != SdCppApi.StatusOk)
                {
                    throw new SdCppFailure(ProviderErrorKind.Rejected,
                        $"stable-diffusion.cpp would not take the {(context ? "context" : "generation")} option {key} = '{raw}': {StatusName(status)}. {api.LastError}".Trim());
                }
            }
            finally
            {
                Marshal.FreeHGlobal(k);
            }
        }

        private static IntPtr Key(string key, out IntPtr same)
        {
            same = SdCppApi.Utf8(key);
            return same;
        }

        private void Check(int status, string key, IntPtr keyToFree)
        {
            if (keyToFree != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(keyToFree);
            }

            if (status != SdCppApi.StatusOk)
            {
                throw new SdCppFailure(ProviderErrorKind.Rejected, $"stable-diffusion.cpp would not take the option {key}: {StatusName(status)}. {api.LastError}".Trim());
            }
        }

        // ---- Pictures ------------------------------------------------------------------------------

        private IntPtr Tensor(byte[] data, int channels, int width, int height)
        {
            var shape = new long[] { channels, width, height };
            IntPtr t = api.TensorCreateU8(SdCppApi.TensorU8, (UIntPtr)3, shape, data, (UIntPtr)data.Length);
            if (t == IntPtr.Zero)
            {
                throw new SdCppFailure(ProviderErrorKind.Rejected, $"stable-diffusion.cpp would not take a {width}x{height} picture: {api.LastError}");
            }

            return t;
        }

        private RgbImage Read(IntPtr tensor)
        {
            if (tensor == IntPtr.Zero || api.TensorType(tensor) != SdCppApi.TensorU8 || (ulong)api.TensorRank(tensor) < 3)
            {
                throw new SdCppFailure(ProviderErrorKind.MalformedResponse, "stable-diffusion.cpp returned a picture that is not 8-bit [C, W, H].");
            }

            long c = api.TensorDimension(tensor, (UIntPtr)0), w = api.TensorDimension(tensor, (UIntPtr)1), h = api.TensorDimension(tensor, (UIntPtr)2);
            long size = (long)(ulong)api.TensorByteSize(tensor);
            if (c < 3 || w < 1 || h < 1 || size != c * w * h)
            {
                throw new SdCppFailure(ProviderErrorKind.MalformedResponse, $"stable-diffusion.cpp returned a picture shaped [{c}, {w}, {h}] in {size} bytes.");
            }

            var raw = new byte[size];
            Marshal.Copy(api.TensorData(tensor), raw, 0, (int)size);
            if (c == 3)
            {
                return new RgbImage((int)w, (int)h, raw);
            }

            var rgb = new byte[w * h * 3];
            for (long i = 0; i < w * h; i++)
            {
                rgb[i * 3] = raw[i * c];
                rgb[i * 3 + 1] = raw[i * c + 1];
                rgb[i * 3 + 2] = raw[i * c + 2];
            }

            return new RgbImage((int)w, (int)h, rgb);
        }

        // ---- Failures ------------------------------------------------------------------------------

        private SdCppFailure Failure(int status, string doing)
        {
            string detail = api.LastError;
            string logged = firstProblem;
            string why = $"stable-diffusion.cpp could not {doing}: {StatusName(status)}.{(detail.Length > 0 ? " " + detail : "")}" +
                         (string.IsNullOrEmpty(logged) ? "" : $" It logged: {logged}");
            if (logged != null && logged.IndexOf("RAM weight capacity", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new SdCppFailure(ProviderErrorKind.ServerError, why +
                    " System memory is too full for the weights placed on the CPU: free some, or keep the text encoder on the card (TextEncoderOnCpu off).");
            }

            switch (status)
            {
                case SdCppApi.StatusOutOfMemory:
                    return new SdCppFailure(ProviderErrorKind.ServerError, why +
                        " The card ran out of memory: unload the language model first, keep the text encoder on the CPU, or turn OffloadToCpu on.");
                case SdCppApi.StatusLoadFailed:
                    return new SdCppFailure(ProviderErrorKind.ModelNotFound, why + " Check that each file is the model the recipe says and is complete.");
                case SdCppApi.StatusInvalidArgument:
                case SdCppApi.StatusUnknownKey:
                case SdCppApi.StatusTypeMismatch:
                case SdCppApi.StatusOutOfRange:
                case SdCppApi.StatusUnsupported:
                    return new SdCppFailure(ProviderErrorKind.Rejected, why);
                default:
                    return new SdCppFailure(ProviderErrorKind.ServerError, why);
            }
        }

        internal static string StatusName(int status)
        {
            switch (status)
            {
                case SdCppApi.StatusOk: return "OK";
                case SdCppApi.StatusInvalidArgument: return "invalid argument";
                case SdCppApi.StatusUnknownKey: return "unknown key";
                case SdCppApi.StatusTypeMismatch: return "wrong type";
                case SdCppApi.StatusOutOfRange: return "out of range";
                case SdCppApi.StatusUnsupported: return "not supported by this model";
                case SdCppApi.StatusOutOfMemory: return "out of memory";
                case SdCppApi.StatusLoadFailed: return "the model did not load";
                case SdCppApi.StatusGenerationFailed: return "generation failed";
                case SdCppApi.StatusBusy: return "busy";
                case SdCppApi.StatusCancelled: return "cancelled";
                default: return "status " + status.ToString(CultureInfo.InvariantCulture);
            }
        }

        // sd.cpp's log, from whichever thread it writes on. Warnings and errors only.
        [AOT.MonoPInvokeCallback(typeof(SdCppApi.LogCallback))]
        private static void OnLog(int level, IntPtr text, IntPtr data)
        {
            if (level < 3)
            {
                return;
            }

            string line = SdCppApi.Text(text).TrimEnd();
            if (line.Length == 0)
            {
                return;
            }

            if (firstProblem == null)
            {
                firstProblem = line;
            }

            if (level >= 4)
            {
                UnityEngine.Debug.LogWarning("[stable-diffusion.cpp] " + line);
            }
            else
            {
                UnityEngine.Debug.Log("[stable-diffusion.cpp] " + line);
            }
        }

        // Called before a domain reload, so sd.cpp never calls a delegate that no longer exists.
        internal static void StopLogging(SdCppApi loadedApi)
        {
            if (logging && loadedApi != null)
            {
                loadedApi.SetLogCallback(null, IntPtr.Zero);
                logging = false;
            }
        }
    }
}
