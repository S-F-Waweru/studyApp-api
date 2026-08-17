using StudyApp.Domain.Enums;

namespace StudyApp.Domain.Entities;

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScopeId { get; set; }
    public ScopeType ScopeType { get; set; }
    public string Filename { get; set; } = default!;
    public string StoragePath { get; set; } = default!; // relative path under /files/documents
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
