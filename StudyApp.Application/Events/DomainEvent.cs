namespace StudyApp.Application.Events;

// Matches the event contract from the architecture doc §6:
// event_type, source_id, source_type, scope_id, scope_type, content_ref, timestamp
public record DomainEvent(
    DomainEventType EventType,
    Guid SourceId,
    SourceType SourceType,
    Guid ScopeId,
    string ScopeType,       // "workspace" | "folder" (kept as string — mirrors Note.ScopeType.ToString())
    string? ContentRef,     // pointer to content — for Note, just the source_id is enough (content lives in the row)
    DateTime Timestamp
);
