using StudyApp.Domain.Enums;

namespace StudyApp.Application.Documents;

public record DocumentDto(Guid Id, Guid ScopeId, ScopeType ScopeType, string Filename, DateTime CreatedAt,
                          string? ContentType, IndexStatus IndexStatus, int ChunkCount, int? PageCount,
                          string? ExtractionError, DateTime? IndexedAt);
