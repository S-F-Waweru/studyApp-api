namespace StudyApp.Application.Ai;

public class AiDefaults
{
    public string ChatBaseUrl { get; set; } = "http://localhost:11434";
    public string ChatModel { get; set; } = "gemma4:31b-cloud";
    public string EmbeddingBaseUrl { get; set; } = "http://localhost:11434";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";

    // seeded into AiSettings on first read — edit at runtime via /api/ai/settings
    public string Guardrails { get; set; } =
        """
        You are a study assistant. Use the following retrieved notes/documents to answer the user's question.
        If the context doesn't contain the answer, say so rather than guessing.
        """;

    public string SystemPromptTemplate { get; set; } =
        """
        {guardrails}

        Workspace context:
        {workspace_context}

        Retrieved context:
        {retrieved_context}
        """;
}