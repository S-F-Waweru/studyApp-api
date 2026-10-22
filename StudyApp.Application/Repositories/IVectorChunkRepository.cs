using StudyApp.Application.Scoping;
using StudyApp.Domain.Entities;

namespace StudyApp.Application.Repositories;

public record ScoredChunk(VectorChunk Chunk, double Distance)
{
    public double Score => 1.0 - Distance; // cosine distance -> similarity
}

public interface IVectorChunkRepository
{
    Task AddRangeAsync(IEnumerable<VectorChunk> chunks, CancellationToken ct = default);
    Task DeleteBySourceIdAsync(Guid sourceId, CancellationToken ct = default);
    Task ReplaceForSourceAsync(Guid sourceId, IReadOnlyList<VectorChunk> chunks, CancellationToken ct = default);

    // Over-fetch deliberately: a narrow filter (one note) makes the HNSW index
    // under-return, so the caller must ask for more than it needs.
    Task<IReadOnlyList<ScoredChunk>> SearchVectorAsync(float[] queryEmbedding, RetrievalFilter filter, int k, CancellationToken ct = default);

    Task<IReadOnlyList<ScoredChunk>> SearchKeywordAsync(string query, RetrievalFilter filter, int k, CancellationToken ct = default);

    Task<IReadOnlyList<VectorChunk>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);

    // Number of chunks a chat's resolved scope can currently reach (scope summary endpoint).
    Task<int> CountAsync(RetrievalFilter filter, CancellationToken ct = default);
}
