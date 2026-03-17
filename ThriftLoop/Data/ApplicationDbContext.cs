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
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Order> Orders => Set<Order>();

    // ── Model Configuration ──────────────────────────────────────────────────
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Users ────────────────────────────────────────────────────────
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

        // ── Items ────────────────────────────────────────────────────────
        modelBuilder.Entity<Item>(entity =>
        {
            entity.ToTable("Items");

            entity.HasKey(i => i.Id);
            entity.Property(i => i.Id).ValueGeneratedOnAdd();

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

            entity.Property(i => i.Size)
                  .IsRequired(false)
                  .HasMaxLength(10);

            entity.Property(i => i.ImageUrl)
                  .IsRequired(false)
                  .HasMaxLength(500);

            entity.Property(i => i.CreatedAt)
                  .IsRequired()
                  .HasColumnType("datetime2")
                  .HasDefaultValueSql("SYSUTCDATETIME()");

            // ── Stealable listing columns ────────────────────────────────
            entity.Property(i => i.ListingType)
                  .IsRequired()
                  .HasDefaultValue(ListingType.Standard);

            entity.Property(i => i.Status)
                  .IsRequired()
                  .HasDefaultValue(ItemStatus.Available);

            entity.Property(i => i.StealDurationHours)
                  .IsRequired(false);

            entity.Property(i => i.StealEndsAt)
                  .IsRequired(false)
                  .HasColumnType("datetime2");

            entity.Property(i => i.CurrentWinnerId)
                  .IsRequired(false);

            // Computed helpers have no backing columns.
            entity.Ignore(i => i.IsInFinalizeWindow);
            entity.Ignore(i => i.FinalizeDeadline);

            // ── Relationship: Item → User (seller) ───────────────────────
            entity.HasOne(i => i.User)
                  .WithMany()
                  .HasForeignKey(i => i.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Orders ───────────────────────────────────────────────────────
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");

            entity.HasKey(o => o.Id);
            entity.Property(o => o.Id).ValueGeneratedOnAdd();

            entity.Property(o => o.FinalPrice)
                  .IsRequired()
                  .HasColumnType("decimal(10,2)");

            entity.Property(o => o.OrderDate)
                  .IsRequired()
                  .HasColumnType("datetime2")
                  .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.Property(o => o.Status)
                  .IsRequired()
                  .HasDefaultValue(OrderStatus.Pending);

            // ── Relationship: Order → Item ───────────────────────────────
            // NoAction: we keep order history even if the item row is ever
            // removed from the marketplace.
            entity.HasOne(o => o.Item)
                  .WithMany()
                  .HasForeignKey(o => o.ItemId)
                  .OnDelete(DeleteBehavior.NoAction);

            // ── Relationship: Order → Buyer (User) ───────────────────────
            // NoAction on both user FKs: SQL Server prohibits multiple cascade
            // paths to the same table, so Cascade would raise a migration error.
            entity.HasOne(o => o.Buyer)
                  .WithMany()
                  .HasForeignKey(o => o.BuyerId)
                  .OnDelete(DeleteBehavior.NoAction);

            // ── Relationship: Order → Seller (User) ──────────────────────
            entity.HasOne(o => o.Seller)
                  .WithMany()
                  .HasForeignKey(o => o.SellerId)
                  .OnDelete(DeleteBehavior.NoAction);
        });
    }
}