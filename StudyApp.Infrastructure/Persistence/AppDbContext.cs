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

        modelBuilder.Entity<Scribble>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Title).IsRequired().HasMaxLength(300);
            entity.Property(s => s.ScopeType).HasConversion<string>();
            entity.Property(s => s.CanvasData).HasColumnType("jsonb");
            entity.HasIndex(s => new { s.ScopeId, s.ScopeType });
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
            entity.HasIndex(d => new { d.ScopeId, d.ScopeType });
        });

        modelBuilder.Entity<VectorChunk>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.SourceType).HasMaxLength(20);
            entity.Property(v => v.ScopeType).HasMaxLength(20);
            entity.Property(v => v.Embedding).HasColumnType("vector(768)"); // nomic-embed-text dimension
            entity.HasIndex(v => v.SourceId);
            entity.HasIndex(v => new { v.ScopeId, v.ScopeType });
        });


        // inside OnModelCreating:
        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.ScopeType).HasConversion<string>();
            entity.HasIndex(s => new { s.ScopeId, s.ScopeType });
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Role).HasConversion<string>();
            entity.Property(m => m.RetrievedChunkIds).HasColumnType("uuid[]"); // Postgres array — matches schema §13
            entity.HasIndex(m => m.ChatSessionId);
        });
    }

}
