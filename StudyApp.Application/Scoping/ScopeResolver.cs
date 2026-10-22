using StudyApp.Application.Repositories;
using StudyApp.Domain.Enums;

namespace StudyApp.Application.Scoping;

public class ScopeResolver : IScopeResolver
{
    private const int MaxFolderWalk = 25;

    private readonly IFolderRepository _folders;
    private readonly IDocumentRepository _documents;
    private readonly INoteRepository _notes;
    private readonly IScribbleRepository _scribbles;
    private readonly INoteScribbleLinkRepository _links;

    public ScopeResolver(IFolderRepository folders, IDocumentRepository documents, INoteRepository notes,
        IScribbleRepository scribbles, INoteScribbleLinkRepository links)
    {
        _folders = folders; _documents = documents;
        _notes = notes; _scribbles = scribbles; _links = links;
    }

    public async Task<RetrievalFilter> ResolveAsync(IReadOnlyList<ScopeTarget> targets, CancellationToken ct = default)
    {
        var workspaceIds = new HashSet<Guid>();
        var folderIds = new HashSet<Guid>();
        var documentIds = new HashSet<Guid>();
        var noteIds = new HashSet<Guid>();
        var scribbleIds = new HashSet<Guid>();

        foreach (var target in targets)
        {
            switch (target.TargetType)
            {
                case ChatScopeType.Workspace:
                    workspaceIds.Add(target.TargetId);
                    foreach (var id in await _folders.GetAllFolderIdsInWorkspaceAsync(target.TargetId))
                        folderIds.Add(id);
                    break;

                case ChatScopeType.Folder:
                    foreach (var id in await _folders.GetDescendantIdsAsync(target.TargetId)) // includes self
                        folderIds.Add(id);
                    if (target.IncludeAncestors)
                        await AddFolderAncestorsAsync(target.TargetId, workspaceIds, folderIds);
                    break;

                case ChatScopeType.Document:
                    documentIds.Add(target.TargetId);
                    if (target.IncludeAncestors)
                        await AddOwningScopeAsync(await OwningScopeOfDocumentAsync(target.TargetId), workspaceIds, folderIds);
                    break;

                case ChatScopeType.Note:
                    noteIds.Add(target.TargetId);
                    if (target.IncludeLinked)
                        foreach (var id in await _links.GetLinkedScribbleIdsAsync(target.TargetId))
                            scribbleIds.Add(id);
                    if (target.IncludeAncestors)
                        await AddOwningScopeAsync(await OwningScopeOfNoteAsync(target.TargetId), workspaceIds, folderIds);
                    break;

                case ChatScopeType.Scribble:
                    scribbleIds.Add(target.TargetId);
                    if (target.IncludeLinked)
                        foreach (var id in await _links.GetLinkedNoteIdsAsync(target.TargetId))
                            noteIds.Add(id);
                    if (target.IncludeAncestors)
                        await AddOwningScopeAsync(await OwningScopeOfScribbleAsync(target.TargetId), workspaceIds, folderIds);
                    break;
            }
        }

        return new RetrievalFilter
        {
            WorkspaceIds = workspaceIds.ToList(),
            FolderIds = folderIds.ToList(),
            DocumentIds = documentIds.ToList(),
            NoteIds = noteIds.ToList(),
            ScribbleIds = scribbleIds.ToList()
        };
    }

    public async Task<Guid?> ResolveOwningWorkspaceAsync(Guid scopeId, ScopeType scopeType, CancellationToken ct = default)
    {
        if (scopeType == ScopeType.Workspace) return scopeId;

        var folder = await _folders.GetByIdAsync(scopeId);
        for (var hops = 0; folder?.ParentFolderId is not null && hops < MaxFolderWalk; hops++)
            folder = await _folders.GetByIdAsync(folder.ParentFolderId.Value);

        return folder?.WorkspaceId;
    }

    public async Task<(Guid ScopeId, ScopeType ScopeType)> ResolveOwningLocationAsync(
        ChatScopeType targetType, Guid targetId, CancellationToken ct = default)
    {
        var owning = targetType switch
        {
            ChatScopeType.Document => await OwningScopeOfDocumentAsync(targetId),
            ChatScopeType.Note => await OwningScopeOfNoteAsync(targetId),
            ChatScopeType.Scribble => await OwningScopeOfScribbleAsync(targetId),
            ChatScopeType.Folder => (targetId, ScopeType.Folder),
            ChatScopeType.Workspace => (targetId, ScopeType.Workspace),
            _ => null
        };

        return owning ?? throw new InvalidOperationException(
            $"Cannot resolve an owning location for chat target {targetType}/{targetId}.");
    }

    public async Task ValidateTargetsAsync(IReadOnlyList<ScopeTarget> targets, CancellationToken ct = default)
    {
        foreach (var target in targets)
        {
            var exists = target.TargetType switch
            {
                ChatScopeType.Document => await _documents.GetByIdAsync(target.TargetId) is not null,
                ChatScopeType.Note => await _notes.GetByIdAsync(target.TargetId) is not null,
                ChatScopeType.Scribble => await _scribbles.GetByIdAsync(target.TargetId) is not null,
                ChatScopeType.Folder => await _folders.GetByIdAsync(target.TargetId) is not null,
                ChatScopeType.Workspace => true, // workspace existence is checked by the caller (Phase 7 adds a user check)
                _ => false
            };

            if (!exists)
                throw new InvalidOperationException($"Chat scope target {target.TargetType}/{target.TargetId} no longer exists.");
        }
    }

    private async Task AddOwningScopeAsync((Guid ScopeId, ScopeType ScopeType)? owning,
        HashSet<Guid> workspaceIds, HashSet<Guid> folderIds)
    {
        if (owning is null) return;
        var (scopeId, scopeType) = owning.Value;

        if (scopeType == ScopeType.Workspace) { workspaceIds.Add(scopeId); return; }
        foreach (var id in await _folders.GetDescendantIdsAsync(scopeId)) folderIds.Add(id);
    }

    private async Task AddFolderAncestorsAsync(Guid folderId, HashSet<Guid> workspaceIds, HashSet<Guid> folderIds)
    {
        foreach (var id in await _folders.GetAncestorIdsAsync(folderId)) folderIds.Add(id);
        var workspaceId = await ResolveOwningWorkspaceAsync(folderId, ScopeType.Folder);
        if (workspaceId is not null) workspaceIds.Add(workspaceId.Value);
    }

    private async Task<(Guid, ScopeType)?> OwningScopeOfDocumentAsync(Guid id) =>
        await _documents.GetByIdAsync(id) is { } d ? (d.ScopeId, d.ScopeType) : null;

    private async Task<(Guid, ScopeType)?> OwningScopeOfNoteAsync(Guid id) =>
        await _notes.GetByIdAsync(id) is { } n ? (n.ScopeId, n.ScopeType) : null;

    private async Task<(Guid, ScopeType)?> OwningScopeOfScribbleAsync(Guid id) =>
        await _scribbles.GetByIdAsync(id) is { } s ? (s.ScopeId, s.ScopeType) : null;
}