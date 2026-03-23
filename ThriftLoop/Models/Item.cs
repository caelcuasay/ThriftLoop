using System.ComponentModel.DataAnnotations.Schema;
using ThriftLoop.Enums;

namespace ThriftLoop.Models;

// ── Domain model ───────────────────────────────────────────────────────────────

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

    // ── Relationships ─────────────────────────────────────────────────────────

    public int UserId { get; set; }
    public User? User { get; set; }
}