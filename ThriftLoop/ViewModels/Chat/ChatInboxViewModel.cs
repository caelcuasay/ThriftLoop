// ViewModels/Chat/ChatInboxViewModel.cs
using ThriftLoop.DTOs.Chat;

namespace ThriftLoop.ViewModels.Chat;

/// <summary>
/// ViewModel for the main chat inbox page.
/// </summary>
public class ChatInboxViewModel
{
    /// <summary>
    /// List of conversations for the current user.
    /// </summary>
    public List<ConversationDTO> Conversations { get; set; } = new();

    /// <summary>
    /// Total number of unread messages across all conversations.
    /// </summary>
    public int TotalUnreadCount { get; set; }

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// Whether there are more conversations to load.
    /// </summary>
    public bool HasMoreConversations { get; set; }

    /// <summary>
    /// Total number of conversations.
    /// </summary>
    public int TotalConversations { get; set; }

    /// <summary>
    /// Whether the inbox is empty (no conversations).
    /// </summary>
    public bool IsEmpty => Conversations.Count == 0;

    /// <summary>
    /// Formatted page title showing unread count if any.
    /// </summary>
    public string PageTitle => TotalUnreadCount > 0
        ? $"Messages ({TotalUnreadCount})"
        : "Messages";
}