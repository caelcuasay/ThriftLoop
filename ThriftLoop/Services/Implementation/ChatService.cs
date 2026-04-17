// Services/Implementation/ChatService.cs
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ThriftLoop.Data;
using ThriftLoop.DTOs.Chat;
using ThriftLoop.Enums;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.Services.Interface;

namespace ThriftLoop.Services.Implementation;

public class ChatService : IChatService
{
    private readonly IConversationRepository _conversationRepo;
    private readonly IMessageRepository _messageRepo;
    private readonly IUserRepository _userRepo;
    private readonly IChatNotificationService _notificationService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ChatService> _logger;
    private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web);

    public ChatService(
        IConversationRepository conversationRepo,
        IMessageRepository messageRepo,
        IUserRepository userRepo,
        IChatNotificationService notificationService,
        ApplicationDbContext context,
        ILogger<ChatService> logger)
    {
        _conversationRepo = conversationRepo;
        _messageRepo = messageRepo;
        _userRepo = userRepo;
        _notificationService = notificationService;
        _context = context;
        _logger = logger;
    }

    public async Task<List<ConversationDTO>> GetUserInboxAsync(int userId, int page = 1, int pageSize = 20)
    {
        var conversations = await _conversationRepo.GetUserConversationsPaginatedAsync(userId, page, pageSize);
        var result = new List<ConversationDTO>();

        foreach (var conv in conversations)
        {
            var otherUserId = conv.UserOneId == userId ? conv.UserTwoId : conv.UserOneId;
            var otherUser = otherUserId == conv.UserOneId ? conv.UserOne : conv.UserTwo;

            var lastMessage = conv.Messages.FirstOrDefault();
            var unreadCount = await _conversationRepo.GetUnreadCountAsync(conv.Id, userId);

            var dto = new ConversationDTO
            {
                Id = conv.Id,
                OtherUserId = otherUserId,
                OtherUserName = otherUser?.FullName ?? otherUser?.Email ?? "Unknown User",
                OtherUserAvatarUrl = null,
                LastMessage = GetLastMessagePreview(lastMessage),
                LastMessageAt = conv.LastMessageAt,
                UnreadCount = unreadCount,
                LastMessageIsFromCurrentUser = lastMessage?.SenderId == userId,
                LastMessageStatus = lastMessage?.Status.ToString() ?? string.Empty
            };

            result.Add(dto);
        }

        return result;
    }

    public async Task<ConversationDetailDTO?> GetConversationDetailAsync(int conversationId, int currentUserId, int page = 1, int pageSize = 50)
    {
        var conversation = await _conversationRepo.GetByIdWithContextAsync(conversationId);
        if (conversation == null)
            return null;

        // Check access
        if (conversation.UserOneId != currentUserId && conversation.UserTwoId != currentUserId)
            return null;

        var otherUserId = conversation.UserOneId == currentUserId ? conversation.UserTwoId : conversation.UserOneId;
        var otherUser = otherUserId == conversation.UserOneId ? conversation.UserOne : conversation.UserTwo;

        var messages = await _messageRepo.GetMessagesByConversationAsync(conversationId, page, pageSize);
        var totalCount = await _messageRepo.GetTotalCountAsync(conversationId);

        var isOnline = await _notificationService.IsUserOnlineAsync(otherUserId);
        var lastActive = await _notificationService.GetLastActiveTimeAsync(otherUserId);

        var messageDtos = new List<MessageDTO>();
        foreach (var m in messages)
        {
            var dto = new MessageDTO
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderId = m.SenderId,
                SenderName = m.SenderId == 0 ? "System" : (m.Sender?.FullName ?? m.Sender?.Email ?? "Unknown"),
                SenderAvatarUrl = null,
                Content = m.Content,
                SentAt = m.SentAt,
                Status = m.Status,
                MessageType = m.MessageType,
                IsFromCurrentUser = m.SenderId == currentUserId,
                FormattedTime = FormatMessageTime(m.SentAt),
                Metadata = !string.IsNullOrEmpty(m.MetadataJson)
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(m.MetadataJson, _jsonOpts)
                    : null
            };

            // Populate order reference if applicable
            if (m.MessageType == MessageType.OrderReference || m.MessageType == MessageType.OrderConfirmed)
            {
                var item = m.ReferencedItem ?? m.ReferencedOrder?.Item;
                var order = m.ReferencedOrder;

                if (item != null)
                {
                    // Use the full builder method to get all inquiry status and permission flags
                    var orderRef = await BuildOrderReferenceFromConversation(conversation, m.Id, currentUserId);
                    dto.OrderReference = orderRef;
                }
            }

            messageDtos.Add(dto);
        }

        // Mark messages as read when user views the conversation
        await _messageRepo.MarkAllAsReadAsync(conversationId, currentUserId);

        var detailDto = new ConversationDetailDTO
        {
            Id = conversation.Id,
            OtherUserId = otherUserId,
            OtherUserName = otherUser?.FullName ?? otherUser?.Email ?? "Unknown User",
            OtherUserAvatarUrl = null,
            IsOtherUserOnline = isOnline,
            OtherUserLastActiveAt = lastActive,
            OtherUserActiveStatus = GetActiveStatusText(isOnline, lastActive),
            Messages = messageDtos.OrderBy(m => m.SentAt).ToList(),
            TotalMessageCount = totalCount,
            Page = page,
            PageSize = pageSize,
            HasMoreMessages = (page * pageSize) < totalCount,
            CreatedAt = conversation.CreatedAt,
            LinkedOrderId = conversation.OrderId,
            ContextItemId = conversation.ContextItemId,
            LinkedItemTitle = conversation.ContextItem?.Title ?? conversation.Order?.Item?.Title,
            LinkedItemPrice = conversation.ContextItem?.Price ?? conversation.Order?.Item?.Price,
            LinkedItemImageUrl = conversation.ContextItem?.ImageUrls?.FirstOrDefault() ?? conversation.Order?.Item?.ImageUrls?.FirstOrDefault(),
            LinkedOrderStatus = conversation.Order?.Status.ToString(),
            LinkedFulfillmentMethod = conversation.Order?.FulfillmentMethod.ToString(),
            CanCreateOrder = CanUserCreateOrderFromConversation(conversation, currentUserId),
            CanConfirmReceipt = CanUserConfirmReceipt(conversation, currentUserId)
        };

        return detailDto;
    }

    public async Task<ConversationDTO> StartConversationAsync(int currentUserId, StartConversationDTO dto)
    {
        if (currentUserId == dto.RecipientId)
            throw new InvalidOperationException("Cannot start a conversation with yourself.");

        var recipient = await _userRepo.GetByIdAsync(dto.RecipientId);
        if (recipient == null)
            throw new InvalidOperationException("Recipient not found.");

        var conversation = await _conversationRepo.GetOrCreateAsync(currentUserId, dto.RecipientId);

        // If an initial message was provided, send it
        if (!string.IsNullOrWhiteSpace(dto.InitialMessage))
        {
            var sendDto = new SendMessageDTO
            {
                ConversationId = conversation.Id,
                Content = dto.InitialMessage,
                MessageType = MessageType.Text
            };
            await SendMessageAsync(currentUserId, sendDto);
        }

        var otherUser = await _userRepo.GetByIdAsync(dto.RecipientId);
        var unreadCount = await _conversationRepo.GetUnreadCountAsync(conversation.Id, currentUserId);

        return new ConversationDTO
        {
            Id = conversation.Id,
            OtherUserId = dto.RecipientId,
            OtherUserName = otherUser?.FullName ?? otherUser?.Email ?? "Unknown User",
            OtherUserAvatarUrl = null,
            LastMessage = dto.InitialMessage ?? "Conversation started",
            LastMessageAt = conversation.LastMessageAt,
            UnreadCount = unreadCount,
            LastMessageIsFromCurrentUser = !string.IsNullOrWhiteSpace(dto.InitialMessage),
            LastMessageStatus = MessageStatus.Sent.ToString()
        };
    }

    public async Task<MessageDTO> SendMessageAsync(int senderId, SendMessageDTO dto)
    {
        Conversation conversation;

        if (dto.ConversationId.HasValue)
        {
            conversation = await _conversationRepo.GetByIdAsync(dto.ConversationId.Value);
            if (conversation == null)
                throw new InvalidOperationException("Conversation not found.");

            // Verify sender is part of the conversation
            if (conversation.UserOneId != senderId && conversation.UserTwoId != senderId)
                throw new InvalidOperationException("You are not a participant in this conversation.");
        }
        else if (dto.RecipientId.HasValue)
        {
            if (senderId == dto.RecipientId.Value)
                throw new InvalidOperationException("Cannot send a message to yourself.");

            var recipient = await _userRepo.GetByIdAsync(dto.RecipientId.Value);
            if (recipient == null)
                throw new InvalidOperationException("Recipient not found.");

            conversation = await _conversationRepo.GetOrCreateAsync(senderId, dto.RecipientId.Value);
        }
        else
        {
            throw new InvalidOperationException("Either ConversationId or RecipientId must be provided.");
        }

        // Handle context item/order if provided
        if (dto.ContextItemId.HasValue && conversation.ContextItemId == null)
        {
            conversation.ContextItemId = dto.ContextItemId;
            await _context.SaveChangesAsync();
        }
        if (dto.ContextOrderId.HasValue && conversation.OrderId == null)
        {
            conversation.OrderId = dto.ContextOrderId;
            await _context.SaveChangesAsync();
        }

        var message = new Message
        {
            ConversationId = conversation.Id,
            SenderId = senderId,
            Content = dto.Content.Trim(),
            MessageType = dto.MessageType,
            ReferencedItemId = dto.ContextItemId,
            ReferencedOrderId = dto.ContextOrderId,
            MetadataJson = dto.MetadataJson,
            SentAt = DateTime.UtcNow,
            Status = MessageStatus.Sent
        };

        var createdMessage = await _messageRepo.CreateAsync(message);
        await _conversationRepo.UpdateLastMessageTimeAsync(conversation.Id, message.SentAt);

        var sender = await _userRepo.GetByIdAsync(senderId);

        return new MessageDTO
        {
            Id = createdMessage.Id,
            ConversationId = createdMessage.ConversationId,
            SenderId = createdMessage.SenderId,
            SenderName = sender?.FullName ?? sender?.Email ?? "Unknown",
            SenderAvatarUrl = null,
            Content = createdMessage.Content,
            SentAt = createdMessage.SentAt,
            Status = createdMessage.Status,
            MessageType = createdMessage.MessageType,
            IsFromCurrentUser = true,
            FormattedTime = FormatMessageTime(createdMessage.SentAt)
        };
    }

    // ── Order/Item Chat Integration ───────────────────────────────────────────

    public async Task<ConversationDetailDTO> ContactSellerAboutItemAsync(int buyerId, int itemId, string? initialMessage = null)
    {
        var item = await _context.Items
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.Id == itemId);

        if (item == null)
            throw new InvalidOperationException("Item not found.");

        if (item.UserId == buyerId)
            throw new InvalidOperationException("You cannot contact yourself about your own item.");

        var sellerId = item.UserId;

        // Get or create conversation with item context
        var conversation = await _conversationRepo.GetOrCreateForItemAsync(buyerId, sellerId, itemId);

        // Check if we already sent an order reference for this item
        var hasOrderReference = await _messageRepo.HasOrderReferenceForItemAsync(conversation.Id, itemId);

        if (!hasOrderReference)
        {
            // Send order reference message (as system or buyer)
            await _messageRepo.CreateOrderReferenceMessageAsync(
                conversation.Id,
                buyerId,
                itemId,
                orderId: null,
                messageType: MessageType.OrderReference);
        }

        // Send initial text message if provided
        if (!string.IsNullOrWhiteSpace(initialMessage))
        {
            var sendDto = new SendMessageDTO
            {
                ConversationId = conversation.Id,
                Content = initialMessage,
                MessageType = MessageType.Text
            };
            await SendMessageAsync(buyerId, sendDto);
        }

        await _conversationRepo.UpdateLastMessageTimeAsync(conversation.Id, DateTime.UtcNow);

        return await GetConversationDetailAsync(conversation.Id, buyerId);
    }

    public async Task<int> InitializeOrderChatAsync(int orderId)
    {
        var order = await _context.Orders
            .Include(o => o.Item)
            .Include(o => o.Buyer)
            .Include(o => o.Seller)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
            throw new InvalidOperationException("Order not found.");

        // Only initialize chat for Halfway and Pickup orders
        if (order.FulfillmentMethod != FulfillmentMethod.Halfway && order.FulfillmentMethod != FulfillmentMethod.Pickup)
        {
            _logger.LogInformation("Chat not initialized for Order {OrderId} - Fulfillment method is {Method}",
                orderId, order.FulfillmentMethod);
            return 0;
        }

        // Get or create conversation linked to this order
        var conversation = await _conversationRepo.GetOrCreateForOrderAsync(order.BuyerId, order.SellerId, orderId);

        // Update order with conversation ID
        order.ChatConversationId = conversation.Id;
        order.ChatInitialized = true;
        await _context.SaveChangesAsync();

        // Check if order reference already exists
        var hasOrderReference = await _messageRepo.HasOrderReferenceForItemAsync(conversation.Id, order.ItemId);

        if (!hasOrderReference && order.Item != null)
        {
            // Send order confirmed message (as system)
            await _messageRepo.CreateOrderReferenceMessageAsync(
                conversation.Id,
                0, // System
                order.ItemId,
                orderId,
                MessageType.OrderConfirmed);
        }

        // Send system confirmation message
        var fulfillmentText = order.FulfillmentMethod == FulfillmentMethod.Halfway
            ? "halfway meetup"
            : "pickup";

        await _messageRepo.CreateSystemMessageAsync(
            conversation.Id,
            MessageType.OrderConfirmed,
            $"✅ Order #{order.Id} confirmed! Please coordinate {fulfillmentText} details in this chat.",
            orderId);

        await _conversationRepo.UpdateLastMessageTimeAsync(conversation.Id, DateTime.UtcNow);

        _logger.LogInformation("Chat initialized for Order {OrderId}, Conversation {ConversationId}", orderId, conversation.Id);

        return conversation.Id;
    }

    public async Task<MessageDTO> SendOrderConfirmationMessageAsync(int conversationId, int orderId, int senderId)
    {
        var order = await _context.Orders
            .Include(o => o.Item)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
            throw new InvalidOperationException("Order not found.");

        var message = await _messageRepo.CreateOrderReferenceMessageAsync(
            conversationId,
            senderId,
            order.ItemId,
            orderId,
            MessageType.OrderConfirmed);

        await _conversationRepo.UpdateLastMessageTimeAsync(conversationId, DateTime.UtcNow);

        var sender = await _userRepo.GetByIdAsync(senderId);

        return new MessageDTO
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            SenderName = sender?.FullName ?? sender?.Email ?? "Unknown",
            Content = message.Content,
            SentAt = message.SentAt,
            Status = message.Status,
            MessageType = message.MessageType,
            IsFromCurrentUser = true,
            FormattedTime = FormatMessageTime(message.SentAt),
            OrderReference = new OrderReferenceDTO
            {
                ItemId = order.ItemId,
                ItemTitle = order.Item?.Title ?? "Unknown Item",
                ItemImageUrl = order.Item?.ImageUrls?.FirstOrDefault(),
                Price = order.FinalPrice - order.DeliveryFee,
                Condition = order.Item?.Condition ?? "Unknown",
                Size = order.Item?.Size,
                Category = order.Item?.Category ?? "Unknown",
                OrderId = orderId,
                ConversationId = conversationId,
                SellerId = order.SellerId,
                SellerName = order.Seller?.Email?.Split('@')[0] ?? "Unknown",
                IsConfirmedOrder = true,
                FulfillmentMethod = order.FulfillmentMethod.ToString()
            }
        };
    }

    public async Task<MessageDTO> SendMeetingProposalAsync(int conversationId, int senderId, string location, DateTime proposedTime, string? notes = null)
    {
        var message = await _messageRepo.CreateMeetingProposalAsync(conversationId, senderId, location, proposedTime.ToUniversalTime(), notes);
        await _conversationRepo.UpdateLastMessageTimeAsync(conversationId, DateTime.UtcNow);

        var sender = await _userRepo.GetByIdAsync(senderId);

        return new MessageDTO
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            SenderName = sender?.FullName ?? sender?.Email ?? "Unknown",
            Content = message.Content,
            SentAt = message.SentAt,
            Status = message.Status,
            MessageType = message.MessageType,
            IsFromCurrentUser = true,
            FormattedTime = FormatMessageTime(message.SentAt),
            Metadata = !string.IsNullOrEmpty(message.MetadataJson)
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(message.MetadataJson, _jsonOpts)
                : null
        };
    }

    public async Task<ItemContextDTO?> GetConversationItemContextAsync(int conversationId)
    {
        var conversation = await _conversationRepo.GetByIdWithContextAsync(conversationId);
        if (conversation == null)
            return null;

        var item = conversation.ContextItem ?? conversation.Order?.Item;
        if (item == null)
            return null;

        return new ItemContextDTO
        {
            ItemId = item.Id,
            Title = item.Title,
            Price = item.Price,
            Condition = item.Condition,
            Size = item.Size,
            Category = item.Category,
            ImageUrl = item.ImageUrls?.FirstOrDefault(),
            SellerId = item.UserId,
            SellerName = item.User?.Email?.Split('@')[0] ?? "Unknown",
            AllowHalfway = item.AllowHalfway,
            AllowPickup = item.AllowPickup,
            AllowDelivery = item.AllowDelivery
        };
    }

    public async Task<bool> ConversationHasActiveContextAsync(int conversationId)
    {
        var conversation = await _conversationRepo.GetByIdAsync(conversationId);
        return conversation?.OrderId != null || conversation?.ContextItemId != null;
    }

    // ── Inquiry Management Methods ────────────────────────────────────────────

    public async Task<InquiryActionResponseDTO> AcceptInquiryAsync(int conversationId, int messageId, int sellerId, string? note = null)
    {
        var conversation = await _conversationRepo.GetByIdWithContextAsync(conversationId);
        if (conversation == null)
            return new InquiryActionResponseDTO { Success = false, Error = "Conversation not found." };

        // Verify seller is a participant
        if (conversation.UserOneId != sellerId && conversation.UserTwoId != sellerId)
            return new InquiryActionResponseDTO { Success = false, Error = "You are not authorized to accept this inquiry." };

        // Verify inquiry is pending
        if (conversation.InquiryStatus != InquiryStatus.Pending)
            return new InquiryActionResponseDTO { Success = false, Error = $"Inquiry is already {conversation.InquiryStatus}." };

        // Check if expired
        if (conversation.IsExpired)
        {
            await _conversationRepo.UpdateInquiryStatusAsync(conversationId, InquiryStatus.Expired);
            return new InquiryActionResponseDTO { Success = false, Error = "This inquiry has expired." };
        }

        // Update inquiry status
        await _conversationRepo.UpdateInquiryStatusAsync(conversationId, InquiryStatus.Accepted);

        // Send system message confirming acceptance
        var noteMessage = string.IsNullOrEmpty(note) ? "" : $"\n\n📝 Seller note: {note}";
        await _messageRepo.CreateSystemMessageAsync(
            conversationId,
            MessageType.OrderConfirmed,
            $"✅ Seller accepted your inquiry for '{conversation.ContextItem?.Title ?? "this item"}'! You can now proceed to checkout.{noteMessage}",
            orderId: null);

        await _conversationRepo.UpdateLastMessageTimeAsync(conversationId, DateTime.UtcNow);

        // Build updated order reference
        var orderRef = await BuildOrderReferenceFromConversation(conversation, messageId, sellerId);

        _logger.LogInformation("Seller {SellerId} accepted inquiry for Conversation {ConversationId}", sellerId, conversationId);

        return new InquiryActionResponseDTO
        {
            Success = true,
            Status = InquiryStatus.Accepted.ToString(),
            Message = "Inquiry accepted! The buyer can now proceed to checkout.",
            UpdatedReference = orderRef
        };
    }

    public async Task<InquiryActionResponseDTO> DeclineInquiryAsync(int conversationId, int messageId, int sellerId, string? note = null)
    {
        var conversation = await _conversationRepo.GetByIdWithContextAsync(conversationId);
        if (conversation == null)
            return new InquiryActionResponseDTO { Success = false, Error = "Conversation not found." };

        // Verify seller is a participant
        if (conversation.UserOneId != sellerId && conversation.UserTwoId != sellerId)
            return new InquiryActionResponseDTO { Success = false, Error = "You are not authorized to decline this inquiry." };

        // Verify inquiry is pending
        if (conversation.InquiryStatus != InquiryStatus.Pending)
            return new InquiryActionResponseDTO { Success = false, Error = $"Inquiry is already {conversation.InquiryStatus}." };

        // Update inquiry status
        await _conversationRepo.UpdateInquiryStatusAsync(conversationId, InquiryStatus.Declined);

        // Send system message about declination
        var noteMessage = string.IsNullOrEmpty(note) ? "" : $"\n\n📝 Seller note: {note}";
        await _messageRepo.CreateSystemMessageAsync(
            conversationId,
            MessageType.Text,
            $"❌ Seller declined your inquiry for '{conversation.ContextItem?.Title ?? "this item"}'.{noteMessage}",
            orderId: null);

        await _conversationRepo.UpdateLastMessageTimeAsync(conversationId, DateTime.UtcNow);

        // Build updated order reference
        var orderRef = await BuildOrderReferenceFromConversation(conversation, messageId, sellerId);

        _logger.LogInformation("Seller {SellerId} declined inquiry for Conversation {ConversationId}", sellerId, conversationId);

        return new InquiryActionResponseDTO
        {
            Success = true,
            Status = InquiryStatus.Declined.ToString(),
            Message = "Inquiry declined.",
            UpdatedReference = orderRef
        };
    }

    public async Task<InquiryActionResponseDTO> CancelInquiryAsync(int conversationId, int messageId, int buyerId)
    {
        var conversation = await _conversationRepo.GetByIdWithContextAsync(conversationId);
        if (conversation == null)
            return new InquiryActionResponseDTO { Success = false, Error = "Conversation not found." };

        // Determine which participant is the buyer (the one who is NOT the seller of the item)
        var item = conversation.ContextItem;
        if (item == null)
            return new InquiryActionResponseDTO { Success = false, Error = "No item context found for this conversation." };

        var sellerId = item.UserId;
        var isBuyer = (conversation.UserOneId == buyerId && conversation.UserTwoId == sellerId) ||
                      (conversation.UserTwoId == buyerId && conversation.UserOneId == sellerId);

        if (!isBuyer)
            return new InquiryActionResponseDTO { Success = false, Error = "You are not authorized to cancel this inquiry." };

        // Verify inquiry is pending
        if (conversation.InquiryStatus != InquiryStatus.Pending)
            return new InquiryActionResponseDTO { Success = false, Error = $"Inquiry is already {conversation.InquiryStatus}." };

        // Update inquiry status
        await _conversationRepo.UpdateInquiryStatusAsync(conversationId, InquiryStatus.Cancelled);

        // Send system message about cancellation
        await _messageRepo.CreateSystemMessageAsync(
            conversationId,
            MessageType.Text,
            $"🔔 Buyer cancelled their inquiry for '{item.Title}'.",
            orderId: null);

        await _conversationRepo.UpdateLastMessageTimeAsync(conversationId, DateTime.UtcNow);

        // Build updated order reference
        var orderRef = await BuildOrderReferenceFromConversation(conversation, messageId, buyerId);

        _logger.LogInformation("Buyer {BuyerId} cancelled inquiry for Conversation {ConversationId}", buyerId, conversationId);

        return new InquiryActionResponseDTO
        {
            Success = true,
            Status = InquiryStatus.Cancelled.ToString(),
            Message = "Inquiry cancelled.",
            UpdatedReference = orderRef
        };
    }

    public async Task<int> ProcessExpiredInquiriesAsync()
    {
        var expiredConversations = await _conversationRepo.GetExpiredPendingInquiriesAsync();
        var expiredCount = 0;

        foreach (var conversation in expiredConversations)
        {
            await _conversationRepo.UpdateInquiryStatusAsync(conversation.Id, InquiryStatus.Expired);

            // Send system message about expiration
            await _messageRepo.CreateSystemMessageAsync(
                conversation.Id,
                MessageType.Text,
                $"⏰ This inquiry has expired. The item '{conversation.ContextItem?.Title ?? "this item"}' is still available for others to inquire about.",
                orderId: null);

            expiredCount++;
            _logger.LogInformation("Inquiry expired for Conversation {ConversationId}", conversation.Id);
        }

        if (expiredCount > 0)
        {
            _logger.LogInformation("Processed {Count} expired inquiries", expiredCount);
        }

        return expiredCount;
    }

    public async Task<List<ConversationDTO>> GetPendingInquiriesForSellerAsync(int sellerId)
    {
        var conversations = await _conversationRepo.GetPendingInquiriesForSellerAsync(sellerId);
        return conversations.Select(c => new ConversationDTO
        {
            Id = c.Id,
            OtherUserId = c.UserOneId == sellerId ? c.UserTwoId : c.UserOneId,
            OtherUserName = (c.UserOneId == sellerId ? c.UserTwo : c.UserOne)?.FullName ?? "Unknown",
            LastMessage = c.Messages.FirstOrDefault()?.Content ?? "New inquiry",
            LastMessageAt = c.LastMessageAt,
            UnreadCount = c.Messages.Count(m => m.SenderId != sellerId && m.ReadAt == null),
            LastMessageIsFromCurrentUser = c.Messages.FirstOrDefault()?.SenderId == sellerId
        }).ToList();
    }

    public async Task<List<ConversationDTO>> GetPendingInquiriesForBuyerAsync(int buyerId)
    {
        var conversations = await _conversationRepo.GetPendingInquiriesForBuyerAsync(buyerId);
        return conversations.Select(c => new ConversationDTO
        {
            Id = c.Id,
            OtherUserId = c.UserOneId == buyerId ? c.UserTwoId : c.UserOneId,
            OtherUserName = (c.UserOneId == buyerId ? c.UserTwo : c.UserOne)?.FullName ?? "Unknown",
            LastMessage = c.Messages.FirstOrDefault()?.Content ?? "Awaiting response",
            LastMessageAt = c.LastMessageAt,
            UnreadCount = c.Messages.Count(m => m.SenderId != buyerId && m.ReadAt == null),
            LastMessageIsFromCurrentUser = c.Messages.FirstOrDefault()?.SenderId == buyerId
        }).ToList();
    }

    public async Task<OrderReferenceDTO?> GetOrderReferenceForConversationAsync(int conversationId)
    {
        var conversation = await _conversationRepo.GetByIdWithContextAsync(conversationId);
        if (conversation?.ContextItem == null)
            return null;

        var orderRefMessage = await _messageRepo.GetLatestOrderReferenceAsync(conversationId);

        return await BuildOrderReferenceFromConversation(conversation, orderRefMessage?.Id ?? 0, 0);
    }

    // ── Private Helper for Order Reference ────────────────────────────────────

    private async Task<OrderReferenceDTO> BuildOrderReferenceFromConversation(Conversation conversation, int messageId, int currentUserId)
    {
        var item = conversation.ContextItem;
        var order = conversation.Order;

        var orderRef = new OrderReferenceDTO
        {
            MessageId = messageId,
            ItemId = item?.Id ?? 0,
            ItemTitle = item?.Title ?? "Unknown Item",
            ItemImageUrl = item?.ImageUrls?.FirstOrDefault(),
            Price = item?.Price ?? 0,
            Condition = item?.Condition ?? "Unknown",
            Size = item?.Size,
            Category = item?.Category ?? "Unknown",
            OrderId = order?.Id,
            ConversationId = conversation.Id,
            SellerId = item?.UserId ?? 0,
            SellerName = item?.User?.Email?.Split('@')[0] ?? "Unknown",
            IsConfirmedOrder = order != null,
            FulfillmentMethod = order?.FulfillmentMethod.ToString(),
            InquiryStatus = conversation.InquiryStatus,
            InquiryExpiresAt = conversation.InquiryExpiresAt
        };

        // Set permission flags based on current user
        var isSeller = item != null && item.UserId == currentUserId;
        var isBuyer = !isSeller && (conversation.UserOneId == currentUserId || conversation.UserTwoId == currentUserId);

        orderRef.CanAccept = isSeller && conversation.InquiryStatus == InquiryStatus.Pending && !conversation.IsExpired;
        orderRef.CanDecline = isSeller && conversation.InquiryStatus == InquiryStatus.Pending && !conversation.IsExpired;
        orderRef.CanCancel = isBuyer && conversation.InquiryStatus == InquiryStatus.Pending && !conversation.IsExpired;

        return orderRef;
    }

    // ── Existing methods (unchanged) ──────────────────────────────────────────

    public async Task MarkMessageAsReadAsync(int messageId, int currentUserId)
    {
        var message = await _messageRepo.GetByIdAsync(messageId);
        if (message == null)
            return;

        var conversation = await _conversationRepo.GetByIdAsync(message.ConversationId);
        if (conversation == null)
            return;

        if (message.SenderId == currentUserId)
            return;

        if (conversation.UserOneId != currentUserId && conversation.UserTwoId != currentUserId)
            return;

        await _messageRepo.MarkAsReadAsync(messageId);
    }

    public async Task MarkConversationAsReadAsync(int conversationId, int currentUserId)
    {
        if (!await _conversationRepo.IsUserInConversationAsync(conversationId, currentUserId))
            return;

        await _messageRepo.MarkAllAsReadAsync(conversationId, currentUserId);
    }

    public async Task<int> GetTotalUnreadCountAsync(int userId)
    {
        return await _conversationRepo.GetTotalUnreadCountAsync(userId);
    }

    public async Task<bool> CanAccessConversationAsync(int conversationId, int userId)
    {
        return await _conversationRepo.IsUserInConversationAsync(conversationId, userId);
    }

    public async Task<User?> GetOtherParticipantAsync(int conversationId, int currentUserId)
    {
        var conversation = await _conversationRepo.GetByIdAsync(conversationId);
        if (conversation == null)
            return null;

        var otherUserId = conversation.UserOneId == currentUserId ? conversation.UserTwoId : conversation.UserOneId;
        return await _userRepo.GetByIdAsync(otherUserId);
    }

    public async Task<List<UserSearchResultDTO>> SearchUsersAsync(int currentUserId, string query, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return new List<UserSearchResultDTO>();

        var normalizedQuery = query.Trim().ToLowerInvariant();

        var users = await _context.Users
            .Where(u => u.Id != currentUserId)
            .Where(u => !u.IsDisabled)
            .Where(u => (u.FullName != null && u.FullName.ToLower().Contains(normalizedQuery)) ||
                       u.Email.ToLower().Contains(normalizedQuery))
            .Take(maxResults)
            .AsNoTracking()
            .ToListAsync();

        var result = new List<UserSearchResultDTO>();

        foreach (var user in users)
        {
            var existingConversation = await _conversationRepo.GetByParticipantsAsync(currentUserId, user.Id);

            result.Add(new UserSearchResultDTO
            {
                Id = user.Id,
                Name = user.FullName ?? user.Email,
                Email = user.Email,
                AvatarUrl = null,
                ExistingConversationId = existingConversation?.Id
            });
        }

        return result;
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    private string GetLastMessagePreview(Message? message)
    {
        if (message == null)
            return "No messages yet";

        return message.MessageType switch
        {
            MessageType.OrderReference => "📦 Order inquiry",
            MessageType.OrderConfirmed => "✅ Order confirmed",
            MessageType.MeetingProposal => "📍 Meeting proposed",
            _ => message.Content.Length > 50 ? message.Content[..50] + "..." : message.Content
        };
    }

    private bool CanUserCreateOrderFromConversation(Conversation conversation, int currentUserId)
    {
        // Seller can create order from item inquiry
        if (conversation.ContextItemId.HasValue && !conversation.OrderId.HasValue)
        {
            return conversation.UserOneId == currentUserId || conversation.UserTwoId == currentUserId;
        }
        return false;
    }

    private bool CanUserConfirmReceipt(Conversation conversation, int currentUserId)
    {
        if (!conversation.OrderId.HasValue)
            return false;

        var order = conversation.Order;
        if (order == null)
            return false;

        // Buyer can confirm receipt if order is pending and fulfillment is Halfway/Pickup
        return order.BuyerId == currentUserId &&
               order.Status == OrderStatus.Pending &&
               (order.FulfillmentMethod == FulfillmentMethod.Halfway ||
                order.FulfillmentMethod == FulfillmentMethod.Pickup);
    }

    private string FormatMessageTime(DateTime timestamp)
    {
        var now = DateTime.UtcNow;
        var localTime = timestamp.ToLocalTime();

        if (timestamp.Date == now.Date)
            return localTime.ToString("h:mm tt");

        if (timestamp.Date == now.Date.AddDays(-1))
            return "Yesterday";

        if (timestamp > now.AddDays(-7))
            return localTime.ToString("dddd");

        return localTime.ToString("MMM d, yyyy");
    }

    private string GetActiveStatusText(bool isOnline, DateTime? lastActive)
    {
        if (isOnline)
            return "Online now";

        if (lastActive.HasValue)
        {
            var timeAgo = DateTime.UtcNow - lastActive.Value;
            if (timeAgo.TotalMinutes < 1)
                return "Active just now";
            if (timeAgo.TotalMinutes < 60)
                return $"Active {timeAgo.TotalMinutes:0}m ago";
            if (timeAgo.TotalHours < 24)
                return $"Active {timeAgo.TotalHours:0}h ago";
            return $"Active {timeAgo.TotalDays:0}d ago";
        }

        return "Offline";
    }
}