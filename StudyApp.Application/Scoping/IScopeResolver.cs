using StudyApp.Domain.Enums;

namespace StudyApp.Application.Scoping;

// Resolves "self + all descendant scope_ids" for either a Folder or a Workspace.
// This is the piece that makes parent-level Chat aggregation (architecture §10) actually usable
// from a Workspace, not just a Folder.
public interface IScopeResolver
{
    Task<List<Guid>> ResolveScopeIdsAsync(Guid scopeId, ScopeType scopeType);
}
