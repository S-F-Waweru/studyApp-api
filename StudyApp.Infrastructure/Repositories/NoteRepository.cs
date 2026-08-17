using Microsoft.EntityFrameworkCore;
using StudyApp.Application.Repositories;
using StudyApp.Domain.Entities;
using StudyApp.Domain.Enums;
using StudyApp.Infrastructure.Persistence;

namespace StudyApp.Infrastructure.Repositories;

public class NoteRepository : INoteRepository
{
    private readonly AppDbContext _context;

    public NoteRepository(AppDbContext context) => _context = context;

    public async Task<Note?> GetByIdAsync(Guid id) =>
        await _context.Notes.FindAsync(id);

    public async Task<IEnumerable<Note>> GetByScopeAsync(Guid scopeId, ScopeType scopeType) =>
        await _context.Notes
            .Where(n => n.ScopeId == scopeId && n.ScopeType == scopeType)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Note>> GetByScopesAsync(IEnumerable<Guid> scopeIds) =>
        await _context.Notes
            .Where(n => scopeIds.Contains(n.ScopeId))
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

    public async Task AddAsync(Note note)
    {
        _context.Notes.Add(note);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Note note)
    {
        _context.Notes.Update(note);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var note = await _context.Notes.FindAsync(id);
        if (note is not null)
        {
            _context.Notes.Remove(note);
            await _context.SaveChangesAsync();
        }
    }
}
