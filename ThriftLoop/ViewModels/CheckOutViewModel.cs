namespace ThriftLoop.ViewModels;

/// <summary>
/// Carries all data the Checkout view needs to render the order summary,
/// price breakdown, and confirm-purchase form.
/// Built by the GET Checkout action; the itemId is re-posted on confirmation.
/// </summary>
public class CheckoutViewModel
{
    // ── Item identity ──────────────────────────────────────────────────────

    public int ItemId { get; init; }
    public string ItemTitle { get; init; } = string.Empty;
    public string? ItemImageUrl { get; init; }
    public string ItemCategory { get; init; } = string.Empty;
    public string ItemCondition { get; init; } = string.Empty;
    public string? ItemSize { get; init; }

    // ── Seller ────────────────────────────────────────────────────────────

    /// <summary>Display-friendly name derived from the seller's email.</summary>
    public string SellerName { get; init; } = string.Empty;

    /// <summary>Full email kept for the "contact seller" note.</summary>
    public string SellerEmail { get; init; } = string.Empty;

    // ── Price breakdown ───────────────────────────────────────────────────

    /// <summary>
    /// The listing price before any steal premium.
    /// For a stolen item this is item.Price − ₱50; for everything else it
    /// equals item.Price directly.
    /// </summary>
    public decimal BasePrice { get; init; }

    /// <summary>
    /// ₱50 when the item was stolen; ₱0 otherwise.
    /// Stored explicitly so the view can conditionally render the premium row.
    /// </summary>
    public decimal StealPremium { get; init; }

    /// <summary>Computed total — BasePrice + StealPremium.</summary>
    public decimal FinalPrice => BasePrice + StealPremium;

    // ── Context flags ─────────────────────────────────────────────────────

    /// <summary>True when this checkout is the result of a Steal action.</summary>
    public bool WasStolen { get; init; }

    /// <summary>True when the item is a Stealable listing type.</summary>
    public bool IsStealable { get; init; }
}