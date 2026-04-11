namespace ThriftLoop.Models;

/// <summary>
/// Represents an item in a user's shopping cart.
/// Only used for Shop items (ShopId != null) which support bulk buying.
/// P2P items cannot be added to cart as they are one-of-a-kind and use
/// the steal/reserve mechanism instead.
/// </summary>
public class CartItem
{
    public int Id { get; set; }

    /// <summary>FK → Users.Id — the user who added this to their cart.</summary>
    public int UserId { get; set; }

    /// <summary>FK → Items.Id — the shop item.</summary>
    public int ItemId { get; set; }

    /// <summary>
    /// FK → ItemVariantSkus.Id — the specific variant/size selected.
    /// This is required for shop items as they have multiple variants/sizes.
    /// </summary>
    public int ItemVariantSkuId { get; set; }

    /// <summary>
    /// How many units the user wants to purchase.
    /// For shop items this can be > 1 (bulk buying).
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// UTC timestamp when the item was added to cart.
    /// Used to sort cart items (newest first).
    /// </summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ────────────────────────────────────────────────────────────

    public User? User { get; set; }
    public Item? Item { get; set; }
    public ItemVariantSku? ItemVariantSku { get; set; }
}
