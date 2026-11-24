using System;
using System.IO;
using System.Text.RegularExpressions;
using Agenerela.Providers;
using UnityEngine;

namespace Agenerela
{
    /// <summary>
    /// An <see cref="IImageCache"/> that keeps one PNG a key in a folder (DR-025): <c>&lt;key&gt;.png</c>,
    /// where the key is <see cref="ImageRequest.Key"/>. Put a <see cref="CachedImageProvider"/> on
    /// it, and a picture made once comes back on every later run without a backend.
    /// </summary>
    /// <remarks>
    /// The folder is the developer's to choose: under <see cref="Application.persistentDataPath"/>
    /// for a player (<see cref="InPersistentData"/>), or a project folder while developing. A file
    /// that cannot be read is a miss and one that cannot be written is dropped, each logged once, so
    /// a full disk never stops a game. Files are written to a temporary name and moved into place,
    /// so a game closed mid-write leaves no half picture behind.
    /// </remarks>
    public sealed class ImageFileCache : IImageCache
    {
        private static readonly Regex SafeKey = new Regex("^[A-Za-z0-9_-]{1,128}$", RegexOptions.CultureInvariant);

        private bool warned;

        /// <param name="folder">Where the files go. Made when the first picture is stored.</param>
        /// <exception cref="ArgumentException"><paramref name="folder"/> is empty.</exception>
        public ImageFileCache(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new ArgumentException("Name the folder the pictures are kept in.", nameof(folder));
            }

            Folder = Path.GetFullPath(folder);
        }

        /// <summary>A cache in <c>&lt;persistentDataPath&gt;/&lt;subfolder&gt;</c>. Call it from the main thread.</summary>
        public static ImageFileCache InPersistentData(string subfolder = "agenerela-images")
        {
            return new ImageFileCache(Path.Combine(Application.persistentDataPath, subfolder ?? "agenerela-images"));
        }

        /// <summary>The folder's full path.</summary>
        public string Folder { get; }

        /// <summary>The file a key is kept in.</summary>
        /// <exception cref="ArgumentException">The key is not 1 to 128 letters, digits, '-' or '_', which every <see cref="ImageRequest.Key"/> is.</exception>
        public string PathOf(string key)
        {
            if (key == null || !SafeKey.IsMatch(key))
            {
                throw new ArgumentException("A cache key is 1 to 128 letters, digits, '-' or '_', as ImageRequest.Key makes them.", nameof(key));
            }

            return Path.Combine(Folder, key + ".png");
        }

        /// <inheritdoc/>
        public bool TryGet(string key, out byte[] png)
        {
            png = null;
            string path = PathOf(key);
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                png = File.ReadAllBytes(path);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                WarnOnce($"could not read {path}: {e.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public void Put(string key, byte[] png)
        {
            string path = PathOf(key);
            if (png == null)
            {
                return;
            }

            string temporary = path + ".part";
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllBytes(temporary, png);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(temporary, path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                WarnOnce($"could not write {path}: {e.Message}");
                try { File.Delete(temporary); } catch (Exception) { /* nothing more to do */ }
            }
        }

        private void WarnOnce(string what)
        {
            if (warned)
            {
                return;
            }

            warned = true;
            Debug.LogWarning($"Agenerela image cache: {what}. Pictures are made again instead of kept; this is said once.");
        }
    }
}
