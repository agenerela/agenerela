using System;
using Newtonsoft.Json;

namespace Agenerela.Evaluation
{
    /// <summary>
    /// Whether the difference in accuracy between the two arms is larger than run-to-run noise at
    /// this number of prompts (build plan §4, item 1). A difference within noise is no result.
    /// </summary>
    /// <remarks>
    /// The noise floor is estimated, not measured at each n: the prototype's baseline arm drifted
    /// by about <see cref="MeasuredFloorPoints"/> points across identical runs at n ≈
    /// <see cref="MeasuredAtN"/>, and the floor is scaled from there as a proportion's standard
    /// error scales, <c>10 × √(50 / n)</c>: about 16 points at n = 20, 5 at n = 200.
    /// </remarks>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class NoiseCheck
    {
        /// <summary>The drift measured across three identical prototype runs, in points.</summary>
        public const double MeasuredFloorPoints = 10;

        /// <summary>The number of prompts it was measured at.</summary>
        public const int MeasuredAtN = 50;

        /// <summary>The formula, as the report prints it.</summary>
        public const string Formula = "10 x sqrt(50 / n) points, scaled from the prototype's 10-point drift at n = 50";

        /// <summary><c>all</c> for the whole set, otherwise a category id.</summary>
        [JsonProperty("scope")] public string Scope;

        /// <summary>The prompts each arm was asked in this scope.</summary>
        [JsonProperty("n")] public int N;

        /// <summary>Each arm's accuracy, in points (0 to 100).</summary>
        [JsonProperty("accuracyA")] public double AccuracyA;
        [JsonProperty("accuracyB")] public double AccuracyB;

        /// <summary>B's accuracy minus A's, in points.</summary>
        [JsonProperty("differencePoints")] public double DifferencePoints;

        [JsonProperty("noiseFloorPoints")] public double NoiseFloorPoints;

        /// <summary>True when the difference is smaller than the noise floor, so it says nothing.</summary>
        [JsonProperty("withinNoise")] public bool WithinNoise;

        /// <summary>The noise floor at <paramref name="n"/> prompts, in points.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is below 1.</exception>
        public static double FloorAt(int n)
        {
            if (n < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(n), n, "The noise floor needs at least one prompt.");
            }

            return MeasuredFloorPoints * Math.Sqrt((double)MeasuredAtN / n);
        }

        internal static NoiseCheck Of(string scope, OutcomeCounts a, OutcomeCounts b)
        {
            double accuracyA = a.Accuracy * 100, accuracyB = b.Accuracy * 100;
            double floor = FloorAt(a.Total);
            double difference = accuracyB - accuracyA;
            return new NoiseCheck
            {
                Scope = scope,
                N = a.Total,
                AccuracyA = accuracyA,
                AccuracyB = accuracyB,
                DifferencePoints = difference,
                NoiseFloorPoints = floor,
                WithinNoise = Math.Abs(difference) < floor,
            };
        }
    }
}
