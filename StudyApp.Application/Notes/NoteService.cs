using StudyApp.Application.Events;
using StudyApp.Application.Notes;
using StudyApp.Application.Repositories;
using StudyApp.Domain.Entities;
using StudyApp.Domain.Enums;

namespace StudyApp.Application.Notes;

public class NoteService : INoteService
{
    private readonly INoteRepository _repository;
    private readonly IEventPublisher _events;


    public NoteService(INoteRepository repository, IEventPublisher events)
    {
        _repository = repository;
        _events = events;
    }

    public async Task<NoteDto> CreateAsync(CreateNoteRequest request)
    {
        var note = new Note
        {
            ScopeId = request.ScopeId,
            ScopeType = request.ScopeType,
            Title = request.Title,
            Content = request.Content
        };
        Console.WriteLine($"----------------------------------------->SERVICE<----------------------------------------");
        Console.WriteLine(note);
        Console.WriteLine($"-----------------------------------------><----------------------------------------");


        await _repository.AddAsync(note);
        await PublishAsync(note, DomainEventType.NoteCreated);
        return ToDto(note);
    }

    public async Task<bool> DeleteAsync(Guid id)
      {
          var note = await _repository.GetByIdAsync(id);
          if (note is null) return false;

          await _repository.DeleteAsync(id);
          await PublishAsync(note, DomainEventType.NoteDeleted);
          return true;
      }


    public async Task<NoteDto?> GetByIdAsync(Guid id)
     {
         var note = await _repository.GetByIdAsync(id);
         return note is null ? null : ToDto(note);
     }

     public async Task<IEnumerable<NoteDto>> GetByScopeAsync(Guid scopeId, ScopeType scopeType)
         {
             var notes = await _repository.GetByScopeAsync(scopeId, scopeType);
             return notes.Select(ToDto);
         }

         public async Task<bool> UpdateAsync(Guid id, UpdateNoteRequest request)
           {
               var note = await _repository.GetByIdAsync(id);
               if (note is null) return false;

               note.Title = request.Title;
               note.Content = request.Content;
               note.UpdatedAt = DateTime.UtcNow;
               await _repository.UpdateAsync(note);
               await PublishAsync(note, DomainEventType.NoteUpdated);
               return true;
           }

    private Task PublishAsync(Note note, DomainEventType eventType) =>
        _events.PublishAsync(new DomainEvent(
            EventType: eventType,
                      SourceId: note.Id,
                      SourceType: SourceType.Note,
                      ScopeId: note.ScopeId,
                      ScopeType: note.ScopeType.ToString().ToLowerInvariant(),
                      ContentRef: note.Id.ToString(), // Note content lives in the row itself — id is enough for the worker to fetch it
                      Timestamp: DateTime.UtcNow

        ));

        private static NoteDto ToDto(Note n) => new(n.Id, n.ScopeId, n.ScopeType, n.Title, n.Content, n.CreatedAt, n.UpdatedAt);

}
