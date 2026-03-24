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

    /// <summary>
    /// Returns all shop items (ShopId != null) that are not sold,
    /// ordered newest first. Used by the Sellers feed page.
    /// </summary>
    Task<IReadOnlyList<Item>> GetAllShopItemsAsync();

    Task UpdateAsync(Item item);
    Task DeleteAsync(int id);

    /// <summary>
    /// Returns a single SKU with its parent Variant → Item → User chain
    /// eagerly loaded. No-tracking read.
    ///
    /// Used by ShopCheckout GET (OrdersController) to build the checkout
    /// summary from a skuId without a separate Item lookup.
    /// </summary>
    Task<ItemVariantSku?> GetSkuByIdWithItemAsync(int skuId);
}