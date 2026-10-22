namespace StudyApp.Application.Embeddings;

// Bump whenever extraction OR chunking changes shape. Every chunk below this
// version is considered stale and will be re-embedded by the reindex job.
public static class ExtractionPipeline
{
    public const int Version = 3; // v3 = structure-aware chunker

    // Rough token estimate; good enough for budgeting. Swap for a real tokenizer later.
    public static int EstimateTokens(string text) => Math.Max(1, text.Length / 4);
}
