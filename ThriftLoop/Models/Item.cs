namespace ThriftLoop.Models;

// ── Enums ──────────────────────────────────────────────────────────────────────

/// <summary>
/// Distinguishes between a standard fixed-price listing and a time-limited
/// Stealable listing where a buyer can "Get" the item and another can "Steal" it.
/// </summary>
public enum ListingType
{
    Standard,
    Stealable
}

/// <summary>
/// Tracks the lifecycle of an item through the purchase flow.
/// Available → Reserved (Get clicked) → Sold (Steal or finalized checkout).
/// </summary>
public enum ItemStatus
{
    /// <summary>The item has not been claimed by any buyer yet.</summary>
    Available,

    /// <summary>
    /// A buyer has clicked "Get". The item is reserved for them while the
    /// Steal countdown is active. Another buyer may still Steal it.
    /// </summary>
    Reserved,

    /// <summary>The item has been purchased (either via Steal or finalized checkout).</summary>
    Sold
}

// ── Domain model ───────────────────────────────────────────────────────────────

/// <summary>
/// Domain model — mirrors the Items database table.
/// Keep this class free of UI concerns (no [Required], no display annotations).
/// All validation annotations belong in ItemCreateViewModel.
/// </summary>
public class Item
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Condition { get; set; } = string.Empty;

    /// <summary>
    /// Standard clothing size (XS / S / M / L / XL / XXL / XXXL).
    /// Nullable — not all item types carry a standard size (e.g. bags, accessories).
    /// </summary>
    public string? Size { get; set; }

    /// <summary>Relative or absolute URL to the uploaded image.</summary>
    public string? ImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Stealable listing fields ───────────────────────────────────────────────

    /// <summary>
    /// Whether this is a Standard or Stealable listing.
    /// Defaults to Standard so existing rows are unaffected by the migration.
    /// </summary>
    public ListingType ListingType { get; set; } = ListingType.Standard;

    /// <summary>
    /// The seller-chosen Steal window in hours (6, 12, or 24).
    /// Only populated when ListingType == Stealable.
    /// </summary>
    public int? StealDurationHours { get; set; }

    /// <summary>
    /// The UTC deadline by which another buyer may "Steal" this item.
    /// Set to <c>DateTime.UtcNow + StealDurationHours</c> when the first
    /// buyer clicks "Get". Null until that moment.
    /// </summary>
    public DateTime? StealEndsAt { get; set; }

    /// <summary>
    /// The ID of the buyer who clicked "Get" and currently holds the reservation.
    /// Null if no one has claimed the item yet.
    /// </summary>
    public int? CurrentWinnerId { get; set; }

    /// <summary>
    /// Lifecycle status of the item within the purchase flow.
    /// Defaults to Available.
    /// </summary>
    public ItemStatus Status { get; set; } = ItemStatus.Available;

    // ── Computed helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when a Stealable item's countdown has expired
    /// but the item has not yet been stolen.
    /// The original winner then has an additional 2-hour finalize window.
    /// </summary>
    public bool IsInFinalizeWindow =>
        ListingType == ListingType.Stealable
        && Status == ItemStatus.Reserved
        && StealEndsAt.HasValue
        && DateTime.UtcNow > StealEndsAt.Value
        && DateTime.UtcNow <= StealEndsAt.Value.AddHours(2);

    /// <summary>
    /// The UTC deadline by which the original "Get" buyer must finalize their
    /// purchase after the Steal window expires (StealEndsAt + 2 hours).
    /// </summary>
    public DateTime? FinalizeDeadline =>
        StealEndsAt.HasValue ? StealEndsAt.Value.AddHours(2) : null;

    // ── Relationships ──────────────────────────────────────────────────────────

    /// <summary>FK → Users.Id</summary>
    public int UserId { get; set; }

    /// <summary>Navigation property — not loaded by default (use .Include() when needed).</summary>
    public User? User { get; set; }
}