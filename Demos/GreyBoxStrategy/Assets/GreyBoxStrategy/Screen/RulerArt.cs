using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Agenerela;
using Agenerela.Providers;
using UnityEngine;

namespace GreyBoxStrategy.Realm
{
    /// <summary>A ruler to paint: the realm's id, name, colour and slot, its persona if a persona rules it, and an optional backstory.</summary>
    public sealed class RulerSubject
    {
        public string Id;
        public string Name;
        /// <summary>The persona's id (<c>warmonger</c>), or null for a realm no persona rules, such as yours.</summary>
        public string Persona;
        /// <summary>The realm's map colour, as the chart palette gives it.</summary>
        public byte[] Colour = { 150, 140, 122 };
        /// <summary>Its seat on the map, 0 up: which heraldic charge its crest has.</summary>
        public int Slot;
        /// <summary>
        /// A few sentences about the realm's past, written by a model (the world backstory to come). Untrusted: it is
        /// capped and cleaned before it reaches the image model (<see cref="ImagePromptBuilder.Untrusted"/>). Null for none.
        /// </summary>
        public string Backstory;
    }

    /// <summary>
    /// The prompts for a ruler's generated portrait and flag (DR-025): the art pack's own style line first, so a
    /// generated face sits beside the pack's, then the persona's look and the realm's colour, and for the flag the
    /// realm's colour and a beast its name chooses. Plain functions of the world's seed and the realm, so the same world
    /// asks for the same pictures every time and the cache finds them.
    /// </summary>
    public static class RulerArtPrompts
    {
        /// <summary>The art pack's style line (<c>Art/Resources/ArtPack/README.md</c>), word for word.</summary>
        public const string Style =
            "Painted illustration in the manner of a late-medieval illuminated chronicle, tempera and gouache, muted earth colours, " +
            "ochre, umber, madder red and slate blue, small gilt accents, soft even light, fine brushwork. The painting fills the whole picture to its edges.";

        /// <summary>What no picture may show: the pack's own list.</summary>
        public const string Exclusions = "text, no lettering, no border, no frame, no modern objects";

        public const int PortraitWidth = 768, PortraitHeight = 896;
        public const int FlagWidth = 640, FlagHeight = 896;

        /// <summary>The longest backstory text kept in a portrait's prompt.</summary>
        public const int BackstoryLength = 160;

        private const string PortraitLine = "Bust portrait, head and shoulders, three-quarter view, centred, plain dark brown background, an invented person. The dark background reaches every edge of the picture.";

        private const string FlagLine =
            "A single heraldic war banner hanging straight down from a short gilt crossbar, seen flat from the front, filling the picture, " +
             "on a plain dark brown wall that reaches every edge of the picture. The cloth is one solid colour with a bold gilt border and a swallow-tailed lower edge.";

        // Two looks a persona can wear; the world's seed and the realm choose one, and whether a king or a queen.
        private static readonly Dictionary<string, string[]> Looks = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["warmonger"] = new[] { "a scarred, hard-faced war leader with a mail coat and a fur mantle, a fierce stare", "a battle-worn ruler in a dented breastplate under a heavy cloak, a cold glare" },
            ["builder"] = new[] { "a calm, thoughtful ruler in a plain wool robe and a simple crown, a patient look", "an earnest, careful ruler with a modest circlet and a plain gown, a steady gaze" },
            ["opportunist"] = new[] { "a thin, sly ruler with narrow eyes and a faint smile, a dark hooded cloak thrown back", "a cold, elegant ruler with a high velvet collar and a thin coronet, a watchful sideways look" },
            ["merchant"] = new[] { "a well-fed merchant ruler in a fur-trimmed brocade robe with a heavy gold chain, a pleased smile", "a shrewd merchant ruler in an embroidered robe and pearls, a knowing half smile" },
            ["proud"] = new[] { "a haughty ruler with chin raised, a tall crown and an ermine mantle", "a stern, proud ruler with a heavy crown and a long mantle, a disdainful look" },
        };

        private static readonly string[] Neutral = { "a ruler with an open, steady face, a plain gold crown and a travelling cloak", "a weathered ruler with a simple circlet and a level look" };

