using StudyApp.Domain.Enums;

namespace StudyApp.Application.Notes;

public interface INoteService
{
    Task<NoteDto> CreateAsync(CreateNoteRequest request);
    Task<NoteDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<NoteDto>> GetByScopeAsync(Guid scopeId, ScopeType scopeType);
    Task<bool> UpdateAsync(Guid id, UpdateNoteRequest request);
    Task<bool> DeleteAsync(Guid id);
}
