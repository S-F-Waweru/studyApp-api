using StudyApp.Application.Chat;
using StudyApp.Application.Embeddings;
using StudyApp.Application.Repositories;
using StudyApp.Application.Scoping;

namespace StudyApp.Application.Retrieval;

public class RetrievalService : IRetrievalService
{
    private readonly IVectorChunkRepository _chunks;
    private readonly RetrievalOptions _options;

    public RetrievalService(IVectorChunkRepository chunks, RetrievalOptions options)
    {
        _chunks = chunks; _options = options;
    }

    public async Task<RetrievalResult> RetrieveAsync(string query, float[] queryEmbedding,
        RetrievalFilter filter, CancellationToken ct = default)
    {
        // relevance floor first: RRF gives every candidate a rank, so a bad nearest
        // neighbour would otherwise be promoted just for showing up in the list
        var vectorHits = (await _chunks.SearchVectorAsync(queryEmbedding, filter, _options.VectorCandidateCount, ct))
            .Where(hit => hit.Score >= _options.MinScore)
            .ToList();

        var lists = new List<IReadOnlyList<ScoredChunk>> { vectorHits };

        if (_options.HybridEnabled && !string.IsNullOrWhiteSpace(query))
        {
            var keywordHits = await _chunks.SearchKeywordAsync(query, filter, _options.KeywordCandidateCount, ct);
            if (keywordHits.Count > 0) lists.Add(keywordHits);
        }

        // 1. fuse by Reciprocal Rank Fusion — cancels out incomparable score scales
        var fused = lists
            .SelectMany(list => list.Select((hit, rank) => new { hit.Chunk.Id, hit.Chunk, Rank = rank + 1 }))
            .GroupBy(x => x.Id)
            .Select(g => new
            {
                Chunk = g.First().Chunk,
                Score = g.Sum(x => 1.0 / (_options.RrfK + x.Rank))
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        // 2. drop weak matches (RRF scores are small; require at least one list to rank it well)
        var threshold = 1.0 / (_options.RrfK + _options.FinalCount);
        var relevant = fused.Where(x => x.Score >= threshold).ToList();

        // 3. diversity caps
        var perSource = new Dictionary<Guid, int>();
        var selected = new List<(Domain.Entities.VectorChunk Chunk, double Score)>();

        foreach (var candidate in relevant)
        {
            if (selected.Count >= _options.FinalCount) break;

            var sourceId = candidate.Chunk.SourceId;
            perSource.TryGetValue(sourceId, out var used);
            if (used >= _options.MaxPerSource) continue;
            if (perSource.Count >= _options.MaxSources && !perSource.ContainsKey(sourceId)) continue;

            perSource[sourceId] = used + 1;
            selected.Add((candidate.Chunk, candidate.Score));
        }

        // 4. token budget
        var budgeted = new List<(Domain.Entities.VectorChunk Chunk, double Score)>();
        var tokens = 0;
        foreach (var item in selected)
        {
            if (tokens + item.Chunk.TokenCount > _options.MaxContextTokens && budgeted.Count > 0) break;
            tokens += item.Chunk.TokenCount;
            budgeted.Add(item);
        }

        var sources = budgeted.Select((item, i) => new RetrievedSource(
            Label: $"S{i + 1}",
            ChunkId: item.Chunk.Id,
            SourceId: item.Chunk.SourceId,
            SourceType: item.Chunk.SourceType,
            Title: item.Chunk.SourceTitle,
            Locator: item.Chunk.Locator,
            HeadingPath: item.Chunk.HeadingPath,
            Score: item.Score)).ToList();

        return new RetrievalResult(budgeted.Select(b => b.Chunk).ToList(), sources);
    }
}