// Services/Interface/IChatService.cs
using ThriftLoop.DTOs.Chat;
using ThriftLoop.Models;

namespace ThriftLoop.Services.Interface;

public interface IChatService
{
    /// <summary>
    /// Gets the inbox (list of conversations) for a user.
    /// </summary>
    Task<List<ConversationDTO>> GetUserInboxAsync(int userId, int page = 1, int pageSize = 20);

    /// <summary>
    /// Gets detailed view of a specific conversation including messages.
    /// </summary>
    Task<ConversationDetailDTO?> GetConversationDetailAsync(int conversationId, int currentUserId, int page = 1, int pageSize = 50);

    /// <summary>
    /// Starts a new conversation between two users.
    /// </summary>
    Task<ConversationDTO> StartConversationAsync(int currentUserId, StartConversationDTO dto);

    /// <summary>
    /// Sends a message. Creates the conversation if it doesn't exist.
    /// </summary>
    Task<MessageDTO> SendMessageAsync(int senderId, SendMessageDTO dto);

    /// <summary>
    /// Marks a specific message as read.
    /// </summary>
    Task MarkMessageAsReadAsync(int messageId, int currentUserId);

    /// <summary>
    /// Marks all messages in a conversation as read for the current user.
    /// </summary>
    Task MarkConversationAsReadAsync(int conversationId, int currentUserId);

    /// <summary>
    /// Gets the total unread message count for a user across all conversations.
    /// </summary>
    Task<int> GetTotalUnreadCountAsync(int userId);

    /// <summary>
    /// Checks if a user can access a conversation.
    /// </summary>
    Task<bool> CanAccessConversationAsync(int conversationId, int userId);

    /// <summary>
    /// Gets the other participant in a conversation.
    /// </summary>
    Task<User?> GetOtherParticipantAsync(int conversationId, int currentUserId);

    /// <summary>
    /// Searches for users to start a conversation with.
    /// </summary>
    Task<List<UserSearchResultDTO>> SearchUsersAsync(int currentUserId, string query, int maxResults = 10);
}

/// <summary>
/// Simple DTO for user search results.
/// </summary>
public class UserSearchResultDTO
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public int? ExistingConversationId { get; set; }
}