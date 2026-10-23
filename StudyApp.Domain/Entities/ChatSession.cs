using StudyApp.Domain.Enums;

namespace StudyApp.Domain.Entities;

public class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? OwnerUserId { get; set; }   // was CreatedByUserId
    public Guid ScopeId { get; set; }               // owning location, for listing
    public ScopeType ScopeType { get; set; }
    public string Title { get; set; } = "New chat";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public List<ChatSessionScope> Targets { get; set; } = new(); // Phase 3
}
