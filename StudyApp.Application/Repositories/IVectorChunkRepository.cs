using StudyApp.Domain.Entities;

namespace StudyApp.Application.Repositories;

public interface IVectorChunkRepository
{
    Task AddRangeAsync(IEnumerable<VectorChunk> chunks);
    Task DeleteBySourceIdAsync(Guid sourceId);
    Task<IEnumerable<VectorChunk>> SearchTopKAsync(float[] queryEmbedding, IEnumerable<Guid> scopeIds, int k);
}
