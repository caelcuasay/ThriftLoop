using ThriftLoop.Enums;
using ThriftLoop.Constants;

namespace ThriftLoop.Models;

// ── Domain model ──────────────────────────────────────────────────────────────

/// <summary>
/// Represents a confirmed purchase intent between a buyer and a seller.
/// Created when a buyer clicks "Confirm Purchase" on the Checkout page.
///
/// ItemId is kept as a direct FK for convenience (quick joins to item title,
/// images, and steal logic) even though the same item is reachable via
/// ItemVariantSku → ItemVariant → Item. Having both avoids multi-hop joins
/// on the most common read paths (order history, admin views).
///
/// ItemVariantSkuId is the authoritative reference for what was purchased —
/// it captures the exact size, variant, and price the buyer chose at checkout.
/// For P2P items this points to the auto-generated default SKU.
///
/// FinalPrice captures the exact total at the moment of confirmation, including
/// the delivery fee. This preserves the ₱50 steal premium and ₱50 delivery fee
/// permanently even if the Item row is later modified.
/// </summary>
public class Order
{
    public int Id { get; set; }

    // ── Foreign keys ──────────────────────────────────────────────────────────

    /// <summary>
    /// FK → Items.Id — the parent listing.
    /// Retained for direct access to item-level data (title, steal status, etc.)
    /// without traversing the Variant → SKU chain.
    /// </summary>
    public int ItemId { get; set; }

    /// <summary>
    /// FK → ItemVariantSkus.Id — the specific size/variant the buyer purchased.
    /// Nullable for backward compatibility with orders created before the variant
    /// system existed. All new orders must populate this field at checkout time.
    /// For P2P items this points to the auto-generated default SKU.
    /// For shop items this is the seller-defined SKU (e.g. Red / Size M).
    /// </summary>
    public int? ItemVariantSkuId { get; set; }

    /// <summary>FK → Users.Id — the user who is buying.</summary>
    public int BuyerId { get; set; }

    /// <summary>FK → Users.Id — the user who listed the item (the seller).</summary>
    public int SellerId { get; set; }

    // ── Order data ────────────────────────────────────────────────────────────

    /// <summary>
    /// The total price the buyer agreed to pay, locked at confirmation time.
    /// This includes the item price (+ steal premium if applicable) AND the
    /// flat delivery fee (<see cref="DeliveryFee"/>).
    ///   Wallet orders: full amount is held in escrow; released to seller and
    ///                  rider separately on delivery confirmation.
    ///   COD orders:    buyer pays in cash; seller receives item price,
    ///                  rider receives the delivery fee.
    /// </summary>
    public decimal FinalPrice { get; set; }

    /// <summary>
    /// Flat delivery fee locked at confirmation time. Always ₱50 (sourced from
    /// <see cref="ItemConstants.DeliveryFee"/>).
    /// For Wallet orders this portion is released from escrow directly to the
    /// rider's wallet after delivery confirmation.
    /// For COD orders the buyer pays the rider in cash; this field records the
    /// expected amount so the rider's wallet credit can be calculated correctly.
    /// </summary>
    public decimal DeliveryFee { get; set; } = ItemConstants.DeliveryFee;

    /// <summary>
    /// How many units the buyer is purchasing.
    /// Always 1 for P2P items.
    /// For shop SKUs this can be > 1 up to the available Quantity at checkout time.
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>UTC timestamp of when the buyer confirmed the purchase.</summary>
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    /// <summary>Current lifecycle state of this order.</summary>
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    // ── Payment ───────────────────────────────────────────────────────────────

    /// <summary>
    /// How the buyer is paying. Defaults to Wallet.
    /// For Wallet orders, funds are held in escrow until MarkDelivered is called.
    /// For Cash orders, no escrow is created — CashCollectedByRider tracks
    /// whether the physical payment has been collected.
    /// </summary>
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Wallet;

    /// <summary>
    /// For Cash orders only. Set to true when the rider (or buyer) confirms
    /// that cash has been collected, triggering a CashCollection transaction
    /// to credit the seller's wallet and a DeliveryFeePayment to credit the
    /// rider's wallet.
    /// </summary>
    public bool CashCollectedByRider { get; set; } = false;

    // ── Navigation properties ─────────────────────────────────────────────────

    public Item? Item { get; set; }
    public ItemVariantSku? ItemVariantSku { get; set; }
    public User? Buyer { get; set; }
    public User? Seller { get; set; }

    /// <summary>The delivery associated with this order (one-to-one).</summary>
    public Delivery? Delivery { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}