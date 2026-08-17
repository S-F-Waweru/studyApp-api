using StudyApp.Domain.Enums;

namespace StudyApp.Domain.Entities;

public class Scribble
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScopeId { get; set; }
    public ScopeType ScopeType { get; set; }
    public string Title { get; set; } = default!;
    public string CanvasData { get; set; } = "{}"; // raw Excalidraw scene JSON, stored as-is
    public string? ExtractedText { get; set; }       // filled in later by the extraction worker (Step 6)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
