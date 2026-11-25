# Images — an image backend, a cache in front of it, and ComfyUI

A language model chooses what an agent does. An image model is a different kind of backend: a
prompt, a size and a seed go in, a picture comes out. The framework gives it its own small
interface, `IImageProvider`, beside `ILLMProvider` rather than inside it (DR-024), and ships no
image model. This page covers the interface, the cache, the prompt builder, the ComfyUI provider
(DR-025), editing a picture and the OpenAI-compatible provider (DR-028).

**The model never asks for a picture itself** (hard rule 5). Your code asks, or a registered
action's handler does: the model chose "paint the event", the handler calls the image provider.

## The interface

```csharp
public interface IImageProvider
{
    string Name { get; }
    ImageCapabilities Capabilities { get; }   // size step, longest side, whether a seed is honoured
    Awaitable<ImageResult> GenerateAsync(ImageRequest request, CancellationToken ct);
}
```

`ImageRequest` holds the prompt, `Width`, `Height`, an optional `Seed` and open `Parameters`
(name → value strings) for anything a backend takes beyond those. `ImageResult` holds the PNG
bytes, the seed used, `Seconds` end to end and `GenerateSeconds` on the card where the backend
reports it, `FromCache`, or an `Error`.

**Failures are results.** A backend that is down, busy, slow or refusing returns
`ImageResult.Failed(kind, ...)` with a `ProviderErrorKind`, the same kinds a decision's
telemetry uses. Show your own picture instead and carry on:

```csharp
var result = await images.GenerateAsync(request, ct);
var texture = ImageTextures.ToTexture(result) ?? myBakedPortrait;
```

Only your own cancellation throws (`OperationCanceledException`), and a request the backend
cannot send at all (a size it cannot make, a parameter its workflow lacks) throws
`ArgumentException` from the call, before anything is sent.

## Editing a picture

An edit is the same call with the picture to start from (DR-028):

```csharp
var edit = new ImageRequest { Prompt = "give him a golden crown", Width = 768, Height = 896, Seed = 7 };
edit.Images.Add(portraitPng);            // the picture to change; later entries are references
edit.Mask = crownAreaPng;                 // optional: fully transparent pixels change, opaque ones are kept
edit.Parameters["strength"] = "0.6";      // how much it may change, where the backend takes it
var result = await images.GenerateAsync(edit, ct);
```

- **`Images`** are PNG bytes. The first is the picture changed; an edit model (Qwen-Image-Edit)
  may take more as references. `Width` and `Height` are still the size of the result.
- **`Mask`** follows OpenAI's convention: a PNG the size of `Images[0]` whose fully transparent
  pixels are the ones to change.
- **Not every backend edits.** `Capabilities.Edits`, `Masks` and `MaxImages` say what it takes,
  and `Capabilities.WhyNot(request)` says why a request cannot be sent, or null. An edit sent to a
  backend that cannot do it throws `ArgumentException` at the call, as a size it cannot make does,
  so the mistake shows on the first run. `ComfyUIProvider` does not edit yet.
- **The cache** keys each input picture and the mask by their bytes, so one edit is never handed
  out for another. A request with no pictures keys exactly as it did before edits existed.

## Writing a prompt: `ImagePromptBuilder`

The game's art direction leads every prompt, and text a model wrote never comes first:

```csharp
string prompt = new ImagePromptBuilder("Painted illustration in the manner of a late-medieval chronicle, ...")
    .Detail("Bust portrait, head and shoulders, plain dark background")   // your own words, as written
    .Untrusted(storytellerLine, maxLength: 160)                           // a model's words: cleaned and capped
    .Exclude("text, no lettering, no border")
    .Build();
```

`Untrusted` removes prompt syntax (brackets, weights, `|`, `:`), control characters and line
breaks, drops every sentence that tries to change the style or speaks to the image model
("ignore…", "photorealistic", "render as…"), and cuts the rest at a word. That is a floor, not a
guarantee; the style line coming first is what keeps the look. There is no default style.

## Keeping pictures: `CachedImageProvider`

```csharp
var cache = ImageFileCache.InPersistentData("my-game-art");      // or new ImageFileCache(folder)
var images = new CachedImageProvider(backend, cache);
```

The same request comes back from the cache without asking the backend; only a success is
stored. The key is `ImageRequest.Key(providerName)`, a SHA-256 of the provider's name, the
prompt, the size, the seed, every parameter and an edit's pictures, so set a seed for a picture a replay must show
again. A cache with no backend (`new CachedImageProvider(null, cache, "comfyui")`) still shows
every picture made before, and fails the rest with `Unreachable`. Write your own `IImageCache`
to keep pictures in a save file instead.

`ImageTextures.ToTexture` (in `Runtime/Unity/`) turns the bytes into a `Texture2D`, which you
own and destroy. `ScriptedImageProvider` answers tests: `Always()`, `Failing(kind)`, or a
function of the request, with an optional delay.

## ComfyUI

`ComfyUIProvider` runs a ComfyUI workflow. It lives in its own assembly, **`Agenerela.ComfyUI`**,
which references only the core and Newtonsoft (hard rule 6), so reference it from your game's
assembly definition to use it.

```csharp
var settings = ComfyUISettings.FromEnvironment(workflowAsset.text);   // COMFY_URL, COMFY_API_TOKEN
settings.DefaultParameters["28.unet_name"] = "z_image_turbo_int8_convrot.safetensors";
var comfy = new ComfyUIProvider(settings);
```

