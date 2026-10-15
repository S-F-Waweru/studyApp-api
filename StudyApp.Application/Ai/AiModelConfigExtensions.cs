using StudyApp.Domain.Entities;
using StudyApp.Domain.Enums;

namespace StudyApp.Application.Ai;

public record ChatModelConfig(AiProviderType Provider, string BaseUrl, string? ApiKey, string Model);

public record EmbeddingModelConfig(AiProviderType Provider, string BaseUrl, string? ApiKey, string Model);

public static class AiModelConfigExtensions
{
    public static ChatModelConfig ToChatConfig(this AiSettings s) =>
        new(s.ChatProvider, s.ChatBaseUrl, s.ChatApiKey, s.ChatModel);

    public static EmbeddingModelConfig ToEmbeddingConfig(this AiSettings s) =>
        new(s.EmbeddingProvider, s.EmbeddingBaseUrl, s.EmbeddingApiKey, s.EmbeddingModel);
}