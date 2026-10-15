using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using StudyApp.Application.Ai;
using StudyApp.Domain.Enums;

namespace StudyApp.Infrastructure.Ai;

public class OllamaEmbeddingProvider : IEmbeddingProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public OllamaEmbeddingProvider(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public AiProviderType ProviderType => AiProviderType.Ollama;

    public async Task<float[]> EmbedAsync(EmbeddingModelConfig config, string text)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ai-embeddings");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{config.BaseUrl}/api/embeddings")
            {
                Content = JsonContent.Create(new { model = config.Model, prompt = text })
            };
            if (!string.IsNullOrEmpty(config.ApiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();
            return result?.Embedding ?? throw new InvalidOperationException("Empty embedding response from Ollama");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"Could not reach the Ollama-compatible endpoint at {config.BaseUrl} for embeddings with model '{config.Model}'.", ex);
        }
    }

    private record OllamaEmbeddingResponse([property: JsonPropertyName("embedding")] float[] Embedding);
}
