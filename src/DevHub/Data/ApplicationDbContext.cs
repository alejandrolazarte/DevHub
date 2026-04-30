using System.Text.Json;
using DevHub.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DevHub.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<RepoCatalogEntry> RepoCatalogEntries => Set<RepoCatalogEntry>();
    public DbSet<GroupRule> GroupRules => Set<GroupRule>();
    public DbSet<CustomRepoCommand> CustomRepoCommands => Set<CustomRepoCommand>();
    public DbSet<HiddenAutoCommand> HiddenAutoCommands => Set<HiddenAutoCommand>();
    public DbSet<CanvasBoard> Canvases => Set<CanvasBoard>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RepoCatalogEntry>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Path).HasMaxLength(1024).IsRequired();
            e.HasIndex(r => r.Path).IsUnique();
        });

        modelBuilder.Entity<GroupRule>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Name).HasMaxLength(256).IsRequired();
            e.Property(r => r.Color).HasMaxLength(64).IsRequired();
            var prefixesComparer = new ValueComparer<List<string>>(
                (a, b) => a != null && b != null && a.SequenceEqual(b),
                c => c.Aggregate(0, (h, v) => HashCode.Combine(h, v.GetHashCode())),
                c => c.ToList());

            e.Property(r => r.Prefixes)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
                .HasColumnType("TEXT")
                .IsRequired()
                .Metadata.SetValueComparer(prefixesComparer);
        });

        modelBuilder.Entity<CustomRepoCommand>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.RepoPath).HasMaxLength(1024).IsRequired();
            e.Property(c => c.Name).HasMaxLength(256).IsRequired();
            e.Property(c => c.Command).HasMaxLength(1024).IsRequired();
        });

        modelBuilder.Entity<HiddenAutoCommand>(e =>
        {
            e.HasKey(h => h.Id);
            e.Property(h => h.RepoPath).HasMaxLength(1024).IsRequired();
            e.Property(h => h.Name).HasMaxLength(256).IsRequired();
            e.HasIndex(h => new { h.RepoPath, h.Name }).IsUnique();
        });

        modelBuilder.Entity<CanvasBoard>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).HasMaxLength(256).IsRequired();
            e.Property(c => c.CytoscapeJson).HasColumnType("TEXT").IsRequired();
            e.HasIndex(c => c.Name).IsUnique();
        });
    }
}
