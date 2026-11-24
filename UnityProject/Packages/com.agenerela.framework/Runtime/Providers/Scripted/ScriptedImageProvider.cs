using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Agenerela.Providers
{
    /// <summary>
    /// An <see cref="IImageProvider"/> that answers from code, for tests and for a game run with no
    /// image server (DR-025): a function of the request gives each result, and every request is
    /// recorded in <see cref="Requests"/>.
    /// </summary>
    public sealed class ScriptedImageProvider : IImageProvider
    {
        /// <summary>The smallest valid PNG: one transparent pixel. What <see cref="Always()"/> answers.</summary>
        public static readonly byte[] OnePixelPng = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

        private readonly Func<ImageRequest, ImageResult> answer;
        private readonly List<ImageRequest> requests = new List<ImageRequest>();

        /// <param name="answer">The result for each request. A null result is answered as a malformed response.</param>
        /// <param name="name">Its <see cref="Name"/>, <c>"scripted"</c> unless set.</param>
        public ScriptedImageProvider(Func<ImageRequest, ImageResult> answer, string name = "scripted")
        {
            this.answer = answer ?? throw new ArgumentNullException(nameof(answer));
            Name = name ?? "scripted";
        }

        /// <summary>A provider that makes <paramref name="png"/> for every request; <see cref="OnePixelPng"/> unless given.</summary>
        public static ScriptedImageProvider Always(byte[] png = null, string name = "scripted")
        {
            byte[] picture = png ?? OnePixelPng;
            return new ScriptedImageProvider(r => ImageResult.Made(picture, name, 0f, r.Seed), name);
        }

        /// <summary>A provider that fails every request with <paramref name="kind"/>.</summary>
        public static ScriptedImageProvider Failing(ProviderErrorKind kind, string message = "Scripted failure.", string name = "scripted")
        {
            return new ScriptedImageProvider(_ => ImageResult.Failed(kind, name, message), name);
        }

        /// <summary>Its name, <c>"scripted"</c> unless set.</summary>
        public string Name { get; }

        /// <summary>Any size, seeds honoured.</summary>
        public ImageCapabilities Capabilities { get; } = new ImageCapabilities();

        /// <summary>Seconds each request waits before it is answered, so a test can cancel one in flight. 0 unless set.</summary>
        public float DelaySeconds { get; set; }

        /// <summary>Every request it was given, in order.</summary>
        public IReadOnlyList<ImageRequest> Requests => requests;

        /// <summary>Records the request, waits <see cref="DelaySeconds"/>, and answers it.</summary>
        public async Awaitable<ImageResult> GenerateAsync(ImageRequest request, CancellationToken ct)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            ct.ThrowIfCancellationRequested();
            requests.Add(request);
            if (DelaySeconds > 0f)
            {
                await Task.Delay(TimeSpan.FromSeconds(DelaySeconds), ct);
            }

            ct.ThrowIfCancellationRequested();
            return answer(request) ?? ImageResult.Failed(ProviderErrorKind.MalformedResponse, Name, "The script answered null.");
        }
    }
}
