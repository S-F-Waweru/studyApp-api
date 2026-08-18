using StudyApp.Domain.Enums;

namespace StudyApp.Domain.Entities;

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChatSessionId { get; set; }
    public ChatRole Role { get; set; }
    public string Content { get; set; } = default!;
    public List<Guid> RetrievedChunkIds { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
