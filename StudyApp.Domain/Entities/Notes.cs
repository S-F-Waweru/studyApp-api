using StudyApp.Domain.Enums;

namespace StudyApp.Domain.Entities;

public class Note {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScopeId { get; set; }
    public ScopeType ScopeType { get; set; }
    public string Title { get; set; } = default!;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

}
