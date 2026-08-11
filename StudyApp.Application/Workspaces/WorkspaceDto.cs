namespace StudyApp.Application.Workspaces;

public record WorkSpaceDto(Guid Id, string Name, DateTime CreatedAt);
public record CreateWorspaceRequest(string Name);
public record UpdateWorkspaceRequest(string Name);
