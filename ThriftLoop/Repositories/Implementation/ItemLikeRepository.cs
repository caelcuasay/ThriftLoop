using Microsoft.EntityFrameworkCore;
using ThriftLoop.Data;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;

namespace ThriftLoop.Repositories.Implementation;

public class ItemLikeRepository : IItemLikeRepository
{
    private readonly ApplicationDbContext _context;

    public ItemLikeRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ItemLike?> AddAsync(int userId, int itemId)
    {
        // Check if already liked
        var existing = await _context.ItemLikes
            .FirstOrDefaultAsync(il => il.UserId == userId && il.ItemId == itemId);

        if (existing != null) return null; // Already liked

        var like = new ItemLike
        {
            UserId = userId,
            ItemId = itemId,
            LikedAt = DateTime.UtcNow
        };

        await _context.ItemLikes.AddAsync(like);
        await _context.SaveChangesAsync();
        return like;
    }

    public async Task RemoveAsync(int userId, int itemId)
    {
        var like = await _context.ItemLikes
            .FirstOrDefaultAsync(il => il.UserId == userId && il.ItemId == itemId);

        if (like is null) return;

        _context.ItemLikes.Remove(like);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<ItemLike>> GetByUserIdAsync(int userId)
        => await _context.ItemLikes
            .AsNoTracking()
            .Where(il => il.UserId == userId)
            .Include(il => il.Item)
                .ThenInclude(i => i!.User)
            .Include(il => il.Item)
                .ThenInclude(i => i!.Shop)
            .OrderByDescending(il => il.LikedAt)
            .ToListAsync();

    public async Task<int> GetCountByItemIdAsync(int itemId)
        => await _context.ItemLikes
            .CountAsync(il => il.ItemId == itemId);

    public async Task<bool> HasLikedAsync(int userId, int itemId)
        => await _context.ItemLikes
            .AnyAsync(il => il.UserId == userId && il.ItemId == itemId);

    public async Task<IReadOnlyList<(Item Item, int LikeCount)>> GetMostLikedAsync(int count = 10, bool? p2pOnly = null)
    {
        var query = _context.ItemLikes
            .AsNoTracking()
            .Include(il => il.Item)
            .Where(il => il.Item != null);

        // Filter by P2P or Shop items if specified
        if (p2pOnly.HasValue)
        {
            if (p2pOnly.Value)
            {
                query = query.Where(il => il.Item!.ShopId == null);
            }
            else
            {
                query = query.Where(il => il.Item!.ShopId != null);
            }
        }

        var grouped = await query
            .GroupBy(il => il.ItemId)
            .Select(g => new
            {
                ItemId = g.Key,
                LikeCount = g.Count()
            })
            .OrderByDescending(x => x.LikeCount)
            .Take(count)
            .ToListAsync();

        // Load the actual items
        var itemIds = grouped.Select(x => x.ItemId).ToList();
        var items = await _context.Items
            .AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .Include(i => i.User)
            .Include(i => i.Shop)
            .ToListAsync();

        var itemDict = items.ToDictionary(i => i.Id);

        return grouped
            .Where(x => itemDict.ContainsKey(x.ItemId))
            .Select(x => (itemDict[x.ItemId], x.LikeCount))
            .ToList();
    }

    public async Task<Dictionary<int, int>> GetLikeCountsAsync(IEnumerable<int> itemIds)
    {
        var ids = itemIds.ToList();
        if (!ids.Any()) return new Dictionary<int, int>();

        var counts = await _context.ItemLikes
            .AsNoTracking()
            .Where(il => ids.Contains(il.ItemId))
            .GroupBy(il => il.ItemId)
            .Select(g => new { ItemId = g.Key, Count = g.Count() })
            .ToListAsync();

        return counts.ToDictionary(x => x.ItemId, x => x.Count);
    }
}
