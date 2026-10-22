using StudyApp.Domain.Entities;
using StudyApp.Domain.Enums;

namespace StudyApp.Application.Ai;

public record ChatModelConfig(AiProviderType Provider, string BaseUrl, string? ApiKey, string Model);

public record EmbeddingModelConfig(AiProviderType Provider, string BaseUrl, string? ApiKey, string Model);

public record VisionModelConfig(AiProviderType Provider, string BaseUrl, string? ApiKey, string Model);

public static class AiModelConfigExtensions
{
    public static ChatModelConfig ToChatConfig(this AiSettings s) =>
        new(s.ChatProvider, s.ChatBaseUrl, s.ChatApiKey, s.ChatModel);

    public static EmbeddingModelConfig ToEmbeddingConfig(this AiSettings s) =>
        new(s.EmbeddingProvider, s.EmbeddingBaseUrl, s.EmbeddingApiKey, s.EmbeddingModel);

    // Falls back to the chat model when no dedicated vision model is configured.
    public static VisionModelConfig ToVisionConfig(this AiSettings s) =>
        s.VisionModel is null
            ? new VisionModelConfig(s.ChatProvider, s.ChatBaseUrl, s.ChatApiKey, s.ChatModel)
            : new VisionModelConfig(s.VisionProvider ?? s.ChatProvider, s.VisionBaseUrl ?? s.ChatBaseUrl,
                                    s.VisionApiKey ?? s.ChatApiKey, s.VisionModel);
}