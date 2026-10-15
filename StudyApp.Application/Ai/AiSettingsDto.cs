using StudyApp.Domain.Enums;

namespace StudyApp.Application.Ai;

public record AiSettingsDto(
    AiProviderType ChatProvider,
    string ChatBaseUrl,
    string ChatModel,
    bool ChatApiKeySet,
    string? ChatApiKeyPreview,
    string Guardrails,
    string SystemPromptTemplate,
    AiProviderType EmbeddingProvider,
    string EmbeddingBaseUrl,
    string EmbeddingModel,
    bool EmbeddingApiKeySet,
    string? EmbeddingApiKeyPreview,
    int EmbeddingDimensions,
    DateTime UpdatedAt);

// ApiKey fields: null = keep stored key, "" = clear, anything else = replace
// Guardrails/SystemPromptTemplate: null = keep, "" = reset to built-in default, value = replace
public record UpdateAiSettingsRequest(
    AiProviderType ChatProvider,
    string ChatBaseUrl,
    string ChatModel,
    string? ChatApiKey,
    AiProviderType EmbeddingProvider,
    string EmbeddingBaseUrl,
    string EmbeddingModel,
    string? EmbeddingApiKey,
    int? EmbeddingDimensions = null,
    string? Guardrails = null,
    string? SystemPromptTemplate = null);

// all-null body = test the currently saved settings; fill fields to test unsaved form values
public record TestAiRequest(
    AiProviderType? ChatProvider = null,
    string? ChatBaseUrl = null,
    string? ChatApiKey = null,
    string? ChatModel = null,
    AiProviderType? EmbeddingProvider = null,
    string? EmbeddingBaseUrl = null,
    string? EmbeddingApiKey = null,
    string? EmbeddingModel = null);

public record ChatConnectionResult(bool Success, string Message, List<string> Models, bool ModelAvailable);
public record EmbeddingConnectionResult(bool Success, string Message, int? Dimensions, bool DimensionsMatchSchema);
public record AiTestReport(ChatConnectionResult Chat, EmbeddingConnectionResult Embeddings);