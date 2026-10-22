using Microsoft.EntityFrameworkCore;
using StudyApp.Application.Repositories;
using StudyApp.Domain.Enums;
using StudyApp.Infrastructure.Persistence;

namespace StudyApp.Infrastructure.Repositories;

public class SourceIndexStatusRepository : ISourceIndexStatusRepository
{
    private readonly AppDbContext _context;

    public SourceIndexStatusRepository(AppDbContext context) => _context = context;

    public async Task MarkAsync(string sourceType, Guid sourceId, string status, int chunkCount = 0,
                                string? error = null, string? contentHash = null, int? pageCount = null,
                                CancellationToken ct = default)
    {
        if (!Enum.TryParse<IndexStatus>(status, true, out var parsed)) parsed = IndexStatus.Failed;

        switch (sourceType.ToLowerInvariant())
        {
            case "document":
                var doc = await _context.Documents.FindAsync([sourceId], ct);
                if (doc is null) return;
                doc.IndexStatus = parsed;
                doc.ChunkCount = chunkCount;
                doc.ExtractionError = error;
                doc.IndexedAt = parsed is IndexStatus.Indexed or IndexStatus.Unchanged ? DateTime.UtcNow : doc.IndexedAt;
                if (contentHash is not null) doc.ContentHash = contentHash;
                if (pageCount is not null) doc.PageCount = pageCount;
                break;

            case "scribble":
                var scribble = await _context.Scribbles.FindAsync([sourceId], ct);
                if (scribble is null) return;
                scribble.IndexStatus = parsed;
                scribble.ChunkCount = chunkCount;
                scribble.ExtractionError = error;
                scribble.IndexedAt = parsed is IndexStatus.Indexed or IndexStatus.Unchanged ? DateTime.UtcNow : scribble.IndexedAt;
                if (contentHash is not null) scribble.ContentHash = contentHash;
                break;

            default: // notes carry no status column
                return;
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<(string SourceType, Guid SourceId)>> GetStaleSourcesAsync(
        int pipelineVersion, int take, CancellationToken ct = default) =>
        await _context.VectorChunks
            .Where(c => c.PipelineVersion < pipelineVersion)
            .Select(c => new { c.SourceType, c.SourceId })
            .Distinct()
            .Take(take)
            .Select(x => ValueTuple.Create(x.SourceType, x.SourceId))
            .ToListAsync(ct);
    
    
    // SourceIndexStatusRepository additions
    public async Task<string?> GetContentHashAsync(string sourceType, Guid sourceId, CancellationToken ct = default) =>
        sourceType.ToLowerInvariant() switch
        {
            "document" => (await _context.Documents.FindAsync([sourceId], ct))?.ContentHash,
            "scribble" => (await _context.Scribbles.FindAsync([sourceId], ct))?.ContentHash,
            _ => null
        };

    public async Task<int?> GetIndexedPipelineVersionAsync(Guid sourceId, CancellationToken ct = default) =>
        await _context.VectorChunks
            .Where(c => c.SourceId == sourceId)
            .MaxAsync(c => (int?)c.PipelineVersion, ct);
}