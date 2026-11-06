namespace LivingVale.Agenerela
{
    /// <summary>
    /// The models F7 offers by default, in order. Each was compared for Living Vale's NPCs on an
    /// 8 GB laptop GPU (llm-wiki findings, 5 November 2026). <c>qwen3.5:2b</c> is first because it is
    /// the game's default; <c>qwen3.5:4b</c> comes next, from the first comparison. The rest are in
    /// the order of their blind writing scores in the second. <c>ValeBootstrap</c>'s Inspector list
    /// starts as a copy of this one.
    /// </summary>
    public static class ModelCycleDefaults
    {
        public static readonly string[] Models =
        {
            "qwen3.5:2b",
            "qwen3.5:4b",
            "gemma4:e2b-it-qat",
            "hf.co/QuantFactory/L3-8B-Stheno-v3.2-GGUF:Q4_K_M",
            "nemotron-3-nano:4b",
            "llama3.2:3b",
            "granite4.1:3b",
            "lfm3:1.7b",
            "hf.co/openbmb/MiniCPM5-2B-GGUF:Q4_K_M",
            "ministral-3:3b",
        };
    }
}
