using System;

namespace GreyBoxStrategy
{
    /// <summary>How big a generated world is (rules v2, section 2.2).</summary>
    public enum MapSize
    {
        /// <summary>About 30 provinces; four countries by default.</summary>
        Small,
        /// <summary>About 60 provinces; six countries by default.</summary>
        Medium,
        /// <summary>About 100 provinces; eight countries by default.</summary>
        Large,
        /// <summary>About 150 provinces; ten countries by default.</summary>
        Huge,
    }

    /// <summary>The shape of the land (section 2.2). All land is one connected whole.</summary>
    public enum LandShape
    {
        /// <summary>One round land in the sea.</summary>
        Continent,
        /// <summary>Two lobes joined by a narrow neck.</summary>
        TwoLands,
        /// <summary>A ring of land around a sea.</summary>
        InlandSea,
        /// <summary>Land open to one edge of the chart, narrowing to a tip.</summary>
        Peninsula,
        /// <summary>A long land along a sea, running off the bottom of the chart.</summary>
        LongCoast,
    }

    /// <summary>How many special places a world has (section 2.5).</summary>
    public enum SpecialPlaces
    {
        None,
        /// <summary>One per 15 provinces.</summary>
        Few,
        /// <summary>One per 8 provinces.</summary>
        Many,
    }

    /// <summary>
    /// What the map generator takes besides the seed and the number of countries (section 2.2). Plain
    /// data, saved in <see cref="GameSetup.Generator"/>.
    /// </summary>
    /// <remarks>
    /// The page's "neutral land" setting is not here: it and "starting realm" fix the same number (the
    /// share of provinces owned at the start), and on a medium map the defaults disagree (3 provinces
    /// for each of 6 countries is 30% owned, not 45%). The realm size is the setting; the neutral share
    /// follows from it, and the generator reports it.
    /// </remarks>
    public sealed class MapSettings
    {
        public MapSize Size = MapSize.Medium;
        public LandShape Shape = LandShape.Continent;
        /// <summary>Provinces each country starts with, its capital included: 2 to 5.</summary>
        public int StartingRealm = 3;
        public SpecialPlaces Specials = SpecialPlaces.Few;

        public MapSettings Clone() => (MapSettings)MemberwiseClone();

        /// <summary>The province count each size aims at.</summary>
        public static int Provinces(MapSize size)
        {
            switch (size)
            {
                case MapSize.Small: return 30;
                case MapSize.Medium: return 60;
                case MapSize.Large: return 100;
                default: return 150;
            }
        }

        /// <summary>The number of countries each size suggests (section 2.2).</summary>
        public static int DefaultCountries(MapSize size)
        {
            switch (size)
            {
                case MapSize.Small: return 4;
                case MapSize.Medium: return 6;
                case MapSize.Large: return 8;
                default: return 10;
            }
        }

        /// <summary>The most countries a size can seat fairly: capitals must stand apart, and realms must not touch.</summary>
        public static int MaxCountries(MapSize size)
        {
            switch (size)
            {
                case MapSize.Small: return 5;
                case MapSize.Medium: return 8;
                default: return 10;
            }
        }

        /// <summary>Why these settings cannot make a world for that many countries, or null.</summary>
        public string WhyInvalid(int countries)
        {
            if (!Enum.IsDefined(typeof(MapSize), Size)) return $"unknown map size {(int)Size}";
            if (!Enum.IsDefined(typeof(LandShape), Shape)) return $"unknown land shape {(int)Shape}";
            if (!Enum.IsDefined(typeof(SpecialPlaces), Specials)) return $"unknown special places setting {(int)Specials}";
            if (StartingRealm < 2 || StartingRealm > 5) return $"a starting realm of {StartingRealm} provinces is outside 2 to 5";
            if (countries > MaxCountries(Size))
                return $"a {Size.ToString().ToLowerInvariant()} map seats at most {MaxCountries(Size)} countries, not {countries}";
            int owned = countries * StartingRealm;
            if (owned * 10 > Provinces(Size) * 6)
                return $"{countries} realms of {StartingRealm} would own over 60% of a {Size.ToString().ToLowerInvariant()} map";
            return null;
        }
    }
}
