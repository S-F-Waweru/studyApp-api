using StudyApp.Application.Embeddings;
using StudyApp.Application.Storage;
using StudyApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace StudyApp.Infrastructure.Embeddings;

public class TextExtractor : ITextExtractor
{
    private readonly AppDbContext _context;
    private readonly IFileStorage _storage;

    public TextExtractor(AppDbContext context, IFileStorage storage)
    {
        _context = context;
        _storage = storage;
    }

    public async Task<string?> ExtractAsync(string sourceType, Guid sourceId) => sourceType switch
    {
        "note" => (await _context.Notes.FindAsync(sourceId))?.Content,
        "scribble" => (await _context.Scribbles.FindAsync(sourceId))?.ExtractedText, // null until captioned/OCR'd — worker just skips embedding that turn
        "document" => await ExtractDocumentTextAsync(sourceId),
        _ => null
    };

    private async Task<string?> ExtractDocumentTextAsync(Guid sourceId)
    {
        var doc = await _context.Documents.FindAsync(sourceId);
        if (doc is null) return null;

        // MVP: plain-text files only. PDFs/docx extraction is a later upgrade behind this same interface.
        if (!doc.Filename.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) return null;

        await using var stream = await _storage.OpenReadAsync(doc.StoragePath);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
