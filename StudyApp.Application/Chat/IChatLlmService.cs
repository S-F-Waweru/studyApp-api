namespace StudyApp.Application.Chat;

public record ChatTurn(string Role, string Content); // "user" | "assistant"

public interface IChatLlmService
{
    Task<string> GenerateReplyAsync(string systemPrompt, IEnumerable<ChatTurn> history, string userMessage);
}
