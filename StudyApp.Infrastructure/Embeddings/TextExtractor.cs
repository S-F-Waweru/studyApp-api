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
    private readonly ScribbleCanvasExtractor _canvas;
    private readonly IImageCaptioner _captioner;

    public TextExtractor(AppDbContext context, IFileStorage storage, PdfTextExtractor pdf,
        OpenXmlTextExtractor openXml, SpreadsheetTextExtractor spreadsheet, MarkdownTextExtractor markdown,
        ScribbleCanvasExtractor canvas, IImageCaptioner captioner)
    {
        _context = context; _storage = storage;
        _pdf = pdf; _openXml = openXml; _spreadsheet = spreadsheet; _markdown = markdown;
        _canvas = canvas; _captioner = captioner;
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
                if (scribble is null) return "";

                // Hash the *extracted* content, never the raw scene: dragging a shape must not
                // trigger re-embedding, but editing text or pasting an image must.
                var (canvasResult, canvasImages) = _canvas.Extract(scribble.Title, scribble.CanvasData);
                var canvasText = string.Join("\n", canvasResult.Blocks.Select(b => $"{b.Kind}|{b.HeadingPath}|{b.Text}"));
                var canvasMedia = string.Join(",", canvasImages.Select(i => i.Bytes.Length));

                return HashString($"{scribble.Title}\u0000{canvasText}\u0000{canvasMedia}");

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

        // after ExtractionResult is produced, enrich image blocks with captions
        var result = type switch
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
        };

        if (result.FailureReason is null)
            result = result with { SuggestedTitle = doc.Filename };

        if (result.Blocks.Any(b => b.Kind == BlockKind.Image))
            result = await CaptionImagesAsync(result, ct);

        return result;
    }

    private async Task<ExtractionResult> ExtractScribbleAsync(Guid sourceId, CancellationToken ct)
    {
        var scribble = await _context.Scribbles.FindAsync([sourceId], ct);
        if (scribble is null) return ExtractionResult.Failed("Scribble not found.");

        // Typed canvas text is read straight out of the Excalidraw scene — no OCR needed for it.
        var (result, images) = _canvas.Extract(scribble.Title, scribble.CanvasData);
        if (result.FailureReason is not null) return result;

        // Pasted screenshots become image blocks so they go through the same caption path as documents.
        var blocks = result.Blocks
            .Concat(images.Select((img, i) => new ExtractedBlock(
                BlockKind.Image, null, img.FrameName, img.FrameName, result.Blocks.Count + i,
                img.Bytes, img.ContentType)))
            .ToList();

        var enriched = result with { Blocks = blocks };
        return images.Count > 0 ? await CaptionImagesAsync(enriched, ct) : enriched;
    }

    private async Task<string> HashStreamAsync(string relativePath)
    {
        await using var stream = await _storage.OpenReadAsync(relativePath);
        return Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(stream));
    }

    private static string HashString(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    private async Task<ExtractionResult> CaptionImagesAsync(ExtractionResult result, CancellationToken ct)
    {
        var blocks = new List<ExtractedBlock>(result.Blocks.Count);

        foreach (var block in result.Blocks)
        {
            if (block.Kind != BlockKind.Image || block.ImageBytes is null) { blocks.Add(block); continue; }

            // skip icons/logos: real diagrams are large
            if (block.ImageBytes.Length < 12_000) { blocks.Add(block); continue; }

            var caption = await _captioner.CaptionAsync(block.ImageBytes, block.ImageContentType ?? "image/png", ct);
            if (caption is null) { blocks.Add(block); continue; } // best-effort: keep the raw image block

            // The caption keeps the bytes so the worker persists exactly one image per figure
            // and indexes the description as searchable text.
            blocks.Add(new ExtractedBlock(BlockKind.Caption, caption, block.HeadingPath, block.Locator,
                                          block.Order, block.ImageBytes, block.ImageContentType));
        }

        return result with { Blocks = blocks };
    }
}
