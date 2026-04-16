// Models/Message.cs
using System.ComponentModel.DataAnnotations;
using ThriftLoop.Enums;

namespace ThriftLoop.Models;

/// <summary>
/// Represents a single message within a 1-on-1 conversation.
/// </summary>
public class Message
{
    public int Id { get; set; }

    /// <summary>
    /// The conversation this message belongs to.
    /// </summary>
    public int ConversationId { get; set; }

    /// <summary>
    /// The user who sent this message.
    /// </summary>
    public int SenderId { get; set; }

    /// <summary>
    /// The text content of the message.
    /// For OrderReference messages, this may contain a JSON payload with order details.
    /// </summary>
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Type of message for rendering different bubble styles.
    /// </summary>
    public MessageType MessageType { get; set; } = MessageType.Text;

    /// <summary>
    /// UTC timestamp when the message was sent.
    /// </summary>
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when the message was delivered to the recipient
    /// (when they received it via SignalR or loaded the conversation).
    /// Null if not yet delivered.
    /// </summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>
    /// UTC timestamp when the recipient actually read the message
    /// (e.g., scrolled it into view or marked as read).
    /// Null if not yet read.
    /// </summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// Current delivery status of the message.
    /// </summary>
    public MessageStatus Status { get; set; } = MessageStatus.Sent;

    // ── Order Reference Data (for MessageType.OrderReference) ──────────────────

    /// <summary>
    /// Optional reference to an Order. When this message is an order reference card,
    /// this field links to the actual order.
    /// Null for regular text messages.
    /// </summary>
    public int? ReferencedOrderId { get; set; }

    /// <summary>
    /// Navigation property to the referenced order.
    /// </summary>
    public Order? ReferencedOrder { get; set; }

    /// <summary>
    /// Optional reference to an Item. When this message is a pre-order item inquiry,
    /// this field links to the item being discussed.
    /// </summary>
    public int? ReferencedItemId { get; set; }

    /// <summary>
    /// Navigation property to the referenced item.
    /// </summary>
    public Item? ReferencedItem { get; set; }

    /// <summary>
    /// Additional metadata stored as JSON for rich message types.
    /// Example for MeetingProposal: {"location":"SM North","time":"2026-04-20T15:00:00Z"}
    /// Example for OrderReference: {"price":500,"condition":"Good","size":"M"}
    /// </summary>
    [MaxLength(1000)]
    public string? MetadataJson { get; set; }

    // ── Navigation Properties ─────────────────────────────────────────────────

    public Conversation Conversation { get; set; } = null!;
    public User Sender { get; set; } = null!;
}