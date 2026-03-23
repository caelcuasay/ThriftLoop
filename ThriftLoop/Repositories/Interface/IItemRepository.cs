using ThriftLoop.Models;

namespace ThriftLoop.Repositories.Interface;

public interface IItemRepository
{
    Task AddAsync(Item item);
    Task<Item?> GetByIdAsync(int id);
    Task<Item?> GetByIdWithUserAsync(int id);

    /// <summary>
    /// Returns the item with Variants and their Skus eagerly loaded.
    /// Used by Shop Details, Edit, and Delete pages.
    /// </summary>
    Task<Item?> GetByIdWithVariantsAsync(int id);

    Task<IReadOnlyList<Item>> GetAllAsync();
    Task<IReadOnlyList<Item>> GetItemsByUserIdAsync(int userId);
    Task<IReadOnlyList<Item>> GetByShopIdAsync(int shopId);
    Task UpdateAsync(Item item);
    Task DeleteAsync(int id);
}