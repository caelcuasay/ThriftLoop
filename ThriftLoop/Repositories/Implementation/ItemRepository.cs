using Microsoft.EntityFrameworkCore;
using ThriftLoop.Data;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;
using ThriftLoop.Enums;

namespace ThriftLoop.Repositories.Implementation;

public class ItemRepository : IItemRepository
{
    private readonly ApplicationDbContext _context;

    public ItemRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Item item)
    {
        await _context.Items.AddAsync(item);
        await _context.SaveChangesAsync();
    }

    public async Task<Item?> GetByIdAsync(int id)
        => await _context.Items
                         .AsNoTracking()
                         .FirstOrDefaultAsync(i => i.Id == id);

    public async Task<Item?> GetByIdWithUserAsync(int id)
        => await _context.Items
                         .AsNoTracking()
                         .Include(i => i.User)
                         .FirstOrDefaultAsync(i => i.Id == id);

    public async Task<Item?> GetByIdWithVariantsAsync(int id)
        => await _context.Items
                         .AsNoTracking()
                         .Include(i => i.User)  // Add User for seller info
                         .Include(i => i.Variants)
                             .ThenInclude(v => v.Skus)
                         .FirstOrDefaultAsync(i => i.Id == id);

    public async Task<Item?> GetByIdWithVariantsTrackedAsync(int id)
        => await _context.Items
                         .Include(i => i.User)
                         .Include(i => i.Variants)
                             .ThenInclude(v => v.Skus)
                         .FirstOrDefaultAsync(i => i.Id == id);

    public async Task<IReadOnlyList<Item>> GetAllAsync()
        => await _context.Items
                         .AsNoTracking()
                         .Include(i => i.User)
                         .Include(i => i.Shop)
                         .Where(i => i.Status != ItemStatus.Sold && i.Status != ItemStatus.Disabled)
                         .OrderByDescending(i => i.CreatedAt)
                         .ToListAsync();

    public async Task<IReadOnlyList<Item>> GetItemsByUserIdAsync(int userId)
        => await _context.Items
                         .AsNoTracking()
                         .Where(i => i.UserId == userId && i.Status != ItemStatus.Sold)
                         .OrderByDescending(i => i.CreatedAt)
                         .ToListAsync();

    public async Task<IReadOnlyList<Item>> GetByShopIdAsync(int shopId)
        => await _context.Items
                         .AsNoTracking()
                         .Where(i => i.ShopId == shopId && i.Status != ItemStatus.Sold)
                         .OrderByDescending(i => i.CreatedAt)
                         .ToListAsync();

    /// <inheritdoc />
    public async Task<IReadOnlyList<Item>> GetAllShopItemsAsync()
        => await _context.Items
                         .AsNoTracking()
                         .Include(i => i.Shop)
                         .Where(i => i.ShopId != null && i.Status != ItemStatus.Sold && i.Status != ItemStatus.Disabled)
                         .OrderByDescending(i => i.CreatedAt)
                         .ToListAsync();

    public async Task UpdateAsync(Item item)
    {
        _context.Items.Update(item);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var item = await _context.Items.FindAsync(id);
        if (item is null) return;
        _context.Items.Remove(item);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<ItemVariantSku?> GetSkuByIdWithItemAsync(int skuId)
        => await _context.ItemVariantSkus
                         .AsNoTracking()
                         .Include(s => s.Variant)
                             .ThenInclude(v => v.Item)
                                 .ThenInclude(i => i!.User)
                         .FirstOrDefaultAsync(s => s.Id == skuId);
}