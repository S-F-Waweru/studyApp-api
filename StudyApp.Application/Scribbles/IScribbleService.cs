using StudyApp.Domain.Enums;

namespace StudyApp.Application.Scribbles;

public interface IScribbleService
{
    Task<ScribbleDto> CreateAsync(CreateScribbleRequest request);
    Task<ScribbleDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<ScribbleDto>> GetByScopeAsync(Guid scopeId, ScopeType scopeType);
    Task<bool> UpdateAsync(Guid id, UpdateScribbleRequest request);
    Task<bool> DeleteAsync(Guid id);
    Task LinkToNoteAsync(Guid scribbleId, Guid noteId);
    Task UnlinkFromNoteAsync(Guid scribbleId, Guid noteId);
}
