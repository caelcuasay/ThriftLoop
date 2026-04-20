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
}