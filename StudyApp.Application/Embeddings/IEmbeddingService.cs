namespace StudyApp.Application.Embeddings;

public interface IEmbeddingService
{
    Task<float[]> EmbedAsync(string text);
}
