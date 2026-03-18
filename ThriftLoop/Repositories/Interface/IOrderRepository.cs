using ThriftLoop.Models;

namespace ThriftLoop.Repositories.Interface;

public interface IOrderRepository
{
    /// <summary>Persists a new Order row and populates order.Id via EF Core.</summary>
    Task AddAsync(Order order);

    /// <summary>
    /// Returns all orders placed by the given buyer, most-recent first.
    /// Eagerly loads the related Item and Seller.
    /// </summary>
    Task<IReadOnlyList<Order>> GetOrdersByBuyerIdAsync(int userId);

    /// <summary>
    /// Returns a single order by primary key with Item, Buyer, and Seller
    /// loaded, or null if not found.
    /// </summary>
    Task<Order?> GetOrderByIdAsync(int id);

    /// <summary>
    /// Returns the order associated with a specific item, or null if none exists.
    /// Used to prevent duplicate order creation on double-submit.
    /// </summary>
    Task<Order?> GetOrderByItemIdAsync(int itemId);

    /// <summary>
    /// Persists all changes to an existing Order row (status, payment flags, etc.).
    /// </summary>
    Task UpdateAsync(Order order);

    /// <summary>
    /// Permanently removes an Order row. Used only to roll back a failed
    /// wallet escrow hold before the order becomes visible to the user.
    /// </summary>
    Task DeleteAsync(int orderId);
}