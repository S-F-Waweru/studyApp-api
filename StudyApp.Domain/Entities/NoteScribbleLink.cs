namespace StudyApp.Domain.Entities;

// Sole coupling point between Note and Scribble — remove this table,
// both entities keep working standalone (architecture doc §5).
public class NoteScribbleLink
{
    public Guid NoteId { get; set; }
    public Guid ScribbleId { get; set; }
}
