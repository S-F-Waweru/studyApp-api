using StudyApp.Application.Events;
using StudyApp.Application.Repositories;
using StudyApp.Application.Storage;
using StudyApp.Domain.Entities;
using StudyApp.Domain.Enums;

namespace StudyApp.Application.Documents;

public class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _repository;
    private readonly IFileStorage _storage;
    private readonly IEventPublisher _events;

    public DocumentService(IDocumentRepository repository, IFileStorage storage, IEventPublisher events)
    {
        _repository = repository;
        _storage = storage;
        _events = events;
    }

    public async Task<DocumentDto> UploadAsync(Guid scopeId, ScopeType scopeType, string filename, Stream content)
    {
        var id = Guid.NewGuid();
        var storagePath = await _storage.SaveAsync("documents", $"{id}_{filename}", content);

        var document = new Document
        {
            Id = id,
            ScopeId = scopeId,
            ScopeType = scopeType,
            Filename = filename,
            StoragePath = storagePath
        };
        await _repository.AddAsync(document);

        await _events.PublishAsync(new DomainEvent(
            EventType: DomainEventType.DocumentCreated,
            SourceId: document.Id,
            SourceType: SourceType.Document,
            ScopeId: document.ScopeId,
            ScopeType: document.ScopeType.ToString().ToLowerInvariant(),
            ContentRef: document.StoragePath, // Document content is a file — worker needs the path, not just the id
            Timestamp: DateTime.UtcNow
        ));

        return ToDto(document);
    }

    public async Task<IEnumerable<DocumentDto>> GetByScopeAsync(Guid scopeId, ScopeType scopeType)
    {
        var docs = await _repository.GetByScopeAsync(scopeId, scopeType);
        return docs.Select(ToDto);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var document = await _repository.GetByIdAsync(id);
        if (document is null) return false;

        await _storage.DeleteAsync(document.StoragePath);
        await _repository.DeleteAsync(id);

        await _events.PublishAsync(new DomainEvent(
            EventType: DomainEventType.DocumentDeleted,
            SourceId: document.Id,
            SourceType: SourceType.Document,
            ScopeId: document.ScopeId,
            ScopeType: document.ScopeType.ToString().ToLowerInvariant(),
            ContentRef: null,
            Timestamp: DateTime.UtcNow
        ));
        return true;
    }

    public async Task<(Stream, string, string)?> GetContentAsync(Guid id)
    {
        var document = await _repository.GetByIdAsync(id);
        if (document is null) return null;

        var stream = await _storage.OpenReadAsync(document.StoragePath);
        var contentType = document.Filename.ToLowerInvariant() switch
        {
            var f when f.EndsWith(".pdf") => "application/pdf",
            var f when f.EndsWith(".png") => "image/png",
            var f when f.EndsWith(".jpg") || f.EndsWith(".jpeg") => "image/jpeg",
            var f when f.EndsWith(".txt") || f.EndsWith(".md") => "text/plain",
            var f when f.EndsWith(".docx") => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            var f when f.EndsWith(".xlsx") => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };
        return (stream, contentType, document.Filename);
    }

    private static DocumentDto ToDto(Document d) => new(d.Id, d.ScopeId, d.ScopeType, d.Filename, d.CreatedAt);
}
