namespace StudyApp.Application.Indexing;

public interface IIndexReindexService
{
    Task<int> ReindexSourceAsync(string sourceType, Guid sourceId, CancellationToken ct = default);
    Task<int> ReindexStaleAsync(int take = 200, CancellationToken ct = default);
}