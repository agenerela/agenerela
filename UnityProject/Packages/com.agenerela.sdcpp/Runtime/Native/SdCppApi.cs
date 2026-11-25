using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Agenerela.Providers.SdCpp.Native
{
    /// <summary>
    /// stable-diffusion.cpp's C API, bound by function pointer from a library loaded at run time
    /// (DR-028, Q6). Matches <c>include/stable-diffusion.h</c> at the pinned release
    /// (<see cref="SdCppRelease.Tag"/>): opaque handles and options set by string key, so no struct
    /// layout is mirrored here.
    /// </summary>
    /// <remarks>
    /// Loaded with <c>LoadLibraryExW</c> or <c>dlopen</c> rather than <c>[DllImport]</c>, so the
    /// backend folder is chosen when the game runs and the editor never locks a plugin asset.
    /// Every call but <see cref="CancelGeneration"/> is made on the provider's worker thread, where
    /// <see cref="LastError"/> (thread-local in sd.cpp) is read.
    /// </remarks>
    internal sealed class SdCppApi
    {
        internal const int StatusOk = 0;
        internal const int StatusInvalidArgument = 1;
        internal const int StatusUnknownKey = 2;
        internal const int StatusTypeMismatch = 3;
        internal const int StatusOutOfRange = 4;
        internal const int StatusUnsupported = 5;
        internal const int StatusOutOfMemory = 6;
        internal const int StatusLoadFailed = 7;
        internal const int StatusGenerationFailed = 8;
        internal const int StatusBusy = 9;
        internal const int StatusCancelled = 10;

        internal const int TensorU8 = 1;
        internal const int CancelAll = 0;
        internal const int CancelReset = 2;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr NoArgsPtr();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void PtrVoid(IntPtr p);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int SetBool(IntPtr opt, IntPtr key, [MarshalAs(UnmanagedType.I1)] bool value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int SetInt(IntPtr opt, IntPtr key, long value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int SetFloat(IntPtr opt, IntPtr key, double value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int SetPtr(IntPtr opt, IntPtr key, IntPtr value);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int SetTensors(IntPtr opt, IntPtr key, IntPtr[] tensors, UIntPtr count);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int CtxCreate(IntPtr opt, out IntPtr ctx);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int Generate(IntPtr ctx, IntPtr opt, out IntPtr result);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void Cancel(IntPtr ctx, int mode);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr TensorCreate(int type, UIntPtr rank, long[] shape, byte[] data, UIntPtr size);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate UIntPtr PtrSize(IntPtr p);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate long TensorDim(IntPtr t, UIntPtr axis);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int PtrInt(IntPtr p);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr ResultImage(IntPtr result, UIntPtr index);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void LoadAllFromPath(IntPtr dir);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void LogCallback(int level, IntPtr text, IntPtr data);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void SetLogCallbackFn(LogCallback callback, IntPtr data);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate UIntPtr ListDevicesFn(byte[] buffer, UIntPtr size);


        internal NoArgsPtr LastErrorPtr, Version, Commit, SystemInfo, CtxOptCreate, ImgGenOptCreate;
        internal PtrVoid OptFree, CtxFree, ResultFree, TensorFree;
        internal SetBool OptSetBool;
        internal SetInt OptSetInt;
        internal SetFloat OptSetFloat;
        internal SetPtr OptSetString, OptSetTensor;
        internal SetTensors OptSetTensors;
        internal CtxCreate ContextCreate;
        internal Generate GenerateImage;
        internal Cancel CancelGeneration;
        internal TensorCreate TensorCreateU8;
        internal PtrSize TensorRank, TensorByteSize, ResultImageCount;
        internal TensorDim TensorDimension;
        internal PtrInt TensorType;
        internal NoArgsPtrFromPtr TensorData;
        internal ResultImage ResultGetImage;
        internal SetLogCallbackFn SetLogCallback;
        internal ListDevicesFn ListDevicesRaw;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate IntPtr NoArgsPtrFromPtr(IntPtr p);

        private SdCppApi(string folder)
        {
            Folder = folder;
            string sdPath = Path.Combine(folder, NativeLoader.LibraryFile("stable-diffusion"));
            string ggmlPath = Path.Combine(folder, NativeLoader.LibraryFile("ggml"));

            // ggml loads its GPU and CPU backends as separate libraries, looking beside the running
            // executable (Unity.exe, not this folder). Loading them from this folder first registers
            // them, so sd.cpp finds them when it creates a context. After a domain reload the
            // libraries are still resident with their backends registered, so it is done once a
            // process: stable-diffusion.dll being loaded already means it was.
            bool resident = NativeLoader.IsResident(sdPath);
            IntPtr ggmlLibrary = NativeLoader.Load(ggmlPath);
            if (!resident)
            {
                var loadAll = NativeLoader.Function<LoadAllFromPath>(ggmlLibrary, "ggml_backend_load_all_from_path");
                IntPtr dir = Utf8(folder);
                try
                {
                    loadAll(dir);
                }
                finally
                {
                    Marshal.FreeHGlobal(dir);
                }
            }

            IntPtr sdLibrary = NativeLoader.Load(sdPath);
            T F<T>(string name) where T : Delegate => NativeLoader.Function<T>(sdLibrary, name);
            LastErrorPtr = F<NoArgsPtr>("sd_get_last_error");
            Version = F<NoArgsPtr>("sd_version");
            Commit = F<NoArgsPtr>("sd_commit");
            SystemInfo = F<NoArgsPtr>("sd_get_system_info");
            CtxOptCreate = F<NoArgsPtr>("sd_ctx_opt_create");
            ImgGenOptCreate = F<NoArgsPtr>("sd_img_gen_opt_create");
            OptFree = F<PtrVoid>("sd_opt_free");
            CtxFree = F<PtrVoid>("sd_ctx_free");
            ResultFree = F<PtrVoid>("sd_result_free");
            TensorFree = F<PtrVoid>("sd_tensor_free");
            OptSetBool = F<SetBool>("sd_opt_set_bool");
            OptSetInt = F<SetInt>("sd_opt_set_int");
            OptSetFloat = F<SetFloat>("sd_opt_set_float");
            OptSetString = F<SetPtr>("sd_opt_set_string");
            OptSetTensor = F<SetPtr>("sd_opt_set_tensor");
            OptSetTensors = F<SetTensors>("sd_opt_set_tensors");
            ContextCreate = F<CtxCreate>("sd_ctx_create");
            GenerateImage = F<Generate>("sd_generate_image");
            CancelGeneration = F<Cancel>("sd_cancel_generation");
            TensorCreateU8 = F<TensorCreate>("sd_tensor_create");
            TensorRank = F<PtrSize>("sd_tensor_rank");
            TensorByteSize = F<PtrSize>("sd_tensor_byte_size");
            TensorDimension = F<TensorDim>("sd_tensor_dim");
            TensorType = F<PtrInt>("sd_tensor_type");
            TensorData = F<NoArgsPtrFromPtr>("sd_tensor_data");
            ResultImageCount = F<PtrSize>("sd_result_get_image_count");
            ResultGetImage = F<ResultImage>("sd_result_get_image");
            SetLogCallback = F<SetLogCallbackFn>("sd_set_log_callback");
            ListDevicesRaw = F<ListDevicesFn>("sd_list_devices");
        }

        // The libraries stay loaded for the life of the process: ggml's backends cannot be unloaded
        // safely once registered. The card's memory is given back by freeing the context instead.

        /// <summary>The folder the library was loaded from.</summary>
        internal string Folder { get; }

        /// <summary>Loads the library in <paramref name="folder"/>. Throws <see cref="DllNotFoundException"/> or <see cref="EntryPointNotFoundException"/> with the reason.</summary>
        internal static SdCppApi Load(string folder) => new SdCppApi(folder);

        /// <summary>The devices ggml found, one a line: a name such as <c>Vulkan1</c> or <c>CPU</c>, a tab, and a description.</summary>
        internal string ListDevices()
        {
            var buffer = new byte[16384];
            ListDevicesRaw(buffer, (UIntPtr)buffer.Length);
            int end = Array.IndexOf(buffer, (byte)0);
            return Encoding.UTF8.GetString(buffer, 0, end < 0 ? buffer.Length : end);
        }

        /// <summary>True when a device other than the CPU is listed.</summary>
        internal static bool HasGpu(string devices)
        {
            foreach (string line in (devices ?? "").Split('\n'))
            {
                string name = line.Split('\t')[0].Trim();
                if (name.Length > 0 && !name.StartsWith("CPU", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>sd.cpp's last error on this thread, or an empty string.</summary>
        internal string LastError => Text(LastErrorPtr());

        /// <summary>A C string as UTF-8 text; empty for null.</summary>
        internal static string Text(IntPtr p)
        {
            if (p == IntPtr.Zero)
            {
                return "";
            }

            int length = 0;
            while (Marshal.ReadByte(p, length) != 0)
            {
                length++;
            }

            var bytes = new byte[length];
            Marshal.Copy(p, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>A NUL-terminated UTF-8 copy of <paramref name="text"/>, freed with <see cref="Marshal.FreeHGlobal"/>.</summary>
        internal static IntPtr Utf8(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? "");
            IntPtr p = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, p, bytes.Length);
            Marshal.WriteByte(p, bytes.Length, 0);
            return p;
        }
    }

    /// <summary>Loads a native library and its functions without <c>[DllImport]</c>.</summary>
    internal static class NativeLoader
    {
        private const uint LoadLibrarySearchDllLoadDir = 0x00000100;
        private const uint LoadLibrarySearchDefaultDirs = 0x00001000;

        internal static bool IsWindows => Environment.OSVersion.Platform == PlatformID.Win32NT;

        /// <summary>The file name a library has on this platform: <c>name.dll</c>, <c>libname.dylib</c> or <c>libname.so</c>.</summary>
        internal static string LibraryFile(string name)
        {
            if (IsWindows)
            {
                return name + ".dll";
            }

            return UnityEngine.Application.platform == UnityEngine.RuntimePlatform.OSXEditor
                   || UnityEngine.Application.platform == UnityEngine.RuntimePlatform.OSXPlayer
                ? "lib" + name + ".dylib"
                : "lib" + name + ".so";
        }

        /// <summary>True when the library at <paramref name="path"/> is already loaded in this process (Windows only; false elsewhere).</summary>
        internal static bool IsResident(string path) => IsWindows && GetModuleHandleW(path) != IntPtr.Zero;

        internal static IntPtr Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new DllNotFoundException($"{path} does not exist.");
            }

            IntPtr handle;
            if (IsWindows)
            {
                // Its own folder first for the libraries it depends on (ggml-base.dll and the rest).
                handle = LoadLibraryExW(path, IntPtr.Zero, LoadLibrarySearchDllLoadDir | LoadLibrarySearchDefaultDirs);
                if (handle == IntPtr.Zero)
                {
                    int code = Marshal.GetLastWin32Error();
                    throw new DllNotFoundException($"Windows could not load {path} (error {code}). " +
                        (code == 126 ? "A library it needs is missing: the Visual C++ 2015–2022 runtime, or vulkan-1.dll from the graphics driver." : ""));
                }
            }
            else
            {
                handle = dlopen(path, 2 /* RTLD_NOW */);
                if (handle == IntPtr.Zero)
                {
                    throw new DllNotFoundException($"Could not load {path}: {SdCppApi.Text(dlerror())}");
                }
            }

            return handle;
        }

        internal static T Function<T>(IntPtr library, string name) where T : Delegate
        {
            IntPtr p = IsWindows ? GetProcAddress(library, name) : dlsym(library, name);
            if (p == IntPtr.Zero)
            {
                throw new EntryPointNotFoundException($"The native library has no function {name}. It is not the stable-diffusion.cpp release this package was built for ({SdCppRelease.Tag}).");
            }

            return Marshal.GetDelegateForFunctionPointer<T>(p);
        }

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryExW(string path, IntPtr file, uint flags);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandleW(string path);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);

        [DllImport("libdl")]
        private static extern IntPtr dlopen(string path, int flags);

        [DllImport("libdl")]
        private static extern IntPtr dlsym(IntPtr handle, string name);

        [DllImport("libdl")]
        private static extern IntPtr dlerror();
    }
}
