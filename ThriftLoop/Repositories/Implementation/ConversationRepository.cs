// Repositories/Implementation/ConversationRepository.cs
using Microsoft.EntityFrameworkCore;
using ThriftLoop.Data;
using ThriftLoop.Enums;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;

namespace ThriftLoop.Repositories.Implementation;

public class ConversationRepository : IConversationRepository
{
    private readonly ApplicationDbContext _context;

    public ConversationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Conversation?> GetByIdAsync(int id)
    {
        return await _context.Conversations
            .Include(c => c.UserOne)
            .Include(c => c.UserTwo)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Conversation?> GetByParticipantsAsync(int userOneId, int userTwoId)
    {
        // Ensure consistent ordering: smaller ID first
        var id1 = Math.Min(userOneId, userTwoId);
        var id2 = Math.Max(userOneId, userTwoId);

        return await _context.Conversations
            .Include(c => c.UserOne)
            .Include(c => c.UserTwo)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserOneId == id1 && c.UserTwoId == id2);
    }

    public async Task<List<Conversation>> GetUserConversationsAsync(int userId)
    {
        return await _context.Conversations
            .Include(c => c.UserOne)
            .Include(c => c.UserTwo)
            .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1))
            .Where(c => c.UserOneId == userId || c.UserTwoId == userId)
            .OrderByDescending(c => c.LastMessageAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<Conversation>> GetUserConversationsPaginatedAsync(int userId, int page, int pageSize)
    {
        return await _context.Conversations
            .Include(c => c.UserOne)
            .Include(c => c.UserTwo)
            .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1))
            .Where(c => c.UserOneId == userId || c.UserTwoId == userId)
            .OrderByDescending(c => c.LastMessageAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Conversation> CreateAsync(int userOneId, int userTwoId)
    {
        // Ensure consistent ordering: smaller ID first
        var id1 = Math.Min(userOneId, userTwoId);
        var id2 = Math.Max(userOneId, userTwoId);

        var conversation = new Conversation
        {
            UserOneId = id1,
            UserTwoId = id2,
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow
        };

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        return conversation;
    }

    public async Task<Conversation> GetOrCreateAsync(int userOneId, int userTwoId)
    {
        var existing = await GetByParticipantsAsync(userOneId, userTwoId);
        if (existing != null)
            return existing;

        return await CreateAsync(userOneId, userTwoId);
    }

    public async Task UpdateLastMessageTimeAsync(int conversationId, DateTime timestamp)
    {
        var conversation = await _context.Conversations.FindAsync(conversationId);
        if (conversation != null)
        {
            conversation.LastMessageAt = timestamp;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> GetUnreadCountAsync(int conversationId, int userId)
    {
        return await _context.Messages
            .Where(m => m.ConversationId == conversationId)
            .Where(m => m.SenderId != userId)
            .Where(m => m.ReadAt == null)
            .CountAsync();
    }

    public async Task<int> GetTotalUnreadCountAsync(int userId)
    {
        return await _context.Messages
            .Where(m => m.SenderId != userId)
            .Where(m => m.ReadAt == null)
            .Join(_context.Conversations,
                m => m.ConversationId,
                c => c.Id,
                (m, c) => new { Message = m, Conversation = c })
            .Where(x => x.Conversation.UserOneId == userId || x.Conversation.UserTwoId == userId)
            .CountAsync();
    }

    public async Task<bool> IsUserInConversationAsync(int conversationId, int userId)
    {
        var conversation = await _context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        return conversation != null &&
               (conversation.UserOneId == userId || conversation.UserTwoId == userId);
    }

    // ── Order/Item Context Methods ────────────────────────────────────────────

    public async Task<Conversation> GetOrCreateForItemAsync(int buyerId, int sellerId, int itemId)
    {
        // First, try to find an existing conversation between these users about this item
        var existing = await _context.Conversations
            .Include(c => c.UserOne)
            .Include(c => c.UserTwo)
            .FirstOrDefaultAsync(c =>
                c.ContextItemId == itemId &&
                ((c.UserOneId == buyerId && c.UserTwoId == sellerId) ||
                 (c.UserOneId == sellerId && c.UserTwoId == buyerId)));

        if (existing != null)
        {
            // If it's a new inquiry on an existing conversation, reset status to Pending
            if (existing.InquiryStatus != InquiryStatus.Pending)
            {
                existing.InquiryStatus = InquiryStatus.Pending;
                existing.InquiryExpiresAt = DateTime.UtcNow.AddHours(48);
                existing.InquiryRespondedAt = null;
                await _context.SaveChangesAsync();
            }
            return existing;
        }

        // Next, try to find any conversation between these users
        var id1 = Math.Min(buyerId, sellerId);
        var id2 = Math.Max(buyerId, sellerId);

        var generalConv = await _context.Conversations
            .FirstOrDefaultAsync(c => c.UserOneId == id1 && c.UserTwoId == id2);

        if (generalConv != null)
        {
            // Link the existing conversation to this item if not already linked
            if (generalConv.ContextItemId == null)
            {
                generalConv.ContextItemId = itemId;
            }
            // Set inquiry status for this new inquiry
            generalConv.InquiryStatus = InquiryStatus.Pending;
            generalConv.InquiryExpiresAt = DateTime.UtcNow.AddHours(48);
            generalConv.InquiryRespondedAt = null;
            await _context.SaveChangesAsync();
            return generalConv;
        }

        // Create new conversation with item context
        var conversation = new Conversation
        {
            UserOneId = id1,
            UserTwoId = id2,
            ContextItemId = itemId,
            InquiryStatus = InquiryStatus.Pending,
            InquiryExpiresAt = DateTime.UtcNow.AddHours(48),
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow
        };

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        return conversation;
    }

    public async Task<Conversation> GetOrCreateForOrderAsync(int buyerId, int sellerId, int orderId)
    {
        // First, check if there's already a conversation linked to this order
        var existing = await _context.Conversations
            .Include(c => c.UserOne)
            .Include(c => c.UserTwo)
            .FirstOrDefaultAsync(c => c.OrderId == orderId);

        if (existing != null)
            return existing;

        // Get the order to find the item context
        var order = await _context.Orders
            .Include(o => o.Item)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        // Check for existing conversation between these users (maybe from pre-order inquiry)
        var id1 = Math.Min(buyerId, sellerId);
        var id2 = Math.Max(buyerId, sellerId);

        var generalConv = await _context.Conversations
            .FirstOrDefaultAsync(c => c.UserOneId == id1 && c.UserTwoId == id2);

        if (generalConv != null)
        {
            // Link the existing conversation to this order
            generalConv.OrderId = orderId;
            if (order?.ItemId != null && generalConv.ContextItemId == null)
            {
                generalConv.ContextItemId = order.ItemId;
            }
            // Mark inquiry as accepted since order was created
            generalConv.InquiryStatus = InquiryStatus.Accepted;
            generalConv.InquiryRespondedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return generalConv;
        }

        // Create new conversation with order context
        var conversation = new Conversation
        {
            UserOneId = id1,
            UserTwoId = id2,
            OrderId = orderId,
            ContextItemId = order?.ItemId,
            InquiryStatus = InquiryStatus.Accepted,
            InquiryRespondedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow
        };

        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        return conversation;
    }

    public async Task<Conversation?> GetByOrderIdAsync(int orderId)
    {
        return await _context.Conversations
            .Include(c => c.UserOne)
            .Include(c => c.UserTwo)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.OrderId == orderId);
    }

    public async Task<Conversation?> GetByItemAndParticipantsAsync(int itemId, int buyerId, int sellerId)
    {
        return await _context.Conversations
            .Include(c => c.UserOne)
            .Include(c => c.UserTwo)
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.ContextItemId == itemId &&
                ((c.UserOneId == buyerId && c.UserTwoId == sellerId) ||
                 (c.UserOneId == sellerId && c.UserTwoId == buyerId)));
    }

    public async Task LinkToOrderAsync(int conversationId, int orderId)
    {
        var conversation = await _context.Conversations.FindAsync(conversationId);
        if (conversation != null && conversation.OrderId == null)
        {
            var order = await _context.Orders
                .Include(o => o.Item)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            conversation.OrderId = orderId;
            if (order?.ItemId != null && conversation.ContextItemId == null)
            {
                conversation.ContextItemId = order.ItemId;
            }
            // Mark inquiry as accepted
            conversation.InquiryStatus = InquiryStatus.Accepted;
            conversation.InquiryRespondedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Conversation?> GetByIdWithContextAsync(int conversationId)
    {
        return await _context.Conversations
            .Include(c => c.UserOne)
            .Include(c => c.UserTwo)
            .Include(c => c.Order)
                .ThenInclude(o => o != null ? o.Item : null)
            .Include(c => c.ContextItem)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId);
    }

    // ── Inquiry Management Methods ────────────────────────────────────────────

    public async Task<bool> UpdateInquiryStatusAsync(int conversationId, InquiryStatus status)
    {
        var conversation = await _context.Conversations.FindAsync(conversationId);
        if (conversation == null)
            return false;

        conversation.InquiryStatus = status;
        conversation.InquiryRespondedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Conversation>> GetExpiredPendingInquiriesAsync()
    {
        var now = DateTime.UtcNow;

        return await _context.Conversations
            .Include(c => c.UserOne)
            .Include(c => c.UserTwo)
            .Include(c => c.ContextItem)
            .Where(c => c.ContextItemId != null)
            .Where(c => c.InquiryStatus == InquiryStatus.Pending)
            .Where(c => c.InquiryExpiresAt != null && c.InquiryExpiresAt <= now)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<Conversation>> GetPendingInquiriesForSellerAsync(int sellerId)
    {
        return await _context.Conversations
            .Include(c => c.UserOne)
            .Include(c => c.UserTwo)
            .Include(c => c.ContextItem)
            .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1))
            .Where(c => c.ContextItemId != null)
            .Where(c => c.InquiryStatus == InquiryStatus.Pending)
            .Where(c => c.InquiryExpiresAt == null || c.InquiryExpiresAt > DateTime.UtcNow)
            .Where(c => c.UserOneId == sellerId || c.UserTwoId == sellerId)
            .OrderByDescending(c => c.LastMessageAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<List<Conversation>> GetPendingInquiriesForBuyerAsync(int buyerId)
    {
        return await _context.Conversations
            .Include(c => c.UserOne)
            .Include(c => c.UserTwo)
            .Include(c => c.ContextItem)
            .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1))
            .Where(c => c.ContextItemId != null)
            .Where(c => c.InquiryStatus == InquiryStatus.Pending)
            .Where(c => c.InquiryExpiresAt == null || c.InquiryExpiresAt > DateTime.UtcNow)
            .Where(c => c.UserOneId == buyerId || c.UserTwoId == buyerId)
            .OrderByDescending(c => c.LastMessageAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<bool> HasActiveInquiryAsync(int conversationId)
    {
        var conversation = await _context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        return conversation != null
            && conversation.ContextItemId != null
            && conversation.InquiryStatus == InquiryStatus.Pending
            && (!conversation.InquiryExpiresAt.HasValue || conversation.InquiryExpiresAt.Value > DateTime.UtcNow);
    }

    public async Task<InquiryStatus?> GetInquiryStatusAsync(int conversationId)
    {
        var conversation = await _context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        return conversation?.ContextItemId != null ? conversation.InquiryStatus : null;
    }
}