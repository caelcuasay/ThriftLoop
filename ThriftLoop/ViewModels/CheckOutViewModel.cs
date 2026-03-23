using ThriftLoop.Enums;
using ThriftLoop.Constants;

namespace ThriftLoop.ViewModels;

/// <summary>
/// Carries all data the Checkout view needs to render the order summary,
/// price breakdown, payment method selector, and confirm-purchase form.
/// Built by the GET Checkout action; itemId and paymentMethod are re-posted
/// on confirmation.
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
    public string SellerName { get; init; } = string.Empty;
    public string SellerEmail { get; init; } = string.Empty;

    // ── Price breakdown ───────────────────────────────────────────────────
    public decimal BasePrice { get; init; }

    /// <summary>
    /// ₱50 premium applied when the buyer stole the listing.
    /// Sourced from <see cref="ItemConstants.StealPremium"/>.
    /// </summary>
    public decimal StealPremium => WasStolen ? ItemConstants.StealPremium : 0m;

    public decimal FinalPrice => BasePrice + StealPremium;

    // ── Context flags ─────────────────────────────────────────────────────
    public bool WasStolen { get; init; }
    public bool IsStealable { get; init; }

    // ── Wallet ────────────────────────────────────────────────────────────
    /// <summary>Buyer's current available (non-escrowed) balance.</summary>
    public decimal BuyerBalance { get; init; }

    /// <summary>True when the buyer has enough funds to pay via wallet.</summary>
    public bool HasSufficientBalance => BuyerBalance >= FinalPrice;

    // ── Payment method (bound on POST) ────────────────────────────────────
    /// <summary>The method chosen by the buyer. Defaults to Wallet.</summary>
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Wallet;
}