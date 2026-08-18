using StudyApp.Domain.Enums;

namespace StudyApp.Domain.Entities;

public class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScopeId { get; set; }
    public ScopeType ScopeType { get; set; }
    public string Title { get; set; } = "New chat";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
