using System.Threading;
using Agenerela.Providers;
using Agenerela.Providers.SdCpp;
using UnityEngine;

namespace Agenerela.Samples
{
    /// <summary>
    /// Makes a portrait with a local image model, then edits it, and shows both with the seconds each
    /// took (DR-028). Put it on any object, give it a model recipe (Create → Agenerela → Local Image
    /// Model), and press the buttons in Play mode.
    /// </summary>
    /// <remarks>
    /// On an 8 GB card the image model does not fit beside a loaded language model. A game that also
    /// decides with Ollama unloads that model first (<c>IModelLifecycle.UnloadAsync</c> on its
    /// provider), makes its pictures behind a loading screen, then calls <see cref="SdCppImageProvider.UnloadAsync"/>.
    /// </remarks>
    public sealed class PaintAPortrait : MonoBehaviour
    {
        [Tooltip("The model recipe: Create → Agenerela → Local Image Model, then Use Z-Image Turbo.")]
        public SdCppModel Model;

        [TextArea]
        public string Prompt = "Painted illustration in the manner of a late-medieval chronicle, bust portrait of an old king, plain dark background";

        [TextArea]
        public string Edit = "wearing a tall golden crown";

        public int Width = 768;
        public int Height = 896;

        private SdCppImageProvider provider;
        private CancellationTokenSource cancel;
        private Texture2D portrait, edited;
        private byte[] portraitPng;
        private string status = "Press Paint.";

        private void OnDestroy()
        {
            cancel?.Cancel();
            provider?.Dispose();
            Destroy(portrait);
            Destroy(edited);
        }

        private async void Paint(bool edit)
        {
            provider ??= Model.CreateProvider();
            cancel = new CancellationTokenSource();
            var request = new ImageRequest { Prompt = edit ? Prompt + ", " + Edit : Prompt, Width = Width, Height = Height };
            if (edit)
            {
                request.Images.Add(portraitPng);
                request.Parameters["strength"] = "0.6";
            }

            status = provider.IsLoaded ? "Painting…" : "Loading the model, then painting…";
            try
            {
                var result = await provider.GenerateAsync(request, cancel.Token);
                if (!result.Succeeded)
                {
                    status = result.ToString();
                    return;
                }

                var texture = new Texture2D(2, 2);
                texture.LoadImage(result.Png);
                if (edit)
                {
                    Destroy(edited);
                    edited = texture;
                }
                else
                {
                    Destroy(portrait);
                    portrait = texture;
                    portraitPng = result.Png;
                }

                status = $"{result} (the model {result.GenerateSeconds:0.0} s), seed {result.Seed}.";
            }
            catch (System.OperationCanceledException)
            {
                status = "Cancelled.";
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 900, 600));
            GUILayout.Label(status);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Paint", GUILayout.Width(120)) && Model != null)
                {
                    Paint(false);
                }

                GUI.enabled = portraitPng != null;
                if (GUILayout.Button("Edit it", GUILayout.Width(120)))
                {
                    Paint(true);
                }

                GUI.enabled = true;
                if (GUILayout.Button("Cancel", GUILayout.Width(120)))
                {
                    cancel?.Cancel();
                }

                if (GUILayout.Button("Unload the model", GUILayout.Width(160)) && provider != null)
                {
                    _ = provider.UnloadAsync(CancellationToken.None);
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                if (portrait != null)
                {
                    GUILayout.Box(portrait, GUILayout.Width(384), GUILayout.Height(448));
                }

                if (edited != null)
                {
                    GUILayout.Box(edited, GUILayout.Width(384), GUILayout.Height(448));
                }
            }

            GUILayout.EndArea();
        }
    }
}
