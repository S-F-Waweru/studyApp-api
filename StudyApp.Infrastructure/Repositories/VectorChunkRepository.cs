using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using StudyApp.Application.Repositories;
using StudyApp.Domain.Entities;
using StudyApp.Infrastructure.Persistence;

namespace StudyApp.Infrastructure.Repositories;

public class VectorChunkRepository : IVectorChunkRepository
{
    private readonly AppDbContext _context;

    public VectorChunkRepository(AppDbContext context) => _context = context;

    public async Task AddRangeAsync(IEnumerable<VectorChunk> chunks)
    {
        _context.VectorChunks.AddRange(chunks);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteBySourceIdAsync(Guid sourceId)
    {
        var chunks = await _context.VectorChunks.Where(c => c.SourceId == sourceId).ToListAsync();
        _context.VectorChunks.RemoveRange(chunks);
        await _context.SaveChangesAsync();
    }

    // Cosine distance search (pgvector's <=> operator), filtered to the given scope_ids (own scope + descendants, per §10).
    public async Task<IEnumerable<VectorChunk>> SearchTopKAsync(float[] queryEmbedding, IEnumerable<Guid> scopeIds, int k)
    {
        var vector = new Vector(queryEmbedding);
        var scopeIdList = scopeIds.ToList();

        return await _context.VectorChunks
            .Where(c => scopeIdList.Contains(c.ScopeId))
            .OrderBy(c => c.Embedding.CosineDistance(vector))
            .Take(k)
            .ToListAsync();
    }
}
