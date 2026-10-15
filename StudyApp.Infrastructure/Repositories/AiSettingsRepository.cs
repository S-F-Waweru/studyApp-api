using Microsoft.EntityFrameworkCore;
using StudyApp.Application.Ai;
using StudyApp.Application.Repositories;
using StudyApp.Domain.Entities;
using StudyApp.Domain.Enums;
using StudyApp.Infrastructure.Persistence;

namespace StudyApp.Infrastructure.Repositories;

public class AiSettingsRepository : IAiSettingsRepository
{
    private readonly AppDbContext _context;
    private readonly AiDefaults _defaults;
    private AiSettings? _cached;

    public AiSettingsRepository(AppDbContext context, AiDefaults defaults)
    {
        _context = context;
        _defaults = defaults;
    }

    public async Task<AiSettings> GetAsync() =>
        _cached ??= await _context.AiSettings.FirstOrDefaultAsync() ?? BuildFromDefaults();

    public async Task UpdateAsync(AiSettings settings)
    {
        settings.UpdatedAt = DateTime.UtcNow;

        if (await _context.AiSettings.AnyAsync(s => s.Id == settings.Id))
            _context.AiSettings.Update(settings);
        else
            _context.AiSettings.Add(settings);

        await _context.SaveChangesAsync();
        _cached = settings;
    }

    private AiSettings BuildFromDefaults() => new()
    {
        ChatProvider = AiProviderType.Ollama,
        ChatBaseUrl = _defaults.ChatBaseUrl,
        ChatModel = _defaults.ChatModel,
        Guardrails = _defaults.Guardrails,
        SystemPromptTemplate = _defaults.SystemPromptTemplate,
        EmbeddingProvider = AiProviderType.Ollama,
        EmbeddingBaseUrl = _defaults.EmbeddingBaseUrl,
        EmbeddingModel = _defaults.EmbeddingModel
    };
}

