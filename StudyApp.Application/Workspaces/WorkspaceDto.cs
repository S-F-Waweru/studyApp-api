namespace StudyApp.Application.Workspaces;

public record WorkSpaceDto(Guid Id, string Name, string? Description, string? CustomInstructions, DateTime CreatedAt);
public record CreateWorspaceRequest(string Name, string? Description = null, string? CustomInstructions = null);
public record UpdateWorkspaceRequest(string Name, string? Description = null, string? CustomInstructions = null);
