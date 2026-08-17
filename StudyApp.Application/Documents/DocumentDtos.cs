using StudyApp.Domain.Enums;

namespace StudyApp.Application.Documents;

public record DocumentDto(Guid Id, Guid ScopeId, ScopeType ScopeType, string Filename, DateTime CreatedAt);
