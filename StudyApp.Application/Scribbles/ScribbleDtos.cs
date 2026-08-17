using StudyApp.Domain.Enums;

namespace StudyApp.Application.Scribbles;

public record ScribbleDto(Guid Id, Guid ScopeId, ScopeType ScopeType, string Title, string CanvasData, string? ExtractedText, DateTime CreatedAt, DateTime? UpdatedAt);
public record CreateScribbleRequest(Guid ScopeId, ScopeType ScopeType, string Title, string CanvasData);
public record UpdateScribbleRequest(string Title, string CanvasData);
