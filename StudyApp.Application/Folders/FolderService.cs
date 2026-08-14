using Microsoft.VisualBasic;
using StudyApp.Application.Folders;
using StudyApp.Application.Repositories;
using StudyApp.Domain.Entities;

namespace StudyApp.Application.Folders;

public class FolderService : IFolderService
{
    private readonly IFolderRepository _repository;

    public FolderService(IFolderRepository repository) {
        _repository = repository;
    }
    public async Task<FolderDto> CreateAsync(CreateFolderRequest request)
    {
        var folder = new Folder
        {
            WorkspaceId = request.WorkspaceId,
            ParentFolderId = request.ParentFolderId,
            Name = request.Name
        };

       await _repository.AddAsync(folder);
        return ToDto(folder);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var folder = GetByIdAsync(id);
        if (folder is null) return false;

        await _repository.DeleteAsync(id);
        return true;
    }

    public async Task<FolderDto?> GetByIdAsync(Guid id)
    {
        var folder = await _repository.GetByIdAsync(id);
        if (folder is null) return null;

        return ToDto(folder);

    }

    public async Task<IEnumerable<FolderDto>> GetChildrenAsync(Guid? parentFolderId, Guid workspaceId)
    {
        var folders = await _repository.GetChildrenAsync(parentFolderId, workspaceId);
        return folders.Select(ToDto);

    }

    // This is what Chat/summarization at this folder level will call
    // to know which scope_ids to include when aggregating downward.
    public Task<IEnumerable<Guid>> GetDescendantScopeIdsAsync(Guid folderId)
    {
        return _repository.GetDescendantIdsAsync(folderId);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateFolderRequest request)
    {
        var folder = await _repository.GetByIdAsync(id);
        if (folder is null) return false;

        folder.Name = request.Name;

        await _repository.UpdateAsync(folder);
        return true;
    }

    private static FolderDto ToDto(Folder f) => new(f.Id, f.WorkspaceId, f.ParentFolderId, f.Name, f.CreatedAt);

}
