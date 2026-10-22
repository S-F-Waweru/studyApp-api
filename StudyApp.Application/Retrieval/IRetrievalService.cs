using StudyApp.Application.Chat;
using StudyApp.Application.Scoping;

namespace StudyApp.Application.Retrieval;

public interface IRetrievalService
{
    Task<RetrievalResult> RetrieveAsync(string query, float[] queryEmbedding, RetrievalFilter filter,
                                        CancellationToken ct = default);
}