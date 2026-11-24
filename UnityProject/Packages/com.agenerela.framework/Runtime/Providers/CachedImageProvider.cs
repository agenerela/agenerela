using System;
using System.Threading;
using UnityEngine;

namespace Agenerela.Providers
{
    /// <summary>
    /// Where pictures already made are kept, by <see cref="ImageRequest.Key"/> (DR-025). The storage
    /// is the developer's: <c>ImageFileCache</c> keeps them in a folder, and a game may keep them in
    /// a save file or memory instead.
    /// </summary>
    /// <remarks>Both calls are made on the caller's thread. A cache that fails to read reports a miss; one that fails to write drops the picture.</remarks>
    public interface IImageCache
    {
        /// <summary>The PNG stored under <paramref name="key"/>, or false.</summary>
        bool TryGet(string key, out byte[] png);

        /// <summary>Stores <paramref name="png"/> under <paramref name="key"/>, replacing what was there.</summary>
        void Put(string key, byte[] png);
    }

    /// <summary>
    /// A cache in front of any <see cref="IImageProvider"/>: a request made before comes back from
    /// <see cref="IImageCache"/> without asking the backend, and a picture newly made is stored
    /// (DR-025). It is an <see cref="IImageProvider"/> itself, so a game holds one either way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only a success is stored, so a backend that was down is asked again next time. A stored file
    /// that is not a PNG counts as a miss.
    /// </para>
    /// <para>
    /// The backend may be null: then a hit still returns the picture and a miss fails with
    /// <see cref="ProviderErrorKind.Unreachable"/>, so a game whose pictures were all made earlier
    /// shows them with no image server at all.
    /// </para>
    /// <para>
    /// Keys use <see cref="KeyName"/>, the backend's name unless given, so pictures from one backend
    /// are never handed out as another's. Pass the same name to keep a cache across backends that
    /// make the same pictures.
    /// </para>
    /// </remarks>
    public sealed class CachedImageProvider : IImageProvider
    {
        private readonly IImageProvider inner;
        private readonly IImageCache cache;

        /// <param name="inner">The backend, or null for cache-only.</param>
        /// <param name="cache">Where pictures are kept.</param>
        /// <param name="keyName">The name keys are made with; <paramref name="inner"/>'s <see cref="IImageProvider.Name"/> when null.</param>
        /// <exception cref="ArgumentNullException"><paramref name="cache"/> is null, or both <paramref name="inner"/> and <paramref name="keyName"/> are.</exception>
        public CachedImageProvider(IImageProvider inner, IImageCache cache, string keyName = null)
        {
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this.inner = inner;
            KeyName = keyName ?? inner?.Name ?? throw new ArgumentNullException(nameof(keyName), "With no backend, name the one whose pictures the cache holds.");
        }

        /// <summary>The backend's name, or <see cref="KeyName"/> with no backend.</summary>
        public string Name => inner?.Name ?? KeyName;

        /// <summary>The name every key is made with.</summary>
        public string KeyName { get; }

        /// <summary>The backend's, or a default set with no backend.</summary>
        public ImageCapabilities Capabilities => inner?.Capabilities ?? new ImageCapabilities();

        /// <summary>True when a picture for <paramref name="request"/> is already stored, so no backend would be asked.</summary>
        public bool Has(ImageRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return cache.TryGet(request.Key(KeyName), out byte[] png) && ImageResult.IsPng(png);
        }

        /// <summary>The stored picture, or the backend's, stored when it succeeds.</summary>
        public async Awaitable<ImageResult> GenerateAsync(ImageRequest request, CancellationToken ct)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            ct.ThrowIfCancellationRequested();
            string key = request.Key(KeyName);
            if (cache.TryGet(key, out byte[] stored) && ImageResult.IsPng(stored))
            {
                return ImageResult.Cached(stored, KeyName);
            }

            if (inner == null)
            {
                return ImageResult.Failed(ProviderErrorKind.Unreachable, KeyName,
                    "The picture is not in the cache, and no image backend is set to make it.");
            }

            var result = await inner.GenerateAsync(request, ct);
            if (result == null)
            {
                return ImageResult.Failed(ProviderErrorKind.MalformedResponse, inner.Name, $"{inner.GetType().Name}.GenerateAsync returned null.");
            }

            if (result.Succeeded)
            {
                cache.Put(key, result.Png);
            }

            return result;
        }
    }
}
