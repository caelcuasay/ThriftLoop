// DTOs/Chat/ConversationDTO.cs
namespace ThriftLoop.DTOs.Chat;

/// <summary>
/// Basic conversation information for inbox listing.
/// </summary>
public class ConversationDTO
{
    public int Id { get; set; }

    /// <summary>
    /// ID of the other user in the conversation (not the current user).
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
    /// The most recent message content in this conversation.
    /// </summary>
    public string LastMessage { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of the most recent message.
    /// </summary>
    public DateTime LastMessageAt { get; set; }

    /// <summary>
    /// Number of unread messages for the current user.
    /// </summary>
    public int UnreadCount { get; set; }

    /// <summary>
    /// Whether the last message was sent by the current user.
    /// Used to determine if "You: " prefix should be shown.
    /// </summary>
    public bool LastMessageIsFromCurrentUser { get; set; }

    /// <summary>
    /// Status of the last message (Sent, Delivered, Read).
    /// Only relevant if LastMessageIsFromCurrentUser is true.
    /// </summary>
    public string LastMessageStatus { get; set; } = string.Empty;
}