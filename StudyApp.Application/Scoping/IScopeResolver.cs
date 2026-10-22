using StudyApp.Domain.Enums;

namespace StudyApp.Application.Scoping;

public interface IScopeResolver
{
    // Resolves declared chat targets into a single retrieval filter
    // (workspace + descendant folders, plus explicit item targets).
    Task<RetrievalFilter> ResolveAsync(IReadOnlyList<ScopeTarget> targets, CancellationToken ct = default);

    // Used for prompt context: which workspace does this chat ultimately belong to?
    Task<Guid?> ResolveOwningWorkspaceAsync(Guid scopeId, ScopeType scopeType, CancellationToken ct = default);

    // Where does a chat target physically live? Item targets (note/document/scribble)
    // report their own filing scope; workspace/folder targets report themselves.
    Task<(Guid ScopeId, ScopeType ScopeType)> ResolveOwningLocationAsync(
        ChatScopeType targetType, Guid targetId, CancellationToken ct = default);

    // Validates a declared target exists — throws rather than silently widening.
    Task ValidateTargetsAsync(IReadOnlyList<ScopeTarget> targets, CancellationToken ct = default);
}
