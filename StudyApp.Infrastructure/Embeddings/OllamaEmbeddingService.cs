using System.Net.Http.Json;
using System.Text.Json.Serialization;
using StudyApp.Application.Embeddings;

namespace StudyApp.Infrastructure.Embeddings;

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _client;

    public OllamaEmbeddingService(HttpClient client) => _client = client; // base address: http://localhost:11434

    public async Task<float[]> EmbedAsync(string text)
    {

        try{
            var response = await _client.PostAsJsonAsync("/api/embeddings", new
            {
                model = "nomic-embed-text",
                prompt = text
            });
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();
            return result?.Embedding ?? throw new InvalidOperationException("Empty embedding response from Ollama");
        }catch (HttpRequestException ex)
            {
                throw new InvalidOperationException("Could not reach Ollama for embeddings — confirm 'ollama serve' is running and 'nomic-embed-text' is pulled.", ex);
            }

    }

    private record OllamaEmbeddingResponse([property: JsonPropertyName("embedding")] float[] Embedding);
}
