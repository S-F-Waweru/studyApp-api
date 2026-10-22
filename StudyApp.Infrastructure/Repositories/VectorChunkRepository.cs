using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using StudyApp.Application.Repositories;
using StudyApp.Application.Scoping;
using StudyApp.Domain.Entities;
using StudyApp.Infrastructure.Persistence;

namespace StudyApp.Infrastructure.Repositories;

public class VectorChunkRepository : IVectorChunkRepository
{
    private readonly AppDbContext _context;

    public VectorChunkRepository(AppDbContext context) => _context = context;

    public async Task AddRangeAsync(IEnumerable<VectorChunk> chunks, CancellationToken ct = default)
    {
        _context.VectorChunks.AddRange(chunks);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteBySourceIdAsync(Guid sourceId, CancellationToken ct = default)
    {
        var chunks = await _context.VectorChunks.Where(c => c.SourceId == sourceId).ToListAsync(ct);
        _context.VectorChunks.RemoveRange(chunks);
        await _context.SaveChangesAsync(ct);
    }

    public async Task ReplaceForSourceAsync(Guid sourceId, IReadOnlyList<VectorChunk> chunks, CancellationToken ct = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var existing = await _context.VectorChunks.Where(c => c.SourceId == sourceId).ToListAsync(ct);
            if (existing.Count > 0)
            {
                _context.VectorChunks.RemoveRange(existing);
                await _context.SaveChangesAsync(ct); // flush deletes before inserts (unique-ish ordering)
            }

            if (chunks.Count > 0)
            {
                _context.VectorChunks.AddRange(chunks);
                await _context.SaveChangesAsync(ct);
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct); // old chunks are still there
            throw;
        }
    }

    public async Task<IReadOnlyList<ScoredChunk>> SearchVectorAsync(float[] queryEmbedding, RetrievalFilter filter, int k, CancellationToken ct = default)
    {
        var vector = new Vector(queryEmbedding);

        // order over an anonymous projection: EF cannot translate an ORDER BY on a
        // constructor-bound record property (it inlines new ScoredChunk(...).Distance)
        var rows = await _context.VectorChunks
            .AsNoTracking()
            .Where(c =>
                (c.ScopeType == "workspace" && filter.WorkspaceIds.Contains(c.ScopeId)) ||
                (c.ScopeType == "folder"    && filter.FolderIds.Contains(c.ScopeId))    ||
                (c.SourceType == "document" && filter.DocumentIds.Contains(c.SourceId)) ||
                (c.SourceType == "note"     && filter.NoteIds.Contains(c.SourceId))     ||
                (c.SourceType == "scribble" && filter.ScribbleIds.Contains(c.SourceId)))
            .Select(c => new { Chunk = c, Distance = c.Embedding.CosineDistance(vector) })
            .OrderBy(x => x.Distance)
            .Take(k)
            .ToListAsync(ct);

        return rows.Select(r => new ScoredChunk(r.Chunk, r.Distance)).ToList();
    }

    public async Task<IReadOnlyList<ScoredChunk>> SearchKeywordAsync(string query, RetrievalFilter filter, int k, CancellationToken ct = default)
    {
        // the tsquery stays inline: hoisting it into a local makes EF treat it as a
        // parameter it evaluates client-side, where the DbFunction call cannot run
        var rows = await _context.VectorChunks
            .AsNoTracking()
            .Where(c =>
                ((c.ScopeType == "workspace" && filter.WorkspaceIds.Contains(c.ScopeId)) ||
                 (c.ScopeType == "folder"    && filter.FolderIds.Contains(c.ScopeId))    ||
                 (c.SourceType == "document" && filter.DocumentIds.Contains(c.SourceId)) ||
                 (c.SourceType == "note"     && filter.NoteIds.Contains(c.SourceId))     ||
                 (c.SourceType == "scribble" && filter.ScribbleIds.Contains(c.SourceId)))
                && EF.Functions.ToTsVector("english", c.ChunkText)
                    .Matches(EF.Functions.WebSearchToTsQuery("english", query)))
            .Select(c => new
            {
                Chunk = c,
                Distance = EF.Functions.ToTsVector("english", c.ChunkText)
                    .Rank(EF.Functions.WebSearchToTsQuery("english", query)) * -1 // rank descending -> fake distance
            })
            .OrderBy(x => x.Distance)
            .Take(k)
            .ToListAsync(ct);

        return rows.Select(r => new ScoredChunk(r.Chunk, r.Distance)).ToList();
    }

    public async Task<IReadOnlyList<VectorChunk>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.ToList();
        return await _context.VectorChunks.Where(c => idList.Contains(c.Id)).ToListAsync(ct);
    }

    public async Task<int> CountAsync(RetrievalFilter filter, CancellationToken ct = default) =>
        await _context.VectorChunks
            .Where(c =>
                (c.ScopeType == "workspace" && filter.WorkspaceIds.Contains(c.ScopeId)) ||
                (c.ScopeType == "folder"    && filter.FolderIds.Contains(c.ScopeId))    ||
                (c.SourceType == "document" && filter.DocumentIds.Contains(c.SourceId)) ||
                (c.SourceType == "note"     && filter.NoteIds.Contains(c.SourceId))     ||
                (c.SourceType == "scribble" && filter.ScribbleIds.Contains(c.SourceId)))
            .CountAsync(ct);
}
