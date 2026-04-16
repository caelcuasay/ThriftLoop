// Services/Implementation/ChatService.cs
using Microsoft.EntityFrameworkCore;
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
                OtherUserAvatarUrl = null, // Add avatar field if you have one
                LastMessage = lastMessage?.Content ?? "No messages yet",
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
        var conversation = await _conversationRepo.GetByIdAsync(conversationId);
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

        var messageDtos = messages.Select(m => new MessageDTO
        {
            Id = m.Id,
            ConversationId = m.ConversationId,
            SenderId = m.SenderId,
            SenderName = m.Sender?.FullName ?? m.Sender?.Email ?? "Unknown",
            SenderAvatarUrl = null,
            Content = m.Content,
            SentAt = m.SentAt,
            Status = m.Status,
            IsFromCurrentUser = m.SenderId == currentUserId,
            FormattedTime = FormatMessageTime(m.SentAt)
        }).OrderBy(m => m.SentAt).ToList();

        // Mark messages as read when user views the conversation
        await _messageRepo.MarkAllAsReadAsync(conversationId, currentUserId);

        return new ConversationDetailDTO
        {
            Id = conversation.Id,
            OtherUserId = otherUserId,
            OtherUserName = otherUser?.FullName ?? otherUser?.Email ?? "Unknown User",
            OtherUserAvatarUrl = null,
            IsOtherUserOnline = isOnline,
            OtherUserLastActiveAt = lastActive,
            OtherUserActiveStatus = GetActiveStatusText(isOnline, lastActive),
            Messages = messageDtos,
            TotalMessageCount = totalCount,
            Page = page,
            PageSize = pageSize,
            HasMoreMessages = (page * pageSize) < totalCount,
            CreatedAt = conversation.CreatedAt
        };
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
                Content = dto.InitialMessage
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

        var message = new Message
        {
            ConversationId = conversation.Id,
            SenderId = senderId,
            Content = dto.Content.Trim(),
            SentAt = DateTime.UtcNow,
            Status = MessageStatus.Sent
        };

        var createdMessage = await _messageRepo.CreateAsync(message);
        await _conversationRepo.UpdateLastMessageTimeAsync(conversation.Id, message.SentAt);

        // Load sender info for the DTO
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
            IsFromCurrentUser = true,
            FormattedTime = FormatMessageTime(createdMessage.SentAt)
        };
    }

    public async Task MarkMessageAsReadAsync(int messageId, int currentUserId)
    {
        var message = await _messageRepo.GetByIdAsync(messageId);
        if (message == null)
            return;

        var conversation = await _conversationRepo.GetByIdAsync(message.ConversationId);
        if (conversation == null)
            return;

        // Only the recipient (not the sender) can mark as read
        if (message.SenderId == currentUserId)
            return;

        // Verify user is part of the conversation
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

    #region Helper Methods

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

    #endregion
}