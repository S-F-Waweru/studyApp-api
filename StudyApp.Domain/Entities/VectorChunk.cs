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
    
    
    // Added in Phase 0 — provenance and re-index control
    public string? SourceTitle { get; set; }              // note title / file name / scribble title
    public string? HeadingPath { get; set; }              // "Kubernetes > Networking > Services"
    public string? Locator { get; set; }                  // "p.42" | "sheet:Overview" | "frame:Networking"
    public string Kind { get; set; } = "text";            // text | table | code | image | ocr
    public int ChunkIndex { get; set; }
    public int TokenCount { get; set; }
    public string? ImagePath { get; set; }                // storage-relative path when Kind == "image"
    public string ContentHash { get; set; } = "";         // SHA-256 of the chunk text (dedupe key)
    public int PipelineVersion { get; set; } = 1;  
    
    
    
    public Vector Embedding { get; set; } = default!;      // pgvector column, 768 dims for nomic-embed-text
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
