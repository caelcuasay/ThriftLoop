using Microsoft.EntityFrameworkCore;
using ThriftLoop.Data;
using ThriftLoop.Models;
using ThriftLoop.Repositories.Interface;

namespace ThriftLoop.Repositories.Implementation;

public class ItemRepository : IItemRepository
{
    private readonly ApplicationDbContext _context;

    public ItemRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(Item item)
    {
        await _context.Items.AddAsync(item);
        await _context.SaveChangesAsync();
        // EF Core back-fills item.Id with the
        // database-generated identity value.
    }

    /// <inheritdoc />
    public async Task<Item?> GetByIdAsync(int id)
        => await _context.Items
                         .AsNoTracking()
                         .FirstOrDefaultAsync(i => i.Id == id);

    /// <inheritdoc />
    public async Task<Item?> GetByIdWithUserAsync(int id)
        => await _context.Items
                         .AsNoTracking()
                         .Include(i => i.User)
                         .FirstOrDefaultAsync(i => i.Id == id);

    /// <inheritdoc />
    /// <remarks>
    /// FIX: Excludes Sold items so they are never shown in the public "For You"
    /// feed. A sold item disappears from the grid the moment ConfirmOrder marks
    /// it — no CSS gray-out, no lingering placeholder.
    /// </remarks>
    public async Task<IReadOnlyList<Item>> GetAllAsync()
        => await _context.Items
                         .AsNoTracking()
                         .Where(i => i.Status != ItemStatus.Sold)
                         .OrderByDescending(i => i.CreatedAt)
                         .ToListAsync();

    /// <inheritdoc />
    public async Task<IReadOnlyList<Item>> GetItemsByUserIdAsync(int userId)
        => await _context.Items
                         .AsNoTracking()
                         .Where(i => i.UserId == userId)
                         .OrderByDescending(i => i.CreatedAt)
                         .ToListAsync();

    /// <inheritdoc />
    public async Task UpdateAsync(Item item)
    {
        // Attach the detached entity and mark it as modified so EF issues
        // a full UPDATE statement. Immutable fields (UserId, CreatedAt) are
        // preserved by the caller before this method is invoked.
        _context.Items.Update(item);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id)
    {
        var item = await _context.Items.FindAsync(id);
        if (item is null) return;

        _context.Items.Remove(item);
        await _context.SaveChangesAsync();
    }
}