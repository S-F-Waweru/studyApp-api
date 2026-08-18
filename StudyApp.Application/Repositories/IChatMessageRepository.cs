using StudyApp.Domain.Entities;

namespace StudyApp.Application.Repositories;

public interface IChatMessageRepository
{
    Task<List<ChatMessage>> GetRecentAsync(Guid chatSessionId, int count);
    Task AddAsync(ChatMessage message);
}
