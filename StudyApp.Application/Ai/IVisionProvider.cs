using StudyApp.Domain.Enums;

namespace StudyApp.Application.Ai;

// VisionModelConfig lives in AiModelConfigExtensions.cs alongside ChatModelConfig/EmbeddingModelConfig.
public interface IVisionProvider
{
    AiProviderType ProviderType { get; }
    Task<string> DescribeAsync(VisionModelConfig config, byte[] image, string contentType, string prompt,
                               CancellationToken ct = default);
}