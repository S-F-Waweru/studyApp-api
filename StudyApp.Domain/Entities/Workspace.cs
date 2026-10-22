namespace StudyApp.Domain.Entities;

public class Workspace
{
    public Guid Id {get; set;} = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string? CustomInstructions { get; set; } // pinned first context for every chat in this workspace
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? OwnerUserId { get; set; }          // Phase 7 — null until auth lands
}
