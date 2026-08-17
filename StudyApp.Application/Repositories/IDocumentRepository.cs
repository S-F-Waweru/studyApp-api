using StudyApp.Domain.Entities;
using StudyApp.Domain.Enums;

namespace StudyApp.Application.Repositories;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(Guid id);
    Task<IEnumerable<Document>> GetByScopeAsync(Guid scopeId, ScopeType scopeType);
    Task AddAsync(Document document);
    Task DeleteAsync(Guid id);
}
