// Repositories/Interface/IMessageRepository.cs
using ThriftLoop.Enums;
using ThriftLoop.Models;

namespace ThriftLoop.Repositories.Interface;

public interface IMessageRepository
{
    /// <summary>
    /// Gets a message by its ID.
    /// </summary>
    Task<Message?> GetByIdAsync(int id);

    /// <summary>
    /// Gets paginated messages for a conversation, ordered by most recent first.
    /// </summary>
    Task<List<Message>> GetMessagesByConversationAsync(int conversationId, int page, int pageSize);

    /// <summary>
    /// Gets the most recent message in a conversation.
    /// </summary>
    Task<Message?> GetLastMessageAsync(int conversationId);

    /// <summary>
    /// Creates a new message.
    /// </summary>
    Task<Message> CreateAsync(Message message);

    /// <summary>
    /// Marks a message as delivered.
    /// </summary>
    Task MarkAsDeliveredAsync(int messageId);

    /// <summary>
    /// Marks a message as read.
    /// </summary>
    Task MarkAsReadAsync(int messageId);

    /// <summary>
    /// Marks all messages in a conversation as delivered for a specific recipient.
    /// </summary>
    Task MarkAllAsDeliveredAsync(int conversationId, int recipientId);

    /// <summary>
    /// Marks all messages in a conversation as read for a specific recipient.
    /// </summary>
    Task MarkAllAsReadAsync(int conversationId, int recipientId);

    /// <summary>
    /// Gets the total count of messages in a conversation.
    /// </summary>
    Task<int> GetTotalCountAsync(int conversationId);

    /// <summary>
    /// Gets unread message IDs for a user in a conversation.
    /// Used for batch marking as read when user views the conversation.
    /// </summary>
    Task<List<int>> GetUnreadMessageIdsAsync(int conversationId, int userId);

    // ── Rich Message Methods ──────────────────────────────────────────────────

    /// <summary>
    /// Creates an order reference message (embedded item/order card).
    /// </summary>
    /// <param name="conversationId">The conversation to add the message to.</param>
    /// <param name="senderId">The user sending the message (usually system or buyer).</param>
    /// <param name="itemId">The item being referenced.</param>
    /// <param name="orderId">Optional order ID if this is a confirmed order.</param>
    /// <param name="messageType">Type of reference message.</param>
    /// <param name="metadataJson">Additional metadata for the message.</param>
    /// <returns>The created message.</returns>
    Task<Message> CreateOrderReferenceMessageAsync(
        int conversationId,
        int? senderId,
        int itemId,
        int? orderId = null,
        MessageType messageType = MessageType.OrderReference,
        string? metadataJson = null);

    /// <summary>
    /// Creates a system message (OrderConfirmed, PaymentReminder, etc.).
    /// </summary>
    /// <param name="conversationId">The conversation to add the message to.</param>
    /// <param name="messageType">Type of system message.</param>
    /// <param name="content">The message content.</param>
    /// <param name="orderId">Optional order ID for context.</param>
    /// <param name="metadataJson">Additional metadata.</param>
    /// <returns>The created message.</returns>
    Task<Message> CreateSystemMessageAsync(
        int conversationId,
        MessageType messageType,
        string content,
        int? orderId = null,
        string? metadataJson = null);

    /// <summary>
    /// Creates a meeting proposal message.
    /// </summary>
    /// <param name="conversationId">The conversation to add the message to.</param>
    /// <param name="senderId">The user proposing the meeting.</param>
    /// <param name="location">Meeting location.</param>
    /// <param name="proposedTime">Proposed meeting time (UTC).</param>
    /// <param name="notes">Additional notes.</param>
    /// <returns>The created message.</returns>
    Task<Message> CreateMeetingProposalAsync(
        int conversationId,
        int senderId,
        string location,
        DateTime proposedTime,
        string? notes = null);

    /// <summary>
    /// Gets all order reference messages in a conversation.
    /// </summary>
    Task<List<Message>> GetOrderReferenceMessagesAsync(int conversationId);

    /// <summary>
    /// Gets the most recent order reference message in a conversation.
    /// </summary>
    Task<Message?> GetLatestOrderReferenceAsync(int conversationId);

    /// <summary>
    /// Checks if a conversation already has an order reference for a specific item.
    /// Prevents duplicate order cards.
    /// </summary>
    Task<bool> HasOrderReferenceForItemAsync(int conversationId, int itemId);
}