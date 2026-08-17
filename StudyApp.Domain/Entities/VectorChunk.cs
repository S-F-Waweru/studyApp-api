using System.Numerics;
using Vector = Pgvector.Vector;
namespace StudyApp.Domain.Entities;

public class VectorChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceId { get; set; }
    public string SourceType { get; set; } = default!;   // "note" | "scribble" | "document"
    public Guid ScopeId { get; set; }
    public string ScopeType { get; set; } = default!;     // "workspace" | "folder"
    public string ChunkText { get; set; } = default!;
    public Vector Embedding { get; set; } = default!;      // pgvector column, 768 dims for nomic-embed-text
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
