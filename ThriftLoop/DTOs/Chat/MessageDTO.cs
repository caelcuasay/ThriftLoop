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
    /// Whether this message was sent by the current authenticated user.
    /// Used for styling (right-aligned bubbles for current user).
    /// </summary>
    public bool IsFromCurrentUser { get; set; }

    /// <summary>
    /// Formatted time string (e.g., "2:30 PM" or "Yesterday").
    /// </summary>
    public string FormattedTime { get; set; } = string.Empty;
}