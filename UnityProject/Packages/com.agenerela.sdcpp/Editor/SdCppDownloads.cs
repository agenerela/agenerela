using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Agenerela.Providers.SdCpp.Editor
{
    /// <summary>
    /// Downloads a file with a progress bar, checks its size and SHA-256 before it is used, and
    /// unpacks a native build safely (DR-028, Q6). Every download is asked for first, with its source
    /// and size.
    /// </summary>
    internal static class SdCppDownloads
    {
        /// <summary>Where downloaded release zips are kept for every project of this user.</summary>
        internal static string Cache => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Agenerela", "sdcpp", SdCppRelease.Tag);

        /// <summary>Asks, then downloads <paramref name="url"/> to <paramref name="path"/>. False when declined, cancelled or the file is wrong; the reason is shown.</summary>
        internal static bool Download(string url, string path, long size, string sha256, string what)
        {
            if (!EditorUtility.DisplayDialog("Download " + what,
                    $"{Path.GetFileName(path)}\n{Mb(size)}\n\nFrom: {url}\nTo: {path}\n\nDownload it now?", "Download", "Cancel"))
            {
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string part = path + ".part";
            using (var request = UnityWebRequest.Get(url))
            {
                request.downloadHandler = new DownloadHandlerFile(part) { removeFileOnAbort = true };
                var operation = request.SendWebRequest();
                try
                {
                    while (!operation.isDone)
                    {
                        float done = request.downloadProgress;
                        if (EditorUtility.DisplayCancelableProgressBar("Downloading " + what, $"{Path.GetFileName(path)}: {Mb((long)(done * size))} of {Mb(size)}", done))
                        {
                            request.Abort();
                            return false;
                        }

                        System.Threading.Thread.Sleep(100);
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    EditorUtility.DisplayDialog("Download failed", $"{Path.GetFileName(path)}: {request.error}", "OK");
                    File.Delete(part);
                    return false;
                }
            }

            string wrong = Check(part, size, sha256);
            if (wrong != null)
            {
                File.Delete(part);
                EditorUtility.DisplayDialog("Download rejected", wrong + " The file was deleted.", "OK");
                return false;
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(part, path);
            return true;
        }

        /// <summary>Why <paramref name="path"/> is not the file expected, or null when its size and hash match.</summary>
        internal static string Check(string path, long size, string sha256)
        {
            long have = new FileInfo(path).Length;
            if (size > 0 && have != size)
            {
                return $"{Path.GetFileName(path)} is {have} bytes; {size} were expected.";
            }

            if (string.IsNullOrEmpty(sha256))
            {
                return null;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Checking", "SHA-256 of " + Path.GetFileName(path), 0.5f);
                using (var stream = File.OpenRead(path))
                using (var sha = SHA256.Create())
                {
                    string hash = string.Concat(sha.ComputeHash(stream).Select(b => b.ToString("x2")));
                    return hash == sha256 ? null : $"{Path.GetFileName(path)} has SHA-256 {hash}; {sha256} was expected.";
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Unpacks the libraries and licence notes of a release zip into <paramref name="folder"/>,
        /// replacing what was there. The zip's executables are left out: a game needs only the libraries.
        /// </summary>
        internal static int Unpack(string zip, string folder)
        {
            string full = Path.GetFullPath(folder);
            if (Directory.Exists(full))
            {
                Directory.Delete(full, true);
            }

            Directory.CreateDirectory(full);
            int count = 0;
            using (var archive = ZipFile.OpenRead(zip))
            {
                foreach (var entry in archive.Entries)
                {
                    string name = entry.Name;
                    string ext = Path.GetExtension(name).ToLowerInvariant();
                    bool library = ext == ".dll" || ext == ".so" || ext == ".dylib" || name.Contains(".so.");
                    bool notice = ext == ".txt" || name.StartsWith("LICENSE", StringComparison.OrdinalIgnoreCase);
                    if (name.Length == 0 || !(library || notice))
                    {
                        continue;
                    }

                    // Flattened into one folder: a name with a path in it can never land outside it.
                    string target = Path.GetFullPath(Path.Combine(full, name));
                    if (!target.StartsWith(full + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    entry.ExtractToFile(target, true);
                    count++;
                }
            }

            return count;
        }

        internal static string Mb(long bytes) => bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):0.00} GB" : $"{bytes / (double)(1L << 20):0} MB";
    }
}
