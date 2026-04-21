// Hubs/ChatHub.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using ThriftLoop.DTOs.Chat;
using ThriftLoop.Services.Interface;

namespace ThriftLoop.Hubs;

[Authorize] // Re-enabled for proper auth propagation
public class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly IChatNotificationService _notificationService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IChatService chatService,
        IChatNotificationService notificationService,
        ILogger<ChatHub> logger)
    {
        _chatService = chatService;
        _notificationService = notificationService;
        _logger = logger;
    }

    private int CurrentUserId => int.Parse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    public override async Task OnConnectedAsync()
    {
        var user = Context.User?.Identity?.Name;
        var userId = CurrentUserId;
        
        _logger.LogInformation("User {User} (ID: {UserId}) connected to chat hub. Connection ID: {ConnectionId}", 
            user, userId, Context.ConnectionId);
        
        // Debug: Check if user is authenticated
        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            _logger.LogInformation("User is authenticated. User ID: {UserId}", userId);
        }
        else
        {
            _logger.LogWarning("User is NOT authenticated!");
        }

        if (userId > 0)
        {
            await _notificationService.UserConnectedAsync(userId, Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");

            _logger.LogInformation("User {UserId} connected to ChatHub with connection {ConnectionId}", userId, Context.ConnectionId);
            await Clients.Others.SendAsync("UserOnline", userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = CurrentUserId;
        var connectionId = Context.ConnectionId;

        if (userId > 0)
        {
            await _notificationService.UserDisconnectedAsync(userId, connectionId);
            await Groups.RemoveFromGroupAsync(connectionId, $"user-{userId}");

            _logger.LogInformation("User {UserId} disconnected from ChatHub", userId);
            await Clients.Others.SendAsync("UserOffline", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Sends a message to a conversation.
    /// </summary>
    public async Task SendMessage(SendMessageDTO dto)
    {
        var senderId = CurrentUserId;

        try
        {
            // Save message to database and get the DTO
            var messageDto = await _chatService.SendMessageAsync(senderId, dto);
            var conversationId = messageDto.ConversationId;

            // Ensure the caller is in the conversation group
            await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation-{conversationId}");

            // Get the recipient ID
            var recipient = await _chatService.GetOtherParticipantAsync(conversationId, senderId);

            // Broadcast to ALL clients in the conversation group (including sender)
            await Clients.Group($"conversation-{conversationId}").SendAsync("ReceiveMessage", messageDto);

            // Send notification to recipient if they're not actively viewing the conversation
            if (recipient != null)
            {
                await Clients.Group($"user-{recipient.Id}").SendAsync("NewMessageNotification", new
                {
                    conversationId,
                    message = messageDto,
                    senderName = messageDto.SenderName
                });

                // Update unread count for recipient
                var unreadCount = await _chatService.GetTotalUnreadCountAsync(recipient.Id);
                await Clients.Group($"user-{recipient.Id}").SendAsync("UnreadCountUpdate", unreadCount);
            }

            _logger.LogInformation("Message {MessageId} sent from {SenderId} to conversation {ConversationId}",
                messageDto.Id, senderId, conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message from user {SenderId}", senderId);
            await Clients.Caller.SendAsync("SendMessageError", new { error = ex.Message });
        }
    }

    /// <summary>
    /// Called when a user joins a specific conversation (opens the chat view).
    /// </summary>
    public async Task JoinConversation(int conversationId)
    {
        var userId = CurrentUserId;

        // Verify user has access to this conversation
        if (!await _chatService.CanAccessConversationAsync(conversationId, userId))
        {
            _logger.LogWarning("User {UserId} attempted to join unauthorized conversation {ConversationId}", userId, conversationId);
            await Clients.Caller.SendAsync("Error", "You do not have access to this conversation.");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation-{conversationId}");

        _logger.LogInformation("User {UserId} joined conversation {ConversationId}", userId, conversationId);

        // Notify other participants that this user is viewing the conversation
        await Clients.GroupExcept($"conversation-{conversationId}", Context.ConnectionId)
            .SendAsync("UserViewingConversation", new { userId, conversationId });

        await Clients.Caller.SendAsync("JoinedConversation", conversationId);
    }

    /// <summary>
    /// Called when a user leaves a conversation view.
    /// </summary>
    public async Task LeaveConversation(int conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation-{conversationId}");

        _logger.LogInformation("User {UserId} left conversation {ConversationId}", CurrentUserId, conversationId);

        await Clients.Caller.SendAsync("LeftConversation", conversationId);
    }

    /// <summary>
    /// Marks a specific message as read.
    /// </summary>
    public async Task MarkMessageAsRead(int messageId)
    {
        var userId = CurrentUserId;

        try
        {
            await _chatService.MarkMessageAsReadAsync(messageId, userId);
            await Clients.Caller.SendAsync("MessageMarkedAsRead", messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking message {MessageId} as read", messageId);
        }
    }

    /// <summary>
    /// Marks all messages in a conversation as read.
    /// </summary>
    public async Task MarkConversationAsRead(int conversationId)
    {
        var userId = CurrentUserId;

        try
        {
            if (!await _chatService.CanAccessConversationAsync(conversationId, userId))
                return;

            await _chatService.MarkConversationAsReadAsync(conversationId, userId);

            // Update unread count for this user
            var unreadCount = await _chatService.GetTotalUnreadCountAsync(userId);
            await Clients.Group($"user-{userId}").SendAsync("UnreadCountUpdate", unreadCount);

            await Clients.Caller.SendAsync("ConversationMarkedAsRead", conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking conversation {ConversationId} as read", conversationId);
        }
    }

    /// <summary>
    /// User is typing indicator.
    /// </summary>
    public async Task Typing(int conversationId, bool isTyping)
    {
        var userId = CurrentUserId;

        if (!await _chatService.CanAccessConversationAsync(conversationId, userId))
            return;

        await Clients.GroupExcept($"conversation-{conversationId}", Context.ConnectionId)
            .SendAsync("UserTyping", new { userId, conversationId, isTyping });
    }

    /// <summary>
    /// Gets the current online status of a user.
    /// </summary>
    public async Task<bool> IsUserOnline(int userId)
    {
        return await _notificationService.IsUserOnlineAsync(userId);
    }

    /// <summary>
    /// Pings the server to keep the connection alive.
    /// </summary>
    public async Task Ping()
    {
        var userId = CurrentUserId;
        await _notificationService.UpdateLastActiveTimeAsync(userId);
        await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
    }

    /// <summary>
    /// Broadcasts ContextCard updates to all clients in the conversation.
    /// </summary>
    public async Task NotifyContextCardUpdated(int conversationId, ContextCardDTO contextCard)
    {
        // Verify user has access to this conversation
        if (!await _chatService.CanAccessConversationAsync(conversationId, CurrentUserId))
        {
            _logger.LogWarning("User {UserId} attempted to notify ContextCard update for unauthorized conversation {ConversationId}", 
                CurrentUserId, conversationId);
            return;
        }

        await Clients.Group($"conversation-{conversationId}")
            .SendAsync("ContextCardUpdated", contextCard);

        _logger.LogInformation("ContextCard {ContextCardId} update broadcast to conversation {ConversationId}", 
            contextCard.Id, conversationId);
    }

    /// <summary>
    /// Broadcasts ContextCard status changes to conversation participants.
    /// </summary>
    public async Task NotifyContextCardStatusChanged(int conversationId, string status)
    {
        // Verify user has access to this conversation
        if (!await _chatService.CanAccessConversationAsync(conversationId, CurrentUserId))
        {
            _logger.LogWarning("User {UserId} attempted to notify ContextCard status change for unauthorized conversation {ConversationId}", 
                CurrentUserId, conversationId);
            return;
        }

        await Clients.Group($"conversation-{conversationId}")
            .SendAsync("ContextCardStatusChanged", new { conversationId, status });

        _logger.LogInformation("ContextCard status change '{Status}' broadcast to conversation {ConversationId}", 
            status, conversationId);
    }
}