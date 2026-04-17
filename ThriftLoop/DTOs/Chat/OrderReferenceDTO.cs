// DTOs/Chat/OrderReferenceDTO.cs
using ThriftLoop.Enums;

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

    // ── Inquiry Status (for pre-order conversations) ──────────────────────────

    /// <summary>
    /// Current status of the inquiry (Pending, Accepted, Declined, Expired, Cancelled).
    /// </summary>
    public InquiryStatus InquiryStatus { get; set; } = InquiryStatus.Pending;

    /// <summary>
    /// UTC timestamp when the inquiry expires.
    /// </summary>
    public DateTime? InquiryExpiresAt { get; set; }

    /// <summary>
    /// Whether the inquiry is still active (Pending and not expired).
    /// </summary>
    public bool IsActiveInquiry => !IsConfirmedOrder
        && InquiryStatus == InquiryStatus.Pending
        && (!InquiryExpiresAt.HasValue || InquiryExpiresAt.Value > DateTime.UtcNow);

    /// <summary>
    /// Whether the inquiry has expired.
    /// </summary>
    public bool IsExpired => !IsConfirmedOrder
        && InquiryExpiresAt.HasValue
        && InquiryExpiresAt.Value <= DateTime.UtcNow
        && InquiryStatus == InquiryStatus.Pending;

    /// <summary>
    /// Whether the current user can accept this inquiry (seller only, pending status).
    /// </summary>
    public bool CanAccept { get; set; }

    /// <summary>
    /// Whether the current user can decline this inquiry (seller only, pending status).
    /// </summary>
    public bool CanDecline { get; set; }

    /// <summary>
    /// Whether the current user can cancel this inquiry (buyer only, pending status).
    /// </summary>
    public bool CanCancel { get; set; }

    /// <summary>
    /// The message ID of this order reference (for SignalR updates).
    /// </summary>
    public int MessageId { get; set; }

    // ── Computed Properties ───────────────────────────────────────────────────

    /// <summary>
    /// Formatted price string (e.g., "₱500.00").
    /// </summary>
    public string FormattedPrice => $"₱{Price:N2}";

    /// <summary>
    /// Status text to display on the card.
    /// </summary>
    public string StatusText
    {
        get
        {
            if (IsConfirmedOrder)
                return "Order Confirmed";

            return InquiryStatus switch
            {
                InquiryStatus.Pending => IsExpired ? "Inquiry Expired" : "Awaiting Response",
                InquiryStatus.Accepted => "Accepted - Proceed to Checkout",
                InquiryStatus.Declined => "Declined",
                InquiryStatus.Expired => "Inquiry Expired",
                InquiryStatus.Cancelled => "Cancelled",
                _ => "Awaiting Response"
            };
        }
    }

    /// <summary>
    /// CSS class for the status badge.
    /// </summary>
    public string StatusClass
    {
        get
        {
            if (IsConfirmedOrder)
                return "order-reference--confirmed";

            return InquiryStatus switch
            {
                InquiryStatus.Pending => IsExpired ? "order-reference--expired" : "order-reference--pending",
                InquiryStatus.Accepted => "order-reference--accepted",
                InquiryStatus.Declined => "order-reference--declined",
                InquiryStatus.Expired => "order-reference--expired",
                InquiryStatus.Cancelled => "order-reference--cancelled",
                _ => "order-reference--pending"
            };
        }
    }

    /// <summary>
    /// Formatted expiration time (e.g., "Expires in 23h 45m").
    /// </summary>
    public string? ExpirationText
    {
        get
        {
            if (!InquiryExpiresAt.HasValue || InquiryStatus != InquiryStatus.Pending)
                return null;

            var timeLeft = InquiryExpiresAt.Value - DateTime.UtcNow;

            if (timeLeft.TotalSeconds <= 0)
                return "Expired";

            if (timeLeft.TotalDays >= 1)
                return $"Expires in {(int)timeLeft.TotalDays}d {(int)timeLeft.Hours}h";

            if (timeLeft.TotalHours >= 1)
                return $"Expires in {(int)timeLeft.TotalHours}h {(int)timeLeft.Minutes}m";

            return $"Expires in {(int)timeLeft.TotalMinutes}m";
        }
    }
}