        private static readonly string[] Ages = { "in {0} thirties", "in {0} forties", "in {0} fifties", "in old age" };

        // Hair, and for a king his beard, so two rulers of one court do not share a face.
        private static readonly string[] Hair = { "black hair", "red hair", "fair hair", "dark brown hair", "grey hair", "chestnut hair" };
        private static readonly string[] Beards = { "a full beard", "a short pointed beard", "a drooping moustache", "a clean-shaven face", "a forked beard" };
        private static readonly string[] Complexions = { "pale skin", "olive skin", "ruddy cheeks", "brown skin", "weathered skin" };

        /// <summary>The beasts and devices a realm's name chooses one of for its banner.</summary>
        public static readonly IReadOnlyList<string> Charges = new[]
        {
            "rampant lion", "leaping stag", "spread eagle", "charging boar", "standing bear", "rearing horse",
            "coiled dragon", "swan", "tower", "crossed keys", "oak tree", "ship", "sun in splendour", "crescent moon",
        };

        // Named colours for the palette's owner colours, in the painted vocabulary.
        private static readonly (string Name, byte R, byte G, byte B)[] Colours =
        {
            ("deep madder red", 176, 46, 36), ("ochre yellow", 214, 152, 40), ("sea green", 38, 134, 104), ("slate blue", 58, 80, 170),
            ("violet purple", 128, 60, 146), ("rose pink", 196, 72, 128), ("sky blue", 40, 150, 196), ("leaf green", 130, 160, 40),
            ("vermilion orange", 214, 96, 30), ("plum purple", 115, 52, 90), ("black", 30, 28, 26), ("white", 236, 232, 220),
        };

        /// <summary>The painted name of the colour nearest <paramref name="rgb"/>: "deep madder red".</summary>
        public static string ColourName(byte[] rgb)
        {
            if (rgb == null || rgb.Length < 3) return "umber brown";
            return Colours.OrderBy(c => Sq(c.R - rgb[0]) + Sq(c.G - rgb[1]) + Sq(c.B - rgb[2])).First().Name;
        }

        /// <summary>The beast or device on <paramref name="subject"/>'s banner, chosen by its name.</summary>
        public static string Charge(RulerSubject subject) => Charges[(int)(ArtCatalogue.Hash("charge:" + (subject?.Name ?? subject?.Id)) % (uint)Charges.Count)];

        /// <summary>A seed the same on every machine for this world, realm and kind of picture.</summary>
        public static long Seed(int worldSeed, string realm, string kind) => ArtCatalogue.Hash($"{worldSeed}:{realm}:{kind}") % 1000000007L;

        /// <summary>The portrait request: style, the pack's portrait line, the ruler's look in the realm's colour, then any backstory.</summary>
        public static ImageRequest Portrait(int worldSeed, RulerSubject subject)
        {
            uint h = ArtCatalogue.Hash($"{worldSeed}:{subject.Id}:look");
            var looks = subject.Persona != null && Looks.TryGetValue(subject.Persona, out var own) ? own : Neutral;
            string look = looks[(int)(h % (uint)looks.Length)];
            bool king = (h >> 8) % 2 == 0;
            string who = king ? "A king" : "A queen";
            int ageIndex = (int)((h >> 12) % (uint)Ages.Length);
            string age = string.Format(Ages[ageIndex], king ? "his" : "her");
            string hair = ageIndex == 3 ? "white hair" : Hair[(int)((h >> 16) % (uint)Hair.Length)];
            string face = Complexions[(int)((h >> 20) % (uint)Complexions.Length)] + (king ? ", " + Beards[(int)((h >> 24) % (uint)Beards.Length)] : "");
            string prompt = new ImagePromptBuilder(Style)
                .Detail(PortraitLine)
                .Detail($"{who} {age} with {hair}, {face}: {look}, wearing {ColourName(subject.Colour)} cloth with gilt trim")
                .Untrusted(subject.Backstory, BackstoryLength)
                .Exclude(Exclusions)
                .Build();
            return new ImageRequest { Prompt = prompt, Width = PortraitWidth, Height = PortraitHeight, Seed = Seed(worldSeed, subject.Id, "portrait") };
        }

