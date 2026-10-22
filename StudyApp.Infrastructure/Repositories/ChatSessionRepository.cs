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

    // Targets MUST be loaded: scope resolution runs off this collection on every message.
    // A bare FindAsync would return an empty set and every send would fail with 409.
    public async Task<ChatSession?> GetByIdAsync(Guid id) =>
        await _context.ChatSessions
            .Include(s => s.Targets)
            .FirstOrDefaultAsync(s => s.Id == id);

    public async Task<IEnumerable<ChatSession>> GetByScopeAsync(Guid scopeId, ScopeType scopeType) =>
        await _context.ChatSessions
            .Include(s => s.Targets)
            .Where(s => s.ScopeId == scopeId && s.ScopeType == scopeType)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

    public async Task AddAsync(ChatSession session)
    {
        _context.ChatSessions.Add(session);
        await _context.SaveChangesAsync();
    }
}
