using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;
using ThriftLoop.Models;
using ThriftLoop.Enums;

namespace ThriftLoop.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    // ── DbSets ────────────────────────────────────────────────────────────────
    public DbSet<User> Users => Set<User>();
    public DbSet<Rider> Riders => Set<Rider>();
    public DbSet<SellerProfile> SellerProfiles => Set<SellerProfile>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<ItemVariant> ItemVariants => Set<ItemVariant>();
    public DbSet<ItemVariantSku> ItemVariantSkus => Set<ItemVariantSku>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
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

            // ── Password Reset ─────────────────────────────────────────────────
            entity.Property(u => u.PasswordResetToken)
                  .IsRequired(false).HasMaxLength(64).IsUnicode(false);
            entity.Property(u => u.PasswordResetTokenExpiry)
                  .IsRequired(false).HasColumnType("datetime2");

            // ── Role ───────────────────────────────────────────────────────────
            entity.Property(u => u.Role)
                  .IsRequired()
                  .HasDefaultValue(UserRole.User);

            // ── Disabled ───────────────────────────────────────────────────────
            entity.Property(u => u.IsDisabled)
                  .IsRequired()
                  .HasDefaultValue(false);
            entity.Property(u => u.DisabledAt)
                  .IsRequired(false)
                  .HasColumnType("datetime2");
        });

        // ── Riders ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<Rider>(entity =>
        {
            entity.ToTable("Riders");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).ValueGeneratedOnAdd();
            entity.Property(r => r.Email)
                  .IsRequired().HasMaxLength(256).IsUnicode(false);
            entity.HasIndex(r => r.Email).IsUnique().HasDatabaseName("UQ_Riders_Email");
            entity.Property(r => r.PasswordHash)
                  .IsRequired(false).HasMaxLength(512).IsUnicode(false);
            entity.Property(r => r.FullName)
                  .IsRequired().HasMaxLength(100);
            entity.Property(r => r.PhoneNumber)
                  .IsRequired().HasMaxLength(20);
            entity.Property(r => r.IsApproved)
                  .IsRequired().HasDefaultValue(false);
            entity.Property(r => r.CreatedAt)
                  .IsRequired().HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(r => r.ActiveDeliveryId).IsRequired(false);
            entity.Property(r => r.ActiveDeliveryStartedAt).IsRequired(false);

            entity.HasOne(r => r.ActiveDelivery)
                  .WithMany()
                  .HasForeignKey(r => r.ActiveDeliveryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── SellerProfiles ────────────────────────────────────────────────────
        modelBuilder.Entity<SellerProfile>(entity =>
        {
            entity.ToTable("SellerProfiles");
            entity.HasKey(sp => sp.Id);
            entity.Property(sp => sp.Id).ValueGeneratedOnAdd();

            entity.HasIndex(sp => sp.UserId)
                  .IsUnique()
                  .HasDatabaseName("UQ_SellerProfiles_UserId");

            entity.Property(sp => sp.ApplicationStatus)
                  .IsRequired()
                  .HasDefaultValue(ApplicationStatus.Pending);

            entity.Property(sp => sp.AppliedAt)
                  .IsRequired().HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");

            entity.Property(sp => sp.ReviewedAt)
                  .IsRequired(false).HasColumnType("datetime2");

            // ── Application details ────────────────────────────────────────────
            entity.Property(sp => sp.StoreAddress)
                  .IsRequired().HasMaxLength(500);

            entity.Property(sp => sp.GovIdUrl)
                  .IsRequired(false).HasMaxLength(512).IsUnicode(false);

            // ── Shop branding ──────────────────────────────────────────────────
            entity.Property(sp => sp.ShopName)
                  .IsRequired().HasMaxLength(100);

            entity.Property(sp => sp.Bio)
                  .IsRequired(false).HasMaxLength(500);

            entity.Property(sp => sp.BannerUrl)
                  .IsRequired(false).HasMaxLength(512).IsUnicode(false);

            entity.Property(sp => sp.LogoUrl)
                  .IsRequired(false).HasMaxLength(512).IsUnicode(false);

            entity.HasOne(sp => sp.User)
                  .WithOne(u => u.SellerProfile)
                  .HasForeignKey<SellerProfile>(sp => sp.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(sp => sp.Items)
                  .WithOne(i => i.Shop)
                  .HasForeignKey(i => i.ShopId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.NoAction);
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
            entity.Property(i => i.OriginalGetterUserId).IsRequired(false);

            entity.Ignore(i => i.IsInFinalizeWindow);
            entity.Ignore(i => i.FinalizeDeadline);

            entity.Property(i => i.ShopId).IsRequired(false);

            entity.HasOne(i => i.User)
                  .WithMany()
                  .HasForeignKey(i => i.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(i => i.Variants)
                  .WithOne(v => v.Item)
                  .HasForeignKey(v => v.ItemId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ItemVariants ──────────────────────────────────────────────────────
        modelBuilder.Entity<ItemVariant>(entity =>
        {
            entity.ToTable("ItemVariants");
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Id).ValueGeneratedOnAdd();
            entity.Property(v => v.Name).IsRequired().HasMaxLength(50);
            entity.HasMany(v => v.Skus)
                  .WithOne(s => s.Variant)
                  .HasForeignKey(s => s.VariantId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ItemVariantSkus ───────────────────────────────────────────────────
        modelBuilder.Entity<ItemVariantSku>(entity =>
        {
            entity.ToTable("ItemVariantSkus");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).ValueGeneratedOnAdd();
            entity.Property(s => s.Size).IsRequired(false).HasMaxLength(20);
            entity.Property(s => s.Price).IsRequired().HasColumnType("decimal(10,2)");
            entity.Property(s => s.Quantity).IsRequired().HasDefaultValue(1);
            entity.Property(s => s.Status).IsRequired().HasDefaultValue(SkuStatus.Available);
        });

        // ── Orders ────────────────────────────────────────────────────────────
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Id).ValueGeneratedOnAdd();
            entity.Property(o => o.FinalPrice).IsRequired().HasColumnType("decimal(10,2)");
            entity.Property(o => o.Quantity).IsRequired().HasDefaultValue(1);
            entity.Property(o => o.OrderDate)
                  .IsRequired().HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(o => o.Status).IsRequired().HasDefaultValue(OrderStatus.Pending);
            entity.Property(o => o.PaymentMethod).IsRequired().HasDefaultValue(PaymentMethod.Wallet);
            entity.Property(o => o.CashCollectedByRider).IsRequired().HasDefaultValue(false);

            entity.HasOne(o => o.Item)
                  .WithMany()
                  .HasForeignKey(o => o.ItemId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(o => o.ItemVariantSku)
                  .WithMany(s => s.Orders)
                  .HasForeignKey(o => o.ItemVariantSkuId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(o => o.Buyer)
                  .WithMany()
                  .HasForeignKey(o => o.BuyerId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(o => o.Seller)
                  .WithMany()
                  .HasForeignKey(o => o.SellerId)
                  .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(o => o.Delivery)
                  .WithOne(d => d.Order)
                  .HasForeignKey<Delivery>(d => d.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Deliveries ────────────────────────────────────────────────────────
        modelBuilder.Entity<Delivery>(entity =>
        {
            entity.ToTable("Deliveries");
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Id).ValueGeneratedOnAdd();
            entity.Property(d => d.Status).IsRequired().HasDefaultValue(DeliveryStatus.Available);
            entity.Property(d => d.CreatedAt)
                  .IsRequired().HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(d => d.AcceptedAt).IsRequired(false).HasColumnType("datetime2");
            entity.Property(d => d.PickedUpAt).IsRequired(false).HasColumnType("datetime2");
            entity.Property(d => d.DeliveredAt).IsRequired(false).HasColumnType("datetime2");
            entity.Property(d => d.ConfirmedByBuyerAt).IsRequired(false).HasColumnType("datetime2");

            entity.HasOne(d => d.Order)
                  .WithOne(o => o.Delivery)
                  .HasForeignKey<Delivery>(d => d.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Rider)
                  .WithMany(r => r.Deliveries)
                  .HasForeignKey(d => d.RiderId)
                  .OnDelete(DeleteBehavior.Restrict);
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
            entity.HasIndex(w => w.RiderId).IsUnique().HasDatabaseName("UQ_Wallets_RiderId");

            entity.HasOne(w => w.User)
                  .WithMany()
                  .HasForeignKey(w => w.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(w => w.Rider)
                  .WithOne(r => r.Wallet)
                  .HasForeignKey<Wallet>(w => w.RiderId)
                  .OnDelete(DeleteBehavior.Cascade);
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

            entity.HasOne(t => t.ToRider)
                  .WithMany()
                  .HasForeignKey(t => t.ToRiderId)
                  .OnDelete(DeleteBehavior.NoAction);
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