Or as an asset: **Create → Agenerela → ComfyUI Image Provider**, which stores the workflow and
the *names* of the two environment variables, never the key, and builds one with
`CreateProvider()`.

- **The workflow** is ComfyUI's Export (API) JSON, or that JSON wrapped with named params the way
  `tools/comfyui/workflows/*.json` are. The request's prompt, seed, width and height go into the
  params named `prompt`, `seed`, `width` and `height`; `Parameters` take a param's name or
  `node.input` (`3.steps`), and the value is written as the type the workflow holds there.
- **The key** goes only in the `Authorization: Bearer` header. Messages name the server by
  scheme, host and port, and any server text they quote has the key replaced.
- **What it does:** posts to `/prompt`, polls `/history/<id>` every `PollSeconds`, downloads
  the first PNG from `/view`, and asks `/free` afterwards unless `FreeAfterEach` is off (for a
  batch, then call `FreeAsync` once). A job past `TimeoutSeconds`, or cancelled, is taken off the
  queue if it is still waiting; one already running is left to finish, since interrupting could
  stop another client's job on a shared card.
- **Failures:** nothing answering is `Unreachable`; a model the server lacks is
  `ModelNotFound`; a refused key, route or workflow is `Rejected`; a busy card (503), a gateway
  with no ComfyUI behind it (502) or a node that failed is `ServerError`; too slow is `Timeout`;
  anything not ComfyUI's shape, or a file that is not a PNG, is `MalformedResponse`.

**A shared card.** On a machine where the same GPU serves your decision model, every picture
unloads that model, and the next decision waits for it to load again. Make pictures where no
decision waits, such as a loading screen, never during play. Painted Realm
(`Demos/GreyBoxStrategy`) paints its rulers that way, with the art pack as the fallback.

## Any OpenAI-compatible server: `OpenAIImagesProvider`

`OpenAIImagesProvider` speaks OpenAI's images API: a new picture through `/images/generations`, an
edit through `/images/edits`. It works with stable-diffusion.cpp's `sd-server`, with LocalAI (new
pictures only) and with OpenAI. It lives in its own assembly, **`Agenerela.OpenAIImages`**, which
references only the core and Newtonsoft.

```csharp
// sd-server started with a model, listening on 127.0.0.1:1234
var settings = new OpenAIImagesSettings { Url = "http://127.0.0.1:1234/v1", SdCppExtraArgs = true };
settings.DefaultParameters["extra.sample_params.sample_steps"] = "8";   // Z-Image Turbo
var images = new OpenAIImagesProvider(settings);
```

Or as an asset: **Create → Agenerela → OpenAI-compatible Image Provider**, which stores the
address, and the *name* of the key's variable (`AGENERELA_IMAGES_API_KEY` unless changed), never
the key. A named address variable that is not set falls back to the asset's address and sends no
key, so a key never reaches a server it was not meant for.

- **What it sends.** A new picture is JSON with `prompt`, `size` (`768x896`), `n` = 1,
  `output_format` = `png`, `model` when set, and each request parameter as a field of its own
  name, such as `quality`. An edit is multipart: the same fields, the pictures as `image` (one) or
  `image[]` (several), and `mask`. The picture comes back from `data[0].b64_json`.
- **Seeds and steps.** OpenAI's API has neither. For `sd-server`, turn `SdCppExtraArgs` on: the
  seed, `strength`, `negative_prompt` and every parameter named `extra.<path>` go into the
  `<sd_cpp_extra_args>` block it reads from the end of the prompt, and the seed is honoured. Leave
  it off for any other server, which would draw the block.
- **LocalAI** does not edit and answers with a URL unless asked: set `Edits` off and add the default
  parameter `response_format` = `b64_json`.
- **`sd-server` edits only with an edit model** (Qwen-Image-Edit, Flux Kontext): its `/images/edits`
  sends the pictures to the model as references. With Z-Image Turbo a mask is refused ("mask_image
  requires init_image") and an edit without one stops the server with a native assertion (24 November
  2026, release `master-1127-79dc7ca`). For Z-Image, set `Edits` off, or repaint in-process with
  `com.agenerela.sdcpp`.
- **Failures:** nothing answering is `Unreachable`; a model the server lacks is `ModelNotFound`; a
  refused key, route or request (a content refusal included) is `Rejected`; a server still busy
  (429 or 503) after `Retry` (DR-026) is `RateLimited`; another 5xx is `ServerError`; too slow is
  `Timeout`; a reply with no PNG in it is `MalformedResponse`. `Describe(request)` shows what would
  be sent, with no key and no picture bytes.

## Inside the game, with no server: `com.agenerela.sdcpp`

A second package runs the image model in the game's own process, as LLMUnity does for language models:
`SdCppImageProvider` calls stable-diffusion.cpp, loads the model once, and makes and repaints pictures on a
worker thread. The native library and the model files are downloaded from the editor and live under
`StreamingAssets`. Its `README.md` covers setting it up, editing and memory on an 8 GB card (DR-028).

## Another backend

Implement `IImageProvider`: return `ImageResult.Made(png, Name, seconds, seed)` for a picture and
`ImageResult.Failed(kind, Name, message)` for every failure, with no key in the message. Put it
in its own assembly or package if it needs anything beyond Newtonsoft. `CachedImageProvider`,
`ImagePromptBuilder` and your game's code stay as they are; the cache key changes with the
provider's name, so pictures from the old backend are not handed out as the new one's.

## Not yet

From #359, still to come: the image slot on the provider settings page (DR-013), a queue of its
own, the decision log's image line, and an action-handler example.
