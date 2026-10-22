namespace StudyApp.Application.Embeddings;

public enum BlockKind { Text, Heading, Table, Code, Image , Caption }

public record ExtractedBlock(
     BlockKind Kind,
     string? Text,
     string? HeadingPath,
     string? Locator, // "p.42" | "sheet:Overview" | "frame:Networking"
     int Order,
     byte[]? ImageBytes = null,
     string? ImageContentType = null
);

public record ExtractionResult(
     IReadOnlyList<ExtractedBlock> Blocks,
     int? PageCount = null,
     string? FailureReason = null,
     string? SuggestedTitle = null
     ){
     public static ExtractionResult Failed (string reason) =>  new(Array.Empty<ExtractedBlock>(), FailureReason:reason);

     //check the validity of this  : Event Processing Worker
     // Nothing worth chunking: no block carries text or image bytes.
     public bool IsEmpty => Blocks.Count == 0 ||
                            Blocks.All(b => string.IsNullOrWhiteSpace(b.Text) && b.ImageBytes is not { Length: > 0 });
     }