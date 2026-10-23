using StudyApp.Domain.Enums;

namespace StudyApp.Domain.Entities;

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? OwnerUserId { get; set; } 
    
    public Guid ScopeId { get; set; }
    public ScopeType ScopeType { get; set; }
    public string Filename { get; set; } = default!;
    public string StoragePath { get; set; } = default!; // relative path under /files/documents
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    
    // Phase 0
    public string? ContentType { get; set; }            // sniffed, not trusted from the extension
    public long SizeBytes { get; set; }
    public IndexStatus IndexStatus { get; set; } = IndexStatus.Pending;
    public string? ExtractionError { get; set; }
    public DateTime? IndexedAt { get; set; }
    public int ChunkCount { get; set; }
    public int? PageCount { get; set; }
    public string? ContentHash { get; set; }            // fingerprint of the raw bytes
}
