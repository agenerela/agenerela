using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace GreyBoxStrategy.Realm
{
    /// <summary>
    /// The screen's pictures (#20): every frame that shows a ruler, a general or an event asks here, and gets the art
    /// pack's picture (<see cref="ArtCatalogue"/>), the old persona sheet where the pack has none, or the empty frame.
    /// </summary>
    public sealed partial class ScreenUI
    {
        /// <summary>The game's seed: with a realm's id it chooses the realm's face, the same all game and in its replay.</summary>
        public int ArtSeed { get; set; }

        private const string YourFaceKey = "realm.art.your-face";

        /// <summary>Which of the four neutral faces your own ruler wears (0 to 3); kept between games.</summary>
        public static int YourFace
        {
            get => PlayerPrefs.GetInt(YourFaceKey, 0);
            set => PlayerPrefs.SetInt(YourFaceKey, ((value % ArtCatalogue.PlayerFaces) + ArtCatalogue.PlayerFaces) % ArtCatalogue.PlayerFaces);
        }

        // Set when you click your own portrait: you chose a face from the pack, so a generated one no longer shows for you.
        private bool chosePackFace;

        /// <summary>The ruler's generated portrait this game (DR-025), or null: none was made, or you picked a pack face for yours.</summary>
        private Texture2D GeneratedFace(string country) =>
            chosePackFace && country == You ? null : RulerArtServer.PortraitOf(country);

        private Dictionary<string, string> rulerFaces;
        private string rulerFacesFor;

        /// <summary>The face id a realm's ruler wears this game (<c>merchant-2</c>), or null for an id that is no realm.</summary>
        public string FaceOf(string country)
        {
            if (country == null) return null;
            string key = $"{ArtSeed}|{You}|{YourFace}|{CountryIds?.Count}";
            if (rulerFaces == null || rulerFacesFor != key)
            {
                rulerFaces = ArtCatalogue.RulerFaces(ArtSeed, CountryIds, id => Players?.GetValueOrDefault(id)?.Persona, You, YourFace);
                rulerFacesFor = key;
            }
            return rulerFaces.TryGetValue(country, out string f) ? f : null;
        }

        // The head and shoulders of a 6:7 bust, as a square: what a round or square face shows.
        private static readonly Rect HeadCrop = new Rect(0.11f, 0.26f, 0.78f, 0.78f * 6f / 7f);

        /// <summary>
        /// A ruler's face for a small round or square frame (the words on the map, the inspector, an envoy's card, the
        /// end screen): the pack's portrait cropped to the head, or the persona sheet's face.
        /// </summary>
        private Image RulerFace(string country)
        {
            var look = CountryLook.Of(country);
            var tex = GeneratedFace(country) ?? ArtCatalogue.Portrait(FaceOf(country));
            if (tex != null) return new Image { image = tex, uv = HeadCrop, scaleMode = ScaleMode.ScaleAndCrop };
            return new Image { image = portraits, uv = Faces.Uv(Players?.GetValueOrDefault(country)?.Persona, (look?.Slot ?? 0) % 2), scaleMode = ScaleMode.ScaleAndCrop };
        }

        /// <summary>
        /// A ruler's portrait in its gilt frame (the realm panel, the top bar): the pack's portrait, the persona
        /// sheet's face where a persona rules and the pack has none, or the empty frame.
        /// </summary>
        private VisualElement RulerPortrait(string country, float width, float height, string tip = null)
        {
            var tex = GeneratedFace(country) ?? ArtCatalogue.Portrait(FaceOf(country), small: height <= 80);
            if (tex != null) return Framed(tex, new Rect(0, 0, 1, 1), width, height, tip);
            string persona = Players?.GetValueOrDefault(country)?.Persona;
            if (portraits != null && persona != null)
                return Framed(portraits, Faces.Uv(persona, (CountryLook.Of(country)?.Slot ?? 0) % 2), width, height, tip);
            return tip != null ? new PortraitSlot(width, height, tip) : new PortraitSlot(width, height);
        }

        /// <summary>A general's portrait in its frame (the army panel), from the general's name; the empty frame for none.</summary>
        private VisualElement GeneralPortrait(string general, float width, float height, string emptyTip)
        {
            var tex = ArtCatalogue.Portrait(ArtCatalogue.GeneralFace(ArtSeed, general), small: height <= 80);
            return tex != null ? Framed(tex, new Rect(0, 0, 1, 1), width, height, general) : new PortraitSlot(width, height, emptyTip);
        }

        /// <summary>
        /// Your own ruler's portrait beside the banner in the top bar. While you rule, a click changes it to the next
        /// of the four neutral faces, and the choice is kept for later games.
        /// </summary>
        private VisualElement MyPortrait(string country)
        {
            bool mine = Fog.On && country == You;
            string name = CountryLook.NameOf(country);
            var e = RulerPortrait(country, 58, 66, mine ? $"{name}'s ruler: you. Click: another face" : $"{name}'s ruler");
            e.name = "my-portrait";
            if (mine && e is Image img)
                img.RegisterCallback<ClickEvent>(_ =>
                {
                    // The first click leaves the generated face for the pack's face you last chose; later ones step on.
                    if (!chosePackFace && RulerArtServer.PortraitOf(country) != null) chosePackFace = true;
                    else YourFace = YourFace + 1;
                    if (ArtCatalogue.Portrait(FaceOf(country)) is Texture2D next) img.image = next;
                });
            return e;
        }

        private static Image Framed(Texture tex, Rect uv, float width, float height, string tip)
        {
            var img = new Image { image = tex, uv = uv, scaleMode = ScaleMode.ScaleAndCrop, tooltip = tip };
            img.style.width = width; img.style.height = height; img.style.flexShrink = 0;
            img.AddToClassList("portrait-slot");
            return img;
        }

        /// <summary>
        /// The picture for a storyteller's event by its template id (<c>plague</c>, <c>begin_fever</c>), or null when
        /// the pack has none: the event window's picture slot shows it with
        /// <c>picture.image = ui.EventPicture(e.TemplateId)</c>.
        /// </summary>
        public Texture2D EventPicture(string templateId) => ArtCatalogue.Event(templateId);
    }
}
