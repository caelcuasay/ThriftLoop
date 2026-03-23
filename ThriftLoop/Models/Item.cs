using System.ComponentModel.DataAnnotations.Schema;
using ThriftLoop.Enums;

namespace ThriftLoop.Models;

// ── Domain model ───────────────────────────────────────────────────────────────

/// <summary>
/// Top-level listing record. Covers both P2P listings posted by regular Users
/// and shop listings posted by approved Sellers.
///
/// Distinguishing P2P vs Shop:
///   ShopId == null  →  P2P listing.  One flat item, one seller, quantity 1.
///   ShopId != null  →  Shop listing. Posted by a Seller via their shop page.
///
/// Variants and inventory:
///   All items — P2P and shop alike — carry at least one ItemVariant with at
///   least one ItemVariantSku. For P2P items a single "Default" variant and
///   SKU are auto-generated at creation time so that Orders always reference
///   a SkuId regardless of listing type. This keeps checkout, escrow, and
///   order history logic uniform across both paths.
///
/// Steal mechanic:
///   The stealable fields (ListingType, StealEndsAt, CurrentWinnerId, etc.)
///   and ItemStatus live here on the Item row — not on the SKU. Steal logic
///   is P2P-only; shop listings always use ListingType.Standard and the SKU's
///   SkuStatus drives their availability.
/// </summary>
public class Item
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;

    /// <summary>Standard clothing size. Nullable — bags, accessories may not have one.</summary>
    public string? Size { get; set; }

    // ── Images ────────────────────────────────────────────────────────────────

    /// <summary>
    /// All uploaded image paths, serialised as a JSON array in the DB column "ImageUrls".
    /// The first element is the cover image shown on listing cards and Details pages.
    /// </summary>
    public List<string> ImageUrls { get; set; } = new();

    /// <summary>
    /// Backward-compatible computed property — returns the first image URL or null.
    /// All existing views that reference item.ImageUrl continue to work unchanged.
    /// </summary>
    [NotMapped]
    public string? ImageUrl => ImageUrls.Count > 0 ? ImageUrls[0] : null;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Stealable listing fields ──────────────────────────────────────────────
    // These fields apply to P2P listings only.
    // Shop listings always have ListingType.Standard and never use these.

    public ListingType ListingType { get; set; } = ListingType.Standard;
    public int? StealDurationHours { get; set; }
    public DateTime? StealEndsAt { get; set; }

    /// <summary>
    /// The user currently holding the item.
    /// For Reserved:              the original getter (User A).
    /// For StolenPendingCheckout: the stealer (User B).
    /// </summary>
    public int? CurrentWinnerId { get; set; }

    /// <summary>
    /// Saved when a steal begins. Holds the original getter's (User A) ID so
    /// CancelSteal can restore the reservation to them instead of returning
    /// the item to Available. Cleared once the steal is confirmed or cancelled.
    /// </summary>
    public int? OriginalGetterUserId { get; set; }

    public ItemStatus Status { get; set; } = ItemStatus.Available;

    // ── Computed helpers ──────────────────────────────────────────────────────

    [NotMapped]
    public bool IsInFinalizeWindow =>
        ListingType == ListingType.Stealable
        && Status == ItemStatus.Reserved
        && StealEndsAt.HasValue
        && DateTime.UtcNow > StealEndsAt.Value
        && DateTime.UtcNow <= StealEndsAt.Value.AddHours(2);

    [NotMapped]
    public DateTime? FinalizeDeadline =>
        StealEndsAt.HasValue ? StealEndsAt.Value.AddHours(2) : null;

    // ── Shop FK ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Null for P2P listings.
    /// Set to the SellerProfile.Id when a Seller posts this item through their shop.
    /// Used to filter shop items on the Sellers page and to render shop context
    /// (shop name, logo) on listing cards without a separate query.
    /// </summary>
    public int? ShopId { get; set; }
    public SellerProfile? Shop { get; set; }

    // ── Owner ─────────────────────────────────────────────────────────────────

    public int UserId { get; set; }
    public User? User { get; set; }

    // ── Variants ──────────────────────────────────────────────────────────────

    /// <summary>
    /// For P2P items: always contains exactly one auto-generated "Default" variant.
    /// For shop items: contains the seller-defined variants (e.g. "Red", "Navy").
    /// Each variant contains one or more ItemVariantSkus (size + price + quantity).
    /// </summary>
    public ICollection<ItemVariant> Variants { get; set; } = new List<ItemVariant>();
}