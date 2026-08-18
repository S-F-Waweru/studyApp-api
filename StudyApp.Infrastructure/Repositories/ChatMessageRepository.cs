using Microsoft.EntityFrameworkCore;
using StudyApp.Application.Repositories;
using StudyApp.Domain.Entities;
using StudyApp.Infrastructure.Persistence;

namespace StudyApp.Infrastructure.Repositories;

public class ChatMessageRepository : IChatMessageRepository
{
    private readonly AppDbContext _context;

    public ChatMessageRepository(AppDbContext context) => _context = context;

    public async Task<List<ChatMessage>> GetRecentAsync(Guid chatSessionId, int count)
    {
        var messages = await _context.ChatMessages
            .Where(m => m.ChatSessionId == chatSessionId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(count)
            .ToListAsync();

        messages.Reverse(); // chronological order for prompt assembly
        return messages;
    }

    public async Task AddAsync(ChatMessage message)
    {
        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();
    }
}
