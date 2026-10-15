using StudyApp.Application.Repositories;
using StudyApp.Domain.Entities;

namespace StudyApp.Application.Ai;

public class AiSettingsService : IAiSettingsService
{
    private readonly IAiSettingsRepository _repository;
    private readonly AiDefaults _defaults;

    public AiSettingsService(IAiSettingsRepository repository, AiDefaults defaults)
    {
        _repository = repository;
        _defaults = defaults;
    }
    
    public async Task<AiSettingsDto> GetAsync() => ToDto(await _repository.GetAsync());
    public async  Task<AiSettingsDto> UpdateAsync(UpdateAiSettingsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ChatBaseUrl) || string.IsNullOrWhiteSpace(request.ChatModel))
            throw new InvalidOperationException("Chat base URL and chat model are required");

        if (string.IsNullOrWhiteSpace(request.EmbeddingBaseUrl) || string.IsNullOrWhiteSpace(request.EmbeddingModel))
            throw new InvalidOperationException("Embedding base URL and embedding model are required");

        if (request.EmbeddingDimensions is int dims && dims != AiSettings.EmbeddingDimension)
            throw new InvalidOperationException(
                $"The vector column is sized vector({AiSettings.EmbeddingDimension}), so only {AiSettings.EmbeddingDimension}-dimensional embedding models work. " +
                $"Setting it to {dims} requires an EF migration on VectorChunk.Embedding first.");

        var settings = await _repository.GetAsync();
        
        settings.ChatProvider = request.ChatProvider;
        settings.ChatBaseUrl = request.ChatBaseUrl.TrimEnd('/');
        settings.ChatModel = request.ChatModel;
        settings.ChatApiKey = ResolveKey(request.ChatApiKey, settings.ChatApiKey);
        settings.Guardrails = ResolveText(request.Guardrails, settings.Guardrails);
        settings.SystemPromptTemplate = ResolveText(request.SystemPromptTemplate, settings.SystemPromptTemplate);
        
        
        settings.EmbeddingProvider = request.EmbeddingProvider;
        settings.EmbeddingBaseUrl = request.EmbeddingBaseUrl.TrimEnd('/');
        settings.EmbeddingModel = request.EmbeddingModel;
        settings.EmbeddingApiKey = ResolveKey(request.EmbeddingApiKey, settings.EmbeddingApiKey);
        settings.EmbeddingDimensions = request.EmbeddingDimensions ?? AiSettings.EmbeddingDimension;

        await _repository.UpdateAsync(settings);
        return ToDto(settings);
    }


    // null = keep, "" = clear, value = replace
    private static string? ResolveKey(string? incoming, string? existing) =>
        incoming is null ? existing : incoming.Length == 0 ? null : incoming.Trim();

    private static string? ResolveText(string? incoming, string? existing) =>
        incoming is null ? existing : string.IsNullOrWhiteSpace(incoming) ? null : incoming.Trim();

    private AiSettingsDto ToDto(AiSettings s) =>
        new(s.ChatProvider, s.ChatBaseUrl, s.ChatModel,
            !string.IsNullOrEmpty(s.ChatApiKey), Mask(s.ChatApiKey),
            s.Guardrails ?? _defaults.Guardrails,
            s.SystemPromptTemplate ?? _defaults.SystemPromptTemplate,
            s.EmbeddingProvider, s.EmbeddingBaseUrl, s.EmbeddingModel,
            !string.IsNullOrEmpty(s.EmbeddingApiKey), Mask(s.EmbeddingApiKey),
            s.EmbeddingDimensions, s.UpdatedAt);

    private static string? Mask(string? key) =>
        string.IsNullOrEmpty(key) ? null : key.Length <= 4 ? "••••" : $"••••{key[^4..]}";
}