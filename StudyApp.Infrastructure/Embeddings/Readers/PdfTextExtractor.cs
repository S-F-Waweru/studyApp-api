using StudyApp.Application.Embeddings;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace StudyApp.Infrastructure.Embeddings.Readers;

public class PdfTextExtractor
{
    public ExtractionResult Extract(byte[] bytes)
    {
        using var pdf = PdfDocument.Open(bytes);
        var blocks = new List<ExtractedBlock>();
        var headingStack = new List<(double Size, string Text)>();
        var order = 0;

        // Pass 1: median font size, used to classify headings per page
        var sizes = new List<double>();
        foreach (var page in pdf.GetPages())
            foreach (var word in page.GetWords(NearestNeighbourWordExtractor.Instance))
                if (word.Letters.Count > 0) sizes.Add(word.Letters[0].FontSize);

        var medianSize = sizes.Count == 0 ? 0 : sizes.OrderBy(s => s).ElementAt(sizes.Count / 2);

        foreach (var page in pdf.GetPages())
        {
            var locator = $"p.{page.Number}";
            var words = page.GetWords(NearestNeighbourWordExtractor.Instance).ToList();

            if (words.Count == 0)
            {
                // scanned page or pure diagram — hand it to the vision stage (Phase 5)
                foreach (var image in page.GetImages())
                    if (image.TryGetPng(out var png))
                        blocks.Add(new ExtractedBlock(BlockKind.Image, null, Current(headingStack), locator, order++,
                                                      png, "image/png"));
                continue;
            }

            var lines = words
                .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 0))
                .OrderByDescending(g => g.Key)
                .Select(g => new
                {
                    Text = string.Join(" ", g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)),
                    Size = g.SelectMany(w => w.Letters).Select(l => l.FontSize).DefaultIfEmpty(medianSize).Average(),
                    Left = g.Min(w => w.BoundingBox.Left),
                    Right = g.Max(w => w.BoundingBox.Right)
                });

            foreach (var line in lines)
            {
                var text = line.Text.Trim();
                if (text.Length == 0) continue;

                var isHeading = medianSize > 0 && line.Size > medianSize * 1.15 && text.Length < 120;
                if (isHeading)
                {
                    headingStack.RemoveAll(h => h.Size >= line.Size);
                    headingStack.Add((line.Size, text));
                    blocks.Add(new ExtractedBlock(BlockKind.Heading, text, Current(headingStack), locator, order++));
                }
                else
                {
                    blocks.Add(new ExtractedBlock(BlockKind.Text, text, Current(headingStack), locator, order++));
                }
            }
        }

        return new ExtractionResult(blocks, PageCount: pdf.NumberOfPages);
    }

    private static string? Current(List<(double Size, string Text)> stack) =>
        stack.Count == 0 ? null : string.Join(" > ", stack.TakeLast(3).Select(h => h.Text));
}