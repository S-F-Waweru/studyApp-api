using StudyApp.Application.Ai;
using StudyApp.Application.Repositories;
using StudyApp.Domain.Entities;
using StudyApp.Domain.Enums;

namespace StudyApp.Application.Chat;

// Builds the system prompt as: guardrails (global AI settings) → workspace context (first context) → retrieved chunks.
public class SystemPromptBuilder : ISystemPromptBuilder
{
    private const int MaxFolderWalk = 25; // safety stop for malformed folder trees

    private readonly IAiSettingsRepository _aiSettings;
    private readonly IWorkrepository _workspaces;
    private readonly IFolderRepository _folders;
    private readonly AiDefaults _defaults;

    public SystemPromptBuilder(
        IAiSettingsRepository aiSettings,
        IWorkrepository workspaces,
        IFolderRepository folders,
        AiDefaults defaults)
    {
        _aiSettings = aiSettings;
        _workspaces = workspaces;
        _folders = folders;
        _defaults = defaults;
    }

    public string Build(Workspace? workspace, RetrievalResult retrieval)
    {
        var settings = _aiSettings.GetAsync().GetAwaiter().GetResult();

        var retrieved = retrieval.Sources.Count == 0
            ? "(no relevant context found — say so instead of guessing)"
            : string.Join("\n\n", retrieval.Sources.Select((source, i) =>
            {
                var chunk = retrieval.Chunks[i];
                var header = $"[{source.Label}] {source.SourceType} \"{source.Title ?? "untitled"}\""
                           + (source.Locator is null ? "" : $" · {source.Locator}")
                           + (source.HeadingPath is null ? "" : $" · {source.HeadingPath}");
                return $"{header}\n{chunk.ChunkText}";
            }));

        var template = string.IsNullOrWhiteSpace(settings.SystemPromptTemplate)
            ? _defaults.SystemPromptTemplate
            : settings.SystemPromptTemplate;

        return template
            .Replace("{guardrails}", string.IsNullOrWhiteSpace(settings.Guardrails) ? _defaults.Guardrails : settings.Guardrails)
            .Replace("{workspace_context}", BuildWorkspaceContext(workspace))
            .Replace("{retrieved_context}", retrieved);
    }

    public async Task<string> BuildAsync(Guid scopeId, ScopeType scopeType, RetrievalResult retrieval)
    {
        var settings = await _aiSettings.GetAsync();
        var workspace = await ResolveWorkspaceAsync(scopeId, scopeType);

        var retrieved = retrieval.Sources.Count == 0
            ? "(no relevant context found — say so instead of guessing)"
            : string.Join("\n\n", retrieval.Sources.Select((source, i) =>
            {
                var chunk = retrieval.Chunks[i];
                var header = $"[{source.Label}] {source.SourceType} \"{source.Title ?? "untitled"}\""
                           + (source.Locator is null ? "" : $" · {source.Locator}")
                           + (source.HeadingPath is null ? "" : $" · {source.HeadingPath}");
                return $"{header}\n{chunk.ChunkText}";
            }));

        var template = string.IsNullOrWhiteSpace(settings.SystemPromptTemplate)
            ? _defaults.SystemPromptTemplate
            : settings.SystemPromptTemplate;

        return template
            .Replace("{guardrails}", string.IsNullOrWhiteSpace(settings.Guardrails) ? _defaults.Guardrails : settings.Guardrails)
            .Replace("{workspace_context}", BuildWorkspaceContext(workspace))
            .Replace("{retrieved_context}", retrieved);
    }

    // Folder-scoped chats still belong to a workspace — walk to the root folder, then take its workspace.
    private async Task<Workspace?> ResolveWorkspaceAsync(Guid scopeId, ScopeType scopeType)
    {
        if (scopeType == ScopeType.Workspace)
            return await _workspaces.GetByIdAsync(scopeId);

        var folder = await _folders.GetByIdAsync(scopeId);
        for (var hops = 0; folder?.ParentFolderId is not null && hops < MaxFolderWalk; hops++)
            folder = await _folders.GetByIdAsync(folder.ParentFolderId.Value);

        return folder is null ? null : await _workspaces.GetByIdAsync(folder.WorkspaceId);
    }

    private static string BuildWorkspaceContext(Workspace? workspace)
    {
        if (workspace is null)
            return "(no workspace context available)";

        var parts = new List<string> { $"Workspace: {workspace.Name}" };

        if (!string.IsNullOrWhiteSpace(workspace.Description))
            parts.Add($"Description: {workspace.Description}");

        if (!string.IsNullOrWhiteSpace(workspace.CustomInstructions))
            parts.Add($"Workspace instructions:\n{workspace.CustomInstructions}");

        return string.Join("\n", parts);
    }
}
