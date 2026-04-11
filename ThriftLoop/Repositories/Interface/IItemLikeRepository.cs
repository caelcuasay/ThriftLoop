using ThriftLoop.Models;

namespace ThriftLoop.Repositories.Interface;

public interface IItemLikeRepository
{
    /// <summary>
    /// Adds a like for an item by a user. Returns the created like or null if already exists.
    /// </summary>
    Task<ItemLike?> AddAsync(int userId, int itemId);

    /// <summary>
    /// Removes a like for an item by a user.
    /// </summary>
    Task RemoveAsync(int userId, int itemId);

    /// <summary>
    /// Gets all liked items for a user, with Item eagerly loaded.
    /// </summary>
    Task<IReadOnlyList<ItemLike>> GetByUserIdAsync(int userId);

    /// <summary>
    /// Gets the count of likes for a specific item.
    /// </summary>
    Task<int> GetCountByItemIdAsync(int itemId);

    /// <summary>
    /// Checks if a user has liked a specific item.
    /// </summary>
    Task<bool> HasLikedAsync(int userId, int itemId);

    /// <summary>
    /// Gets the most liked items, optionally filtered by P2P or Shop items.
    /// </summary>
    Task<IReadOnlyList<(Item Item, int LikeCount)>> GetMostLikedAsync(int count = 10, bool? p2pOnly = null);

    /// <summary>
    /// Gets all like counts for a list of item IDs.
    /// </summary>
    Task<Dictionary<int, int>> GetLikeCountsAsync(IEnumerable<int> itemIds);
}
