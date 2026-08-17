using StudyApp.Domain.Entities;
using StudyApp.Domain.Enums;

namespace StudyApp.Application.Repositories;

public interface IScribbleRepository
{
    Task<Scribble?> GetByIdAsync(Guid id);
    Task<IEnumerable<Scribble>> GetByScopeAsync(Guid scopeId, ScopeType scopeType);
    Task<IEnumerable<Scribble>> GetByScopesAsync(IEnumerable<Guid> scopeIds);
    Task AddAsync(Scribble scribble);
    Task UpdateAsync(Scribble scribble);
    Task DeleteAsync(Guid id);
}
