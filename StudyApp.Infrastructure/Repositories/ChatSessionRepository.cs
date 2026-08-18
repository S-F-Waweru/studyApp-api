using Microsoft.EntityFrameworkCore;
using StudyApp.Application.Repositories;
using StudyApp.Domain.Entities;
using StudyApp.Domain.Enums;
using StudyApp.Infrastructure.Persistence;

namespace StudyApp.Infrastructure.Repositories;

public class ChatSessionRepository : IChatSessionRepository
{
    private readonly AppDbContext _context;

    public ChatSessionRepository(AppDbContext context) => _context = context;

    public async Task<ChatSession?> GetByIdAsync(Guid id) => await _context.ChatSessions.FindAsync(id);

    public async Task<IEnumerable<ChatSession>> GetByScopeAsync(Guid scopeId, ScopeType scopeType) =>
        await _context.ChatSessions
            .Where(s => s.ScopeId == scopeId && s.ScopeType == scopeType)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

    public async Task AddAsync(ChatSession session)
    {
        _context.ChatSessions.Add(session);
        await _context.SaveChangesAsync();
    }
}
