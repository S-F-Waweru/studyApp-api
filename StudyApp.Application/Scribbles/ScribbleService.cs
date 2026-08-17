using StudyApp.Application.Events;
using StudyApp.Application.Repositories;
using StudyApp.Domain.Entities;
using StudyApp.Domain.Enums;

namespace StudyApp.Application.Scribbles;

public class ScribbleService : IScribbleService
{
    private readonly IScribbleRepository _repository;
    private readonly INoteScribbleLinkRepository _links;
    private readonly IEventPublisher _events;

    public ScribbleService(IScribbleRepository repository, INoteScribbleLinkRepository links, IEventPublisher events)
    {
        _repository = repository;
        _links = links;
        _events = events;
    }

    public async Task<ScribbleDto> CreateAsync(CreateScribbleRequest request)
    {
        var scribble = new Scribble
        {
            ScopeId = request.ScopeId,
            ScopeType = request.ScopeType,
            Title = request.Title,
            CanvasData = request.CanvasData
        };
        await _repository.AddAsync(scribble);
        await PublishAsync(scribble, DomainEventType.ScribbleCreated);
        return ToDto(scribble);
    }

    public async Task<ScribbleDto?> GetByIdAsync(Guid id)
    {
        var scribble = await _repository.GetByIdAsync(id);
        return scribble is null ? null : ToDto(scribble);
    }

    public async Task<IEnumerable<ScribbleDto>> GetByScopeAsync(Guid scopeId, ScopeType scopeType)
    {
        var scribbles = await _repository.GetByScopeAsync(scopeId, scopeType);
        return scribbles.Select(ToDto);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateScribbleRequest request)
    {
        var scribble = await _repository.GetByIdAsync(id);
        if (scribble is null) return false;

        scribble.Title = request.Title;
        scribble.CanvasData = request.CanvasData;
        scribble.ExtractedText = null; // canvas changed — stale extraction, re-run pipeline
        scribble.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(scribble);
        await PublishAsync(scribble, DomainEventType.ScribbleUpdated);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var scribble = await _repository.GetByIdAsync(id);
        if (scribble is null) return false;

        await _repository.DeleteAsync(id);
        await PublishAsync(scribble, DomainEventType.ScribbleDeleted);
        return true;
    }

    public Task LinkToNoteAsync(Guid scribbleId, Guid noteId) => _links.LinkAsync(noteId, scribbleId);

    public Task UnlinkFromNoteAsync(Guid scribbleId, Guid noteId) => _links.UnlinkAsync(noteId, scribbleId);

    private Task PublishAsync(Scribble scribble, DomainEventType eventType) =>
        _events.PublishAsync(new DomainEvent(
            EventType: eventType,
            SourceId: scribble.Id,
            SourceType: SourceType.Scribble,
            ScopeId: scribble.ScopeId,
            ScopeType: scribble.ScopeType.ToString().ToLowerInvariant(),
            ContentRef: scribble.Id.ToString(),
            Timestamp: DateTime.UtcNow
        ));

    private static ScribbleDto ToDto(Scribble s) =>
        new(s.Id, s.ScopeId, s.ScopeType, s.Title, s.CanvasData, s.ExtractedText, s.CreatedAt, s.UpdatedAt);
}
