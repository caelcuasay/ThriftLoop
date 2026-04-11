using ThriftLoop.Models;

namespace ThriftLoop.Repositories.Interface;

public interface ICartRepository
{
    /// <summary>
    /// Adds an item to the user's cart. If the SKU already exists, updates the quantity.
    /// </summary>
    Task<CartItem> AddAsync(int userId, int itemId, int itemVariantSkuId, int quantity);

    /// <summary>
    /// Gets a single cart item by its ID.
    /// </summary>
    Task<CartItem?> GetByIdAsync(int cartItemId);

    /// <summary>
    /// Gets all cart items for a user, with Item, Variant, and SKU eagerly loaded.
    /// </summary>
    Task<IReadOnlyList<CartItem>> GetByUserIdAsync(int userId);

    /// <summary>
    /// Gets the count of items in the user's cart.
    /// </summary>
    Task<int> GetCountByUserIdAsync(int userId);

    /// <summary>
    /// Updates the quantity of a cart item.
    /// </summary>
    Task UpdateQuantityAsync(int cartItemId, int quantity);

    /// <summary>
    /// Removes a specific item from the cart.
    /// </summary>
    Task RemoveAsync(int cartItemId);

    /// <summary>
    /// Clears all items from the user's cart.
    /// </summary>
    Task ClearByUserIdAsync(int userId);

    /// <summary>
    /// Checks if a specific SKU is already in the user's cart.
    /// </summary>
    Task<bool> HasItemAsync(int userId, int itemVariantSkuId);
}
