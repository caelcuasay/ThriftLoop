using ThriftLoop.Models;

namespace ThriftLoop.Repositories.Interface;

public interface IItemRepository
{
    /// <summary>Persists a new Item row and populates item.Id via EF Core.</summary>
    Task AddAsync(Item item);

    /// <summary>
    /// Returns a single item by primary key, or null if not found.
    /// Result is not change-tracked. Does NOT load the User navigation property.
    /// </summary>
    Task<Item?> GetByIdAsync(int id);

    /// <summary>
    /// Returns a single item by primary key with the related User (seller)
    /// eagerly loaded, or null if not found.
    /// Used by the Details page.
    /// </summary>
    Task<Item?> GetByIdWithUserAsync(int id);

    /// <summary>
    /// Returns every item across all users, ordered by most-recently created first.
    /// Used by the public "For You" feed.
    /// </summary>
    Task<IReadOnlyList<Item>> GetAllAsync();

    /// <summary>
    /// Returns all items belonging to the specified user,
    /// ordered by most-recently created first.
    /// </summary>
    Task<IReadOnlyList<Item>> GetItemsByUserIdAsync(int userId);

    /// <summary>
    /// Persists all changes to an existing Item row.
    /// The caller is responsible for preserving immutable fields
    /// (UserId, CreatedAt) on the entity before calling this method.
    /// </summary>
    Task UpdateAsync(Item item);

    /// <summary>Removes the Item row with the given primary key (no-op if not found).</summary>
    Task DeleteAsync(int id);
}