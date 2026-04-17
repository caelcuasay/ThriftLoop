// Repositories/Interface/IConversationRepository.cs
using ThriftLoop.Enums;
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

    // ── Inquiry Management Methods ────────────────────────────────────────────

    /// <summary>
    /// Updates the inquiry status of a conversation.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="status">The new inquiry status.</param>
    /// <returns>True if updated successfully.</returns>
    Task<bool> UpdateInquiryStatusAsync(int conversationId, InquiryStatus status);

    /// <summary>
    /// Gets all conversations with pending inquiries that have expired.
    /// Used by background service to auto-expire stale inquiries.
    /// </summary>
    /// <returns>List of expired pending inquiries.</returns>
    Task<List<Conversation>> GetExpiredPendingInquiriesAsync();

    /// <summary>
    /// Gets all active inquiries for a specific seller.
    /// </summary>
    /// <param name="sellerId">The seller's user ID.</param>
    /// <returns>List of conversations with pending inquiries.</returns>
    Task<List<Conversation>> GetPendingInquiriesForSellerAsync(int sellerId);

    /// <summary>
    /// Gets all active inquiries initiated by a specific buyer.
    /// </summary>
    /// <param name="buyerId">The buyer's user ID.</param>
    /// <returns>List of conversations with pending inquiries.</returns>
    Task<List<Conversation>> GetPendingInquiriesForBuyerAsync(int buyerId);

    /// <summary>
    /// Checks if a conversation has an active (pending) inquiry.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <returns>True if the conversation has a pending inquiry.</returns>
    Task<bool> HasActiveInquiryAsync(int conversationId);

    /// <summary>
    /// Gets the inquiry status of a conversation.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <returns>The inquiry status, or null if not an inquiry conversation.</returns>
    Task<InquiryStatus?> GetInquiryStatusAsync(int conversationId);
}