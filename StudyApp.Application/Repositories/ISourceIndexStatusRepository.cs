namespace StudyApp.Application.Repositories;

public interface ISourceIndexStatusRepository
{
    Task MarkAsync(string sourceType, Guid sourceId, string status, int chunkCount = 0,
                   string? error = null, string? contentHash = null, int? pageCount = null,
                   CancellationToken ct = default);

    // Sources whose chunks were produced by an older pipeline (for the reindex job).
    Task<IReadOnlyList<(string SourceType, Guid SourceId)>> GetStaleSourcesAsync(int pipelineVersion, int take, CancellationToken ct = default);
    
    // ISourceIndexStatusRepository additions
    Task<string?> GetContentHashAsync(string sourceType, Guid sourceId, CancellationToken ct = default);
    Task<int?> GetIndexedPipelineVersionAsync(Guid sourceId, CancellationToken ct = default);
}