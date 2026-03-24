using ThriftLoop.Enums;
using ThriftLoop.Constants;

namespace ThriftLoop.ViewModels;

/// <summary>
/// Carries all data the Checkout view needs to render the order summary,
/// price breakdown, payment method selector, and confirm-purchase form.
///
/// Covers two paths:
///   P2P  — Standard / Stealable single-unit order (IsShopOrder = false).
///   Shop — One SKU, fluid quantity, Option B     (IsShopOrder = true).
///
/// Built by the GET Checkout / ShopCheckout action; the relevant hidden
/// fields are re-posted on confirmation.
/// </summary>
public class CheckoutViewModel
{
    // ── Item identity ──────────────────────────────────────────────────────
    public int ItemId { get; init; }
    public string ItemTitle { get; init; } = string.Empty;
    public string? ItemImageUrl { get; init; }
    public string ItemCategory { get; init; } = string.Empty;
    public string ItemCondition { get; init; } = string.Empty;

    /// <summary>For P2P items only — size label from Item.Size.</summary>
    public string? ItemSize { get; init; }

    // ── Seller ────────────────────────────────────────────────────────────
    public string SellerName { get; init; } = string.Empty;
    public string SellerEmail { get; init; } = string.Empty;

    // ── Price ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Per-unit price for shop orders (= ItemVariantSku.Price).
    /// Item price for P2P orders (steal premium excluded).
    /// </summary>
    public decimal BasePrice { get; init; }

    /// <summary>
    /// ₱50 premium applied when the buyer stole a Stealable listing.
    /// Always zero for shop orders.
    /// Sourced from <see cref="ItemConstants.StealPremium"/>.
    /// </summary>
    public decimal StealPremium => (WasStolen && !IsShopOrder) ? ItemConstants.StealPremium : 0m;

    /// <summary>
    /// Total the buyer will be charged, locked at checkout time.
    ///   Shop: BasePrice × Quantity
    ///   P2P:  BasePrice + StealPremium
    /// </summary>
    public decimal FinalPrice => IsShopOrder
        ? BasePrice * Quantity
        : BasePrice + StealPremium;

    // ── P2P context flags ─────────────────────────────────────────────────
    public bool WasStolen { get; init; }
    public bool IsStealable { get; init; }

    // ── Shop-order extras (Option B) ──────────────────────────────────────

    /// <summary>True when this checkout is for a shop SKU, not a P2P item.</summary>
    public bool IsShopOrder { get; init; } = false;

    /// <summary>
    /// FK → ItemVariantSkus.Id.
    /// The specific size/variant the buyer selected on the Details page.
    /// Populated for shop orders only; 0 for P2P.
    /// </summary>
    public int SkuId { get; init; }

    /// <summary>
    /// How many units the buyer wants to purchase.
    /// Always 1 for P2P. Buyer-chosen for shop orders, capped at available stock.
    /// </summary>
    public int Quantity { get; init; } = 1;

    /// <summary>
    /// The SKU's available stock at the time the GET was served.
    /// Used to cap the quantity stepper shown in the view.
    /// </summary>
    public int MaxQuantity { get; init; } = 1;

    /// <summary>Display name of the selected variant (e.g. "Red"). Empty string for P2P.</summary>
    public string SelectedVariantName { get; init; } = string.Empty;

    /// <summary>Size label of the selected SKU (e.g. "M"). Null when the SKU has no size.</summary>
    public string? SelectedSize { get; init; }

    // ── Wallet ────────────────────────────────────────────────────────────

    /// <summary>Buyer's current available (non-escrowed) balance.</summary>
    public decimal BuyerBalance { get; init; }

    /// <summary>True when the buyer has enough wallet funds to cover FinalPrice.</summary>
    public bool HasSufficientBalance => BuyerBalance >= FinalPrice;

    // ── Payment method (bound on POST) ────────────────────────────────────

    /// <summary>The method chosen by the buyer. Defaults to Wallet.</summary>
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Wallet;
}