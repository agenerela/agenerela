namespace GreyBoxStrategy
{
    /// <summary>
    /// The game's dice. Every roll is a hash of (game seed, turn, who, purpose), never the next
    /// number from a shared generator, so a roll does not depend on how many rolls came before it.
    /// </summary>
    /// <remarks>
    /// That is what makes an A/B run over a whole game fair (hard rule 2): two games with one seed
    /// roll the same dice for the same battle even when the countries decided differently on the
    /// way there. A stateful generator would drift the moment one decision changed.
    /// </remarks>
    public static class Dice
    {
        /// <summary>A number in [0, 1).</summary>
        public static double Unit(int seed, int turn, string who, string purpose)
        {
            ulong h = Mix((ulong)(uint)seed);
            h = Mix(h ^ (ulong)(uint)turn);
            h = Mix(h ^ Fnv(who));
            h = Mix(h ^ Fnv(purpose));
            return (h >> 11) * (1.0 / (1UL << 53));
        }

        /// <summary>A number in [min, max).</summary>
        public static double Range(int seed, int turn, string who, string purpose, double min, double max)
        {
            return min + (max - min) * Unit(seed, turn, who, purpose);
        }

        /// <summary>An integer in [minInclusive, maxExclusive).</summary>
        public static int Int(int seed, int turn, string who, string purpose, int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            int span = maxExclusive - minInclusive;
            int r = (int)(Unit(seed, turn, who, purpose) * span);
            if (r >= span) r = span - 1;
            return minInclusive + r;
        }

        private static ulong Fnv(string s)
        {
            ulong h = 14695981039346656037UL;
            if (s == null) return h;
            foreach (char c in s)
            {
                h ^= c;
                h *= 1099511628211UL;
            }
            return h;
        }

        // SplitMix64's finaliser: cheap, and every input bit reaches every output bit.
        private static ulong Mix(ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            return x ^ (x >> 31);
        }
    }
}
