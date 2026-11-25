# Agenerela — Local Image Models (stable-diffusion.cpp)

This package runs an image model **inside Unity, with no server**. It does for pictures what
LLMUnity does for language models. You add the package, install a native build, pick the model
files, and get Agenerela's `IImageProvider`. It makes pictures and edits them, through the same
interface as ComfyUI and OpenAI-compatible servers. The design is decision record DR-028 in the
build plan.

It calls [stable-diffusion.cpp](https://github.com/leejet/stable-diffusion.cpp) (MIT licence),
which runs models such as Z-Image Turbo, Flux, SDXL, Qwen-Image and Qwen-Image-Edit. Neither the
library nor any model is in the package. Both are downloaded only when you choose to, and each
download is checked against its size and SHA-256.

## Setting it up

1. **Add the package.** Add it beside `com.agenerela.framework`, by path in
   `Packages/manifest.json`:
   `"com.agenerela.sdcpp": "file:<path>/UnityProject/Packages/com.agenerela.sdcpp"`.
2. **Install a native build.** Open **Window → Agenerela → Local Image Models**:
   - **Vulkan** (31 MB) runs on any vendor's card.
   - **CUDA 12** is faster on NVIDIA, but it is 413 MB plus 563 MB of CUDA runtime.
   - **CPU** is the fallback.

   Builds go into `Assets/StreamingAssets/Agenerela/sdcpp/<build>/`, so a game build carries them.
   The fastest installed build is used.
3. **Make a recipe.** Use **Create → Agenerela → Local Image Model**, then **Use Z-Image Turbo**.
   In the recipe's Inspector, download each file or choose ones already on disk with
   **Choose…**. Downloads go to `StreamingAssets/Agenerela/models/`.
4. **Ask for pictures.** The recipe gives you a provider:

```csharp
var images = recipe.CreateProvider();                         // an IImageProvider
var result = await images.GenerateAsync(new ImageRequest
{
    Prompt = "Painted illustration, bust portrait of an old king", Width = 768, Height = 896, Seed = 7,
}, ct);
var texture = ImageTextures.ToTexture(result) ?? fallback;
```

Keep `StreamingAssets/Agenerela/sdcpp/` and `models/` out of version control, since they are
gigabytes. The repository's `.gitignore` already ignores them.

## Editing a picture

An edit is the same call with `ImageRequest.Images`, and optionally a `Mask`. The mask uses
OpenAI's convention: its fully transparent pixels are the ones that change. How the edit works
depends on the recipe's **Edit Mode**:

- **Repaint** works with any model.
  - The picture is the starting point.
  - The mask limits the change.
  - The `strength` parameter (0 to 1, default 0.75) says how far the result may move from the
    picture.
- **Instruction** needs an edit model such as Qwen-Image-Edit.
  - Every picture is a reference, and the prompt says what to change.
  - It takes no mask.

## Memory and the GPU

- **Weights on an 8 GB card.** Z-Image Turbo's weights at Q4 come to 6.7 GB. The recipe's defaults
  bring the card's share down by keeping the text encoder on the CPU and decoding in tiles.
- **More options if it is still tight:**
  - **Offload To CPU** keeps every weight in RAM and streams it to the card.
  - `max_vram` caps the card's share.
- **A language model as well.** On an 8 GB card the image model does not fit beside a loaded
  language model.
  1. Unload the language model first (`IModelLifecycle.UnloadAsync` on its provider).
  2. Make the pictures.
  3. Call `UnloadAsync` on this provider.
- **Rendering.** While a picture is being made, the card is shared with Unity's rendering and
  frames slow down. Make pictures behind a loading screen or during a pause.
- **Lifetime.** The model loads on the first request or on `WarmUpAsync`. `UnloadAsync` and
  `Dispose` give its memory back. A domain reload in the editor frees it too.

## Failures

Failures come back as results, never as exceptions. Show your own picture instead:

| Kind | Cause |
|---|---|
| `Unreachable` | No native build is installed |
| `ModelNotFound` | A model file is missing or broken |
| `Rejected` | sd.cpp will not take an option |
| `ServerError` | The card ran out of memory, or the generation failed |

Each message says what to do. Cancelling your token stops a picture mid-way and throws
`OperationCanceledException`.

## Options

| Where | What goes there |
|---|---|
| `SdCppSettings` and the recipe | The common settings |
| `ContextOptions` | Any of sd.cpp's context keys, such as `max_vram` or `model_args` |
| `ImageRequest.Parameters` and `DefaultParameters` | Any generation key, such as `sample.steps`, `strength` or `negative_prompt` |

The keys are listed in sd.cpp's
[`docs/guides/c_api.md`](https://github.com/leejet/stable-diffusion.cpp/blob/master/docs/guides/c_api.md).

## Platforms

| Platform | Status |
|---|---|
| Windows x64 | Tested |
| Linux x64 (Vulkan, CPU) | Build listed, not tested yet |
| macOS arm64 | Build listed, not tested yet |
| Mobile, WebGL, consoles | Not planned |

The package is bound to one stable-diffusion.cpp release, `SdCppRelease.Tag`. It moves by hand,
together with the binding's tests.
