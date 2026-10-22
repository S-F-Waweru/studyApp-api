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
    private readonly ISystemPromptBuilder _promptBuilder;

    public ChatService(
        IChatSessionRepository sessions,
        IChatMessageRepository messages,
        IVectorChunkRepository vectorChunks,
        IEmbeddingService embedder,
        IScopeResolver scopeResolver,
        IChatLlmService llm,
        ISystemPromptBuilder promptBuilder)
    {
        _sessions = sessions;
        _messages = messages;
        _vectorChunks = vectorChunks;
        _embedder = embedder;
        _scopeResolver = scopeResolver;
        _llm = llm;
        _promptBuilder = promptBuilder;
    }

    public async Task<ChatSessionDto> CreateSessionAsync(CreateChatSessionRequest request)
    {
        var targets = (request.Targets ?? new List<ChatScopeTargetDto>())
            .Select(t => new ScopeTarget(t.TargetType, t.TargetId, t.IncludeLinked, t.IncludeAncestors))
            .ToList();

        // legacy callers still send ScopeId/ScopeType
        if (targets.Count == 0 && request.ScopeId is not null && request.ScopeType is not null)
            targets.Add(new ScopeTarget(
                request.ScopeType == ScopeType.Folder ? ChatScopeType.Folder : ChatScopeType.Workspace,
                request.ScopeId.Value));

        if (targets.Count == 0)
            throw new InvalidOperationException("A chat session needs at least one scope target.");

        await _scopeResolver.ValidateTargetsAsync(targets); // never create a chat against a dead target

        // owning location for listing: the first workspace/folder target, else the item's folder
        var (scopeId, scopeType) = await ResolveOwningScopeAsync(targets);

        var session = new ChatSession
        {
            ScopeId = scopeId,
            ScopeType = scopeType,
            Title = request.Title,
            Targets = targets.Select(t => new ChatSessionScope
            {
                TargetType = t.TargetType,
                TargetId = t.TargetId,
                IncludeLinked = t.IncludeLinked,
                IncludeAncestors = t.IncludeAncestors
            }).ToList()
        };

        await _sessions.AddAsync(session);
        return ToSessionDto(session);
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

        var userMessage = new ChatMessage { ChatSessionId = chatSessionId, Role = ChatRole.User, Content = request.Content };
        await _messages.AddAsync(userMessage);

        // 1. re-resolve every message: new material appears without recreating the chat
        var targets = session.Targets
            .Select(t => new ScopeTarget(t.TargetType, t.TargetId, t.IncludeLinked, t.IncludeAncestors))
            .ToList();

        var filter = await _scopeResolver.ResolveAsync(targets);

        // 2. NEVER widen silently — a dead scope must fail loudly
        if (filter.IsEmpty)
            throw new ScopeUnavailableException("The selected chat scope no longer contains any material.");

        var queryEmbedding = await _embedder.EmbedAsync(request.Content);
        var chunks = (await _vectorChunks.SearchVectorAsync(queryEmbedding, filter, TopK)).ToList();

        var recent = await _messages.GetRecentAsync(chatSessionId, HistoryLimit);
        var history = recent
            .Where(m => m.Id != userMessage.Id)
            .Select(m => new ChatTurn(m.Role == ChatRole.User ? "user" : "assistant", m.Content));

        var retrieval = new RetrievalResult(chunks.Select(c => c.Chunk).ToList(), BuildSources(chunks));
        var systemPrompt = await _promptBuilder.BuildAsync(session.ScopeId, session.ScopeType, retrieval.Chunks);

        string reply;
        try { reply = await _llm.GenerateReplyAsync(systemPrompt, history, request.Content); }
        catch (InvalidOperationException ex) { reply = $"(Assistant unavailable: {ex.Message})"; }

        var assistantMessage = new ChatMessage
        {
            ChatSessionId = chatSessionId,
            Role = ChatRole.Assistant,
            Content = reply,
            RetrievedChunkIds = retrieval.Chunks.Select(c => c.Id).ToList()
        };
        await _messages.AddAsync(assistantMessage);

        return ToDto(assistantMessage, retrieval.Sources);
    }

    public async Task<ChatScopeSummaryDto> GetScopeSummaryAsync(Guid chatSessionId)
    {
        var session = await _sessions.GetByIdAsync(chatSessionId)
            ?? throw new InvalidOperationException("Chat session not found");

        var targets = session.Targets
            .Select(t => new ScopeTarget(t.TargetType, t.TargetId, t.IncludeLinked, t.IncludeAncestors))
            .ToList();

        var filter = await _scopeResolver.ResolveAsync(targets);
        var chunkCount = await _vectorChunks.CountAsync(filter);

        return new ChatScopeSummaryDto(session.Id, filter.DeclaredTargetCount, chunkCount,
            session.Targets.Select(t => new ChatScopeTargetDto(t.TargetType, t.TargetId, t.IncludeLinked, t.IncludeAncestors)).ToList());
    }

    public async Task<List<ChatSessionDto>> GetSessionsByScopeAsync(Guid scopeId, ScopeType scopeType)
    {
        var sessions = await _sessions.GetByScopeAsync(scopeId, scopeType);
        return sessions.Select(s => new ChatSessionDto(s.Id, s.ScopeId, s.ScopeType, s.Title, s.CreatedAt)).ToList();
    }

    private static ChatMessageDto ToDto(ChatMessage m) =>
        new(m.Id, m.Role.ToString().ToLowerInvariant(), m.Content, m.RetrievedChunkIds, m.CreatedAt);

    private static ChatMessageDto ToDto(ChatMessage m, IReadOnlyList<RetrievedSource> sources) =>
        new(m.Id, m.Role.ToString().ToLowerInvariant(), m.Content, m.RetrievedChunkIds, m.CreatedAt, sources.ToList());

    private static ChatSessionDto ToSessionDto(ChatSession s) =>
        new(s.Id, s.ScopeId, s.ScopeType, s.Title, s.CreatedAt,
            s.Targets.Select(t => new ChatScopeTargetDto(t.TargetType, t.TargetId, t.IncludeLinked, t.IncludeAncestors)).ToList());

    private static List<RetrievedSource> BuildSources(IEnumerable<ScoredChunk> scored) =>
        scored.Select((s, i) => new RetrievedSource(
            Label: $"S{i + 1}",
            ChunkId: s.Chunk.Id,
            SourceId: s.Chunk.SourceId,
            SourceType: s.Chunk.SourceType,
            Title: s.Chunk.SourceTitle,
            Locator: s.Chunk.Locator,
            HeadingPath: s.Chunk.HeadingPath,
            Score: s.Score)).ToList();

    private async Task<(Guid, ScopeType)> ResolveOwningScopeAsync(List<ScopeTarget> targets)
    {
        foreach (var target in targets)
        {
            if (target.TargetType == ChatScopeType.Workspace)
                return (target.TargetId, ScopeType.Workspace);

            if (target.TargetType == ChatScopeType.Folder)
                return (target.TargetId, ScopeType.Folder);
        }

        var firstTarget = targets.First();
        return await _scopeResolver.ResolveOwningLocationAsync(firstTarget.TargetType, firstTarget.TargetId);
    }
}
