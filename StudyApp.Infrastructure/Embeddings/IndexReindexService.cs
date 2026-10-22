using StudyApp.Application.Embeddings;
using StudyApp.Application.Events;
using StudyApp.Application.Indexing;
using StudyApp.Application.Repositories;

namespace StudyApp.Infrastructure.Embeddings;

public class IndexReindexService : IIndexReindexService
{
    private readonly ISourceIndexStatusRepository _status;
    private readonly IEventPublisher _events;
    private readonly IDocumentRepository _documents;
    private readonly IScribbleRepository _scribbles;
    private readonly INoteRepository _notes;

    public IndexReindexService(ISourceIndexStatusRepository status, IEventPublisher events,
        IDocumentRepository documents, IScribbleRepository scribbles, INoteRepository notes)
    {
        _status = status;
        _events = events;
        _documents = documents;
        _scribbles = scribbles;
        _notes = notes;

    }





    public async Task<int> ReindexSourceAsync(string sourceType, Guid sourceId, CancellationToken ct = default) =>
        await PublishAsync(sourceType, sourceId, ct) ? 1 : 0;

    public async Task<int> ReindexStaleAsync(int take = 200, CancellationToken ct = default)
    {
        var stale = await _status.GetStaleSourcesAsync(ExtractionPipeline.Version, take, ct);

        var queued = 0;
        foreach (var (sourceType, sourceId) in stale)
            if (await PublishAsync(sourceType, sourceId, ct)) queued++;

        return queued;
    }



    private async Task<bool> PublishAsync(string sourceType, Guid sourceId, CancellationToken ct)
    {
        switch (sourceType.ToLowerInvariant())
        {
            case "document":
                var doc = await _documents.GetByIdAsync(sourceId);
                if (doc is null) return false;
                await _events.PublishAsync(new DomainEvent(DomainEventType.DocumentUpdated, doc.Id, SourceType.Document,
                    doc.ScopeId, doc.ScopeType.ToString().ToLowerInvariant(), doc.StoragePath, DateTime.UtcNow));
                return true;

            case "scribble":
                var scribble = await _scribbles.GetByIdAsync(sourceId);
                if (scribble is null) return false;
                await _events.PublishAsync(new DomainEvent(DomainEventType.ScribbleUpdated, scribble.Id,
                    SourceType.Scribble,
                    scribble.ScopeId, scribble.ScopeType.ToString().ToLowerInvariant(), scribble.Id.ToString(),
                    DateTime.UtcNow));
                return true;

            case "note":
                var note = await _notes.GetByIdAsync(sourceId);
                if (note is null) return false;
                await _events.PublishAsync(new DomainEvent(DomainEventType.NoteUpdated, note.Id, SourceType.Note,
                    note.ScopeId, note.ScopeType.ToString().ToLowerInvariant(), note.Id.ToString(), DateTime.UtcNow));
                return true;

            default:
                return false;
        }
    }
}