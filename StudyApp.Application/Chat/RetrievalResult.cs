// StudyApp.Application/Chat/RetrievalResult.cs
using StudyApp.Domain.Entities;

namespace StudyApp.Application.Chat;

public record RetrievedSource(string Label, Guid ChunkId, Guid SourceId, string SourceType,
    string? Title, string? Locator, string? HeadingPath, double Score);

public record RetrievalResult(IReadOnlyList<VectorChunk> Chunks, IReadOnlyList<RetrievedSource> Sources);