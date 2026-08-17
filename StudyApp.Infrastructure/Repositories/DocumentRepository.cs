using Microsoft.EntityFrameworkCore;
using StudyApp.Application.Repositories;
using StudyApp.Domain.Entities;
using StudyApp.Domain.Enums;
using StudyApp.Infrastructure.Persistence;

namespace StudyApp.Infrastructure.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly AppDbContext _context;

    public DocumentRepository(AppDbContext context) => _context = context;

    public async Task<Document?> GetByIdAsync(Guid id) => await _context.Documents.FindAsync(id);

    public async Task<IEnumerable<Document>> GetByScopeAsync(Guid scopeId, ScopeType scopeType) =>
        await _context.Documents
            .Where(d => d.ScopeId == scopeId && d.ScopeType == scopeType)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

    public async Task AddAsync(Document document)
    {
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var document = await _context.Documents.FindAsync(id);
        if (document is not null)
        {
            _context.Documents.Remove(document);
            await _context.SaveChangesAsync();
        }
    }
}
