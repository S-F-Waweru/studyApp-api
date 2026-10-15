using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using StudyApp.Application.Ai;
using StudyApp.Application.Chat;
using StudyApp.Domain.Enums;

namespace StudyApp.Infrastructure.Ai;

public class OllamaChatProvider : IChatProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public OllamaChatProvider(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public AiProviderType ProviderType => AiProviderType.Ollama;

    public async Task<string> GenerateReplyAsync(ChatModelConfig config, string systemPrompt, IEnumerable<ChatTurn> history, string userMessage)
    {
        var messages = new List<object> { new { role = "system", content = systemPrompt } };
        messages.AddRange(history.Select(h => (object)new { role = h.Role, content = h.Content }));
        messages.Add(new { role = "user", content = userMessage });

        try
        {
            var client = _httpClientFactory.CreateClient("ai-chat");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{config.BaseUrl}/api/chat")
            {
                Content = JsonContent.Create(new { model = config.Model, messages, stream = false })
            };
            if (!string.IsNullOrEmpty(config.ApiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>();
            return result?.Message.Content ?? throw new InvalidOperationException("Empty response from Ollama chat");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Could not reach the Ollama-compatible endpoint at {config.BaseUrl} for model '{config.Model}'. " +
                "Confirm the base URL, that the endpoint is running/authorized, and that the model is pulled.", ex);
        }
    }

    public async Task<List<string>> ListModelsAsync(ChatModelConfig config)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ai-chat");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{config.BaseUrl}/api/tags");
            if (!string.IsNullOrEmpty(config.ApiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>();
            return result?.Models.Select(m => m.Name).OrderBy(n => n).ToList() ?? new List<string>();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Could not list models from Ollama at {config.BaseUrl}.", ex);
        }
    }

    private record OllamaChatResponse([property: JsonPropertyName("message")] OllamaMessage Message);
    private record OllamaMessage([property: JsonPropertyName("content")] string Content);
    private record OllamaTagsResponse([property: JsonPropertyName("models")] List<OllamaModel> Models);
    private record OllamaModel([property: JsonPropertyName("name")] string Name);
}
