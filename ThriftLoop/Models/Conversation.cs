// Models/Conversation.cs
using System.ComponentModel.DataAnnotations;
using ThriftLoop.Enums;

namespace ThriftLoop.Models;

/// <summary>
/// Represents a 1-on-1 direct messaging conversation between two users.
/// The pair (UserOneId, UserTwoId) is unique to prevent duplicate conversations.
/// </summary>
public class Conversation
{
    public int Id { get; set; }

    /// <summary>
    /// The user who initiated the conversation (lower ID by convention, but not enforced).
    /// </summary>
    public int UserOneId { get; set; }

    /// <summary>
    /// The other participant in the conversation.
    /// </summary>
    public int UserTwoId { get; set; }

    /// <summary>
    /// Timestamp when the conversation was created (first message sent).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp of the most recent message in this conversation.
    /// Used for sorting the inbox.
    /// </summary>
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    // ── Inquiry Status (for item inquiry conversations) ────────────────────────

    /// <summary>
    /// Current status of the item inquiry. Only relevant when ContextItemId is not null.
    /// Defaults to Pending when an inquiry is created.
    /// </summary>
    public InquiryStatus InquiryStatus { get; set; } = InquiryStatus.Pending;

    /// <summary>
    /// UTC timestamp when the inquiry expires. After this time, the inquiry
    /// automatically becomes Expired if still Pending.
    /// Default expiration is 48 hours from creation.
    /// </summary>
    public DateTime? InquiryExpiresAt { get; set; }

    /// <summary>
    /// UTC timestamp when the seller responded (accepted or declined).
    /// Null if still pending.
    /// </summary>
    public DateTime? InquiryRespondedAt { get; set; }

    // ── Order Linking ──────────────────────────────────────────────────────────

    /// <summary>
    /// Optional link to an Order. When this conversation was created for a specific
    /// Halfway or Pickup order, this field references that order.
    /// Null for conversations started independently (e.g., from profile).
    /// </summary>
    public int? OrderId { get; set; }

    /// <summary>
    /// Navigation property to the linked order.
    /// </summary>
    public Order? Order { get; set; }

    // ── Item Context (for pre-order conversations) ─────────────────────────────

    /// <summary>
    /// Optional link to an Item. When a buyer contacts a seller about a specific
    /// item before placing an order, this field references that item.
    /// Used to display the item context in the chat header and to generate
    /// order reference messages.
    /// </summary>
    public int? ContextItemId { get; set; }

    /// <summary>
    /// Navigation property to the context item.
    /// </summary>
    public Item? ContextItem { get; set; }

    // ── Navigation Properties ─────────────────────────────────────────────────

    public User UserOne { get; set; } = null!;
    public User UserTwo { get; set; } = null!;

    public ICollection<Message> Messages { get; set; } = new List<Message>();

    // ── Computed Properties ───────────────────────────────────────────────────

    /// <summary>
    /// Whether this conversation is an active item inquiry (Pending and not expired).
    /// </summary>
    public bool IsActiveInquiry => ContextItemId.HasValue
        && InquiryStatus == InquiryStatus.Pending
        && (!InquiryExpiresAt.HasValue || InquiryExpiresAt.Value > DateTime.UtcNow);

    /// <summary>
    /// Whether the inquiry has expired.
    /// </summary>
    public bool IsExpired => InquiryExpiresAt.HasValue
        && InquiryExpiresAt.Value <= DateTime.UtcNow
        && InquiryStatus == InquiryStatus.Pending;
}