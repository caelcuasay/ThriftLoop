// Repositories/Implementation/MessageRepository.cs
using Microsoft.EntityFrameworkCore;
using ThriftLoop.Data;
using ThriftLoop.Enums;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;

namespace ThriftLoop.Repositories.Implementation;

public class MessageRepository : IMessageRepository
{
    private readonly ApplicationDbContext _context;

    public MessageRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Message?> GetByIdAsync(int id)
    {
        return await _context.Messages
            .Include(m => m.Sender)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<List<Message>> GetMessagesByConversationAsync(int conversationId, int page, int pageSize)
    {
        return await _context.Messages
            .Include(m => m.Sender)
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
}