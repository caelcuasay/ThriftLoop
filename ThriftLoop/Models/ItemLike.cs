namespace ThriftLoop.Models;

/// <summary>
/// Represents a "like" or "favorite" on an item.
/// Used primarily for P2P items (ShopId == null) since they cannot be added to cart.
/// Users can like P2P items to track them and see if they're still available.
/// Shop items can also be liked for wishlist functionality.
/// </summary>
public class ItemLike
{
    public int Id { get; set; }

    /// <summary>FK → Users.Id — the user who liked the item.</summary>
    public int UserId { get; set; }

    /// <summary>FK → Items.Id — the item that was liked.</summary>
    public int ItemId { get; set; }

    /// <summary>
    /// UTC timestamp when the like was created.
    /// Used to sort liked items and for analytics (most liked, trending, etc.).
    /// </summary>
    public DateTime LikedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ────────────────────────────────────────────────────────────

    public User? User { get; set; }
    public Item? Item { get; set; }
}
