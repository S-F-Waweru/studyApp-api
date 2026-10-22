using StudyApp.Domain.Enums;

namespace StudyApp.Application.Chat;

public record ChatSessionDto(Guid Id, Guid ScopeId, ScopeType ScopeType, string Title, DateTime CreatedAt,
                             List<ChatScopeTargetDto>? Targets = null);

public record ChatScopeTargetDto(ChatScopeType TargetType, Guid TargetId, bool IncludeLinked, bool IncludeAncestors);

public record CreateChatSessionRequest(
    string Title,
    List<ChatScopeTargetDto>? Targets = null,
    Guid? ScopeId = null,          // legacy single-scope callers
    ScopeType? ScopeType = null);

public record ChatMessageDto(Guid Id, string Role, string Content, List<Guid> RetrievedChunkIds,
                             DateTime CreatedAt, List<RetrievedSource>? Sources = null);

public record SendMessageRequest(string Content);

public record ChatScopeSummaryDto(Guid SessionId, int DeclaredTargets, int ResolvedChunks,
                                  List<ChatScopeTargetDto> Targets);
