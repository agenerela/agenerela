using System;
using System.Threading;
using UnityEngine;

namespace Agenerela.Providers
{
    /// <summary>
    /// An image backend: a prompt, a size and a seed in, a PNG out (DR-024, DR-025), and for a backend
    /// that edits, a picture to change as well (DR-028). The framework
    /// ships no image model. <see cref="ScriptedImageProvider"/> answers tests, the
    /// <c>Agenerela.ComfyUI</c> assembly holds a ComfyUI one, and a developer writes their own for
    /// any other backend.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Failures are results, not exceptions.</b> A backend that is down, busy, slow or refuses
    /// returns an <see cref="ImageResult"/> whose <see cref="ImageResult.Error"/> names the
    /// <see cref="ProviderErrorKind"/>, so a game shows its own picture instead and goes on. Only
    /// the caller's own cancellation throws (<see cref="OperationCanceledException"/>), and a
    /// request that cannot be sent at all throws <see cref="ArgumentException"/> from the call.
    /// </para>
    /// <para>
    /// <b>The model never asks for a picture itself</b> (hard rule 5, DR-024): a game's code or a
    /// registered action's handler calls <see cref="GenerateAsync"/>. Text a model wrote goes
    /// through <see cref="ImagePromptBuilder"/> first.
    /// </para>
    /// <para>
    /// <b>Writing one for another backend:</b> implement the two members and
    /// <see cref="GenerateAsync"/>; return <see cref="ImageResult.Failed"/> for every failure of the
    /// backend, with a message that says what to do and never contains a key (hard rule 1). Put it
    /// in its own assembly or package if it needs anything beyond Newtonsoft (hard rule 6).
    /// <see cref="CachedImageProvider"/> works in front of any of them.
    /// </para>
    /// </remarks>
    public interface IImageProvider
    {
        /// <summary>A short stable name, such as <c>"comfyui"</c>: recorded on each result and part of the cache key.</summary>
        string Name { get; }

        /// <summary>The sizes it makes and whether it honours a seed.</summary>
        ImageCapabilities Capabilities { get; }

        /// <summary>Makes one picture for <paramref name="request"/>.</summary>
        /// <returns>The PNG, or a failed result saying why. Never null.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// The request cannot be sent, such as a size the backend cannot make or an edit it cannot do:
        /// whatever <see cref="ImageCapabilities.WhyNot"/> reports.
        /// </exception>
        /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
        Awaitable<ImageResult> GenerateAsync(ImageRequest request, CancellationToken ct);
    }

    /// <summary>What an <see cref="IImageProvider"/> can make, and whether it edits (DR-028).</summary>
    public sealed class ImageCapabilities
    {
        /// <summary>Width and height must be multiples of this: 16 for the diffusion models ComfyUI runs, 1 for no rule.</summary>
        public int SizeStep = 1;

        /// <summary>The longest side it makes, in pixels.</summary>
        public int MaxSide = 2048;

        /// <summary>Whether the same seed and prompt give the same picture again on this backend.</summary>
        public bool HonoursSeed = true;

        /// <summary>
        /// Whether it changes a picture it is given (<see cref="ImageRequest.Images"/>). False unless a
        /// backend says otherwise, so one written before edits existed refuses them cleanly.
        /// </summary>
        public bool Edits;

        /// <summary>Whether an edit may carry a <see cref="ImageRequest.Mask"/>. Only meaningful when <see cref="Edits"/> is true.</summary>
        public bool Masks;

        /// <summary>The most input pictures one edit takes: the one changed and its references. Only meaningful when <see cref="Edits"/> is true.</summary>
        public int MaxImages = 1;

        /// <summary>
        /// Why <paramref name="request"/> cannot be sent to this backend, or null when it can: its
        /// size (<see cref="WhyNotSize"/>), an edit to a backend that does not edit, too many input
        /// pictures, a mask it does not take or with no picture to apply to, or input bytes that are
        /// not a PNG. What a provider checks before sending, and what a game may ask first.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
        public string WhyNot(ImageRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            string size = WhyNotSize(request.Width, request.Height);
            if (size != null)
            {
                return size;
            }

            int count = request.Images?.Count ?? 0;
            if (count == 0)
            {
                return request.Mask != null
                    ? "The request has a Mask but no Images: a mask says which part of Images[0] to change, so add the picture or drop the mask."
                    : null;
            }

            if (!Edits)
            {
                return $"This backend does not edit pictures (ImageCapabilities.Edits is false), and the request has {count} in Images. " +
                       "Send it with no Images for a new picture, or use a backend that edits.";
            }

            if (count > Math.Max(1, MaxImages))
            {
                return $"The request has {count} pictures in Images; this backend takes at most {Math.Max(1, MaxImages)} in one edit.";
            }

            for (int i = 0; i < count; i++)
            {
                if (!ImageResult.IsPng(request.Images[i]))
                {
                    return $"Images[{i}] is not a PNG: it is null, empty, or does not start with the PNG signature. Encode it as PNG first.";
                }
            }

            if (request.Mask != null)
            {
                if (!Masks)
                {
                    return "This backend edits a whole picture and takes no Mask (ImageCapabilities.Masks is false). Drop the mask.";
                }

                if (!ImageResult.IsPng(request.Mask))
                {
                    return "The Mask is not a PNG. It is a PNG the size of Images[0] whose fully transparent pixels are the ones to change.";
                }
            }

            return null;
        }

        /// <summary>
        /// Why <paramref name="width"/> by <paramref name="height"/> cannot be made, or null when it can.
        /// What a provider checks before sending.
        /// </summary>
        public string WhyNotSize(int width, int height)
        {
            int step = Math.Max(1, SizeStep);
            if (width < step || height < step)
            {
                return $"The size {width}x{height} is too small: each side must be at least {step} px.";
            }

            if (width > MaxSide || height > MaxSide)
            {
                return $"The size {width}x{height} is too large: the longest side this backend makes is {MaxSide} px.";
            }

            if (width % step != 0 || height % step != 0)
            {
                return $"The size {width}x{height} is not a multiple of {step} on each side, which this backend needs.";
            }

            return null;
        }
    }
}
