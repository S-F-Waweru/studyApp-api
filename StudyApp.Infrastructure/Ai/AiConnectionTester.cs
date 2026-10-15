using StudyApp.Application.Ai;
using StudyApp.Application.Repositories;
using StudyApp.Domain.Entities;

namespace StudyApp.Infrastructure.Ai;

public class AiConnectionTester : IAiConnectionTester
{
    private readonly IAiSettingsRepository _settings;
    private readonly IEnumerable<IChatProvider> _chatProviders;
    private readonly IEnumerable<IEmbeddingProvider> _embeddingProviders;

    public AiConnectionTester(
        IAiSettingsRepository settings,
        IEnumerable<IChatProvider> chatProviders,
        IEnumerable<IEmbeddingProvider> embeddingProviders)
    {
        _settings = settings;
        _chatProviders = chatProviders;
        _embeddingProviders = embeddingProviders;
    }

    public async Task<AiTestReport> TestAsync(TestAiRequest request)
    {
        var saved = await _settings.GetAsync();

        var chatConfig = new ChatModelConfig(
            request.ChatProvider ?? saved.ChatProvider,
            (request.ChatBaseUrl ?? saved.ChatBaseUrl).TrimEnd('/'),
            request.ChatApiKey ?? saved.ChatApiKey,
            request.ChatModel ?? saved.ChatModel);

        var embeddingConfig = new EmbeddingModelConfig(
            request.EmbeddingProvider ?? saved.EmbeddingProvider,
            (request.EmbeddingBaseUrl ?? saved.EmbeddingBaseUrl).TrimEnd('/'),
            request.EmbeddingApiKey ?? saved.EmbeddingApiKey,
            request.EmbeddingModel ?? saved.EmbeddingModel);

        return new AiTestReport(await TestChatAsync(chatConfig), await TestEmbeddingsAsync(embeddingConfig));
    }

    private async Task<ChatConnectionResult> TestChatAsync(ChatModelConfig config)
    {
        var provider = _chatProviders.FirstOrDefault(p => p.ProviderType == config.Provider);
        if (provider is null)
            return new ChatConnectionResult(false, $"No chat provider registered for '{config.Provider}'", new List<string>(), false);

        try
        {
            var models = await provider.ListModelsAsync(config);
            var available = models.Any(m =>
                string.Equals(m, config.Model, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Split(':')[0], config.Model, StringComparison.OrdinalIgnoreCase)); // "name" vs "name:latest"

            var message = !models.Any()
                ? "Connected, but the provider reported no models."
                : available
                    ? $"Connected. Model '{config.Model}' is available."
                    : $"Connected, but '{config.Model}' was not found. Available: {string.Join(", ", models.Take(10))}";

            return new ChatConnectionResult(true, message, models, available);
        }
        catch (Exception ex)
        {
            return new ChatConnectionResult(false, ex.Message, new List<string>(), false);
        }
    }

    private async Task<EmbeddingConnectionResult> TestEmbeddingsAsync(EmbeddingModelConfig config)
    {
        var provider = _embeddingProviders.FirstOrDefault(p => p.ProviderType == config.Provider);
        if (provider is null)
            return new EmbeddingConnectionResult(false, $"No embedding provider registered for '{config.Provider}'", null, false);

        try
        {
            var embedding = await provider.EmbedAsync(config, "dimension probe");
            var matches = embedding.Length == AiSettings.EmbeddingDimension;

            var message = matches
                ? $"Connected. '{config.Model}' returned {embedding.Length} dimensions."
                : $"Connected, but '{config.Model}' returned {embedding.Length} dimensions — the vector column is vector({AiSettings.EmbeddingDimension}). " +
                  "This model would require a schema migration before it can be used.";

            return new EmbeddingConnectionResult(true, message, embedding.Length, matches);
        }
        catch (Exception ex)
        {
            return new EmbeddingConnectionResult(false, ex.Message, null, false);
        }
    }
}
