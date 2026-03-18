using Microsoft.EntityFrameworkCore;
using ThriftLoop.Data;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;

namespace ThriftLoop.Repositories.Implementation;

public class OrderRepository : IOrderRepository
{
    private readonly ApplicationDbContext _context;

    public OrderRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(Order order)
    {
        await _context.Orders.AddAsync(order);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> GetOrdersByBuyerIdAsync(int userId)
        => await _context.Orders
                         .AsNoTracking()
                         .Include(o => o.Item)
                         .Include(o => o.Seller)
                         .Where(o => o.BuyerId == userId)
                         .OrderByDescending(o => o.OrderDate)
                         .ToListAsync();

    /// <inheritdoc />
    public async Task<Order?> GetOrderByIdAsync(int id)
        => await _context.Orders
                         .AsNoTracking()
                         .Include(o => o.Item)
                         .Include(o => o.Buyer)
                         .Include(o => o.Seller)
                         .FirstOrDefaultAsync(o => o.Id == id);

    /// <inheritdoc />
    public async Task<Order?> GetOrderByItemIdAsync(int itemId)
        => await _context.Orders
                         .AsNoTracking()
                         .FirstOrDefaultAsync(o => o.ItemId == itemId);

    /// <inheritdoc />
    public async Task UpdateAsync(Order order)
    {
        _context.Orders.Update(order);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int orderId)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order is null) return;
        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();
    }
}