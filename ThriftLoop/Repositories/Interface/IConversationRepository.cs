// Repositories/Interface/IConversationRepository.cs
using ThriftLoop.Models;

namespace ThriftLoop.Repositories.Interface;

public interface IConversationRepository
{
    /// <summary>
    /// Gets a conversation by its ID, including both participants.
    /// </summary>
    Task<Conversation?> GetByIdAsync(int id);

    /// <summary>
    /// Gets a conversation by the two participant IDs.
    /// Returns null if no conversation exists between these users.
    /// </summary>
    Task<Conversation?> GetByParticipantsAsync(int userOneId, int userTwoId);

    /// <summary>
    /// Gets all conversations for a user, ordered by most recent message first.
    /// Includes the other participant's info and the last message.
    /// </summary>
    Task<List<Conversation>> GetUserConversationsAsync(int userId);

    /// <summary>
    /// Gets paginated conversations for a user.
    /// </summary>
    Task<List<Conversation>> GetUserConversationsPaginatedAsync(int userId, int page, int pageSize);

    /// <summary>
    /// Creates a new conversation between two users.
    /// Ensures UserOneId < UserTwoId for consistency.
    /// </summary>
    Task<Conversation> CreateAsync(int userOneId, int userTwoId);

    /// <summary>
    /// Gets or creates a conversation between two users.
    /// </summary>
    Task<Conversation> GetOrCreateAsync(int userOneId, int userTwoId);

    /// <summary>
    /// Updates the LastMessageAt timestamp when a new message is sent.
    /// </summary>
    Task UpdateLastMessageTimeAsync(int conversationId, DateTime timestamp);

    /// <summary>
    /// Gets the count of unread messages for a user in a specific conversation.
    /// </summary>
    Task<int> GetUnreadCountAsync(int conversationId, int userId);

    /// <summary>
    /// Gets total unread message count across all conversations for a user.
    /// </summary>
    Task<int> GetTotalUnreadCountAsync(int userId);

    /// <summary>
    /// Checks if a user is a participant in a conversation.
    /// </summary>
    Task<bool> IsUserInConversationAsync(int conversationId, int userId);
}