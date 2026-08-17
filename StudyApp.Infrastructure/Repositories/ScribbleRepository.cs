using Microsoft.EntityFrameworkCore;
using StudyApp.Application.Repositories;
using StudyApp.Domain.Entities;
using StudyApp.Domain.Enums;
using StudyApp.Infrastructure.Persistence;

namespace StudyApp.Infrastructure.Repositories;

public class ScribbleRepository : IScribbleRepository
{
    private readonly AppDbContext _context;

    public ScribbleRepository(AppDbContext context) => _context = context;

    public async Task<Scribble?> GetByIdAsync(Guid id) =>
        await _context.Scribbles.FindAsync(id);

    public async Task<IEnumerable<Scribble>> GetByScopeAsync(Guid scopeId, ScopeType scopeType) =>
        await _context.Scribbles
            .Where(s => s.ScopeId == scopeId && s.ScopeType == scopeType)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Scribble>> GetByScopesAsync(IEnumerable<Guid> scopeIds) =>
        await _context.Scribbles
            .Where(s => scopeIds.Contains(s.ScopeId))
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

    public async Task AddAsync(Scribble scribble)
    {
        _context.Scribbles.Add(scribble);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Scribble scribble)
    {
        _context.Scribbles.Update(scribble);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var scribble = await _context.Scribbles.FindAsync(id);
        if (scribble is not null)
        {
            _context.Scribbles.Remove(scribble);
            await _context.SaveChangesAsync();
        }
    }
}
