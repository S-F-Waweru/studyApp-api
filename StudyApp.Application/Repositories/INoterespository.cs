
using StudyApp.Domain.Entities;
using StudyApp.Domain.Enums;

namespace StudyApp.Application.Repositories;

public interface INoteRepository
{
    Task<Note?> GetByIdAsync(Guid id);
    Task<IEnumerable<Note>> GetByScopeAsync(Guid scopeId, ScopeType scopeType);
    Task<IEnumerable<Note>> GetByScopesAsync(IEnumerable<Guid> scopeIds); // used later for parent-level aggregation
    Task AddAsync(Note note);
    Task UpdateAsync(Note note);
    Task DeleteAsync(Guid id);
}
