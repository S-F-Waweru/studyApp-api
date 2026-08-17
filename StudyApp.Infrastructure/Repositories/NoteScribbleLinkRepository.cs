using Microsoft.EntityFrameworkCore;
using StudyApp.Application.Repositories;
using StudyApp.Domain.Entities;
using StudyApp.Infrastructure.Persistence;

namespace StudyApp.Infrastructure.Repositories;

public class NoteScribbleLinkRepository : INoteScribbleLinkRepository
{
    private readonly AppDbContext _context;

    public NoteScribbleLinkRepository(AppDbContext context) => _context = context;

    public async Task LinkAsync(Guid noteId, Guid scribbleId)
    {
        var exists = await _context.NoteScribbleLinks
            .AnyAsync(l => l.NoteId == noteId && l.ScribbleId == scribbleId);
        if (exists) return;

        _context.NoteScribbleLinks.Add(new NoteScribbleLink { NoteId = noteId, ScribbleId = scribbleId });
        await _context.SaveChangesAsync();
    }

    public async Task UnlinkAsync(Guid noteId, Guid scribbleId)
    {
        var link = await _context.NoteScribbleLinks
            .FirstOrDefaultAsync(l => l.NoteId == noteId && l.ScribbleId == scribbleId);
        if (link is not null)
        {
            _context.NoteScribbleLinks.Remove(link);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<Guid>> GetLinkedScribbleIdsAsync(Guid noteId) =>
        await _context.NoteScribbleLinks
            .Where(l => l.NoteId == noteId)
            .Select(l => l.ScribbleId)
            .ToListAsync();

    public async Task<IEnumerable<Guid>> GetLinkedNoteIdsAsync(Guid scribbleId) =>
        await _context.NoteScribbleLinks
            .Where(l => l.ScribbleId == scribbleId)
            .Select(l => l.NoteId)
            .ToListAsync();
}
