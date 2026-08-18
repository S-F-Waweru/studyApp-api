using System.Net.Http.Json;
using System.Text.Json.Serialization;
using StudyApp.Application.Chat;

namespace StudyApp.Infrastructure.Chat;

public class OllamaChatService : IChatLlmService
{
    private readonly HttpClient _client;
    private const string Model = "qwen3:1.7b"; // swap freely — chat LLM is not sticky like the embedding model (architecture §7)

    public OllamaChatService(HttpClient client) => _client = client; // base address: http://localhost:11434

    public async Task<string> GenerateReplyAsync(string systemPrompt, IEnumerable<ChatTurn> history, string userMessage)
    {
        try {
        var messages = new List<object> { new { role = "system", content = systemPrompt } };
        messages.AddRange(history.Select(h => (object)new { role = h.Role, content = h.Content }));
        messages.Add(new { role = "user", content = userMessage });

        var response = await _client.PostAsJsonAsync("/api/chat", new
        {
            model = Model,
            messages,
            stream = false
        });
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>();
        return result?.Message.Content ?? throw new InvalidOperationException("Empty response from Ollama chat");

    }catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Could not reach Ollama for LLM — confirm 'ollama serve' is running andSelected model is pulled.", ex);
        }
    }

    private record OllamaChatResponse([property: JsonPropertyName("message")] OllamaMessage Message);
    private record OllamaMessage([property: JsonPropertyName("content")] string Content);
}
