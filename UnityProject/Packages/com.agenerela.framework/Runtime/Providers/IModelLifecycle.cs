using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Agenerela.Providers
{
    /// <summary>
    /// What a provider that can manage its server's models adds to <see cref="ILLMProvider"/>
    /// (DR-019): which models the server has, and loading or unloading this provider's own model
    /// now. Optional: a game asks <c>provider is IModelLifecycle</c>, and a provider that does not
    /// implement it has nothing to manage. <see cref="OllamaProvider"/> implements it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>None of these is a decision</b>, so none goes through the decision queue (DR-015, as
    /// amended): a warm-up does only what the first decision on that model would have to do
    /// anyway, so it never holds a slot a decision is waiting for. Nor do they report to
    /// <see cref="ProviderHealth"/>, which follows decisions.
    /// </para>
    /// <para>
    /// <b>Switching models</b> is giving an agent the provider for the other model, through
    /// <see cref="Agent.Provider"/>: warm the new one up, set it, then unload the old one. Each
    /// provider instance keeps its own model, so its telemetry and its queue never mix two.
    /// </para>
    /// <para>
    /// Failures throw <see cref="ProviderException"/>, as <see cref="ILLMProvider.RequestAsync"/>
    /// does, so a server that is not running and a model that was never pulled are told apart.
    /// Cancelling the token aborts the request and throws <see cref="OperationCanceledException"/>;
    /// that is how a warm-up a newer one replaces is stopped. Call them from the main thread.
    /// </para>
    /// </remarks>
    public interface IModelLifecycle
    {
        /// <summary>The models the server has installed, as it names them, such as <c>qwen3.5:2b</c>.</summary>
        Awaitable<IReadOnlyList<ModelInfo>> ListModelsAsync(CancellationToken ct);

        /// <summary>
        /// Loads this provider's model into memory now, and completes once it is loaded, so the
        /// first decision does not wait for it.
        /// </summary>
        Awaitable WarmUpAsync(CancellationToken ct);

        /// <summary>
        /// Takes this provider's model out of memory now. True once the server no longer holds it
        /// loaded; false if it still did when the provider stopped waiting.
        /// </summary>
        Awaitable<bool> UnloadAsync(CancellationToken ct);
    }

    /// <summary>One model a server has installed, as <see cref="IModelLifecycle.ListModelsAsync"/> lists it.</summary>
    public sealed class ModelInfo
    {
        /// <param name="name">The model's name as the server gives it.</param>
        /// <param name="sizeBytes">Its size on disk in bytes; 0 when the server does not say.</param>
        /// <exception cref="ArgumentException"><paramref name="name"/> is null or blank.</exception>
        public ModelInfo(string name, long sizeBytes)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A model needs a name.", nameof(name));
            }

            Name = name;
            SizeBytes = Math.Max(0L, sizeBytes);
        }

        /// <summary>The name to put in a provider's settings, such as <c>qwen3.5:2b</c>.</summary>
        public string Name { get; }

        /// <summary>Its size on disk in bytes; 0 when the server does not say.</summary>
        public long SizeBytes { get; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Name;
        }
    }
}
