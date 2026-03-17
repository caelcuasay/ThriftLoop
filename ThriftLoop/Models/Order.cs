namespace ThriftLoop.Models;

// ── Enum ──────────────────────────────────────────────────────────────────────

/// <summary>
/// Lifecycle of an order from placement through resolution.
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// The buyer has confirmed on the checkout page but the seller has not yet
    /// acknowledged payment or arranged handover. This is the initial state.
    /// </summary>
    Pending,

    /// <summary>Payment and/or delivery have been confirmed by both parties.</summary>
    Completed,

    /// <summary>The order was cancelled before completion.</summary>
    Cancelled
}

// ── Domain model ──────────────────────────────────────────────────────────────

/// <summary>
/// Represents a confirmed purchase intent between a buyer and a seller.
/// Created when a buyer clicks "Confirm Purchase" on the Checkout page.
/// FinalPrice captures the exact amount at the moment of confirmation —
/// this preserves the ₱50 steal premium permanently even if the Item row
/// is later modified.
/// </summary>
public class Order
{
    public int Id { get; set; }

    // ── Foreign keys ──────────────────────────────────────────────────────

    /// <summary>FK → Items.Id — the item being purchased.</summary>
    public int ItemId { get; set; }

    /// <summary>FK → Users.Id — the user who is buying.</summary>
    public int BuyerId { get; set; }

    /// <summary>FK → Users.Id — the user who listed the item (the seller).</summary>
    public int SellerId { get; set; }

    // ── Order data ────────────────────────────────────────────────────────

    /// <summary>
    /// The price the buyer agreed to pay, locked at confirmation time.
    /// For a stolen Stealable item this will be the base price + ₱50.
    /// </summary>
    public decimal FinalPrice { get; set; }

    /// <summary>UTC timestamp of when the buyer confirmed the purchase.</summary>
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    /// <summary>Current lifecycle state of this order.</summary>
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    // ── Navigation properties ─────────────────────────────────────────────

    /// <summary>The item that was purchased. Not loaded by default.</summary>
    public Item? Item { get; set; }

    /// <summary>The buyer. Not loaded by default.</summary>
    public User? Buyer { get; set; }

    /// <summary>The seller. Not loaded by default.</summary>
    public User? Seller { get; set; }
}