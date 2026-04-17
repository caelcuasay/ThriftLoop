// Services/Interface/IChatService.cs
using ThriftLoop.DTOs.Chat;
using ThriftLoop.Enums;
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

    // ── Order/Item Chat Integration ───────────────────────────────────────────

    /// <summary>
    /// Contacts a seller about a specific item (pre-order inquiry).
    /// Creates or gets a conversation linked to the item, and sends an initial order reference message.
    /// </summary>
    /// <param name="buyerId">The user inquiring about the item.</param>
    /// <param name="itemId">The item being discussed.</param>
    /// <param name="initialMessage">Optional initial text message from the buyer.</param>
    /// <returns>The conversation detail DTO with the item context.</returns>
    Task<ConversationDetailDTO> ContactSellerAboutItemAsync(int buyerId, int itemId, string? initialMessage = null);

    /// <summary>
    /// Initializes a chat for a confirmed Halfway or Pickup order.
    /// Links the conversation to the order and sends an order confirmation message.
    /// </summary>
    /// <param name="orderId">The confirmed order.</param>
    /// <returns>The conversation ID that was created or linked.</returns>
    Task<int> InitializeOrderChatAsync(int orderId);

    /// <summary>
    /// Sends an order reference message in an existing conversation.
    /// Used when seller accepts an inquiry and creates an order.
    /// </summary>
    /// <param name="conversationId">The conversation to add the message to.</param>
    /// <param name="orderId">The newly created order.</param>
    /// <param name="senderId">The user sending the message (usually seller).</param>
    /// <returns>The created message DTO.</returns>
    Task<MessageDTO> SendOrderConfirmationMessageAsync(int conversationId, int orderId, int senderId);

    /// <summary>
    /// Creates a meeting proposal message.
    /// </summary>
    /// <param name="conversationId">The conversation.</param>
    /// <param name="senderId">The user proposing the meeting.</param>
    /// <param name="location">Meeting location.</param>
    /// <param name="proposedTime">Proposed meeting time (local time, will be converted to UTC).</param>
    /// <param name="notes">Optional additional notes.</param>
    /// <returns>The created message DTO.</returns>
    Task<MessageDTO> SendMeetingProposalAsync(int conversationId, int senderId, string location, DateTime proposedTime, string? notes = null);

    /// <summary>
    /// Gets the item context for a conversation.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <returns>Item details if the conversation has context, null otherwise.</returns>
    Task<ItemContextDTO?> GetConversationItemContextAsync(int conversationId);

    /// <summary>
    /// Checks if a conversation already has an active order or item inquiry.
    /// </summary>
    Task<bool> ConversationHasActiveContextAsync(int conversationId);

    // ── Inquiry Management Methods ────────────────────────────────────────────

    /// <summary>
    /// Seller accepts an item inquiry.
    /// Updates the inquiry status to Accepted and notifies the buyer.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="messageId">The order reference message ID.</param>
    /// <param name="sellerId">The seller's user ID (for authorization).</param>
    /// <param name="note">Optional note from seller.</param>
    /// <returns>The updated order reference DTO.</returns>
    Task<InquiryActionResponseDTO> AcceptInquiryAsync(int conversationId, int messageId, int sellerId, string? note = null);

    /// <summary>
    /// Seller declines an item inquiry.
    /// Updates the inquiry status to Declined and notifies the buyer.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="messageId">The order reference message ID.</param>
    /// <param name="sellerId">The seller's user ID (for authorization).</param>
    /// <param name="note">Optional note from seller.</param>
    /// <returns>The updated order reference DTO.</returns>
    Task<InquiryActionResponseDTO> DeclineInquiryAsync(int conversationId, int messageId, int sellerId, string? note = null);

    /// <summary>
    /// Buyer cancels their own item inquiry.
    /// Updates the inquiry status to Cancelled and notifies the seller.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="messageId">The order reference message ID.</param>
    /// <param name="buyerId">The buyer's user ID (for authorization).</param>
    /// <returns>The updated order reference DTO.</returns>
    Task<InquiryActionResponseDTO> CancelInquiryAsync(int conversationId, int messageId, int buyerId);

    /// <summary>
    /// Processes expired inquiries (called by background service).
    /// Updates status to Expired for all pending inquiries past their expiration.
    /// </summary>
    /// <returns>Number of inquiries expired.</returns>
    Task<int> ProcessExpiredInquiriesAsync();

    /// <summary>
    /// Gets all pending inquiries for a seller.
    /// </summary>
    /// <param name="sellerId">The seller's user ID.</param>
    /// <returns>List of conversation DTOs with pending inquiries.</returns>
    Task<List<ConversationDTO>> GetPendingInquiriesForSellerAsync(int sellerId);

    /// <summary>
    /// Gets all pending inquiries initiated by a buyer.
    /// </summary>
    /// <param name="buyerId">The buyer's user ID.</param>
    /// <returns>List of conversation DTOs with pending inquiries.</returns>
    Task<List<ConversationDTO>> GetPendingInquiriesForBuyerAsync(int buyerId);

    /// <summary>
    /// Gets the order reference message for a specific conversation.
    /// </summary>
    /// <param name="conversationId">The conversation ID.</param>
    /// <returns>The order reference DTO, or null if none exists.</returns>
    Task<OrderReferenceDTO?> GetOrderReferenceForConversationAsync(int conversationId);
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

/// <summary>
/// DTO for item context in a conversation.
/// </summary>
public class ItemContextDTO
{
    public int ItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Condition { get; set; } = string.Empty;
    public string? Size { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int SellerId { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public bool AllowHalfway { get; set; }
    public bool AllowPickup { get; set; }
    public bool AllowDelivery { get; set; }
    public string FormattedPrice => $"₱{Price:N2}";
}