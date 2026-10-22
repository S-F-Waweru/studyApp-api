using StudyApp.Domain.Enums;

namespace StudyApp.Application.Scoping;

public record ScopeTarget(
    ChatScopeType TargetType,
    Guid TargetId,
    bool IncludeLinked = true,
    bool IncludeAncestors = false);

public sealed record RetrievalFilter
{
    public required IReadOnlyList<Guid> WorkspaceIds { get; init; }
    public required IReadOnlyList<Guid> FolderIds { get; init; }
    public required IReadOnlyList<Guid> DocumentIds { get; init; }
    public required IReadOnlyList<Guid> NoteIds { get; init; }
    public required IReadOnlyList<Guid> ScribbleIds { get; init; }

    public bool IsEmpty =>
        WorkspaceIds.Count == 0 && FolderIds.Count == 0 && DocumentIds.Count == 0
        && NoteIds.Count == 0 && ScribbleIds.Count == 0;

    public int DeclaredTargetCount =>
        WorkspaceIds.Count + FolderIds.Count + DocumentIds.Count + NoteIds.Count + ScribbleIds.Count;
}