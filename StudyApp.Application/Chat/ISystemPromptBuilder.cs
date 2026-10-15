using StudyApp.Domain.Entities;
using StudyApp.Domain.Enums;

namespace StudyApp.Application.Chat;

public interface ISystemPromptBuilder
{
    Task<string> BuildAsync(Guid scopeId, ScopeType scopeType, IEnumerable<VectorChunk> chunks);
}
