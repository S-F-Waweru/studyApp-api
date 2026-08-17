namespace StudyApp.Infrastructure.Embeddings;

public static class TextChunker
{
    // Simple fixed-size character chunking with overlap. Good enough for v1 —
    // swap for a token-aware/semantic chunker later without touching callers.
    public static List<string> Chunk(string text, int maxChunkSize = 800, int overlap = 100)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return chunks;

        var start = 0;
        while (start < text.Length)
        {
            var length = Math.Min(maxChunkSize, text.Length - start);
            chunks.Add(text.Substring(start, length));
            if (start + length >= text.Length) break;
            start += maxChunkSize - overlap;
        }
        return chunks;
    }
}
