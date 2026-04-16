// ViewModels/Chat/ChatConversationViewModel.cs
using ThriftLoop.DTOs.Chat;

namespace ThriftLoop.ViewModels.Chat;

/// <summary>
/// ViewModel for the conversation detail page.
/// </summary>
public class ChatConversationViewModel
{
    /// <summary>
    /// The conversation details including messages.
    /// </summary>
    public ConversationDetailDTO Conversation { get; set; } = null!;

    /// <summary>
    /// Current user's ID.
    /// </summary>
    public int CurrentUserId { get; set; }

    /// <summary>
    /// Total unread count across all conversations (for sidebar).
    /// </summary>
    public int TotalUnreadCount { get; set; }

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// Whether there are older messages to load.
    /// </summary>
    public bool HasOlderMessages => Conversation?.HasMoreMessages ?? false;

    /// <summary>
    /// Pre-filled message content (e.g., from query string when starting chat).
    /// </summary>
    public string? PrefillMessage { get; set; }

    /// <summary>
    /// Maximum allowed message length.
    /// </summary>
    public int MaxMessageLength => 2000;

    /// <summary>
    /// Whether the other user is online.
    /// </summary>
    public bool IsOtherUserOnline => Conversation?.IsOtherUserOnline ?? false;

    /// <summary>
    /// Formatted active status text for the other user.
    /// </summary>
    public string OtherUserStatus => Conversation?.OtherUserActiveStatus ?? "Offline";

    /// <summary>
    /// CSS class for online status indicator.
    /// </summary>
    public string OnlineStatusClass => IsOtherUserOnline ? "online" : "offline";
}