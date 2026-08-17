namespace StudyApp.Application.Embeddings;

// Implementations vary per source_type: Note (already text), Document (read file), Scribble (extracted_text or caption).
public interface ITextExtractor
{
    Task<string?> ExtractAsync(string sourceType, Guid sourceId);
}
