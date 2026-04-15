using ThriftLoop.Enums;
using ThriftLoop.Constants;

namespace ThriftLoop.ViewModels;

/// <summary>
/// ViewModel for the unified Checkout page that supports both P2P and Shop orders,
/// as well as the three fulfillment methods (Delivery, Halfway, Pickup).
///
/// FinalPrice calculation logic:
/// - If Delivery: Includes DeliveryFee in the view (though not deducted from wallet).
/// - If Halfway/Pickup: DeliveryFee is excluded.
/// </summary>
public class CheckoutViewModel
{
    // ── Item Information ──────────────────────────────────────────────────────
    public int ItemId { get; set; }
    public string ItemTitle { get; set; } = string.Empty;
    public string? ItemImageUrl { get; set; }
    public string ItemCategory { get; set; } = string.Empty;
    public string ItemCondition { get; set; } = string.Empty;
    public string? ItemSize { get; set; }

    // ── Seller Information ────────────────────────────────────────────────────
    public string SellerName { get; set; } = string.Empty;
    public string SellerEmail { get; set; } = string.Empty;

    // ── Pricing ───────────────────────────────────────────────────────────────
    public decimal BasePrice { get; set; }
    public decimal DeliveryFee { get; set; } = ItemConstants.DeliveryFee;

    /// <summary>
    /// The total price displayed to the user. 
    /// Includes the delivery fee only if 'Delivery' is selected.
    /// </summary>
    public decimal FinalPrice => WasStolen
        ? BasePrice + StealPremium + (SelectedFulfillmentMethod == "Delivery" ? DeliveryFee : 0)
        : BasePrice + (SelectedFulfillmentMethod == "Delivery" ? DeliveryFee : 0);

    // ── Stealable Fields (P2P specific) ───────────────────────────────────────
    public bool IsStealable { get; set; }
    public bool WasStolen { get; set; }
    public decimal StealPremium => WasStolen ? ItemConstants.StealPremium : 0;

    // ── Shop Order Fields (Option B) ──────────────────────────────────────────
    public bool IsShopOrder { get; set; }
    public int? SkuId { get; set; }
    public int Quantity { get; set; } = 1;
    public int MaxQuantity { get; set; } = 1;
    public string SelectedVariantName { get; set; } = string.Empty;
    public string? SelectedSize { get; set; }

    // ── Fulfillment Options ───────────────────────────────────────────────────
    public bool AllowDelivery { get; set; } = true;
    public bool AllowHalfway { get; set; } = false;
    public bool AllowPickup { get; set; } = false;

    /// <summary>
    /// Bound to the radio button selection in the view.
    /// Values: "Delivery", "Halfway", or "Pickup".
    /// </summary>
    public string SelectedFulfillmentMethod { get; set; } = "Delivery";

    // ── Buyer Information ─────────────────────────────────────────────────────
    public decimal BuyerBalance { get; set; }

    /// <summary>
    /// Check against FinalPrice. Note: In the controller logic, for Delivery, 
    /// the fee is expected to be paid in cash, so ensure your OrderController 
    /// wallet deduction logic matches this requirement.
    /// </summary>
    public bool HasSufficientBalance => BuyerBalance >= FinalPrice;

    // ── Computed Properties for View Logic ────────────────────────────────────
    public bool HasAnyFulfillmentOption => AllowDelivery || AllowHalfway || AllowPickup;
    public bool IsDeliverySelected => SelectedFulfillmentMethod == "Delivery";
    public bool IsHalfwaySelected => SelectedFulfillmentMethod == "Halfway";
    public bool IsPickupSelected => SelectedFulfillmentMethod == "Pickup";

    /// <summary>
    /// Used to display specific instructions to the buyer based on their selection.
    /// </summary>
    public string FulfillmentDescription => SelectedFulfillmentMethod switch
    {
        "Delivery" => $"A ThriftLoop rider will deliver this item. Delivery fee: ₱{DeliveryFee:N2} (paid to rider in cash).",
        "Halfway" => "You and the seller will meet at a halfway point. A chat will be opened to coordinate meeting details.",
        "Pickup" => "You will pick up the item directly from the seller's location. A chat will be opened to coordinate.",
        _ => "Please select a fulfillment method."
    };
}