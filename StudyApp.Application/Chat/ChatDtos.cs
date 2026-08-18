using StudyApp.Domain.Enums;

namespace StudyApp.Application.Chat;

public record ChatSessionDto(Guid Id, Guid ScopeId, ScopeType ScopeType, string Title, DateTime CreatedAt);
public record CreateChatSessionRequest(Guid ScopeId, ScopeType ScopeType, string Title);
public record ChatMessageDto(Guid Id, string Role, string Content, List<Guid> RetrievedChunkIds, DateTime CreatedAt);
public record SendMessageRequest(string Content);
