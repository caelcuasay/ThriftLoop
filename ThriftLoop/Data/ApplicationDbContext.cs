// Data/ApplicationDbContext.cs
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
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<ItemLike> ItemLikes => Set<ItemLike>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();

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

            // Coordinates precision — prevent SQL truncation warnings
            entity.Property(u => u.Latitude)
                  .IsRequired(false)
                  .HasPrecision(9, 6);

            entity.Property(u => u.Longitude)
                  .IsRequired(false)
                  .HasPrecision(9, 6);
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

            // Coordinates precision — prevent SQL truncation warnings
            entity.Property(r => r.Latitude)
                  .IsRequired(false)
                  .HasPrecision(9, 6);

            entity.Property(r => r.Longitude)
                  .IsRequired(false)
                  .HasPrecision(9, 6);

            entity.HasOne(r => r.ActiveDelivery)
                  .WithMany()
                  .HasForeignKey(r => r.ActiveDeliveryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.Property(r => r.RejectionReason)
                  .IsRequired(false)
                  .HasMaxLength(500);

            entity.Property(r => r.RejectedAt)
                  .IsRequired(false)
                  .HasColumnType("datetime2");

            entity.Property(r => r.ResubmittedAt)
                  .IsRequired(false)
                  .HasColumnType("datetime2");

            entity.Property(r => r.UpdatedAt)
                  .IsRequired(false)
                  .HasColumnType("datetime2");
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

            // Optional shop coordinates for geolocation
            entity.Property(sp => sp.Latitude)
                  .IsRequired(false)
                  .HasPrecision(9, 6);

            entity.Property(sp => sp.Longitude)
                  .IsRequired(false)
                  .HasPrecision(9, 6);
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

            // ── Fulfillment Options ────────────────────────────────────────────
            entity.Property(i => i.AllowDelivery)
                  .IsRequired()
                  .HasDefaultValue(true);

            entity.Property(i => i.AllowHalfway)
                  .IsRequired()
                  .HasDefaultValue(false);

            entity.Property(i => i.AllowPickup)
                  .IsRequired()
                  .HasDefaultValue(false);

            // ── Discount Fields ────────────────────────────────────────────────
            entity.Property(i => i.OriginalPrice)
                  .IsRequired(false)
                  .HasColumnType("decimal(10,2)");

            entity.Property(i => i.DiscountPercentage)
                  .IsRequired(false)
                  .HasColumnType("decimal(5,2)");

            entity.Property(i => i.DiscountedAt)
                  .IsRequired(false)
                  .HasColumnType("datetime2");

            entity.Property(i => i.DiscountExpiresAt)
                  .IsRequired(false)
                  .HasColumnType("datetime2");

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
            entity.Ignore(i => i.HasActiveDiscount);
            entity.Ignore(i => i.HasExpiredDiscount);
            entity.Ignore(i => i.SavingsAmount);
            entity.Ignore(i => i.CanBeDiscounted);

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

            // ── Discount Fields ────────────────────────────────────────────────
            entity.Property(s => s.OriginalPrice)
                  .IsRequired(false)
                  .HasColumnType("decimal(10,2)");

            entity.Property(s => s.DiscountPercentage)
                  .IsRequired(false)
                  .HasColumnType("decimal(5,2)");
        });

        // ── Orders ────────────────────────────────────────────────────────────
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Id).ValueGeneratedOnAdd();
            entity.Property(o => o.FinalPrice).IsRequired().HasColumnType("decimal(10,2)");
            entity.Property(o => o.DeliveryFee).IsRequired().HasColumnType("decimal(10,2)");
            entity.Property(o => o.Quantity).IsRequired().HasDefaultValue(1);
            entity.Property(o => o.OrderDate)
                  .IsRequired().HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(o => o.Status).IsRequired().HasDefaultValue(OrderStatus.Pending);
            entity.Property(o => o.FulfillmentMethod)
                  .IsRequired()
                  .HasDefaultValue(FulfillmentMethod.Delivery);
            entity.Property(o => o.PaymentMethod).IsRequired().HasDefaultValue(PaymentMethod.Wallet);
            entity.Property(o => o.CashCollectedByRider).IsRequired().HasDefaultValue(false);
            entity.Property(o => o.ChatInitialized).IsRequired().HasDefaultValue(false);
            entity.Property(o => o.ChatSessionId).IsRequired(false).HasMaxLength(100).IsUnicode(false);

            // ── Chat Conversation FK ───────────────────────────────────────────
            entity.Property(o => o.ChatConversationId)
                  .IsRequired(false);

            entity.HasOne(o => o.ChatConversation)
                  .WithOne(c => c.Order)
                  .HasForeignKey<Conversation>(c => c.OrderId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);

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

        // ── CartItems ─────────────────────────────────────────────────────────
        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.ToTable("CartItems");
            entity.HasKey(ci => ci.Id);
            entity.Property(ci => ci.Id).ValueGeneratedOnAdd();
            entity.Property(ci => ci.Quantity).IsRequired().HasDefaultValue(1);
            entity.Property(ci => ci.AddedAt)
                  .IsRequired().HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");

            // Each user can only have one cart entry per SKU
            entity.HasIndex(ci => new { ci.UserId, ci.ItemVariantSkuId })
                  .IsUnique()
                  .HasDatabaseName("UQ_CartItems_User_Sku");

            entity.HasOne(ci => ci.User)
                  .WithMany()
                  .HasForeignKey(ci => ci.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ci => ci.Item)
                  .WithMany()
                  .HasForeignKey(ci => ci.ItemId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ci => ci.ItemVariantSku)
                  .WithMany()
                  .HasForeignKey(ci => ci.ItemVariantSkuId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── ItemLikes ──────────────────────────────────────────────────────────
        modelBuilder.Entity<ItemLike>(entity =>
        {
            entity.ToTable("ItemLikes");
            entity.HasKey(il => il.Id);
            entity.Property(il => il.LikedAt)
                  .IsRequired().HasColumnType("datetime2").HasDefaultValueSql("SYSUTCDATETIME()");

            // Each user can only like an item once
            entity.HasIndex(il => new { il.UserId, il.ItemId })
                  .IsUnique()
                  .HasDatabaseName("UQ_ItemLikes_User_Item");

            // Index for filtering most liked items
            entity.HasIndex(il => il.ItemId)
                  .HasDatabaseName("IX_ItemLikes_ItemId");

            entity.HasOne(il => il.User)
                  .WithMany()
                  .HasForeignKey(il => il.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(il => il.Item)
                  .WithMany()
                  .HasForeignKey(il => il.ItemId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── OrderItems ─────────────────────────────────────────────────────────
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("OrderItems");
            entity.HasKey(oi => oi.Id);
            entity.Property(oi => oi.Id).ValueGeneratedOnAdd();
            entity.Property(oi => oi.Quantity).IsRequired().HasDefaultValue(1);
            entity.Property(oi => oi.UnitPrice).IsRequired().HasColumnType("decimal(10,2)");

            entity.HasOne(oi => oi.Order)
                  .WithMany(o => o.OrderItems)
                  .HasForeignKey(oi => oi.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(oi => oi.ItemVariantSku)
                  .WithMany()
                  .HasForeignKey(oi => oi.ItemVariantSkuId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Conversations ──────────────────────────────────────────────────────
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.ToTable("Conversations");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).ValueGeneratedOnAdd();

            // Unique index on the pair (UserOneId, UserTwoId) to prevent duplicate conversations
            entity.HasIndex(c => new { c.UserOneId, c.UserTwoId })
                  .IsUnique()
                  .HasDatabaseName("UQ_Conversations_UserPair");

            entity.Property(c => c.CreatedAt)
                  .IsRequired()
                  .HasColumnType("datetime2")
                  .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.Property(c => c.LastMessageAt)
                  .IsRequired()
                  .HasColumnType("datetime2")
                  .HasDefaultValueSql("SYSUTCDATETIME()");

            // ── Order Linking ──────────────────────────────────────────────────
            entity.Property(c => c.OrderId)
                  .IsRequired(false);

            entity.HasOne(c => c.Order)
                  .WithOne(o => o.ChatConversation)
                  .HasForeignKey<Conversation>(c => c.OrderId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);

            // ── Item Context ───────────────────────────────────────────────────
            entity.Property(c => c.ContextItemId)
                  .IsRequired(false);

            entity.HasOne(c => c.ContextItem)
                  .WithMany()
                  .HasForeignKey(c => c.ContextItemId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);

            // Foreign keys to User
            entity.HasOne(c => c.UserOne)
                  .WithMany()
                  .HasForeignKey(c => c.UserOneId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.UserTwo)
                  .WithMany()
                  .HasForeignKey(c => c.UserTwoId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Messages ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable("Messages");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Id).ValueGeneratedOnAdd();

            entity.Property(m => m.Content)
                  .IsRequired()
                  .HasMaxLength(2000);

            entity.Property(m => m.MessageType)
                  .IsRequired()
                  .HasDefaultValue(MessageType.Text);

            entity.Property(m => m.SentAt)
                  .IsRequired()
                  .HasColumnType("datetime2")
                  .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.Property(m => m.DeliveredAt)
                  .IsRequired(false)
                  .HasColumnType("datetime2");

            entity.Property(m => m.ReadAt)
                  .IsRequired(false)
                  .HasColumnType("datetime2");

            entity.Property(m => m.Status)
                  .IsRequired()
                  .HasDefaultValue(MessageStatus.Sent);

            // ── Order/Item References ──────────────────────────────────────────
            entity.Property(m => m.ReferencedOrderId)
                  .IsRequired(false);

            entity.HasOne(m => m.ReferencedOrder)
                  .WithMany()
                  .HasForeignKey(m => m.ReferencedOrderId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.Property(m => m.ReferencedItemId)
                  .IsRequired(false);

            entity.HasOne(m => m.ReferencedItem)
                  .WithMany()
                  .HasForeignKey(m => m.ReferencedItemId)
                  .IsRequired(false)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.Property(m => m.MetadataJson)
                  .IsRequired(false)
                  .HasMaxLength(1000);

            // Index for efficient inbox queries (get latest messages per conversation)
            entity.HasIndex(m => new { m.ConversationId, m.SentAt })
                  .HasDatabaseName("IX_Messages_ConversationId_SentAt");

            // Index for finding unread messages for a specific user
            entity.HasIndex(m => new { m.SenderId, m.Status })
                  .HasDatabaseName("IX_Messages_SenderId_Status");

            // Index for message type filtering
            entity.HasIndex(m => m.MessageType)
                  .HasDatabaseName("IX_Messages_MessageType");

            // Foreign keys
            entity.HasOne(m => m.Conversation)
                  .WithMany(c => c.Messages)
                  .HasForeignKey(m => m.ConversationId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.Sender)
                  .WithMany()
                  .HasForeignKey(m => m.SenderId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}