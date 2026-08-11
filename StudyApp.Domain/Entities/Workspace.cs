namespace StudyApp.Domain.Entities;

public class Workspace
{
    public Guid Id {get; set;} = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
