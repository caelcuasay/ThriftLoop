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

    // ── Order/Item Context Methods ────────────────────────────────────────────

    /// <summary>
    /// Gets or creates a conversation for a specific item inquiry.
    /// Links the conversation to the context item.
    /// </summary>
    /// <param name="buyerId">The user inquiring about the item.</param>
    /// <param name="sellerId">The seller of the item.</param>
    /// <param name="itemId">The item being discussed.</param>
    /// <returns>The existing or newly created conversation.</returns>
    Task<Conversation> GetOrCreateForItemAsync(int buyerId, int sellerId, int itemId);

    /// <summary>
    /// Gets or creates a conversation for a confirmed order.
    /// Links the conversation to the order.
    /// </summary>
    /// <param name="buyerId">The buyer.</param>
    /// <param name="sellerId">The seller.</param>
    /// <param name="orderId">The confirmed order.</param>
    /// <returns>The existing or newly created conversation.</returns>
    Task<Conversation> GetOrCreateForOrderAsync(int buyerId, int sellerId, int orderId);

    /// <summary>
    /// Gets a conversation by its linked order ID.
    /// </summary>
    Task<Conversation?> GetByOrderIdAsync(int orderId);

    /// <summary>
    /// Gets a conversation by its context item ID and participants.
    /// </summary>
    Task<Conversation?> GetByItemAndParticipantsAsync(int itemId, int buyerId, int sellerId);

    /// <summary>
    /// Links an existing conversation to an order (called after checkout).
    /// </summary>
    Task LinkToOrderAsync(int conversationId, int orderId);

    /// <summary>
    /// Gets conversation details with full context (order, item) loaded.
    /// </summary>
    Task<Conversation?> GetByIdWithContextAsync(int conversationId);
}