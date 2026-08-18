namespace StudyApp.Application.Chat;

public interface IChatService
{
    Task<ChatSessionDto> CreateSessionAsync(CreateChatSessionRequest request);
    Task<List<ChatMessageDto>> GetHistoryAsync(Guid chatSessionId, int count = 50);
    Task<ChatMessageDto> SendMessageAsync(Guid chatSessionId, SendMessageRequest request);
}
