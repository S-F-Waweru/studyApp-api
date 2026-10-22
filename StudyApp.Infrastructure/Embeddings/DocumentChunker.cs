using System.Security.Cryptography;
using System.Text;
using StudyApp.Application.Embeddings;

namespace StudyApp.Infrastructure.Embeddings;

public record ChunkSpec(
    string Text,
    string EmbedText,
    string? HeadingPath,
    string? Locator,
    string Kind,
    string ContentHash,
    string? SourceTitle,
    byte[]? ImageBytes = null,
    string? ImageContentType = null);

public interface IDocumentChunker
{
    IReadOnlyList<ChunkSpec> Chunk(IReadOnlyList<ExtractedBlock> blocks);
}

public class DocumentChunker : IDocumentChunker
{
    private const int TargetTokens = 500;      // ~2000 chars, comfortable for nomic-embed-text
    private const double OverlapRatio = 0.12;  // sentence-level overlap between sibling chunks

    public IReadOnlyList<ChunkSpec> Chunk(IReadOnlyList<ExtractedBlock> blocks)
    {
        var output = new List<ChunkSpec>();
        var buffer = new List<ExtractedBlock>();

        void Flush()
        {
            if (buffer.Count == 0) return;
            output.AddRange(PackBlocks(buffer));
            buffer.Clear();
        }

        foreach (var block in blocks.OrderBy(b => b.Order))
        {
            // Headings never consume a chunk on their own — they set context for what follows.
            if (block.Kind == BlockKind.Heading) { Flush(); continue; }

            // Atomic blocks: emit whole, never merged, never split.
            if (block.Kind is BlockKind.Table or BlockKind.Code or BlockKind.Image or BlockKind.Caption)
            {
                Flush();
                if (!string.IsNullOrWhiteSpace(block.Text))
                    output.Add(MakeSpec(block, block.Text!, block.Kind.ToString().ToLowerInvariant()));
                continue;
            }

            // A heading change is a natural chunk boundary.
            if (buffer.Count > 0 && buffer[^1].HeadingPath != block.HeadingPath) Flush();

            buffer.Add(block);
            if (buffer.Sum(b => ExtractionPipeline.EstimateTokens(b.Text ?? "")) >= TargetTokens) Flush();
        }

        Flush();
        return output;
    }

    private IEnumerable<ChunkSpec> PackBlocks(List<ExtractedBlock> blocks)
    {
        var text = string.Join("\n\n", blocks.Select(b => b.Text!.Trim()));
        var heading = blocks[0].HeadingPath;
        var locator = blocks[0].Locator;

        // Split on sentence boundaries if a single packed run overshot.
        foreach (var part in SplitBySentences(text, TargetTokens))
            yield return MakeSpec(blocks[0], part, "text", heading, locator);
    }

    private static IEnumerable<string> SplitBySentences(string text, int targetTokens)
    {
        if (ExtractionPipeline.EstimateTokens(text) <= targetTokens) { yield return text; yield break; }

        var sentences = text.Split(". ", StringSplitOptions.RemoveEmptyEntries);
        var current = new StringBuilder();
        var currentTokens = 0;

        foreach (var sentence in sentences)
        {
            var tokens = ExtractionPipeline.EstimateTokens(sentence);
            if (currentTokens + tokens > targetTokens && current.Length > 0)
            {
                yield return current.ToString().Trim();
                // overlap: keep the last sentence for continuity
                var last = current.ToString().Trim();
                var lastSentence = last[(last.LastIndexOf('.') + 1)..].Trim();
                current.Clear();
                if (lastSentence.Length > 0) current.Append(lastSentence).Append(". ");
                currentTokens = ExtractionPipeline.EstimateTokens(current.ToString());
            }

            current.Append(sentence).Append(". ");
            currentTokens += tokens;
        }

        if (current.Length > 0) yield return current.ToString().Trim();
    }

    private static ChunkSpec MakeSpec(ExtractedBlock block, string text, string kind,
                                      string? heading = null, string? locator = null)
    {
        heading ??= block.HeadingPath;
        locator ??= block.Locator;

        // Breadcrumb is embedded but is NOT duplicated in what the model reads... it is
        // included once at the top; harmless and helps the model cite a section name.
        var embedText = string.IsNullOrWhiteSpace(heading) ? text : $"{heading}\n\n{text}";

        // Captions carry the same bytes as the figure they describe, so the worker can
        // persist the image next to its searchable description.
        var carriesImage = block.Kind is BlockKind.Image or BlockKind.Caption;

        return new ChunkSpec(
            Text: text,
            EmbedText: embedText,
            HeadingPath: heading,
            Locator: locator,
            Kind: kind,
            ContentHash: Hash(text),
            SourceTitle: null,
            ImageBytes: carriesImage ? block.ImageBytes : null,
            ImageContentType: carriesImage ? block.ImageContentType : null);
    }

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))));
}