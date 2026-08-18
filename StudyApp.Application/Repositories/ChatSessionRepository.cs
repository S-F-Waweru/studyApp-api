using StudyApp.Domain.Entities;
using StudyApp.Domain.Enums;

namespace StudyApp.Application.Repositories;

public interface IChatSessionRepository
{
    Task<ChatSession?> GetByIdAsync(Guid id);
    Task<IEnumerable<ChatSession>> GetByScopeAsync(Guid scopeId, ScopeType scopeType);
    Task AddAsync(ChatSession session);
}
