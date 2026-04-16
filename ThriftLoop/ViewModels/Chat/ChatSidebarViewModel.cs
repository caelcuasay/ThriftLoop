// ViewModels/Chat/ChatSidebarViewModel.cs
using ThriftLoop.DTOs.Chat;

namespace ThriftLoop.ViewModels.Chat;

/// <summary>
/// ViewModel for the chat sidebar component (used across chat pages).
/// </summary>
public class ChatSidebarViewModel
{
    /// <summary>
    /// List of recent conversations for the sidebar.
    /// </summary>
    public List<ConversationDTO> RecentConversations { get; set; } = new();

    /// <summary>
    /// Total number of unread messages across all conversations.
    /// </summary>
    public int TotalUnreadCount { get; set; }

    /// <summary>
    /// Currently active conversation ID (if viewing a specific conversation).
    /// </summary>
    public int? ActiveConversationId { get; set; }

    /// <summary>
    /// Current user's ID.
    /// </summary>
    public int CurrentUserId { get; set; }

    /// <summary>
    /// Whether the sidebar should show a "Load More" button.
    /// </summary>
    public bool HasMoreConversations { get; set; }

    /// <summary>
    /// Current page for pagination.
    /// </summary>
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// Whether the sidebar is in a collapsed/minimized state (mobile).
    /// </summary>
    public bool IsCollapsed { get; set; }

    /// <summary>
    /// Title text with unread badge count.
    /// </summary>
    public string TitleText => TotalUnreadCount > 0
        ? $"Conversations ({TotalUnreadCount})"
        : "Conversations";

    /// <summary>
    /// Whether the sidebar has any conversations to display.
    /// </summary>
    public bool HasConversations => RecentConversations.Count > 0;

    /// <summary>
    /// Empty state message to display when there are no conversations.
    /// </summary>
    public string EmptyStateMessage => "No conversations yet. Start a chat with someone!";

    /// <summary>
    /// Gets the CSS class for a conversation item based on its state.
    /// </summary>
    public string GetConversationItemClass(ConversationDTO conversation)
    {
        var classes = new List<string> { "conversation-item" };

        if (conversation.Id == ActiveConversationId)
            classes.Add("active");

        if (conversation.UnreadCount > 0)
            classes.Add("unread");

        return string.Join(" ", classes);
    }

    /// <summary>
    /// Gets the formatted last message preview text.
    /// </summary>
    public string GetLastMessagePreview(ConversationDTO conversation)
    {
        var prefix = conversation.LastMessageIsFromCurrentUser ? "You: " : "";
        var message = conversation.LastMessage.Length > 30
            ? conversation.LastMessage.Substring(0, 30) + "..."
            : conversation.LastMessage;

        return prefix + message;
    }

    /// <summary>
    /// Gets the unread badge text (capped at 99+).
    /// </summary>
    public string GetUnreadBadgeText(int unreadCount)
    {
        return unreadCount > 99 ? "99+" : unreadCount.ToString();
    }

    /// <summary>
    /// Gets the relative time string for last message.
    /// </summary>
    public string GetRelativeTime(DateTime timestamp)
    {
        var now = DateTime.UtcNow;
        var diff = now - timestamp;

        if (diff.TotalMinutes < 1)
            return "just now";
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7)
            return $"{(int)diff.TotalDays}d ago";

        return timestamp.ToLocalTime().ToString("MMM d");
    }
}