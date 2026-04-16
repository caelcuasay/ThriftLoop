// Repositories/Implementation/ConversationRepository.cs
using Microsoft.EntityFrameworkCore;
using ThriftLoop.Data;
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
}