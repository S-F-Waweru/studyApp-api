using StudyApp.Application.Embeddings;
using StudyApp.Application.Repositories;
using StudyApp.Application.Scoping;
using StudyApp.Domain.Entities;
using StudyApp.Domain.Enums;

namespace StudyApp.Application.Chat;

public class ChatService : IChatService
{
    private const int TopK = 6;
    private const int HistoryLimit = 12; // v1 rule: last N messages, no summarization yet (architecture §4)

    private readonly IChatSessionRepository _sessions;
    private readonly IChatMessageRepository _messages;
    private readonly IVectorChunkRepository _vectorChunks;
    private readonly IEmbeddingService _embedder;
    private readonly IScopeResolver _scopeResolver;
    private readonly IChatLlmService _llm;

    public ChatService(
        IChatSessionRepository sessions,
        IChatMessageRepository messages,
        IVectorChunkRepository vectorChunks,
        IEmbeddingService embedder,
        IScopeResolver scopeResolver,
        IChatLlmService llm)
    {
        _sessions = sessions;
        _messages = messages;
        _vectorChunks = vectorChunks;
        _embedder = embedder;
        _scopeResolver = scopeResolver;
        _llm = llm;
    }

    public async Task<ChatSessionDto> CreateSessionAsync(CreateChatSessionRequest request)
    {
        var session = new ChatSession { ScopeId = request.ScopeId, ScopeType = request.ScopeType, Title = request.Title };
        await _sessions.AddAsync(session);
        return new ChatSessionDto(session.Id, session.ScopeId, session.ScopeType, session.Title, session.CreatedAt);
    }

    public async Task<List<ChatMessageDto>> GetHistoryAsync(Guid chatSessionId, int count = 50)
    {
        var messages = await _messages.GetRecentAsync(chatSessionId, count);
        return messages.Select(ToDto).ToList();
    }

    public async Task<ChatMessageDto> SendMessageAsync(Guid chatSessionId, SendMessageRequest request)
    {
        var session = await _sessions.GetByIdAsync(chatSessionId)
            ?? throw new InvalidOperationException("Chat session not found");

        // 1. store the user's message first
        var userMessage = new ChatMessage { ChatSessionId = chatSessionId, Role = ChatRole.User, Content = request.Content };
        await _messages.AddAsync(userMessage);

        // 2. embed it
        var queryEmbedding = await _embedder.EmbedAsync(request.Content);

        // 3. resolve which scopes are searchable (self + descendants — Folder or Workspace)
        var scopeIds = await _scopeResolver.ResolveScopeIdsAsync(session.ScopeId, session.ScopeType);

        // 4. vector search, top-k across the resolved scopes
        var chunks = (await _vectorChunks.SearchTopKAsync(queryEmbedding, scopeIds, TopK)).ToList();

        // 5. recent history, capped (v1: simple truncation, oldest dropped silently)
        var recent = await _messages.GetRecentAsync(chatSessionId, HistoryLimit);
        var history = recent
            .Where(m => m.Id != userMessage.Id)
            .Select(m => new ChatTurn(m.Role == ChatRole.User ? "user" : "assistant", m.Content));

        // 6. assemble system prompt with retrieved context
        var contextBlock = chunks.Count == 0
            ? "(no relevant context found)"
            : string.Join("\n\n---\n\n", chunks.Select(c => c.ChunkText));

        var systemPrompt =
            $"""
             You are a study assistant. Use the following retrieved notes/documents to answer the user's question.
             If the context doesn't contain the answer, say so rather than guessing.

             Context:
             {contextBlock}
             """;

        // 7. call the LLM
        var reply = await _llm.GenerateReplyAsync(systemPrompt, history, request.Content);

        // 8. store the assistant's reply with source traceability
        var assistantMessage = new ChatMessage
        {
            ChatSessionId = chatSessionId,
            Role = ChatRole.Assistant,
            Content = reply,
            RetrievedChunkIds = chunks.Select(c => c.Id).ToList()
        };
        await _messages.AddAsync(assistantMessage);

        return ToDto(assistantMessage);
    }

    private static ChatMessageDto ToDto(ChatMessage m) =>
        new(m.Id, m.Role.ToString().ToLowerInvariant(), m.Content, m.RetrievedChunkIds, m.CreatedAt);
}
