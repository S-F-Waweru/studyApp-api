using StudyApp.Domain.Enums;

namespace StudyApp.Application.Ai;

public interface IEmbeddingProvider
{
    AiProviderType ProviderType { get; }
    Task<float[]> EmbedAsync(EmbeddingModelConfig config, string text);
}