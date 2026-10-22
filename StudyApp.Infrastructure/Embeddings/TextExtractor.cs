using Microsoft.EntityFrameworkCore;
using StudyApp.Application.Embeddings;
using StudyApp.Application.Storage;
using StudyApp.Infrastructure.Embeddings.Readers;
using StudyApp.Infrastructure.Persistence;

namespace StudyApp.Infrastructure.Embeddings;

public class TextExtractor : ITextExtractor
{
    private readonly AppDbContext _context;
    private readonly IFileStorage _storage;
    private readonly PdfTextExtractor _pdf;
    private readonly OpenXmlTextExtractor _openXml;
    private readonly SpreadsheetTextExtractor _spreadsheet;
    private readonly MarkdownTextExtractor _markdown;

    public TextExtractor(AppDbContext context, IFileStorage storage, PdfTextExtractor pdf,
        OpenXmlTextExtractor openXml, SpreadsheetTextExtractor spreadsheet, MarkdownTextExtractor markdown)
    {
        _context = context; _storage = storage;
        _pdf = pdf; _openXml = openXml; _spreadsheet = spreadsheet; _markdown = markdown;
    }

    public async Task<string> FingerprintAsync(string sourceType, Guid sourceId, CancellationToken ct = default)
    {
        switch (sourceType)
        {
            case "document":
                var doc = await _context.Documents.FindAsync([sourceId], ct);
                return doc is null ? "" : await HashStreamAsync(doc.StoragePath);

            case "scribble":
                var scribble = await _context.Scribbles.FindAsync([sourceId], ct);
                // Notes/Scribbles: hash the *content*, never the raw canvas JSON, so
                // moving a box on the canvas does not trigger re-embedding.
                return scribble is null ? "" : HashString(scribble.Title + "\u0000" + scribble.CanvasData.Length + scribble.CanvasData);

            case "note":
                var note = await _context.Notes.FindAsync([sourceId], ct);
                return note is null ? "" : HashString(note.Title + "\u0000" + note.Content);

            default: return "";
        }
    }

    public async Task<ExtractionResult> ExtractAsync(string sourceType, Guid sourceId, CancellationToken ct = default) =>
        sourceType switch
        {
            "note" => await ExtractNoteAsync(sourceId, ct),
            "scribble" => await ExtractScribbleAsync(sourceId, ct), // Phase 6
            "document" => await ExtractDocumentAsync(sourceId, ct),
            _ => ExtractionResult.Failed($"Unsupported source type '{sourceType}'.")
        };

    private async Task<ExtractionResult> ExtractNoteAsync(Guid sourceId, CancellationToken ct)
    {
        var note = await _context.Notes.FindAsync([sourceId], ct);
        if (note is null) return ExtractionResult.Failed("Note not found.");

        var blocks = new List<ExtractedBlock>
        {
            new(BlockKind.Heading, note.Title, null, null, 0),
            new(BlockKind.Text, note.Content, note.Title, null, 1)
        };
        return new ExtractionResult(blocks, SuggestedTitle: note.Title);
    }

    private async Task<ExtractionResult> ExtractDocumentAsync(Guid sourceId, CancellationToken ct)
    {
        var doc = await _context.Documents.FindAsync([sourceId], ct);
        if (doc is null) return ExtractionResult.Failed("Document not found.");

        await using var stream = await _storage.OpenReadAsync(doc.StoragePath);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var type = FileTypeDetector.Detect(bytes.AsSpan(0, Math.Min(64, bytes.Length)), doc.Filename);

        return type switch
        {
            DetectedFileType.Pdf => _pdf.Extract(bytes),
            DetectedFileType.Docx => _openXml.ExtractDocx(new MemoryStream(bytes)),
            DetectedFileType.Pptx => _openXml.ExtractPptx(new MemoryStream(bytes)),
            DetectedFileType.Xlsx => _spreadsheet.Extract(new MemoryStream(bytes)),
            DetectedFileType.Markdown => _markdown.Extract(System.Text.Encoding.UTF8.GetString(bytes)),
            DetectedFileType.PlainText => new ExtractionResult(new List<ExtractedBlock>
            {
                new(BlockKind.Text, System.Text.Encoding.UTF8.GetString(bytes), null, null, 0)
            }),
            DetectedFileType.Image => new ExtractionResult(new List<ExtractedBlock>
            {
                new(BlockKind.Image, null, null, "p.1", 0, bytes, "image/png")
            }),
            _ => ExtractionResult.Failed($"Unsupported file type for '{doc.Filename}'.")
        } is var result
            ? result with { SuggestedTitle = doc.Filename }
            : result;
    }

    private async Task<ExtractionResult> ExtractScribbleAsync(Guid sourceId, CancellationToken ct)
    {
        var scribble = await _context.Scribbles.FindAsync([sourceId], ct);
        if (scribble is null) return ExtractionResult.Failed("Scribble not found.");
        return new ExtractionResult(new List<ExtractedBlock>
        {
            new(BlockKind.Heading, scribble.Title, null, null, 0)
        }, SuggestedTitle: scribble.Title);
    }

    private async Task<string> HashStreamAsync(string relativePath)
    {
        await using var stream = await _storage.OpenReadAsync(relativePath);
        return Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(stream));
    }

    private static string HashString(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}
