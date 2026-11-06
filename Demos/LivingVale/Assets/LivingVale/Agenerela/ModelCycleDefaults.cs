namespace LivingVale.Agenerela
{
    /// <summary>
    /// The models F7 offers by default, in order. Each was compared for Living Vale's NPCs on an
    /// 8 GB laptop GPU (llm-wiki findings, 5 November 2026). <c>gemma4:e2b-it-qat</c> is first: it
    /// scored best in the blind writing comparison, and the lead made it the game's default on
    /// 5 November. <c>qwen3.5:2b</c>, the framework's default and that comparison's control, is
    /// second; <c>qwen3.5:4b</c> comes next, from the first comparison. The rest are in the order of
    /// their blind writing scores. <c>ValeBootstrap</c>'s Inspector list
    /// starts as a copy of this one.
    /// </summary>
    public static class ModelCycleDefaults
    {
        public static readonly string[] Models =
        {
            "gemma4:e2b-it-qat",
            "qwen3.5:2b",
            "qwen3.5:4b",
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
