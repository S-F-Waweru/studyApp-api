using Microsoft.EntityFrameworkCore;
using StudyApp.Domain.Entities;


namespace StudyApp.Infrastructure.Persistence;
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<Note> Notes => Set<Note>();

    public DbSet<Scribble> Scribbles => Set<Scribble>();
    public DbSet<NoteScribbleLink> NoteScribbleLinks => Set<NoteScribbleLink>();

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<VectorChunk> VectorChunks => Set<VectorChunk>();

    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    public DbSet<AiSettings> AiSettings => Set<AiSettings>();
    public DbSet<ChatSessionScope> ChatSessionScopes => Set<ChatSessionScope>();



    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // enables the Vector type mapping — required alongside the UseNpgsql(...).UseVector() call in Program.cs
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.HasKey(w => w.Id);
            entity.Property(w => w.Name).IsRequired().HasMaxLength(200);
            entity.Property(w => w.Description).HasMaxLength(2000);
            entity.Property(w => w.CustomInstructions).HasMaxLength(4000);

        });

        modelBuilder.Entity<Folder>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.Property(f => f.Name).IsRequired().HasMaxLength(200);
            entity.HasOne<Folder>()
                  .WithMany()
                  .HasForeignKey(f => f.ParentFolderId)
                  .OnDelete(DeleteBehavior.Restrict); // avoid cascade surprises on nested folders
        });

        modelBuilder.Entity<Note>(entity =>
        {
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Title).IsRequired().HasMaxLength(300);
            entity.Property(n => n.ScopeType).HasConversion<string>(); // stored as text, not int — readable in the DB, safe to reorder enum values later
            entity.HasIndex(n => new { n.ScopeId, n.ScopeType }); // every scoped query filters on this pair
        });

        modelBuilder.Entity<NoteScribbleLink>(entity =>
        {
            entity.HasKey(l => new { l.NoteId, l.ScribbleId }); // composite key — pure join table, no surrogate id needed
        });


        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Filename).IsRequired().HasMaxLength(300);
            entity.Property(d => d.ScopeType).HasConversion<string>();
            entity.Property(d => d.ContentType).HasMaxLength(200);
            entity.Property(d => d.ExtractionError).HasMaxLength(2000);
            entity.Property(d => d.IndexStatus).HasConversion<string>(); // readable in the DB, safe to reorder
            entity.HasIndex(d => new { d.ScopeId, d.ScopeType });
        });

        modelBuilder.Entity<Scribble>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Title).IsRequired().HasMaxLength(300);
            entity.Property(s => s.ScopeType).HasConversion<string>();
            entity.Property(s => s.CanvasData).HasColumnType("jsonb");
            entity.Property(s => s.ExtractionError).HasMaxLength(2000);
            entity.Property(s => s.IndexStatus).HasConversion<string>();
            entity.HasIndex(s => new { s.ScopeId, s.ScopeType });
        });

        modelBuilder.Entity<VectorChunk>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.SourceType).HasMaxLength(20);
            entity.Property(v => v.ScopeType).HasMaxLength(20);
            entity.Property(v => v.SourceTitle).HasMaxLength(300);
            entity.Property(v => v.HeadingPath).HasMaxLength(500);
            entity.Property(v => v.Locator).HasMaxLength(100);
            entity.Property(v => v.Kind).HasMaxLength(20);
            entity.Property(v => v.ContentHash).HasMaxLength(64);
            entity.Property(v => v.Embedding).HasColumnType("vector(768)"); // nomic-embed-text dimension
            entity.HasIndex(v => v.SourceId);
            entity.HasIndex(v => new { v.ScopeId, v.ScopeType });
            entity.HasIndex(v => v.PipelineVersion);              // reindex job scans on this
        });


        // inside OnModelCreating:
        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.ScopeType).HasConversion<string>();
            entity.Property(s => s.Title).HasMaxLength(300);
            entity.HasIndex(s => new { s.ScopeId, s.ScopeType });
            entity.HasMany(s => s.Targets)
                  .WithOne()
                  .HasForeignKey(t => t.ChatSessionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatSessionScope>(entity =>
        {
            entity.HasKey(t => new { t.ChatSessionId, t.TargetType, t.TargetId }); // idempotent targets
            entity.Property(t => t.TargetType).HasConversion<string>();
            entity.HasIndex(t => new { t.TargetType, t.TargetId });                // reverse lookup
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Role).HasConversion<string>();
            entity.Property(m => m.RetrievedChunkIds).HasColumnType("uuid[]"); // Postgres array — matches schema §13
            entity.HasIndex(m => m.ChatSessionId);
        });

        modelBuilder.Entity<AiSettings>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.ChatProvider).HasConversion<string>();
            entity.Property(s => s.EmbeddingProvider).HasConversion<string>();
            entity.Property(s => s.ChatBaseUrl).IsRequired().HasMaxLength(500);
            entity.Property(s => s.ChatModel).IsRequired().HasMaxLength(200);
            entity.Property(s => s.ChatApiKey).HasMaxLength(500);
            entity.Property(s => s.EmbeddingBaseUrl).IsRequired().HasMaxLength(500);
            entity.Property(s => s.EmbeddingModel).IsRequired().HasMaxLength(200);
            entity.Property(s => s.EmbeddingApiKey).HasMaxLength(500);
        });
    }

}
