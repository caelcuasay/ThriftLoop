using Microsoft.EntityFrameworkCore;
using ThriftLoop.Data;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;

namespace ThriftLoop.Repositories.Implementation;

public class CartRepository : ICartRepository
{
    private readonly ApplicationDbContext _context;

    public CartRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CartItem> AddAsync(int userId, int itemId, int itemVariantSkuId, int quantity)
    {
        quantity = Math.Max(1, quantity);

        // Check if this SKU is already in the cart
        var existing = await _context.CartItems
            .FirstOrDefaultAsync(ci => ci.UserId == userId && ci.ItemVariantSkuId == itemVariantSkuId);

        if (existing != null)
        {
            // Update quantity
            existing.Quantity += quantity;
            await _context.SaveChangesAsync();
            return existing;
        }

        // Create new cart item
        var cartItem = new CartItem
        {
            UserId = userId,
            ItemId = itemId,
            ItemVariantSkuId = itemVariantSkuId,
            Quantity = quantity,
            AddedAt = DateTime.UtcNow
        };

        await _context.CartItems.AddAsync(cartItem);
        await _context.SaveChangesAsync();
        return cartItem;
    }

    public async Task<CartItem?> GetByIdAsync(int cartItemId)
        => await _context.CartItems
            .AsNoTracking()
            .FirstOrDefaultAsync(ci => ci.Id == cartItemId);

    public async Task<IReadOnlyList<CartItem>> GetByUserIdAsync(int userId)
        => await _context.CartItems
            .AsNoTracking()
            .Where(ci => ci.UserId == userId)
            .Include(ci => ci.Item)
                .ThenInclude(i => i!.Shop)
            .Include(ci => ci.ItemVariantSku)
                .ThenInclude(s => s!.Variant)
            .OrderByDescending(ci => ci.AddedAt)
            .ToListAsync();

    public async Task<int> GetCountByUserIdAsync(int userId)
        => await _context.CartItems
            .Where(ci => ci.UserId == userId)
            .SumAsync(ci => ci.Quantity);

    public async Task UpdateQuantityAsync(int cartItemId, int quantity)
    {
        quantity = Math.Max(1, quantity);

        var cartItem = await _context.CartItems.FindAsync(cartItemId);
        if (cartItem is null) return;

        cartItem.Quantity = quantity;
        await _context.SaveChangesAsync();
    }

    public async Task RemoveAsync(int cartItemId)
    {
        var cartItem = await _context.CartItems.FindAsync(cartItemId);
        if (cartItem is null) return;

        _context.CartItems.Remove(cartItem);
        await _context.SaveChangesAsync();
    }

    public async Task ClearByUserIdAsync(int userId)
    {
        var items = await _context.CartItems
            .Where(ci => ci.UserId == userId)
            .ToListAsync();

        _context.CartItems.RemoveRange(items);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasItemAsync(int userId, int itemVariantSkuId)
        => await _context.CartItems
            .AnyAsync(ci => ci.UserId == userId && ci.ItemVariantSkuId == itemVariantSkuId);
}
