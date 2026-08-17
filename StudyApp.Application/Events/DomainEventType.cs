namespace StudyApp.Application.Events;
// One value per (source, action) pair. Extend this as Scribble/Document/ChatSession are added —
// keeps event_type typed and exhaustive instead of loose strings that can typo/drift.
public enum DomainEventType
{
    NoteCreated,
    NoteUpdated,
    NoteDeleted,
    ScribbleCreated,
    ScribbleUpdated,
    ScribbleDeleted,
    DocumentCreated,
    DocumentUpdated,
    DocumentDeleted
}
