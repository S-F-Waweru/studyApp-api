using Microsoft.EntityFrameworkCore;
using StudyApp.Domain.Entities;


namespace StudyApp.Infrastructure.Persistence;
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Folder> Folders => Set<Folder>();


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
    }

}
