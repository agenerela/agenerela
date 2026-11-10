using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Agenerela.Providers;
using UnityEngine;

namespace LivingVale.Agenerela
{
    /// <summary>Which models the local server has. Null means the server could not be asked.</summary>
    public interface IModelCatalog
    {
        Task<IReadOnlyCollection<string>> InstalledAsync(CancellationToken ct);
    }

    /// <summary>
    /// The models the server of the provider in use has, through the framework's
    /// <see cref="IModelLifecycle.ListModelsAsync"/> (#198), with a short deadline of its own, so a
    /// server that is down or slow never holds the game: the answer is then null.
    /// </summary>
    public sealed class ProviderModelCatalog : IModelCatalog
    {
        private readonly Func<ILLMProvider> provider;
        private readonly TimeSpan timeout;

        /// <param name="provider">The provider in use; read on every press, since F7 changes it.</param>
        public ProviderModelCatalog(Func<ILLMProvider> provider, float timeoutSeconds = 2f)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            timeout = TimeSpan.FromSeconds(Mathf.Max(0.1f, timeoutSeconds));
        }

        public async Task<IReadOnlyCollection<string>> InstalledAsync(CancellationToken ct)
        {
            if (!(provider() is IModelLifecycle server))
            {
                return null;
            }

            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                deadline.CancelAfter(timeout);
                try
                {
                    var names = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var model in await server.ListModelsAsync(deadline.Token))
                    {
                        names.Add(model.Name);
                    }

                    return names;
                }
                catch (Exception e) when (!ct.IsCancellationRequested)
                {
                    Debug.LogWarning($"[LivingVale] Could not list the server's models: {e.Message}");
                    return null;
                }
            }
        }
    }

    /// <summary>What one press of the model key did.</summary>
    public sealed class ModelChoice
    {
        public bool Changed { get; }

        /// <summary>The model now selected, changed or not.</summary>
        public string Model { get; }

        /// <summary>A game-authored line for the player: what was skipped, or why nothing changed. Empty when there is nothing to say.</summary>
        public string Notice { get; }

        /// <summary>The listed models passed over because the server does not have them, in list order.</summary>
        public IReadOnlyList<string> Skipped { get; }

        public ModelChoice(bool changed, string model, string notice, IReadOnlyList<string> skipped = null)
        {
            Changed = changed;
            Model = model;
            Notice = notice ?? "";
            Skipped = skipped ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// The ordered list of models the game may talk with, and which one is selected. Each press
    /// moves to the next listed model the server actually has, wrapping round and skipping the
    /// rest; with the server down or nothing else installed, the selection stays.
    /// </summary>
    public sealed class ModelSelector
    {
        private readonly List<string> models = new List<string>();
        private readonly IModelCatalog catalog;

        /// <param name="models">In order; blanks and repeats are dropped.</param>
        /// <param name="current">The model selected now. Added at the front when the list lacks it.</param>
        public ModelSelector(IEnumerable<string> models, string current, IModelCatalog catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            foreach (var model in models ?? Array.Empty<string>())
            {
                string name = model?.Trim();
                if (!string.IsNullOrEmpty(name) && !this.models.Contains(name))
                {
                    this.models.Add(name);
                }
            }

            Current = string.IsNullOrWhiteSpace(current) ? (this.models.Count > 0 ? this.models[0] : "") : current.Trim();
            if (Current.Length > 0 && !this.models.Contains(Current))
            {
                this.models.Insert(0, Current);
            }
        }

        public IReadOnlyList<string> Models => models;

        public string Current { get; private set; }

        /// <summary>Asks the server, then selects the next installed model. Never throws for a server problem.</summary>
        public async Task<ModelChoice> NextAsync(CancellationToken ct)
        {
            var installed = await catalog.InstalledAsync(ct);

            // Before anything changes: a cancelled press must leave the selection where it was.
            ct.ThrowIfCancellationRequested();
            if (installed == null)
            {
                return new ModelChoice(false, Current, "model server not answering");
            }

            // Short, because it sits beside the badge on one line; the names go to the log.
            string next = PickNext(models, Current, installed, out var skipped);
            if (skipped.Count > 0)
            {
                Debug.Log($"[LivingVale] Not installed in Ollama, skipped: {string.Join(", ", skipped)}.");
            }

            string skippedLine = skipped.Count == 1 ? "1 model not installed, skipped"
                : skipped.Count > 1 ? $"{skipped.Count} models not installed, skipped" : "";
            if (next == null)
            {
                return new ModelChoice(false, Current, "no other listed model is installed", skipped);
            }

            Current = next;
            return new ModelChoice(true, next, skippedLine, skipped);
        }

        /// <summary>
        /// The first model after <paramref name="current"/> in <paramref name="models"/>, wrapping
        /// round, that <paramref name="installed"/> has; null when none other is. Names without a
        /// tag match Ollama's <c>:latest</c>.
        /// </summary>
        public static string PickNext(IReadOnlyList<string> models, string current, IReadOnlyCollection<string> installed, out List<string> skipped)
        {
            skipped = new List<string>();
            int start = -1;
            for (int i = 0; i < models.Count; i++)
            {
                if (models[i] == current)
                {
                    start = i;
                    break;
                }
            }

            for (int step = 1; step <= models.Count; step++)
            {
                string candidate = models[((start + step) % models.Count + models.Count) % models.Count];
                if (candidate == current)
                {
                    break;
                }

                if (IsInstalled(candidate, installed))
                {
                    return candidate;
                }

                skipped.Add(candidate);
            }

            return null;
        }

        private static bool IsInstalled(string model, IReadOnlyCollection<string> installed)
        {
            foreach (var name in installed)
            {
                if (name == model || (!model.Contains(":") && name == model + ":latest"))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
