namespace StudyApp.Application.Embeddings;

// Implementations vary per source_type: Note (already text), Document (read file),
// Scribble (canvas JSON + image captions).
public interface ITextExtractor
{
    Task<ExtractionResult> ExtractAsync(string sourceType, Guid sourceId, CancellationToken ct = default);

    // Cheap: hash of the raw source only. Lets the worker skip expensive work
    // (vision captions, re-embedding) when nothing actually changed.
    Task<string> FingerprintAsync(string sourceType, Guid sourceId, CancellationToken ct = default);
}
