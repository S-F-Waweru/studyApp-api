using StudyApp.Domain.Entities;

namespace StudyApp.Application.Repositories;

public interface IFolderRepository
{
    Task<Folder?> GetByIdAsync(Guid id);
    Task<IEnumerable<Folder>> GetChildrenAsync(Guid? parentFolderId, Guid workspaceId);
    Task<IEnumerable<Guid>> GetDescendantIdsAsync(Guid folderId); // includes self
    Task<IEnumerable<Guid>> GetAllFolderIdsInWorkspaceAsync(Guid workspaceId);
    Task AddAsync(Folder folder);
    Task UpdateAsync(Folder folder);
    Task DeleteAsync(Guid id);
}
