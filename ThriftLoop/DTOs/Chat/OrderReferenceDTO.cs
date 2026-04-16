// DTOs/Chat/OrderReferenceDTO.cs
namespace ThriftLoop.DTOs.Chat;

/// <summary>
/// DTO for embedded order reference card displayed in chat.
/// Used when a buyer contacts a seller about an item (pre-order)
/// or when an order is created (post-order confirmation).
/// </summary>
public class OrderReferenceDTO
{
    /// <summary>
    /// The ID of the item being discussed.
    /// </summary>
    public int ItemId { get; set; }

    /// <summary>
    /// The title of the item.
    /// </summary>
    public string ItemTitle { get; set; } = string.Empty;

    /// <summary>
    /// Primary image URL of the item.
    /// </summary>
    public string? ItemImageUrl { get; set; }

    /// <summary>
    /// The listed price of the item.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Condition of the item (New, Like New, Good, Fair, Poor).
    /// </summary>
    public string Condition { get; set; } = string.Empty;

    /// <summary>
    /// Size of the item, if applicable.
    /// </summary>
    public string? Size { get; set; }

    /// <summary>
    /// Category of the item.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// The order ID, if this reference is for a confirmed order.
    /// Null for pre-order inquiries.
    /// </summary>
    public int? OrderId { get; set; }

    /// <summary>
    /// The conversation ID this reference belongs to.
    /// </summary>
    public int ConversationId { get; set; }

    /// <summary>
    /// The seller's user ID.
    /// </summary>
    public int SellerId { get; set; }

    /// <summary>
    /// The seller's display name.
    /// </summary>
    public string SellerName { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a confirmed order (true) or a pre-order inquiry (false).
    /// </summary>
    public bool IsConfirmedOrder { get; set; }

    /// <summary>
    /// The fulfillment method for this order (Delivery, Halfway, Pickup).
    /// Only populated for confirmed orders.
    /// </summary>
    public string? FulfillmentMethod { get; set; }

    /// <summary>
    /// Formatted price string (e.g., "₱500.00").
    /// </summary>
    public string FormattedPrice => $"₱{Price:N2}";

    /// <summary>
    /// Status text to display on the card.
    /// </summary>
    public string StatusText => IsConfirmedOrder ? "Order Confirmed" : "Awaiting Response";

    /// <summary>
    /// CSS class for the status badge.
    /// </summary>
    public string StatusClass => IsConfirmedOrder ? "order-reference--confirmed" : "order-reference--pending";
}