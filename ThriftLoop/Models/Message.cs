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
    /// </summary>
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

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

    // ── Navigation Properties ─────────────────────────────────────────────────

    public Conversation Conversation { get; set; } = null!;
    public User Sender { get; set; } = null!;
}