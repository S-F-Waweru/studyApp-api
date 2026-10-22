using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using StudyApp.Application.Ai;
using StudyApp.Domain.Enums;

namespace StudyApp.Infrastructure.Ai;

public class OllamaVisionProvider : IVisionProvider
{
    private readonly IHttpClientFactory _factory;
    public OllamaVisionProvider(IHttpClientFactory factory) => _factory = factory;

    public AiProviderType ProviderType => AiProviderType.Ollama;

    public async Task<string> DescribeAsync(VisionModelConfig config, byte[] image, string contentType,
        string prompt, CancellationToken ct = default)
    {
        var client = _factory.CreateClient("ai-chat");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{config.BaseUrl}/api/chat")
        {
            Content = JsonContent.Create(new
            {
                model = config.Model,
                stream = false,
                messages = new[]
                {
                    new { role = "user", content = prompt, images = new[] { Convert.ToBase64String(image) } }
                }
            })
        };
        if (!string.IsNullOrEmpty(config.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaVisionResponse>(cancellationToken: ct);
        return result?.Message.Content ?? string.Empty;
    }

    private record OllamaVisionResponse([property: JsonPropertyName("message")] OllamaMessage Message);
    private record OllamaMessage([property: JsonPropertyName("content")] string Content);
}