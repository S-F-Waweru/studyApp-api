using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using StudyApp.Application.Ai;
using StudyApp.Application.Chat;
using StudyApp.Domain.Enums;

namespace StudyApp.Infrastructure.Ai;

// Works with any OpenAI-style /v1/chat/completions endpoint: OpenAI, Groq, OpenRouter, LM Studio, etc. — the BYOK path.
public class OpenAiCompatibleChatProvider : IChatProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public OpenAiCompatibleChatProvider(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public AiProviderType ProviderType => AiProviderType.OpenAiCompatible;

    public async Task<string> GenerateReplyAsync(ChatModelConfig config, string systemPrompt, IEnumerable<ChatTurn> history, string userMessage)
    {
        var messages = new List<object> { new { role = "system", content = systemPrompt } };
        messages.AddRange(history.Select(h => (object)new { role = h.Role, content = h.Content }));
        messages.Add(new { role = "user", content = userMessage });

        try
        {
            var client = _httpClientFactory.CreateClient("ai-chat");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{config.BaseUrl}/v1/chat/completions")
            {
                Content = JsonContent.Create(new { model = config.Model, messages, stream = false })
            };
            if (!string.IsNullOrEmpty(config.ApiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OpenAiChatResponse>();
            return result?.Choices.FirstOrDefault()?.Message.Content
                ?? throw new InvalidOperationException("Empty response from the OpenAI-compatible chat endpoint");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Could not reach the OpenAI-compatible endpoint at {config.BaseUrl} for model '{config.Model}'. " +
                "Confirm the base URL and that the API key is valid.", ex);
        }
    }

    public async Task<List<string>> ListModelsAsync(ChatModelConfig config)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ai-chat");
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{config.BaseUrl}/v1/models");
            if (!string.IsNullOrEmpty(config.ApiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OpenAiModelsResponse>();
            return result?.Data.Select(m => m.Id).OrderBy(id => id).ToList() ?? new List<string>();
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Could not list models from {config.BaseUrl}. Confirm the base URL and API key.", ex);
        }
    }

    private record OpenAiChatResponse([property: JsonPropertyName("choices")] List<OpenAiChoice> Choices);
    private record OpenAiChoice([property: JsonPropertyName("message")] OpenAiMessage Message);
    private record OpenAiMessage([property: JsonPropertyName("content")] string Content);
    private record OpenAiModelsResponse([property: JsonPropertyName("data")] List<OpenAiModel> Data);
    private record OpenAiModel([property: JsonPropertyName("id")] string Id);
}
