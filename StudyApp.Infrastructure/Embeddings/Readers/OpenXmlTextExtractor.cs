using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;
using StudyApp.Application.Embeddings;

namespace StudyApp.Infrastructure.Embeddings.Readers;

public class OpenXmlTextExtractor
{
    public ExtractionResult ExtractDocx(Stream stream)
    {
        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return ExtractionResult.Failed("Document body is empty.");

        var blocks = new List<ExtractedBlock>();
        var headingStack = new List<(int Level, string Text)>();
        var order = 0;

        foreach (var element in body.Elements())
        {
            switch (element)
            {
                case Paragraph p when !string.IsNullOrWhiteSpace(p.InnerText):
                    var style = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
                    var level = style is not null && style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase)
                        && int.TryParse(style.Replace("Heading", "").Trim(), out var l) ? l : 0;

                    if (level > 0)
                    {
                        headingStack.RemoveAll(h => h.Level >= level);
                        headingStack.Add((level, p.InnerText.Trim()));
                        blocks.Add(new ExtractedBlock(BlockKind.Heading, p.InnerText.Trim(),
                            Current(headingStack), null, order++));
                    }
                    else
                    {
                        blocks.Add(new ExtractedBlock(BlockKind.Text, p.InnerText.Trim(),
                            Current(headingStack), null, order++));
                    }
                    break;

                case Table table:
                    var rows = table.Elements<TableRow>().Select(r =>
                        string.Join(" | ", r.Elements<TableCell>().Select(c => c.InnerText.Trim())));
                    blocks.Add(new ExtractedBlock(BlockKind.Table, string.Join("\n", rows),
                        Current(headingStack), null, order++));
                    break;
            }
        }

        return new ExtractionResult(blocks);
    }

    public ExtractionResult ExtractPptx(Stream stream)
    {
        using var doc = PresentationDocument.Open(stream, false);
        var blocks = new List<ExtractedBlock>();
        var order = 0;

        foreach (var (slide, index) in (doc.PresentationPart?.SlideParts ?? []).Select((s, i) => (s, i + 1)))
        {
            var title = slide.Slide.Descendants<P.TextBody>()
                .SelectMany(t => t.Descendants<A.Paragraph>())
                .Select(p => string.Concat(p.Descendants<A.Text>().Select(t => t.Text)))
                .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? $"Slide {index}";

            blocks.Add(new ExtractedBlock(BlockKind.Heading, title, null, $"slide {index}", order++));

            var body = string.Join("\n", slide.Slide.Descendants<P.TextBody>()
                .SelectMany(t => t.Descendants<A.Paragraph>())
                .Select(p => string.Concat(p.Descendants<A.Text>().Select(t => t.Text)))
                .Where(t => !string.IsNullOrWhiteSpace(t) && t != title));

            if (!string.IsNullOrWhiteSpace(body))
                blocks.Add(new ExtractedBlock(BlockKind.Text, body, title, $"slide {index}", order++));
        }

        return new ExtractionResult(blocks, PageCount: blocks.Count);
    }

    private static string? Current(List<(int Level, string Text)> stack) =>
        stack.Count == 0 ? null : string.Join(" > ", stack.TakeLast(3).Select(h => h.Text));
}