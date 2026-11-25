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

        /// <summary>Any size up to 65,536 px a side, seeds honoured, and edits with a mask and up to 16 pictures. A test may change it.</summary>
        public ImageCapabilities Capabilities { get; } = new ImageCapabilities { MaxSide = 1 << 16, Edits = true, Masks = true, MaxImages = 16 };

        /// <summary>Seconds each request waits before it is answered, so a test can cancel one in flight. 0 unless set.</summary>
        public float DelaySeconds { get; set; }

        /// <summary>Every request it was given, in order.</summary>
        public IReadOnlyList<ImageRequest> Requests => requests;

        /// <summary>Checks the request against <see cref="Capabilities"/>, records it, waits <see cref="DelaySeconds"/>, and answers it.</summary>
        /// <exception cref="ArgumentException">What <see cref="ImageCapabilities.WhyNot"/> reports.</exception>
        public Awaitable<ImageResult> GenerateAsync(ImageRequest request, CancellationToken ct)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            // Checked here, outside the async part, so a request that cannot be sent throws at the call.
            string why = Capabilities.WhyNot(request);
            if (why != null)
            {
                throw new ArgumentException(why, nameof(request));
            }

            return Answer(request, ct);
        }

        private async Awaitable<ImageResult> Answer(ImageRequest request, CancellationToken ct)
        {
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
