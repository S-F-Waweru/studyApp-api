namespace StudyApp.Infrastructure.Embeddings;

public enum DetectedFileType { Pdf, Docx, Pptx, Xlsx, Markdown, PlainText, Image, Unknown }

public static class FileTypeDetector
{
    public static DetectedFileType Detect(ReadOnlySpan<byte> head, string filename)
    {
        if (head.Length >= 5 && head[..5].SequenceEqual("%PDF-"u8)) return DetectedFileType.Pdf;

        // ZIP magic — need the inner entry to distinguish Office formats
        if (head.Length >= 4 && head[0] == 0x50 && head[1] == 0x4B && head[2] == 0x03 && head[3] == 0x04)
            return ByExtension(filename);

        if (head.Length >= 8 && head[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return DetectedFileType.Image; // PNG
        if (head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
            return DetectedFileType.Image; // JPEG

        return ByExtension(filename);
    }

    private static DetectedFileType ByExtension(string filename) =>
        Path.GetExtension(filename).ToLowerInvariant() switch
        {
            ".pdf" => DetectedFileType.Pdf,
            ".docx" => DetectedFileType.Docx,
            ".pptx" => DetectedFileType.Pptx,
            ".xlsx" => DetectedFileType.Xlsx,
            ".md" or ".markdown" => DetectedFileType.Markdown,
            ".txt" or ".csv" or ".json" or ".cs" or ".ts" or ".sql" => DetectedFileType.PlainText,
            ".png" or ".jpg" or ".jpeg" or ".webp" => DetectedFileType.Image,
            _ => DetectedFileType.Unknown
        };
}