using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using StudyApp.Application.Ai;
using StudyApp.Domain.Enums;

namespace StudyApp.Infrastructure.Ai;

// Vision counterpart of OpenAiCompatibleChatProvider: /v1/chat/completions with an image_url part.
public class OpenAiCompatibleVisionProvider : IVisionProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public OpenAiCompatibleVisionProvider(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public AiProviderType ProviderType => AiProviderType.OpenAiCompatible;

    public async Task<string> DescribeAsync(VisionModelConfig config, byte[] image, string contentType,
        string prompt, CancellationToken ct = default)
    {
        var dataUrl = $"data:{contentType};base64,{Convert.ToBase64String(image)}";

        var messages = new[]
        {
            new
            {
                role = "user",
                content = new object[]
                {
                    new { type = "text", text = prompt },
                    new { type = "image_url", image_url = new { url = dataUrl } }
                }
            }
        };

        var client = _httpClientFactory.CreateClient("ai-chat");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{config.BaseUrl}/v1/chat/completions")
        {
            Content = JsonContent.Create(new { model = config.Model, messages, stream = false })
        };
        if (!string.IsNullOrEmpty(config.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OpenAiVisionResponse>(cancellationToken: ct);
        return result?.Choices.FirstOrDefault()?.Message.Content ?? string.Empty;
    }

    private record OpenAiVisionResponse([property: JsonPropertyName("choices")] List<OpenAiVisionChoice> Choices);
    private record OpenAiVisionChoice([property: JsonPropertyName("message")] OpenAiVisionMessage Message);
    private record OpenAiVisionMessage([property: JsonPropertyName("content")] string Content);
}
