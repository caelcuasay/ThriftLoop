// Repositories/Implementation/MessageRepository.cs
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ThriftLoop.Data;
using ThriftLoop.Enums;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;

namespace ThriftLoop.Repositories.Implementation;

public class MessageRepository : IMessageRepository
{
    private readonly ApplicationDbContext _context;
    private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web);

    public MessageRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Message?> GetByIdAsync(int id)
    {
        return await _context.Messages
            .Include(m => m.Sender)
            .Include(m => m.ReferencedOrder)
            .Include(m => m.ReferencedItem)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<List<Message>> GetMessagesByConversationAsync(int conversationId, int page, int pageSize)
    {
        return await _context.Messages
            .Include(m => m.Sender)
            .Include(m => m.ReferencedOrder)
                .ThenInclude(o => o != null ? o.Item : null)
            .Include(m => m.ReferencedItem)
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Message?> GetLastMessageAsync(int conversationId)
    {
        return await _context.Messages
            .Include(m => m.Sender)
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.SentAt)
            .AsNoTracking()
            .FirstOrDefaultAsync();
    }

    public async Task<Message> CreateAsync(Message message)
    {
        _context.Messages.Add(message);
        await _context.SaveChangesAsync();
        return message;
    }

    public async Task MarkAsDeliveredAsync(int messageId)
    {
        var message = await _context.Messages.FindAsync(messageId);
        if (message != null && message.DeliveredAt == null)
        {
            message.DeliveredAt = DateTime.UtcNow;
            message.Status = MessageStatus.Delivered;
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAsReadAsync(int messageId)
    {
        var message = await _context.Messages.FindAsync(messageId);
        if (message != null && message.ReadAt == null)
        {
            message.ReadAt = DateTime.UtcNow;
            message.Status = MessageStatus.Read;
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsDeliveredAsync(int conversationId, int recipientId)
    {
        var messages = await _context.Messages
            .Where(m => m.ConversationId == conversationId)
            .Where(m => m.SenderId != recipientId)
            .Where(m => m.DeliveredAt == null)
            .ToListAsync();

        if (messages.Any())
        {
            var now = DateTime.UtcNow;
            foreach (var message in messages)
            {
                message.DeliveredAt = now;
                message.Status = MessageStatus.Delivered;
            }
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(int conversationId, int recipientId)
    {
        var messages = await _context.Messages
            .Where(m => m.ConversationId == conversationId)
            .Where(m => m.SenderId != recipientId)
            .Where(m => m.ReadAt == null)
            .ToListAsync();

        if (messages.Any())
        {
            var now = DateTime.UtcNow;
            foreach (var message in messages)
            {
                message.ReadAt = now;
                message.Status = MessageStatus.Read;
            }
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> GetTotalCountAsync(int conversationId)
    {
        return await _context.Messages
            .Where(m => m.ConversationId == conversationId)
            .CountAsync();
    }

    public async Task<List<int>> GetUnreadMessageIdsAsync(int conversationId, int userId)
    {
        return await _context.Messages
            .Where(m => m.ConversationId == conversationId)
            .Where(m => m.SenderId != userId)
            .Where(m => m.ReadAt == null)
            .Select(m => m.Id)
            .ToListAsync();
    }

    // ── Rich Message Methods ──────────────────────────────────────────────────

    public async Task<Message> CreateOrderReferenceMessageAsync(
        int conversationId,
        int senderId,
        int itemId,
        int? orderId = null,
        MessageType messageType = MessageType.OrderReference,
        string? metadataJson = null)
    {
        // Get item details for the content preview
        var item = await _context.Items
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.Id == itemId);

        var content = item != null
            ? $"📦 Order Reference: {item.Title}"
            : "📦 Order Reference";

        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = content,
            MessageType = messageType,
            ReferencedItemId = itemId,
            ReferencedOrderId = orderId,
            MetadataJson = metadataJson,
            SentAt = DateTime.UtcNow,
            Status = MessageStatus.Sent
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        return message;
    }

    public async Task<Message> CreateSystemMessageAsync(
        int conversationId,
        MessageType messageType,
        string content,
        int? orderId = null,
        string? metadataJson = null)
    {
        // System messages have SenderId = 0 (or could use a special system user ID)
        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = 0, // System user
            Content = content,
            MessageType = messageType,
            ReferencedOrderId = orderId,
            MetadataJson = metadataJson,
            SentAt = DateTime.UtcNow,
            Status = MessageStatus.Sent
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        return message;
    }

    public async Task<Message> CreateMeetingProposalAsync(
        int conversationId,
        int senderId,
        string location,
        DateTime proposedTime,
        string? notes = null)
    {
        var metadata = new
        {
            location,
            proposedTime = proposedTime.ToString("O"),
            notes
        };

        var metadataJson = JsonSerializer.Serialize(metadata, _jsonOpts);

        var content = $"📍 Meeting Proposal: {location} at {proposedTime:ddd, MMM d • h:mm tt}";
        if (!string.IsNullOrEmpty(notes))
        {
            content += $"\n📝 {notes}";
        }

        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = content,
            MessageType = MessageType.MeetingProposal,
            MetadataJson = metadataJson,
            SentAt = DateTime.UtcNow,
            Status = MessageStatus.Sent
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        return message;
    }

    public async Task<List<Message>> GetOrderReferenceMessagesAsync(int conversationId)
    {
        return await _context.Messages
            .Include(m => m.ReferencedOrder)
                .ThenInclude(o => o != null ? o.Item : null)
            .Include(m => m.ReferencedItem)
            .Where(m => m.ConversationId == conversationId)
            .Where(m => m.MessageType == MessageType.OrderReference ||
                        m.MessageType == MessageType.OrderConfirmed)
            .OrderByDescending(m => m.SentAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Message?> GetLatestOrderReferenceAsync(int conversationId)
    {
        return await _context.Messages
            .Include(m => m.ReferencedOrder)
                .ThenInclude(o => o != null ? o.Item : null)
            .Include(m => m.ReferencedItem)
            .Where(m => m.ConversationId == conversationId)
            .Where(m => m.MessageType == MessageType.OrderReference ||
                        m.MessageType == MessageType.OrderConfirmed)
            .OrderByDescending(m => m.SentAt)
            .AsNoTracking()
            .FirstOrDefaultAsync();
    }

    public async Task<bool> HasOrderReferenceForItemAsync(int conversationId, int itemId)
    {
        return await _context.Messages
            .Where(m => m.ConversationId == conversationId)
            .Where(m => m.ReferencedItemId == itemId)
            .Where(m => m.MessageType == MessageType.OrderReference ||
                        m.MessageType == MessageType.OrderConfirmed)
            .AnyAsync();
    }
}