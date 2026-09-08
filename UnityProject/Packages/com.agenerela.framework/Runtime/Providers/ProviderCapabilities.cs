namespace Agenerela.Providers
{
    /// <summary>
    /// The format a provider wants the decision schema in. Not a detail of transport —
    /// it selects a different serializer over the same provider-neutral
    /// <c>DecisionSchema</c> (#8).
    /// </summary>
    public enum SchemaDialect
    {
        /// <summary>
        /// JSON Schema. Ollama takes it in its <c>format</c> field and compiles it to a
        /// sampling grammar internally; cloud APIs take their own near-equivalent of it.
        /// Emitted by <c>JsonSchemaSerializer</c> (#9).
        /// </summary>
        JsonSchema,

        /// <summary>
        /// GBNF grammar text. llama.cpp — and so LLMUnity, and so the in-process provider
        /// the framework actually ships with — wants the grammar directly and will not
        /// convert a JSON Schema for you. Needs its own serializer emitting the same
        /// constraints; budgeted as real work in Phase 6b, not a free adapter.
        /// </summary>
        Gbnf,
    }

    /// <summary>
    /// A vendor's request quota. Null on <see cref="ProviderCapabilities.RateLimit"/> means
    /// "no limit worth modelling" — the local providers, where the only ceiling is your own
    /// hardware.
    /// </summary>
    /// <remarks>
    /// This lives in the interface rather than inside one provider because every cloud
    /// vendor sets different numbers, and the scheduler (Phase 2) has to pace requests
    /// against whichever one is plugged in. Numbers here describe the vendor's published
    /// limits; enforcement — throttling and budget caps — is Phase 6, in the cloud provider
    /// base class rather than in each vendor.
    /// </remarks>
    public sealed class RateLimit
    {
        /// <summary>
        /// Requests per minute. The one that bites during a benchmark run, where decisions
        /// arrive as fast as the harness can issue them. Gemini's free tier was ~15.
        /// </summary>
        public int RequestsPerMinute;

        /// <summary>
        /// Requests per day. The one that bites during an evaluation sweep — a labelled
        /// prompt set run across several model configurations can exhaust a daily quota
        /// long before the sweep finishes. Gemini's free tier was ~1000.
        /// </summary>
        public int RequestsPerDay;
    }

    /// <summary>
    /// What a provider can do, stated rather than assumed. The differences below are the
    /// reason this interface exists at all — they were observed against real backends, not
    /// invented for symmetry, and each one changes what the framework must emit or how it
    /// must pace itself.
    /// </summary>
    /// <remarks>
    /// Treated as constant for the lifetime of a provider instance, so callers may cache
    /// it. Nothing here turns a guard off: the validation pipeline runs over every decision
    /// whatever a provider claims (see <see cref="ILLMProvider"/>).
    /// </remarks>
    public sealed class ProviderCapabilities
    {
        /// <summary>
        /// Can the provider constrain generation to the schema? If false, the guard
        /// pipeline is the ONLY protection — it must still be safe. A provider that only
        /// does plain chat is legal here; it simply asks the model for JSON in the prompt
        /// and is likelier to come back with something unparseable, which the pipeline
        /// classifies rather than crashes on.
        /// </summary>
        public bool SupportsConstrainedDecoding;

        /// <summary>
        /// Gemini needs field order stated; Ollama infers it from the schema. Field order
        /// is <c>action</c>, then <c>target</c>, then <c>statement</c>, and it is worth
        /// +11.7 accuracy points on its own — so where a vendor will not infer it, the
        /// serializer emits it explicitly (Gemini's <c>propertyOrdering</c>). A provider
        /// that sets this false is saying "my dialect preserves the order I am given",
        /// not "order does not matter here".
        /// </summary>
        public bool NeedsExplicitPropertyOrdering;

        /// <summary>
        /// Which serializer to run over <c>DecisionSchema</c> (#8): JsonSchema for Ollama
        /// and cloud APIs, Gbnf for the in-process provider.
        /// </summary>
        public SchemaDialect Dialect;

        /// <summary>
        /// Null for local providers. Cloud vendors each set different limits, so the
        /// scheduler reads this rather than hard-coding one vendor's numbers.
        /// </summary>
        public RateLimit RateLimit;

        /// <summary>
        /// Does the vendor reject <c>""</c> as an enum value? Gemini answers HTTP 400 to
        /// one, which is half of why the <c>no_target</c> sentinel exists (the other half
        /// being that it gives the model a way to *say* "what you asked for isn't here").
        /// </summary>
        /// <remarks>
        /// The framework never emits an empty enum value for any provider, so this flag
        /// changes nothing at runtime. It is here because it is the sharpest example of a
        /// vendor difference that a serializer has to know about rather than discover in
        /// production: it lets a serializer assert its own output, and gives the Phase 6
        /// conformance suite something to check against, instead of the constraint living
        /// only in a comment about Gemini.
        /// </remarks>
        public bool RejectsEmptyEnumValues;
    }
}
