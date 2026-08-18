using StudyApp.Application.Repositories;
using StudyApp.Domain.Enums;

namespace StudyApp.Application.Scoping;

public class ScopeResolver : IScopeResolver
{
    private readonly IFolderRepository _folders;

    public ScopeResolver(IFolderRepository folders) => _folders = folders;

    public async Task<List<Guid>> ResolveScopeIdsAsync(Guid scopeId, ScopeType scopeType)
    {
        if (scopeType == ScopeType.Folder)
        {
            var descendants = await _folders.GetDescendantIdsAsync(scopeId); // already includes self
            return descendants.ToList();
        }

        // Workspace: itself + every folder anywhere underneath it
        var folderIds = await _folders.GetAllFolderIdsInWorkspaceAsync(scopeId);
        var ids = folderIds.ToList();
        ids.Add(scopeId);
        return ids;
    }
}
