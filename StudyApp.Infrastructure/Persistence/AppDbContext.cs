using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudyApp.Domain.Entities;
using StudyApp.Infrastructure.Identity;


namespace StudyApp.Infrastructure.Persistence;
public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
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
    
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();



    // protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    // {
    //     // enables the Vector type mapping — required alongside the UseNpgsql(...).UseVector() call in Program.cs
    // }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.HasKey(w => w.Id);
            entity.Property(w => w.Name).IsRequired().HasMaxLength(200);
            entity.Property(w => w.Description).HasMaxLength(2000);
            entity.Property(w => w.CustomInstructions).HasMaxLength(4000);
            entity.HasIndex(w => w.OwnerUserId);
            entity.HasOne<AppUser>()
                    .WithMany()
                    .HasForeignKey(w => w.OwnerUserId)
                    .OnDelete(DeleteBehavior.Restrict);
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
            entity.HasIndex(n => new { n.OwnerUserId, n.ScopeId, n.ScopeType }); // owner-first: every scoped query carries the owner filter
            entity.HasOne<AppUser>()
                  .WithMany()
                  .HasForeignKey(n => n.OwnerUserId)
                  .OnDelete(DeleteBehavior.Restrict);
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
            entity.HasIndex(d => new { d.OwnerUserId, d.ScopeId, d.ScopeType });
            entity.HasOne<AppUser>()
                  .WithMany()
                  .HasForeignKey(d => d.OwnerUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Scribble>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Title).IsRequired().HasMaxLength(300);
            entity.Property(s => s.ScopeType).HasConversion<string>();
            entity.Property(s => s.CanvasData).HasColumnType("jsonb");
            entity.Property(s => s.ExtractionError).HasMaxLength(2000);
            entity.Property(s => s.IndexStatus).HasConversion<string>();
            entity.HasIndex(s => new { s.OwnerUserId, s.ScopeId, s.ScopeType });
            entity.HasOne<AppUser>()
                  .WithMany()
                  .HasForeignKey(s => s.OwnerUserId)
                  .OnDelete(DeleteBehavior.Restrict);
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
            entity.HasOne<AppUser>()
                  .WithMany()
                  .HasForeignKey(n => n.OwnerUserId)
                  .OnDelete(DeleteBehavior.Restrict);
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


        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.TokenHash).IsRequired().HasMaxLength(64); // SHA-256 as uppercase hex
            entity.Property(t => t.RevocationReason).HasMaxLength(50);

            // unique: a hash collision would let one token authenticate as another
            entity.HasIndex(t => t.TokenHash).IsUnique();

            // every refresh lookup is by hash, but revocation is by family or by user
            entity.HasIndex(t => new { t.UserId, t.FamilyId });
        });
    }
    
    

}
