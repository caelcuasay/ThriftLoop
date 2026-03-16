using Microsoft.EntityFrameworkCore;
using ThriftLoop.Models;

namespace ThriftLoop.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // ── DbSets ───────────────────────────────────────────────────────────────
    public DbSet<User> Users => Set<User>();
    public DbSet<Item> Items => Set<Item>();   // ← ADD THIS LINE

    // ── Model Configuration ──────────────────────────────────────────────────
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Users (unchanged) ────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).ValueGeneratedOnAdd();
            entity.Property(u => u.Email)
                  .IsRequired()
                  .HasMaxLength(256)
                  .IsUnicode(false);
            entity.HasIndex(u => u.Email)
                  .IsUnique()
                  .HasDatabaseName("UQ_Users_Email");
            entity.Property(u => u.PasswordHash)
                  .IsRequired(false)
                  .HasMaxLength(512)
                  .IsUnicode(false);
            entity.Property(u => u.CreatedAt)
                  .IsRequired()
                  .HasColumnType("datetime2")
                  .HasDefaultValueSql("SYSUTCDATETIME()");
        });

        // ── Items (new) ──────────────────────────────────────────────────
        modelBuilder.Entity<Item>(entity =>
        {
            entity.ToTable("Items");

            entity.HasKey(i => i.Id);

            entity.Property(i => i.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(i => i.Title)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(i => i.Description)
                  .IsRequired()
                  .HasMaxLength(1000);

            entity.Property(i => i.Price)
                  .IsRequired()
                  .HasColumnType("decimal(10,2)");

            entity.Property(i => i.Category)
                  .IsRequired()
                  .HasMaxLength(50);

            entity.Property(i => i.Condition)
                  .IsRequired()
                  .HasMaxLength(50);

            entity.Property(i => i.ImageUrl)
                  .IsRequired(false)
                  .HasMaxLength(500);

            entity.Property(i => i.CreatedAt)
                  .IsRequired()
                  .HasColumnType("datetime2")
                  .HasDefaultValueSql("SYSUTCDATETIME()");

            // ── Relationship: Item → User (many-to-one) ──────────────────
            entity.HasOne(i => i.User)
                  .WithMany()                       // User has no Items collection yet
                  .HasForeignKey(i => i.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}