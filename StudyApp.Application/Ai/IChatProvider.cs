using StudyApp.Application.Chat;
using StudyApp.Domain.Enums;

namespace StudyApp.Application.Ai;

public interface IChatProvider
{
    AiProviderType ProviderType { get; }
    Task<string> GenerateReplyAsync(ChatModelConfig config, string systemPrompt, IEnumerable<ChatTurn> history, string userMessage);
    Task<List<string>> ListModelsAsync(ChatModelConfig config);
}