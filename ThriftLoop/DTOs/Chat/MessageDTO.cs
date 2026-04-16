// DTOs/Chat/MessageDTO.cs
using ThriftLoop.Enums;

namespace ThriftLoop.DTOs.Chat;

/// <summary>
/// Represents a single message returned to the client.
/// </summary>
public class MessageDTO
{
    public int Id { get; set; }

    public int ConversationId { get; set; }

    public int SenderId { get; set; }

    /// <summary>
    /// Display name of the sender.
    /// </summary>
    public string SenderName { get; set; } = string.Empty;

    /// <summary>
    /// Profile picture URL of the sender (if available).
    /// </summary>
    public string? SenderAvatarUrl { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime SentAt { get; set; }

    public MessageStatus Status { get; set; }

    /// <summary>
    /// Type of message for rendering different bubble styles.
    /// </summary>
    public MessageType MessageType { get; set; } = MessageType.Text;

    /// <summary>
    /// Whether this message was sent by the current authenticated user.
    /// Used for styling (right-aligned bubbles for current user).
    /// </summary>
    public bool IsFromCurrentUser { get; set; }

    /// <summary>
    /// Formatted time string (e.g., "2:30 PM" or "Yesterday").
    /// </summary>
    public string FormattedTime { get; set; } = string.Empty;

    // ── Order Reference Data (populated when MessageType is OrderReference) ────

    /// <summary>
    /// Order reference data for embedded order card.
    /// Null for regular text messages.
    /// </summary>
    public OrderReferenceDTO? OrderReference { get; set; }

    /// <summary>
    /// Additional metadata for rich message types.
    /// Example for MeetingProposal: {"location":"SM North","time":"2026-04-20T15:00:00Z"}
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Helper property to check if this is a system message.
    /// </summary>
    public bool IsSystemMessage => MessageType == MessageType.OrderConfirmed ||
                                   MessageType == MessageType.PaymentReminder;

    /// <summary>
    /// Helper property to check if this message has rich content.
    /// </summary>
    public bool HasRichContent => MessageType != MessageType.Text;

    /// <summary>
    /// CSS class for the message bubble.
    /// </summary>
    public string BubbleClass
    {
        get
        {
            if (IsSystemMessage)
                return "message-bubble system";

            return IsFromCurrentUser ? "message-bubble sent" : "message-bubble received";
        }
    }
}