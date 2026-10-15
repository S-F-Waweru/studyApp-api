using StudyApp.Application.Ai;
using StudyApp.Application.Embeddings;
using StudyApp.Application.Repositories;
using StudyApp.Domain.Entities;

namespace StudyApp.Infrastructure.Ai;

public class ConfiguredEmbeddingService : IEmbeddingService
{
    private readonly IAiSettingsRepository _settings;
    private readonly IEnumerable<IEmbeddingProvider> _providers;

    public ConfiguredEmbeddingService(IAiSettingsRepository settings, IEnumerable<IEmbeddingProvider> providers)
    {
        _settings = settings;
        _providers = providers;
    }

    public async Task<float[]> EmbedAsync(string text)
    {
        var settings = await _settings.GetAsync();
        var config = settings.ToEmbeddingConfig();

        var provider = _providers.FirstOrDefault(p => p.ProviderType == config.Provider)
            ?? throw new InvalidOperationException($"No embedding provider registered for '{config.Provider}'");

        var embedding = await provider.EmbedAsync(config, text);

        if (embedding.Length != AiSettings.EmbeddingDimension)
            throw new InvalidOperationException(
                $"Embedding model '{config.Model}' returned {embedding.Length} dimensions, but the vector column is vector({AiSettings.EmbeddingDimension}). " +
                "Pick a matching embedding model in AI settings.");

        return embedding;
    }
}
