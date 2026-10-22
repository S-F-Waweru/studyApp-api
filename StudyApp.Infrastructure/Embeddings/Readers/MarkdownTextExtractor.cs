using Markdig;
using Markdig.Syntax;
using StudyApp.Application.Embeddings;

namespace StudyApp.Infrastructure.Embeddings.Readers;

public class MarkdownTextExtractor
{
    public ExtractionResult Extract(string markdown)
    {
        var document = Markdown.Parse(markdown);
        var blocks = new List<ExtractedBlock>();
        var headingStack = new List<(int Level, string Text)>();
        var order = 0;

        foreach (var block in document)
        {
            if (block is HeadingBlock heading)
            {
                var text = heading.Inline?.FirstChild?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text)) continue;

                headingStack.RemoveAll(h => h.Level >= heading.Level);
                headingStack.Add((heading.Level, text));
                blocks.Add(new ExtractedBlock(BlockKind.Heading, text,
                    headingStack.Count == 0 ? null : string.Join(" > ", headingStack.Select(h => h.Text)), null, order++));
            }
            else if (block is FencedCodeBlock code)
            {
                blocks.Add(new ExtractedBlock(BlockKind.Code, code.Lines.ToString(), Current(headingStack), null, order++));
            }
            else if (block is ParagraphBlock paragraph)
            {
                var text = paragraph.Inline?.FirstChild?.ToString();
                if (string.IsNullOrWhiteSpace(text)) continue;
                blocks.Add(new ExtractedBlock(BlockKind.Text, text, Current(headingStack), null, order++));
            }
        }

        return new ExtractionResult(blocks);
    }

    private static string? Current(List<(int Level, string Text)> stack) =>
        stack.Count == 0 ? null : string.Join(" > ", stack.TakeLast(3).Select(h => h.Text));
}