        /// <summary>The flag request: style, then a hanging banner in the realm's colour bearing the beast its name chooses.</summary>
        public static ImageRequest Flag(int worldSeed, RulerSubject subject)
        {
            string colour = ColourName(subject.Colour);
            string metal = colour.Contains("yellow") || colour.Contains("white") ? "dark umber" : "gold";
            string prompt = new ImagePromptBuilder(Style)
                .Detail(FlagLine)
                .Detail($"The cloth is {colour}. In its middle, one large {metal} {Charge(subject)}, drawn simply and boldly as in a herald's roll")
                .Exclude(Exclusions + ", no people, no second banner, no pattern on the field")
                .Build();
            return new ImageRequest { Prompt = prompt, Width = FlagWidth, Height = FlagHeight, Seed = Seed(worldSeed, subject.Id, "flag") };
        }

        private static int Sq(int x) => x * x;
    }

    /// <summary>The pictures one world's rulers got: PNG bytes by realm id, and what happened on the way.</summary>
    public sealed class RulerArtSet
    {
        public readonly Dictionary<string, byte[]> Portraits = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        public readonly Dictionary<string, byte[]> Flags = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        /// <summary>Pictures newly made, found in the cache, and left to the art pack.</summary>
        public int Made, FromCache, Fallbacks;
        /// <summary>Pictures refused by the maker's check (a painted frame), each asked for once more.</summary>
        public int Refused;
        /// <summary>Why the backend stopped being asked, or null when it never failed.</summary>
        public string StoppedBecause;
        public float Seconds;

        /// <summary>"10 pictures: 6 made, 4 from the cache, 0 left to the art pack, in 98 s".</summary>
        public override string ToString() =>
            $"{Made + FromCache + Fallbacks} pictures: {Made} made, {FromCache} from the cache, {Fallbacks} left to the art pack, {Refused} refused for a frame, in {Seconds:0} s" +
            (StoppedBecause != null ? $"; the image server stopped being asked: {StoppedBecause}" : "");
    }

    /// <summary>
    /// Makes every ruler's portrait and flag for one world, behind the loading card (DR-025): each from the cache when
    /// it is there, otherwise from the image backend. On the backend's first failure, past the time budget, or when the
    /// player skips, the backend is asked no more and the rest come from the cache or fall back to the art pack. Never
    /// throws for a failure: a world always starts.
    /// </summary>
    public sealed class RulerArtMaker
    {
        private readonly IImageProvider backend;
        private readonly IImageCache cache;

        /// <param name="backend">The image server, or null to use the cache alone.</param>
        /// <param name="cache">Where this world's pictures are kept.</param>
        /// <param name="keyName">The backend's name keys are made with, for a cache read with no backend.</param>
        public RulerArtMaker(IImageProvider backend, IImageCache cache, string keyName = "comfyui")
        {
            this.backend = backend;
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
            KeyName = backend?.Name ?? keyName;
        }

        public string KeyName { get; }

        /// <summary>
        /// Whether a picture may hang, such as one with no painted frame of its own (<see cref="RulerArtServer.LooksFramed"/>).
        /// A picture it refuses is asked for once more with another seed; if that one hangs, it is stored under the first
        /// request's key too, so the next visit finds it at once. Null takes every picture.
        /// </summary>
        public Func<byte[], bool> Accept;

        /// <summary>
        /// Told each picture as it hangs (the realm's id, true for its banner, the PNG), so a court painted in the
        /// background shows each face as it arrives. Called on the thread that awaited <see cref="MakeAsync"/>.
        /// </summary>
        public Action<string, bool, byte[]> Painted;

        /// <summary>What each further try adds to the seed.</summary>
        public const long RetrySeedOffset = 7919;

        /// <summary>How many more tries a refused picture gets, each with another seed. 2: a banner came framed twice in 20.</summary>
        public int Retries = 2;

        /// <summary>
        /// The portrait and flag of every subject: <paramref name="progress"/> is told each step ("Painting Valen's
        /// ruler", 0 to 1). Past <paramref name="budgetSeconds"/>, or once <paramref name="skip"/> is cancelled, no new
        /// picture is asked for.
        /// </summary>
        public async Awaitable<RulerArtSet> MakeAsync(int worldSeed, IReadOnlyList<RulerSubject> subjects, Action<string, float> progress,
            float budgetSeconds, CancellationToken skip)
        {
            var set = new RulerArtSet();
            var clock = Stopwatch.StartNew();
            var cached = new CachedImageProvider(backend, cache, KeyName);
            var cacheOnly = new CachedImageProvider(null, cache, KeyName);
            var jobs = new List<(RulerSubject Subject, bool Flag, ImageRequest Request)>();
            foreach (var s in subjects ?? Array.Empty<RulerSubject>())
            {
                jobs.Add((s, false, RulerArtPrompts.Portrait(worldSeed, s)));
                jobs.Add((s, true, RulerArtPrompts.Flag(worldSeed, s)));
            }

            bool asking = backend != null;
            if (!asking) set.StoppedBecause = "no image server is set";
            for (int i = 0; i < jobs.Count; i++)
            {
                var (subject, flag, request) = jobs[i];
                if (asking && skip.IsCancellationRequested) { asking = false; set.StoppedBecause = "skipped"; }
                if (asking && clock.Elapsed.TotalSeconds > budgetSeconds) { asking = false; set.StoppedBecause = $"the {budgetSeconds:0} s budget ran out"; }
                bool known = cached.Has(request);
                progress?.Invoke(known || !asking
                    ? $"Hanging {subject.Name}'s {(flag ? "banner" : "portrait")}"
                    : $"Painting {subject.Name}'s {(flag ? "banner" : "ruler")} ({i + 1} of {jobs.Count})", i / (float)Math.Max(1, jobs.Count));

                async Awaitable<ImageResult> Ask(ImageRequest r)
                {
                    try
                    {
                        return await (asking ? cached : cacheOnly).GenerateAsync(r, asking ? skip : CancellationToken.None);
                    }
                    catch (OperationCanceledException)
                    {
                        asking = false;
                        set.StoppedBecause = "skipped";
                        return await cacheOnly.GenerateAsync(r, CancellationToken.None);
                    }
                    catch (ArgumentException e)
                    {
                        // A workflow that cannot take the request: no use asking again.
                        asking = false;
                        set.StoppedBecause = e.Message;
                        return ImageResult.Failed(ProviderErrorKind.Rejected, KeyName, e.Message);
                    }
                }

                var result = await Ask(request);
                bool refused = false;
                if (result.Succeeded && Accept != null && !Accept(result.Png))
                {
                    set.Refused++;
                    refused = true;
                    for (int k = 1; k <= Retries && refused; k++)
                    {
                        var again = new ImageRequest
                        {
                            Prompt = request.Prompt, Width = request.Width, Height = request.Height,
                            Seed = (request.Seed ?? 0) + k * RetrySeedOffset, Parameters = new Dictionary<string, string>(request.Parameters),
                        };
                        var next = await Ask(again);
                        if (!next.Succeeded) { result = next; refused = false; }
                        else if (Accept(next.Png))
                        {
                            // Kept under the first request's key too, so the next visit finds it without a refusal.
                            cache.Put(request.Key(KeyName), next.Png);
                            result = next;
                            refused = false;
                        }
                        else set.Refused++;
                    }
                }

                if (refused)
                {
                    set.Fallbacks++; // every try had a frame of its own: the art pack's picture hangs instead
                }
                else if (result.Succeeded)
                {
                    (flag ? set.Flags : set.Portraits)[subject.Id] = result.Png;
                    Painted?.Invoke(subject.Id, flag, result.Png);
                    if (result.FromCache) set.FromCache++; else set.Made++;
                }
                else
                {
                    set.Fallbacks++;
                    if (asking)
                    {
                        // The server is down, busy or refusing: every later picture would wait just as long.
                        asking = false;
                        set.StoppedBecause = $"{result.Error}: {result.ErrorMessage}";
                    }
                }
            }

            progress?.Invoke("The court is painted", 1f);
            set.Seconds = (float)clock.Elapsed.TotalSeconds;
            return set;
        }
    }
}
