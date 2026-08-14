namespace StudyApp.Application.Folders;

public interface IFolderService
{
    Task<FolderDto> CreateAsync(CreateFolderRequest request);
    Task<FolderDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<FolderDto>> GetChildrenAsync(Guid? parentFolderId, Guid workspaceId);
    Task<IEnumerable<Guid>> GetDescendantScopeIdsAsync(Guid folderId);
    Task<bool> UpdateAsync(Guid id, UpdateFolderRequest request);
    Task<bool> DeleteAsync(Guid id);
}
