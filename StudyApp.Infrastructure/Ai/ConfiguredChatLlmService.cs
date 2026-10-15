using StudyApp.Application.Ai;
using StudyApp.Application.Chat;
using StudyApp.Application.Repositories;

namespace StudyApp.Infrastructure.Ai;

// Reads the saved settings on every call, so changes from the UI take effect immediately — no restart.
public class ConfiguredChatLlmService : IChatLlmService
{
    private readonly IAiSettingsRepository _settings;
    private readonly IEnumerable<IChatProvider> _providers;

    public ConfiguredChatLlmService(IAiSettingsRepository settings, IEnumerable<IChatProvider> providers)
    {
        _settings = settings;
        _providers = providers;
    }

    public async Task<string> GenerateReplyAsync(string systemPrompt, IEnumerable<ChatTurn> history, string userMessage)
    {
        var config = (await _settings.GetAsync()).ToChatConfig();

        var provider = _providers.FirstOrDefault(p => p.ProviderType == config.Provider)
            ?? throw new InvalidOperationException($"No chat provider registered for '{config.Provider}'");

        return await provider.GenerateReplyAsync(config, systemPrompt, history, userMessage);
    }
}
