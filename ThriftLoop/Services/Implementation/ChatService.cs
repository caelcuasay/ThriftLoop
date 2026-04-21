// Services/Implementation/ChatService.cs

using Microsoft.AspNetCore.SignalR;

using Microsoft.EntityFrameworkCore;

using System.Text.Json;

using ThriftLoop.Data;

using ThriftLoop.DTOs.Chat;

using ThriftLoop.Enums;

using ThriftLoop.Hubs;

using ThriftLoop.Models;

using ThriftLoop.Repositories.Interface;

using ThriftLoop.Services.Interface;

using ThriftLoop.Services.WalletManagement.Interface;

namespace ThriftLoop.Services.Implementation;



public class ChatService : IChatService

{

    private readonly IConversationRepository _conversationRepo;

    private readonly IMessageRepository _messageRepo;

    private readonly IUserRepository _userRepo;

    private readonly IChatNotificationService _notificationService;

    private readonly ApplicationDbContext _context;

    private readonly ILogger<ChatService> _logger;

    private readonly IHubContext<ChatHub> _hubContext;

    private readonly IWalletService _walletService; // Added IWalletService field

    private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web);

    public ChatService(
        IConversationRepository conversationRepo,
        IMessageRepository messageRepo,
        IUserRepository userRepo,
        IChatNotificationService notificationService,
        ApplicationDbContext context,
        ILogger<ChatService> logger,
        IHubContext<ChatHub> hubContext,
        IWalletService walletService) // Added IWalletService parameter
    {
        _conversationRepo = conversationRepo;
        _messageRepo = messageRepo;
        _userRepo = userRepo;
        _notificationService = notificationService;
        _context = context;
        _logger = logger;
        _hubContext = hubContext;
        _walletService = walletService; // Assigned IWalletService parameter
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

                SenderName = m.SenderId == null ? "System" : (m.Sender?.FullName ?? m.Sender?.Email ?? "Unknown"),

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

                    dto.OrderReference = new OrderReferenceDTO

                    {

                        ItemId = item.Id,

                        ItemTitle = item.Title,

                        ItemImageUrl = item.ImageUrls?.FirstOrDefault(),

                        Price = item.Price,

                        Condition = item.Condition,

                        Size = item.Size,

                        Category = item.Category,

                        OrderId = order?.Id,

                        ConversationId = conversationId,

                        SellerId = item.UserId,

                        SellerName = item.User?.Email?.Split('@')[0] ?? "Unknown",

                        IsConfirmedOrder = order != null,

                        FulfillmentMethod = order?.FulfillmentMethod.ToString()

                    };

                }

            }



            messageDtos.Add(dto);

        }



        // Mark messages as read when user views the conversation

        await _messageRepo.MarkAllAsReadAsync(conversationId, currentUserId);



        // Get context cards for this conversation

        var contextCards = await GetContextCardsAsync(conversationId, currentUserId);



        // Create ContextCard messages

        var contextCardMessages = new List<MessageDTO>();

        foreach (var card in contextCards)

        {

            var contextCardMessage = new MessageDTO

            {

                Id = card.Id, // Use context card ID as message ID for uniqueness

                ConversationId = card.ConversationId,

                SenderId = 0, // System message

                SenderName = "System",

                SenderAvatarUrl = null,

                Content = $"Context Card: {card.ItemTitle}",

                SentAt = card.CreatedAt,

                Status = MessageStatus.Sent,

                MessageType = MessageType.ContextCard,

                IsFromCurrentUser = false,

                FormattedTime = FormatMessageTime(card.CreatedAt),

                ContextCard = card

            };

            contextCardMessages.Add(contextCardMessage);

        }



        // Combine regular messages with context card messages

        var allMessages = messageDtos.Concat(contextCardMessages)

                                    .OrderBy(m => m.SentAt)

                                    .ToList();



        var detailDto = new ConversationDetailDTO

        {

            Id = conversation.Id,

            OtherUserId = otherUserId,

            OtherUserName = otherUser?.FullName ?? otherUser?.Email ?? "Unknown User",

            OtherUserAvatarUrl = null,

            IsOtherUserOnline = isOnline,

            OtherUserLastActiveAt = lastActive,

            OtherUserActiveStatus = GetActiveStatusText(isOnline, lastActive),

            Messages = allMessages,

            TotalMessageCount = totalCount + contextCards.Count, // Include context cards in total count

            Page = page,

            PageSize = pageSize,

            HasMoreMessages = (page * pageSize) < totalCount,

            CreatedAt = conversation.CreatedAt

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

            IsFromCurrentUser = false, // Neutral value - clients derive this from senderId

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



        // Check if we already have a context card for this item in this conversation

        var existingContextCards = await GetContextCardsAsync(conversation.Id, buyerId);

        var hasExistingCard = existingContextCards.Any(cc => cc.ItemId == itemId && cc.IsActive);



        if (!hasExistingCard)

        {

            // Create a new context card for this item inquiry

            await CreateContextCardAsync(conversation.Id, itemId, buyerId, sellerId);

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

                null, // System

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

            $"Order #{order.Id} confirmed! Please coordinate {fulfillmentText} details in this chat.",

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

            IsFromCurrentUser = false, // Neutral value - clients derive this from senderId

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

            IsFromCurrentUser = false, // Neutral value - clients derive this from senderId

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



    // Context Card Implementation



    public async Task<ContextCardDTO> CreateContextCardAsync(int conversationId, int itemId, int buyerId, int sellerId)

    {

        var item = await _context.Items

            .Include(i => i.User)

            .FirstOrDefaultAsync(i => i.Id == itemId);



        if (item == null)

            throw new InvalidOperationException("Item not found.");



        var buyer = await _userRepo.GetByIdAsync(buyerId);

        var seller = await _userRepo.GetByIdAsync(sellerId);



        if (buyer == null || seller == null)

            throw new InvalidOperationException("User not found.");



        var contextCard = new ContextCard

        {

            ConversationId = conversationId,

            ItemId = itemId,

            SellerId = sellerId,

            BuyerId = buyerId,

            Status = ContextCardStatus.Pending,

            CreatedAt = DateTime.UtcNow,

            ExpiresAt = DateTime.UtcNow.AddHours(1)

        };



        _context.ContextCards.Add(contextCard);

        await _context.SaveChangesAsync();



        // Create a message for the context card

        var message = new Message

        {

            ConversationId = conversationId,

            SenderId = null, // System message

            Content = "Inquiry Created",

            MessageType = MessageType.ContextCard,

            SentAt = DateTime.UtcNow,

            Status = MessageStatus.Sent,

            ReferencedItemId = itemId

        };



        var createdMessage = await _messageRepo.CreateAsync(message);



        return new ContextCardDTO

        {

            Id = contextCard.Id,

            ConversationId = contextCard.ConversationId,

            ItemId = contextCard.ItemId,

            ItemTitle = item.Title,

            ItemPrice = item.Price,

            ItemImageUrl = item.ImageUrls?.FirstOrDefault(),

            Condition = item.Condition,

            Size = item.Size,

            Category = item.Category,

            SellerId = contextCard.SellerId,

            SellerName = seller.FullName ?? seller.Email,

            BuyerId = contextCard.BuyerId,

            BuyerName = buyer.FullName ?? buyer.Email,

            OrderId = contextCard.OrderId,

            Status = contextCard.Status,

            CreatedAt = contextCard.CreatedAt,

            ExpiresAt = contextCard.ExpiresAt,

            CompletedAt = contextCard.CompletedAt,

            PaymentMethod = contextCard.PaymentMethod.HasValue ? (ThriftLoop.DTOs.Chat.PaymentMethod?)contextCard.PaymentMethod.Value : null,

            IsCurrentUserSeller = false, // Will be set by calling method

            IsCurrentUserBuyer = false,   // Will be set by calling method

            AvailableActions = GetAvailableActions(contextCard.Status, false, false)

        };

    }



    public async Task<List<ContextCardDTO>> GetContextCardsAsync(int conversationId, int currentUserId)

    {

        var contextCards = await _context.ContextCards

            .Include(cc => cc.Item)

            .Include(cc => cc.Seller)

            .Include(cc => cc.Buyer)

            .Include(cc => cc.Order)

            .Where(cc => cc.ConversationId == conversationId)

            .OrderByDescending(cc => cc.CreatedAt)

            .ToListAsync();



        var result = new List<ContextCardDTO>();



        foreach (var card in contextCards)

        {

            var isCurrentUserBuyer = currentUserId == card.BuyerId;

            decimal buyerWalletBalance = 0;

            

            // Get buyer's wallet balance if current user is the buyer

            if (isCurrentUserBuyer)

            {

                var buyerWallet = await _walletService.GetOrCreateWalletAsync(card.BuyerId);

                buyerWalletBalance = buyerWallet.Balance;

            }



            var dto = new ContextCardDTO

            {

                Id = card.Id,

                ConversationId = card.ConversationId,

                ItemId = card.ItemId,

                ItemTitle = card.Item.Title,

                ItemPrice = card.Item.Price,

                ItemImageUrl = card.Item.ImageUrls?.FirstOrDefault(),

                Condition = card.Item.Condition,

                Size = card.Item.Size,

                Category = card.Item.Category,

                SellerId = card.SellerId,

                SellerName = card.Seller.FullName ?? card.Seller.Email,

                BuyerId = card.BuyerId,

                BuyerName = card.Buyer.FullName ?? card.Buyer.Email,

                OrderId = card.OrderId,

                Status = card.Status,

                CreatedAt = card.CreatedAt,

                ExpiresAt = card.ExpiresAt,

                CompletedAt = card.CompletedAt,

                PaymentMethod = card.PaymentMethod.HasValue ? (ThriftLoop.DTOs.Chat.PaymentMethod?)card.PaymentMethod.Value : null,

                IsCurrentUserSeller = currentUserId == card.SellerId,

                IsCurrentUserBuyer = isCurrentUserBuyer,

                BuyerWalletBalance = buyerWalletBalance,

                AvailableActions = GetAvailableActions(card.Status, currentUserId == card.SellerId, isCurrentUserBuyer)

            };



            result.Add(dto);

        }



        return result;

    }



    public async Task<ContextCardDTO> UpdateContextCardAsync(int contextCardId, ContextCardAction action, int currentUserId, ThriftLoop.DTOs.Chat.PaymentMethod? paymentMethod = null)

    {

        var contextCard = await _context.ContextCards

            .Include(cc => cc.Item)

            .Include(cc => cc.Seller)

            .Include(cc => cc.Buyer)

            .Include(cc => cc.Order)

            .FirstOrDefaultAsync(cc => cc.Id == contextCardId);



        if (contextCard == null)

            throw new InvalidOperationException("Context card not found.");



        // Validate user permissions

        if (action == ContextCardAction.Accept || action == ContextCardAction.Decline || action == ContextCardAction.ItemHandedOff)

        {

            if (currentUserId != contextCard.SellerId)

                throw new UnauthorizedAccessException("Only seller can perform this action.");

        }

        else if (action == ContextCardAction.ItemReceived || action == ContextCardAction.SelectPayment)

        {

            if (currentUserId != contextCard.BuyerId)

                throw new UnauthorizedAccessException("Only buyer can perform this action.");

        }

        else if (action == ContextCardAction.Cancel)

        {

            if (currentUserId != contextCard.SellerId && currentUserId != contextCard.BuyerId)

                throw new UnauthorizedAccessException("Only seller or buyer can cancel the transaction.");

        }



        // Update status based on action

        switch (action)

        {

            case ContextCardAction.Accept:

                contextCard.Status = ContextCardStatus.Accepted;

                

                // Make item unavailable

                contextCard.Item.Status = ItemStatus.Sold;

                await _context.SaveChangesAsync();

                

                // Create order

                var order = new Order

                {

                    ItemId = contextCard.ItemId,

                    BuyerId = contextCard.BuyerId,

                    SellerId = contextCard.SellerId,

                    Status = OrderStatus.Pending,

                    FulfillmentMethod = FulfillmentMethod.Halfway, // Default, can be updated later

                    OrderDate = DateTime.UtcNow,

                    FinalPrice = contextCard.Item.Price,

                    DeliveryFee = 0

                };

                

                _context.Orders.Add(order);

                await _context.SaveChangesAsync();

                

                contextCard.OrderId = order.Id;

                break;



            case ContextCardAction.Decline:

                contextCard.Status = ContextCardStatus.Declined;

                break;



            case ContextCardAction.Cancel:

                contextCard.Status = ContextCardStatus.Cancelled;

                break;



            case ContextCardAction.ItemHandedOff:

                contextCard.Status = ContextCardStatus.ItemHandedOff;

                break;



            case ContextCardAction.ItemReceived:

                contextCard.Status = ContextCardStatus.ItemReceived;

                break;



            case ContextCardAction.SelectPayment:

                _logger.LogInformation("SelectPayment action called with paymentMethod: {PaymentMethod}", paymentMethod);

                

                if (!paymentMethod.HasValue)

                {

                    _logger.LogWarning("Payment method is null or empty for SelectPayment action");

                    throw new ArgumentException("Payment method is required for SelectPayment action.");

                }

                

                contextCard.PaymentMethod = (ThriftLoop.Enums.PaymentMethod)paymentMethod.Value;

                contextCard.Status = ContextCardStatus.Completed;

                contextCard.CompletedAt = DateTime.UtcNow;

                

                // Update order status

                if (contextCard.Order != null)

                {

                    contextCard.Order.Status = OrderStatus.Completed;

                }

                

                // Process wallet payment - transfer funds from buyer to seller

                if (paymentMethod.Value == ThriftLoop.DTOs.Chat.PaymentMethod.Wallet)

                {

                    _logger.LogInformation("Processing wallet payment for ContextCard {ContextCardId}: Buyer {BuyerId} paying Seller {SellerId} ₱{Amount}",

                        contextCard.Id, contextCard.BuyerId, contextCard.SellerId, contextCard.Item.Price);

                    

                    // Check buyer has sufficient balance

                    var buyerWallet = await _walletService.GetOrCreateWalletAsync(contextCard.BuyerId);

                    if (buyerWallet.Balance < contextCard.Item.Price)

                    {

                        throw new InvalidOperationException($"Insufficient wallet balance. Required: ₱{contextCard.Item.Price:N2}, Available: ₱{buyerWallet.Balance:N2}");

                    }

                    

                    // Transfer funds: buyer -> seller (direct wallet-to-wallet transfer)

                    await _walletService.TransferWalletToWalletAsync(

                        contextCard.OrderId ?? 0,

                        contextCard.BuyerId,

                        contextCard.SellerId,

                        contextCard.Item.Price);

                    

                    _logger.LogInformation("Wallet payment completed successfully for ContextCard {ContextCardId}", contextCard.Id);

                }

                break;



        }



        await _context.SaveChangesAsync();



        // ── Send one update to the whole conversation group ──────────────────────

        var broadcastDto = new ContextCardDTO

        {

            Id = contextCard.Id,

            ConversationId = contextCard.ConversationId,

            ItemId = contextCard.ItemId,

            ItemTitle = contextCard.Item.Title,

            ItemPrice = contextCard.Item.Price,

            ItemImageUrl = contextCard.Item.ImageUrls?.FirstOrDefault(),

            Condition = contextCard.Item.Condition,

            Size = contextCard.Item.Size,

            Category = contextCard.Item.Category,

            SellerId = contextCard.SellerId,

            SellerName = contextCard.Seller.FullName ?? contextCard.Seller.Email,

            BuyerId = contextCard.BuyerId,

            BuyerName = contextCard.Buyer.FullName ?? contextCard.Buyer.Email,

            OrderId = contextCard.OrderId,

            Status = contextCard.Status,

            CreatedAt = contextCard.CreatedAt,

            ExpiresAt = contextCard.ExpiresAt,

            CompletedAt = contextCard.CompletedAt,

            PaymentMethod = contextCard.PaymentMethod.HasValue

                ? (ThriftLoop.DTOs.Chat.PaymentMethod?)contextCard.PaymentMethod.Value : null,

            // IsCurrentUserSeller/Buyer intentionally left false here;

            // client resolves these from SellerId/BuyerId vs its own currentUserId

            IsCurrentUserSeller = false,

            IsCurrentUserBuyer = false,

            AvailableActions = new List<ContextCardAction>()

        };



        await _hubContext.Clients.Group($"conversation-{contextCard.ConversationId}")

            .SendAsync("ContextCardUpdated", broadcastDto);



        // Send notification message

        await SendContextCardUpdateMessage(contextCard, action, currentUserId);



        return new ContextCardDTO

        {

            Id = contextCard.Id,

            ConversationId = contextCard.ConversationId,

            ItemId = contextCard.ItemId,

            ItemTitle = contextCard.Item.Title,

            ItemPrice = contextCard.Item.Price,

            ItemImageUrl = contextCard.Item.ImageUrls?.FirstOrDefault(),

            Condition = contextCard.Item.Condition,

            Size = contextCard.Item.Size,

            Category = contextCard.Item.Category,

            SellerId = contextCard.SellerId,

            SellerName = contextCard.Seller.FullName ?? contextCard.Seller.Email,

            BuyerId = contextCard.BuyerId,

            BuyerName = contextCard.Buyer.FullName ?? contextCard.Buyer.Email,

            OrderId = contextCard.OrderId,

            Status = contextCard.Status,

            CreatedAt = contextCard.CreatedAt,

            ExpiresAt = contextCard.ExpiresAt,

            CompletedAt = contextCard.CompletedAt,

            PaymentMethod = contextCard.PaymentMethod.HasValue ? (ThriftLoop.DTOs.Chat.PaymentMethod?)contextCard.PaymentMethod.Value : null,

            IsCurrentUserSeller = currentUserId == contextCard.SellerId,

            IsCurrentUserBuyer = currentUserId == contextCard.BuyerId,

            AvailableActions = GetAvailableActions(contextCard.Status, currentUserId == contextCard.SellerId, currentUserId == contextCard.BuyerId)

        };

    }



    public async Task ProcessExpiredContextCardsAsync()

    {

        var expiredCards = await _context.ContextCards

            .Where(cc => cc.Status == ContextCardStatus.Pending && cc.ExpiresAt < DateTime.UtcNow)

            .ToListAsync();



        foreach (var card in expiredCards)

        {

            card.Status = ContextCardStatus.Expired;

        }



        if (expiredCards.Any())

        {

            await _context.SaveChangesAsync();

        }

    }



    private List<ContextCardAction> GetAvailableActions(ContextCardStatus status, bool isSeller, bool isBuyer)

    {

        var actions = new List<ContextCardAction>();



        switch (status)

        {

            case ContextCardStatus.Pending:

                if (isSeller)

                {

                    actions.Add(ContextCardAction.Accept);

                    actions.Add(ContextCardAction.Decline);

                }

                else if (isBuyer)

                {

                    actions.Add(ContextCardAction.Cancel);

                }

                break;



            case ContextCardStatus.Accepted:

                if (isSeller)

                {

                    actions.Add(ContextCardAction.ItemHandedOff);

                    actions.Add(ContextCardAction.Cancel);

                }

                else if (isBuyer)

                {

                    actions.Add(ContextCardAction.Cancel);

                }

                break;



            case ContextCardStatus.ItemHandedOff:

                if (isBuyer)

                {

                    actions.Add(ContextCardAction.ItemReceived);

                    actions.Add(ContextCardAction.Cancel);

                }

                break;



            case ContextCardStatus.ItemReceived:

                if (isBuyer)

                {

                    actions.Add(ContextCardAction.SelectPayment);

                    actions.Add(ContextCardAction.Cancel);

                }

                break;

        }



        return actions;

    }



    private async Task SendContextCardUpdateMessage(ContextCard contextCard, ContextCardAction action, int currentUserId)

    {

        bool isSeller = currentUserId == contextCard.SellerId;

        string messageText = action switch

        {

            ContextCardAction.Accept => "Seller has accepted the transaction.",

            ContextCardAction.Decline => "Seller has declined the transaction.",

            ContextCardAction.Cancel => isSeller ? "Seller has cancelled the transaction." : "Buyer has cancelled the transaction.",

            ContextCardAction.ItemHandedOff => "Seller has handed off the item.",

            ContextCardAction.ItemReceived => "Buyer has received the item.",

            ContextCardAction.SelectPayment => contextCard.PaymentMethod == PaymentMethod.Wallet
                ? "method was Wallet"
                : $"Payment method selected: {contextCard.PaymentMethod}",

            _ => "Transaction status updated."

        };



        var message = new Message

        {

            ConversationId = contextCard.ConversationId,

            SenderId = null, // System message

            Content = messageText,

            MessageType = MessageType.Text,

            SentAt = DateTime.UtcNow,

            Status = MessageStatus.Sent

        };



        await _messageRepo.CreateAsync(message);

        await _conversationRepo.UpdateLastMessageTimeAsync(contextCard.ConversationId, DateTime.UtcNow);



        // Broadcast so both clients see it instantly without a reload

        var messageDto = new MessageDTO

        {

            Id = message.Id,

            ConversationId = message.ConversationId,

            SenderId = null,

            SenderName = "System",

            Content = message.Content,

            SentAt = message.SentAt,

            Status = message.Status,

            MessageType = message.MessageType,

            IsFromCurrentUser = false,

            FormattedTime = FormatMessageTime(message.SentAt)

        };



        await _hubContext.Clients.Group($"conversation-{contextCard.ConversationId}")

            .SendAsync("ReceiveMessage", messageDto);

    }

}