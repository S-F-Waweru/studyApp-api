using StudyApp.Domain.Enums;

namespace StudyApp.Domain.Entities;

public class AiSettings
{
    public const int EmbeddingDimension = 768;

    public Guid Id { get; set; } = Guid.NewGuid();

    public AiProviderType ChatProvider { get; set; } = AiProviderType.Ollama;

    public string ChatBaseUrl { get; set; } = "http://localhost:11434";
    public string ChatModel { get; set; } = default!;
    public string? ChatApiKey { get; set; }

    public string? Guardrails { get; set; } // global rules pinned into every prompt; null = built-in default
    public string? SystemPromptTemplate { get; set; } // placeholders: {guardrails}, {workspace_context}, {retrieved_context}

    public AiProviderType EmbeddingProvider { get; set; } = AiProviderType.Ollama;
    public string EmbeddingBaseUrl { get; set; } = "http://localhost:11434";
    public string EmbeddingModel { get; set; } = default!;
    public string? EmbeddingApiKey { get; set; }
    public int EmbeddingDimensions { get; set; } = EmbeddingDimension;

    // Phase 5 — image captioning. Null VisionModel falls back to ChatModel.
    public AiProviderType? VisionProvider { get; set; }
    public string? VisionBaseUrl { get; set; }
    public string? VisionModel { get; set; }
    public string? VisionApiKey { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}