namespace StudyApp.Application.Folders;

public record FolderDto(Guid Id, Guid WorkspaceId, Guid? ParentFolderId, string Name, DateTime CreatedAt);
public record CreateFolderRequest(Guid WorkspaceId, Guid? ParentFolderId, string Name);
public record UpdateFolderRequest(string Name);
