using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using StudyApp.Application.Ai;
using StudyApp.Domain.Enums;

namespace StudyApp.Infrastructure.Ai;

public class OpenAiCompatibleEmbeddingProvider : IEmbeddingProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public OpenAiCompatibleEmbeddingProvider(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public AiProviderType ProviderType => AiProviderType.OpenAiCompatible;

    public async Task<float[]> EmbedAsync(EmbeddingModelConfig config, string text, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ai-embeddings");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{config.BaseUrl}/v1/embeddings")
            {
                Content = JsonContent.Create(new { model = config.Model, input = text })
            };
            if (!string.IsNullOrEmpty(config.ApiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

            using var response = await client.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OpenAiEmbeddingResponse>(ct);
            return result?.Data.FirstOrDefault()?.Embedding
                ?? throw new InvalidOperationException("Empty embedding response from the OpenAI-compatible endpoint");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Could not reach the OpenAI-compatible endpoint at {config.BaseUrl} for embeddings with model '{config.Model}'. " +
                "Confirm the base URL and that the API key is valid.", ex);
        }
    }

    private record OpenAiEmbeddingResponse([property: JsonPropertyName("data")] List<OpenAiEmbeddingData> Data);
    private record OpenAiEmbeddingData([property: JsonPropertyName("embedding")] float[] Embedding);
}
