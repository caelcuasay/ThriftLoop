using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;
using ThriftLoop.Models;

namespace ThriftLoop.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    // ── DbSets ────────────────────────────────────────────────────────────────
    public DbSet<User> Users => Set<User>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Withdrawal> Withdrawals => Set<Withdrawal>();

    // ── JSON options (shared, thread-safe) ────────────────────────────────────
    private static readonly JsonSerializerOptions _jsonOpts =
        new(JsonSerializerDefaults.Web);

    // ── Model Configuration ───────────────────────────────────────────────────
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Users ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).ValueGeneratedOnAdd();
            entity.Property(u => u.Email)
                  .IsRequired().HasMaxLength(256).IsUnicode(false);
            entity.HasIndex(u => u.Email).IsUnique().HasDatabaseName("UQ_Users_Email");
            entity.Property(u => u.PasswordHash)
                  .IsRequired(false).HasMaxLength(512).IsUnicode(false);
            entity.Property(u => u.CreatedAt)
                  .IsRequired().HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
        });

        // ── Items ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<Item>(entity =>
        {
            entity.ToTable("Items");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Id).ValueGeneratedOnAdd();

            entity.Property(i => i.Title).IsRequired().HasMaxLength(100);
            entity.Property(i => i.Description).IsRequired().HasMaxLength(1000);
            entity.Property(i => i.Price).IsRequired().HasColumnType("decimal(10,2)");
            entity.Property(i => i.Category).IsRequired().HasMaxLength(50);
            entity.Property(i => i.Condition).IsRequired().HasMaxLength(50);
            entity.Property(i => i.Size).IsRequired(false).HasMaxLength(10);

            // ── ImageUrls: stored as a JSON array ─────────────────────────────
            var imageUrlsComparer = new ValueComparer<List<string>>(
                (a, b) => a != null && b != null && a.SequenceEqual(b),
                c => c.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
                c => c.ToList()
            );

            entity.Property(i => i.ImageUrls)
                  .HasColumnName("ImageUrls")
                  .HasColumnType("nvarchar(max)")
                  .HasConversion(
                      v => JsonSerializer.Serialize(v, _jsonOpts),
                      v => string.IsNullOrEmpty(v)
                           ? new List<string>()
                           : JsonSerializer.Deserialize<List<string>>(v, _jsonOpts)
                             ?? new List<string>()
                  )
                  .Metadata.SetValueComparer(imageUrlsComparer);

            entity.Ignore(i => i.ImageUrl);

            entity.Property(i => i.CreatedAt)
                  .IsRequired().HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(i => i.ListingType)
                  .IsRequired().HasDefaultValue(ListingType.Standard);
            entity.Property(i => i.Status)
                  .IsRequired().HasDefaultValue(ItemStatus.Available);
            entity.Property(i => i.StealDurationHours).IsRequired(false);
            entity.Property(i => i.StealEndsAt).IsRequired(false).HasColumnType("datetime2");
            entity.Property(i => i.CurrentWinnerId).IsRequired(false);

            // ── OriginalGetterUserId ───────────────────────────────────────────
            // Populated when a steal begins (saves User A's ID before overwriting
            // CurrentWinnerId with the stealer). Cleared on confirm or cancel.
            // MIGRATION: Add-Migration AddOriginalGetterUserId → Update-Database
            entity.Property(i => i.OriginalGetterUserId).IsRequired(false);

            entity.Ignore(i => i.IsInFinalizeWindow);
            entity.Ignore(i => i.FinalizeDeadline);

            entity.HasOne(i => i.User)
                  .WithMany()
                  .HasForeignKey(i => i.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Orders ────────────────────────────────────────────────────────────
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Id).ValueGeneratedOnAdd();
            entity.Property(o => o.FinalPrice).IsRequired().HasColumnType("decimal(10,2)");
            entity.Property(o => o.OrderDate)
                  .IsRequired().HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(o => o.Status).IsRequired().HasDefaultValue(OrderStatus.Pending);
            entity.Property(o => o.PaymentMethod).IsRequired().HasDefaultValue(PaymentMethod.Wallet);
            entity.Property(o => o.CashCollectedByRider).IsRequired().HasDefaultValue(false);
            entity.HasOne(o => o.Item).WithMany().HasForeignKey(o => o.ItemId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(o => o.Buyer).WithMany().HasForeignKey(o => o.BuyerId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(o => o.Seller).WithMany().HasForeignKey(o => o.SellerId).OnDelete(DeleteBehavior.NoAction);
        });

        // ── Wallets ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.ToTable("Wallets");
            entity.HasKey(w => w.Id);
            entity.Property(w => w.Id).ValueGeneratedOnAdd();
            entity.Property(w => w.Balance).IsRequired().HasColumnType("decimal(12,2)").HasDefaultValue(0m);
            entity.Property(w => w.PendingBalance).IsRequired().HasColumnType("decimal(12,2)").HasDefaultValue(0m);
            entity.Property(w => w.UpdatedAt)
                  .IsRequired().HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasIndex(w => w.UserId).IsUnique().HasDatabaseName("UQ_Wallets_UserId");
            entity.HasOne(w => w.User).WithMany().HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Transactions ──────────────────────────────────────────────────────
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.ToTable("Transactions");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Id).ValueGeneratedOnAdd();
            entity.Property(t => t.Amount).IsRequired().HasColumnType("decimal(12,2)");
            entity.Property(t => t.Type).IsRequired();
            entity.Property(t => t.Status).IsRequired().HasDefaultValue(TransactionStatus.Pending);
            entity.Property(t => t.CreatedAt)
                  .IsRequired().HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(t => t.CompletedAt).IsRequired(false).HasColumnType("datetime2");
            entity.Property(t => t.OrderId).IsRequired(false);
            entity.HasOne(t => t.Order).WithMany().HasForeignKey(t => t.OrderId)
                  .IsRequired(false).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(t => t.FromUser).WithMany().HasForeignKey(t => t.FromUserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(t => t.ToUser).WithMany().HasForeignKey(t => t.ToUserId).OnDelete(DeleteBehavior.NoAction);
        });

        // ── Withdrawals ───────────────────────────────────────────────────────
        modelBuilder.Entity<Withdrawal>(entity =>
        {
            entity.ToTable("Withdrawals");
            entity.HasKey(w => w.Id);
            entity.Property(w => w.Id).ValueGeneratedOnAdd();
            entity.Property(w => w.Amount).IsRequired().HasColumnType("decimal(12,2)");
            entity.Property(w => w.Method).IsRequired();
            entity.Property(w => w.Status).IsRequired().HasDefaultValue(WithdrawalStatus.Requested);
            entity.Property(w => w.RequestedAt)
                  .IsRequired().HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(w => w.CompletedAt).IsRequired(false).HasColumnType("datetime2");
            entity.Property(w => w.Reference).IsRequired(false).HasMaxLength(200);
            entity.HasOne(w => w.User).WithMany().HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}