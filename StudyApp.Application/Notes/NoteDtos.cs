using StudyApp.Domain.Enums;

namespace StudyApp.Application.Notes;

public record NoteDto(Guid Id, Guid ScopeId, ScopeType ScopeType, string Title, string Content, DateTime CreatedAt, DateTime? UpdatedAt);
public record CreateNoteRequest(Guid ScopeId, ScopeType ScopeType, string Title, string Content);
public record UpdateNoteRequest(string Title, string Content);
