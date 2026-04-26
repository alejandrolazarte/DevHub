using System.Text.Json;
using DevHub.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DevHub.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<RepoCatalogEntry> RepoCatalogEntries => Set<RepoCatalogEntry>();
    public DbSet<GroupRule> GroupRules => Set<GroupRule>();

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
    }
}
