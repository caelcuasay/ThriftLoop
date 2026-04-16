// DTOs/Chat/ConversationDetailDTO.cs
namespace ThriftLoop.DTOs.Chat;

/// <summary>
/// Detailed conversation view including messages and participant info.
/// </summary>
public class ConversationDetailDTO
{
    public int Id { get; set; }

    /// <summary>
    /// ID of the other user in the conversation.
    /// </summary>
    public int OtherUserId { get; set; }

    /// <summary>
    /// Display name of the other user.
    /// </summary>
    public string OtherUserName { get; set; } = string.Empty;

    /// <summary>
    /// Profile picture URL of the other user (if available).
    /// </summary>
    public string? OtherUserAvatarUrl { get; set; }

    /// <summary>
    /// Whether the other user is currently online (via SignalR).
    /// </summary>
    public bool IsOtherUserOnline { get; set; }

    /// <summary>
    /// When the other user was last active (if known).
    /// </summary>
    public DateTime? OtherUserLastActiveAt { get; set; }

    /// <summary>
    /// Formatted string for last active time (e.g., "Active 5m ago", "Online now").
    /// </summary>
    public string OtherUserActiveStatus { get; set; } = string.Empty;

    /// <summary>
    /// List of messages in this conversation (paginated).
    /// </summary>
    public List<MessageDTO> Messages { get; set; } = new();

    /// <summary>
    /// Total number of messages in the conversation.
    /// </summary>
    public int TotalMessageCount { get; set; }

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Number of messages per page.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Whether there are more messages to load.
    /// </summary>
    public bool HasMoreMessages { get; set; }

    public DateTime CreatedAt { get; set; }

    // ── Order/Item Context ─────────────────────────────────────────────────────

    /// <summary>
    /// Linked order ID if this conversation is associated with a confirmed order.
    /// Null for general conversations or pre-order inquiries.
    /// </summary>
    public int? LinkedOrderId { get; set; }

    /// <summary>
    /// Context item ID if this conversation is about a specific item.
    /// Null for general conversations.
    /// </summary>
    public int? ContextItemId { get; set; }

    /// <summary>
    /// Title of the context item (if applicable).
    /// </summary>
    public string? LinkedItemTitle { get; set; }

    /// <summary>
    /// Price of the linked item (if applicable).
    /// </summary>
    public decimal? LinkedItemPrice { get; set; }

    /// <summary>
    /// Image URL of the linked item (if applicable).
    /// </summary>
    public string? LinkedItemImageUrl { get; set; }

    /// <summary>
    /// Order status if this conversation is linked to an order.
    /// </summary>
    public string? LinkedOrderStatus { get; set; }

    /// <summary>
    /// Fulfillment method if this conversation is linked to an order.
    /// </summary>
    public string? LinkedFulfillmentMethod { get; set; }

    // ── Computed Properties ────────────────────────────────────────────────────

    /// <summary>
    /// Whether this conversation has item/order context.
    /// </summary>
    public bool HasContext => ContextItemId.HasValue || LinkedOrderId.HasValue;

    /// <summary>
    /// Whether this is a post-order conversation (order confirmed).
    /// </summary>
    public bool IsOrderConversation => LinkedOrderId.HasValue;

    /// <summary>
    /// Whether this is a pre-order inquiry (buyer asking about item).
    /// </summary>
    public bool IsItemInquiry => ContextItemId.HasValue && !LinkedOrderId.HasValue;

    /// <summary>
    /// Context header text to display above messages.
    /// </summary>
    public string ContextHeaderText
    {
        get
        {
            if (IsOrderConversation)
                return $"Order Conversation — {LinkedItemTitle ?? "Item"}";

            if (IsItemInquiry)
                return $"Inquiry about {LinkedItemTitle ?? "this item"}";

            return string.Empty;
        }
    }

    /// <summary>
    /// Context subtitle text (e.g., order status or fulfillment method).
    /// </summary>
    public string? ContextSubtitleText
    {
        get
        {
            if (IsOrderConversation && !string.IsNullOrEmpty(LinkedOrderStatus))
                return $"{LinkedOrderStatus} · {LinkedFulfillmentMethod ?? "Delivery"}";

            if (IsItemInquiry && LinkedItemPrice.HasValue)
                return $"₱{LinkedItemPrice:N2}";

            return null;
        }
    }

    /// <summary>
    /// Whether the current user can create an order from this conversation.
    /// True for item inquiries where the current user is the seller.
    /// </summary>
    public bool CanCreateOrder { get; set; }

    /// <summary>
    /// Whether the current user can confirm receipt from this conversation.
    /// True for order conversations where the order is awaiting confirmation.
    /// </summary>
    public bool CanConfirmReceipt { get; set; }
}