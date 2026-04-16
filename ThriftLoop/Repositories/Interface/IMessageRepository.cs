// Repositories/Interface/IMessageRepository.cs
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
}