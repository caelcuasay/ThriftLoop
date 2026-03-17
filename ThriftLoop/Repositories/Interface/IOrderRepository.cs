using ThriftLoop.Models;

namespace ThriftLoop.Repositories.Interface;

public interface IOrderRepository
{
    /// <summary>
    /// Persists a new Order row and populates order.Id via EF Core.
    /// </summary>
    Task AddAsync(Order order);

    /// <summary>
    /// Returns all orders placed by the given buyer, most-recent first.
    /// Eagerly loads the related Item and Seller so callers do not need
    /// a second query to display order summaries.
    /// </summary>
    Task<IReadOnlyList<Order>> GetOrdersByBuyerIdAsync(int userId);

    /// <summary>
    /// Returns a single order by primary key with Item, Buyer, and Seller
    /// loaded, or null if not found.
    /// </summary>
    Task<Order?> GetOrderByIdAsync(int id);

    /// <summary>
    /// Returns the order associated with a specific item, or null if none exists.
    /// Used by the checkout controller to prevent duplicate order creation
    /// when a buyer double-submits the confirmation form.
    /// </summary>
    Task<Order?> GetOrderByItemIdAsync(int itemId);
}